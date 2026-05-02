using NINA.Core.Utility;
using System;
using System.Diagnostics;
using System.IO;

namespace NINA.Plugin.Python {

    internal static class PythonEnvironmentSetup {
        private const string PythonDownloadUrl = "https://www.python.org/downloads/windows/";

        public static PythonEnvironmentSetupResult SetupDefaultVirtualEnvironment() {
            var pythonInstallation = PythonDiscovery.ResolvePythonInstallation();
            string pythonExecutablePath = pythonInstallation.PythonExecutablePath;
            if (string.IsNullOrWhiteSpace(pythonExecutablePath) || !File.Exists(pythonExecutablePath)) {
                throw new FileNotFoundException(
                    "The selected Python installation does not have python.exe. Set PYTHONNET_PYDLL to a Python installation that includes python.exe, or install Python normally.");
            }

            string virtualEnvironmentPath = GetDefaultVirtualEnvironmentPath();
            string virtualEnvironmentPythonPath = Path.Combine(virtualEnvironmentPath, "Scripts", "python.exe");
            bool createdVirtualEnvironment = false;

            Directory.CreateDirectory(Path.GetDirectoryName(virtualEnvironmentPath));

            if (!File.Exists(Path.Combine(virtualEnvironmentPath, "pyvenv.cfg"))) {
                RunProcess(pythonExecutablePath, "-m", "venv", virtualEnvironmentPath);
                createdVirtualEnvironment = true;
            }

            if (!File.Exists(virtualEnvironmentPythonPath)) {
                throw new FileNotFoundException(
                    "The Python virtual environment was created, but Scripts\\python.exe was not found.",
                    virtualEnvironmentPythonPath);
            }

            RunProcess(virtualEnvironmentPythonPath, "-m", "ensurepip", "--upgrade");

            Environment.SetEnvironmentVariable(
                PythonRuntimeManager.PythonVenvEnvironmentVariable,
                virtualEnvironmentPath,
                EnvironmentVariableTarget.User);
            Environment.SetEnvironmentVariable(
                PythonRuntimeManager.PythonVenvEnvironmentVariable,
                virtualEnvironmentPath,
                EnvironmentVariableTarget.Process);

            Logger.Info($"Python scripting virtual environment configured at {virtualEnvironmentPath}");

            return new PythonEnvironmentSetupResult(
                pythonExecutablePath,
                virtualEnvironmentPath,
                virtualEnvironmentPythonPath,
                createdVirtualEnvironment,
                PythonRuntimeManager.IsInitialized);
        }

        public static void OpenPythonDownloadPage() {
            Process.Start(new ProcessStartInfo {
                FileName = PythonDownloadUrl,
                UseShellExecute = true
            });
        }

        private static string GetDefaultVirtualEnvironmentPath() {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NINA",
                "PythonScripting",
                ".venv");
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
            if (!process.WaitForExit(120000)) {
                process.Kill();
                throw new TimeoutException($"{fileName} did not finish within 120 seconds.");
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

    internal sealed class PythonEnvironmentSetupResult {
        public PythonEnvironmentSetupResult(
            string basePythonExecutablePath,
            string virtualEnvironmentPath,
            string virtualEnvironmentPythonPath,
            bool createdVirtualEnvironment,
            bool restartRequired) {
            BasePythonExecutablePath = basePythonExecutablePath;
            VirtualEnvironmentPath = virtualEnvironmentPath;
            VirtualEnvironmentPythonPath = virtualEnvironmentPythonPath;
            CreatedVirtualEnvironment = createdVirtualEnvironment;
            RestartRequired = restartRequired;
        }

        public string BasePythonExecutablePath { get; }
        public string VirtualEnvironmentPath { get; }
        public string VirtualEnvironmentPythonPath { get; }
        public bool CreatedVirtualEnvironment { get; }
        public bool RestartRequired { get; }
    }
}
