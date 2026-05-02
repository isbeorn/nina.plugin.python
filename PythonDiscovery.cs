using NINA.Core.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace NINA.Plugin.Python {

    internal static class PythonDiscovery {
        private const string PythonExecutableFileName = "python.exe";
        private static readonly Version PreferredPythonVersion = new Version(3, 12);

        public static PythonInstallation ResolvePythonInstallation() {
            string pythonDllPath = Environment.GetEnvironmentVariable(PythonRuntimeManager.PythonDllEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(pythonDllPath)) {
                return FromDllPath(pythonDllPath);
            }

            var launcherInstallation = TryResolvePythonInstallationFromLauncher("-3.12");
            if (launcherInstallation != null) {
                return launcherInstallation;
            }

            launcherInstallation = TryResolvePythonInstallationFromLauncher("-3");
            if (launcherInstallation != null) {
                return launcherInstallation;
            }

            var installation = FindPythonInstallations().FirstOrDefault();
            if (installation != null) {
                return installation;
            }

            throw new FileNotFoundException(
                "Python was not found. Install 64-bit Python 3, or set PYTHONNET_PYDLL to the full path of the Python DLL.");
        }

        public static PythonInstallation FromBasePythonHome(string basePythonHome, string version) {
            string homePath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(basePythonHome));
            string pythonDllPath = ResolvePythonDllPathFromHome(homePath, version);
            string pythonExecutablePath = Path.Combine(homePath, PythonExecutableFileName);

            return new PythonInstallation(
                homePath,
                File.Exists(pythonExecutablePath) ? pythonExecutablePath : null,
                pythonDllPath,
                ResolveVersion(version, pythonDllPath));
        }

        private static PythonInstallation FromDllPath(string pythonDllPath) {
            string resolvedPythonDllPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(pythonDllPath));
            string homePath = Path.GetDirectoryName(resolvedPythonDllPath);
            string pythonExecutablePath = Path.Combine(homePath, PythonExecutableFileName);

            return new PythonInstallation(
                homePath,
                File.Exists(pythonExecutablePath) ? pythonExecutablePath : null,
                resolvedPythonDllPath,
                ResolveVersion(null, resolvedPythonDllPath));
        }

        private static PythonInstallation FromExecutablePath(string pythonExecutablePath) {
            string resolvedPythonExecutablePath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(pythonExecutablePath));
            string homePath = Path.GetDirectoryName(resolvedPythonExecutablePath);
            string pythonDllPath = ResolvePythonDllPathFromHome(homePath, null);

            return new PythonInstallation(
                homePath,
                resolvedPythonExecutablePath,
                pythonDllPath,
                ResolveVersion(null, pythonDllPath));
        }

        private static string ResolvePythonDllPathFromHome(string homePath, string version) {
            if (!string.IsNullOrWhiteSpace(version) && TryCreateDllFileName(version, out string versionedDllFileName)) {
                string versionedDllPath = Path.Combine(homePath, versionedDllFileName);
                if (File.Exists(versionedDllPath)) {
                    return versionedDllPath;
                }
            }

            string[] pythonDllPaths = Directory.Exists(homePath)
                ? Directory.GetFiles(homePath, "python*.dll", SearchOption.TopDirectoryOnly)
                : Array.Empty<string>();

            var candidates = pythonDllPaths
                .Select(path => new {
                    Path = path,
                    Version = TryParseDllVersion(path)
                })
                .Where(candidate => candidate.Version != null)
                .OrderBy(candidate => GetVersionPreference(candidate.Version))
                .ThenByDescending(candidate => candidate.Version)
                .ToList();

            if (candidates.Count > 0) {
                return candidates[0].Path;
            }

            throw new FileNotFoundException(
                $"Unable to resolve a Python DLL in {homePath}. Set {PythonRuntimeManager.PythonDllEnvironmentVariable} to the full Python DLL path.");
        }

        private static IEnumerable<PythonInstallation> FindPythonInstallations() {
            return GetPythonInstallSearchRoots()
                .Where(Directory.Exists)
                .SelectMany(root => Directory.GetDirectories(root, "Python3*", SearchOption.TopDirectoryOnly))
                .Select(TryResolvePythonInstallationFromHome)
                .Where(installation => installation != null)
                .OrderBy(installation => GetVersionPreference(installation.Version))
                .ThenByDescending(installation => installation.Version);
        }

        private static IEnumerable<string> GetPythonInstallSearchRoots() {
            yield return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                "Python");

            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrWhiteSpace(programFiles)) {
                yield return programFiles;
            }
        }

        private static PythonInstallation TryResolvePythonInstallationFromHome(string homePath) {
            try {
                string pythonDllPath = ResolvePythonDllPathFromHome(homePath, null);
                string pythonExecutablePath = Path.Combine(homePath, PythonExecutableFileName);

                return new PythonInstallation(
                    homePath,
                    File.Exists(pythonExecutablePath) ? pythonExecutablePath : null,
                    pythonDllPath,
                    ResolveVersion(null, pythonDllPath));
            } catch (Exception ex) {
                Logger.Debug($"Skipping Python installation candidate {homePath}: {ex.Message}");
                return null;
            }
        }

        private static PythonInstallation TryResolvePythonInstallationFromLauncher(string launcherVersion) {
            try {
                var result = RunProcess("py", launcherVersion, "-c", "import sys; print(sys.executable)");
                string pythonExecutablePath = result.StandardOutput.Trim();
                if (File.Exists(pythonExecutablePath)) {
                    return FromExecutablePath(pythonExecutablePath);
                }
            } catch (Exception ex) {
                Logger.Debug($"Python launcher did not resolve {launcherVersion}: {ex.Message}");
            }

            return null;
        }

        private static int GetVersionPreference(Version version) {
            if (version == null) {
                return 3;
            }

            if (version.Major == PreferredPythonVersion.Major && version.Minor == PreferredPythonVersion.Minor) {
                return 0;
            }

            return version.Major == 3 ? 1 : 2;
        }

        private static Version ResolveVersion(string version, string pythonDllPath) {
            if (!string.IsNullOrWhiteSpace(version) && Version.TryParse(version, out var parsedVersion)) {
                return parsedVersion;
            }

            return TryParseDllVersion(pythonDllPath);
        }

        private static bool TryCreateDllFileName(string version, out string fileName) {
            fileName = null;

            if (!Version.TryParse(version, out var parsedVersion)) {
                return false;
            }

            fileName = $"python{parsedVersion.Major}{parsedVersion.Minor}.dll";
            return true;
        }

        private static Version TryParseDllVersion(string pythonDllPath) {
            string fileName = Path.GetFileNameWithoutExtension(pythonDllPath);
            if (string.IsNullOrWhiteSpace(fileName) || !fileName.StartsWith("python", StringComparison.OrdinalIgnoreCase)) {
                return null;
            }

            string digits = fileName.Substring("python".Length);
            if (digits.Length < 2 || !digits.All(char.IsDigit)) {
                return null;
            }

            if (!int.TryParse(digits.Substring(0, 1), out int major)) {
                return null;
            }

            if (!int.TryParse(digits.Substring(1), out int minor)) {
                return null;
            }

            return new Version(major, minor);
        }

        private static ProcessResult RunProcess(string fileName, params string[] arguments) {
            var startInfo = new ProcessStartInfo {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            foreach (string argument in arguments) {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            if (process == null) {
                throw new InvalidOperationException($"Failed to start {fileName}.");
            }

            string standardOutput = process.StandardOutput.ReadToEnd();
            string standardError = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(30000)) {
                process.Kill();
                throw new TimeoutException($"{fileName} did not finish within 30 seconds.");
            }

            if (process.ExitCode != 0) {
                throw new InvalidOperationException(
                    $"{fileName} exited with code {process.ExitCode}.{Environment.NewLine}{standardError}{Environment.NewLine}{standardOutput}".Trim());
            }

            return new ProcessResult(standardOutput, standardError);
        }

        private sealed class ProcessResult {
            public ProcessResult(string standardOutput, string standardError) {
                StandardOutput = standardOutput;
                StandardError = standardError;
            }

            public string StandardOutput { get; }
            public string StandardError { get; }
        }
    }

    internal sealed class PythonInstallation {
        public PythonInstallation(string homePath, string pythonExecutablePath, string pythonDllPath, Version version) {
            HomePath = homePath;
            PythonExecutablePath = pythonExecutablePath;
            PythonDllPath = pythonDllPath;
            Version = version;
        }

        public string HomePath { get; }
        public string PythonExecutablePath { get; }
        public string PythonDllPath { get; }
        public Version Version { get; }
    }
}
