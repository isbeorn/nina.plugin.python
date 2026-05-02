using NINA.Core.Utility;
using Python.Runtime;
using System;
using System.Collections.Generic;
using System.IO;

namespace NINA.Plugin.Python {

    internal static class PythonRuntimeManager {
        internal const string PythonDllEnvironmentVariable = "PYTHONNET_PYDLL";
        internal const string PythonVenvEnvironmentVariable = "PYTHONNET_VENV";

        private static readonly object syncRoot = new object();
        private static bool initializedByPlugin;
        private static IntPtr allowThreadsState = IntPtr.Zero;

        public static bool IsInitialized => PythonEngine.IsInitialized;

        public static void Execute(Action action) {
            Execute(() => {
                action();
                return true;
            });
        }

        public static T Execute<T>(Func<T> action) {
            lock (syncRoot) {
                EnsureInitialized();

                using (Py.GIL()) {
                    return action();
                }
            }
        }

        public static void PrepareForApplicationShutdown() {
            if (initializedByPlugin) {
                Logger.Info("Python scripting engine left initialized for process exit");
            }
        }

        public static PythonSetupTestResult TestSetup() {
            if (!PythonEngine.IsInitialized) {
                ResolvePythonRuntimeConfiguration();
            }

            string pythonVersion = string.Empty;
            string pythonExecutable = string.Empty;
            string pythonPrefix = string.Empty;
            string pythonBasePrefix = string.Empty;

            Execute(() => {
                using var sys = Py.Import("sys");
                pythonVersion = sys.GetAttr("version").ToString();
                pythonExecutable = sys.GetAttr("executable").ToString();
                pythonPrefix = sys.GetAttr("prefix").ToString();
                pythonBasePrefix = sys.GetAttr("base_prefix").ToString();
            });

            string virtualEnvironmentPath = string.Equals(pythonPrefix, pythonBasePrefix, StringComparison.OrdinalIgnoreCase)
                ? null
                : pythonPrefix;

            return new PythonSetupTestResult(
                Runtime.PythonDLL,
                virtualEnvironmentPath,
                pythonVersion,
                pythonExecutable,
                pythonPrefix,
                pythonBasePrefix);
        }

        public static void Shutdown() {
            lock (syncRoot) {
                if (!initializedByPlugin) {
                    return;
                }

                if (!PythonEngine.IsInitialized) {
                    initializedByPlugin = false;
                    allowThreadsState = IntPtr.Zero;
                    return;
                }

                try {
                    if (allowThreadsState != IntPtr.Zero) {
                        PythonEngine.EndAllowThreads(allowThreadsState);
                        allowThreadsState = IntPtr.Zero;
                    }

                    PythonEngine.Shutdown();
                    initializedByPlugin = false;
                    Logger.Info("Python scripting engine shut down");
                } catch (Exception ex) {
                    Logger.Error("Failed to shut down Python scripting engine", ex);
                    throw;
                }
            }
        }

        private static void EnsureInitialized() {
            if (PythonEngine.IsInitialized) {
                return;
            }

            var configuration = ResolvePythonRuntimeConfiguration();
            ConfigurePythonRuntime(configuration);

            try {
                PythonEngine.Initialize();
                allowThreadsState = PythonEngine.BeginAllowThreads();
                initializedByPlugin = true;
                Logger.Info($"Python scripting engine initialized using {Runtime.PythonDLL}");
            } catch (Exception ex) {
                Logger.Error($"Failed to initialize Python scripting engine using {Runtime.PythonDLL}", ex);

                if (PythonEngine.IsInitialized) {
                    if (allowThreadsState != IntPtr.Zero) {
                        PythonEngine.EndAllowThreads(allowThreadsState);
                        allowThreadsState = IntPtr.Zero;
                    }

                    PythonEngine.Shutdown();
                }

                allowThreadsState = IntPtr.Zero;
                initializedByPlugin = false;
                throw;
            }
        }

        private static PythonRuntimeConfiguration ResolvePythonRuntimeConfiguration() {
            string virtualEnvironmentPath = ResolveVirtualEnvironmentPath();

            if (!string.IsNullOrWhiteSpace(virtualEnvironmentPath)) {
                return ResolveVirtualEnvironmentConfiguration(virtualEnvironmentPath);
            }

            return new PythonRuntimeConfiguration(ResolvePythonDllPath(), null, null, null);
        }

        private static string ResolvePythonDllPath() {
            string explicitPythonDllPath = ResolveExplicitPythonDllPath();
            if (!string.IsNullOrWhiteSpace(explicitPythonDllPath)) {
                return explicitPythonDllPath;
            }

            return PythonDiscovery.ResolvePythonInstallation().PythonDllPath;
        }

        private static string ResolveVirtualEnvironmentPath() {
            string virtualEnvironmentPath = Environment.GetEnvironmentVariable(PythonVenvEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(virtualEnvironmentPath)) {
                return null;
            }

            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(virtualEnvironmentPath));
        }

        private static PythonRuntimeConfiguration ResolveVirtualEnvironmentConfiguration(string virtualEnvironmentPath) {
            if (!Directory.Exists(virtualEnvironmentPath)) {
                throw new DirectoryNotFoundException(
                    $"{PythonVenvEnvironmentVariable} points to a Python virtual environment directory that does not exist: {virtualEnvironmentPath}");
            }

            string configurationPath = Path.Combine(virtualEnvironmentPath, "pyvenv.cfg");
            if (!File.Exists(configurationPath)) {
                throw new FileNotFoundException(
                    $"{PythonVenvEnvironmentVariable} must point to a Python virtual environment root directory containing pyvenv.cfg.",
                    configurationPath);
            }

            string pythonExecutablePath = Path.Combine(virtualEnvironmentPath, "Scripts", "python.exe");
            if (!File.Exists(pythonExecutablePath)) {
                throw new FileNotFoundException(
                    $"{PythonVenvEnvironmentVariable} must point to a Windows Python virtual environment with Scripts\\python.exe.",
                    pythonExecutablePath);
            }

            var configurationValues = ReadVirtualEnvironmentConfiguration(configurationPath);
            configurationValues.TryGetValue("home", out string basePythonHome);
            if (string.IsNullOrWhiteSpace(basePythonHome)) {
                throw new InvalidOperationException(
                    $"{configurationPath} does not contain a home entry pointing to the base Python installation.");
            }

            configurationValues.TryGetValue("version", out string pythonVersion);
            var basePythonInstallation = PythonDiscovery.FromBasePythonHome(basePythonHome, pythonVersion);
            string pythonDllPath = ResolveExplicitPythonDllPath() ?? basePythonInstallation.PythonDllPath;

            return new PythonRuntimeConfiguration(
                pythonDllPath,
                virtualEnvironmentPath,
                pythonExecutablePath,
                basePythonInstallation.HomePath);
        }

        private static string ResolveExplicitPythonDllPath() {
            if (!string.IsNullOrWhiteSpace(Runtime.PythonDLL)) {
                return Runtime.PythonDLL;
            }

            string environmentPath = Environment.GetEnvironmentVariable(PythonDllEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(environmentPath)) {
                return environmentPath;
            }

            return null;
        }

        private static Dictionary<string, string> ReadVirtualEnvironmentConfiguration(string configurationPath) {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string line in File.ReadAllLines(configurationPath)) {
                int separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0) {
                    continue;
                }

                string name = line.Substring(0, separatorIndex).Trim();
                string value = line.Substring(separatorIndex + 1).Trim();
                if (!string.IsNullOrWhiteSpace(name)) {
                    values[name] = value;
                }
            }

            return values;
        }

        private static void ConfigurePythonRuntime(PythonRuntimeConfiguration configuration) {
            Runtime.PythonDLL = configuration.PythonDllPath;

            if (string.IsNullOrWhiteSpace(configuration.VirtualEnvironmentPath)) {
                return;
            }

            PythonEngine.ProgramName = configuration.PythonExecutablePath;
            Environment.SetEnvironmentVariable("PYTHONHOME", null, EnvironmentVariableTarget.Process);
            PrependPath(Path.Combine(configuration.VirtualEnvironmentPath, "Scripts"));
            PrependPath(configuration.BasePythonHome);
        }

        private static void PrependPath(string pathToPrepend) {
            if (string.IsNullOrWhiteSpace(pathToPrepend) || !Directory.Exists(pathToPrepend)) {
                return;
            }

            string path = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process) ?? string.Empty;
            string[] pathEntries = path.Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string pathEntry in pathEntries) {
                if (string.Equals(pathEntry.Trim(), pathToPrepend, StringComparison.OrdinalIgnoreCase)) {
                    return;
                }
            }

            Environment.SetEnvironmentVariable(
                "PATH",
                string.IsNullOrWhiteSpace(path) ? pathToPrepend : pathToPrepend + Path.PathSeparator + path,
                EnvironmentVariableTarget.Process);
        }

        private sealed class PythonRuntimeConfiguration {
            public PythonRuntimeConfiguration(
                string pythonDllPath,
                string virtualEnvironmentPath,
                string pythonExecutablePath,
                string basePythonHome) {
                PythonDllPath = pythonDllPath;
                VirtualEnvironmentPath = virtualEnvironmentPath;
                PythonExecutablePath = pythonExecutablePath;
                BasePythonHome = basePythonHome;
            }

            public string PythonDllPath { get; }
            public string VirtualEnvironmentPath { get; }
            public string PythonExecutablePath { get; }
            public string BasePythonHome { get; }
        }
    }

    internal class PythonSetupTestResult {
        public PythonSetupTestResult(
            string pythonDllPath,
            string virtualEnvironmentPath,
            string pythonVersion,
            string pythonExecutable,
            string pythonPrefix,
            string pythonBasePrefix) {
            PythonDllPath = pythonDllPath;
            VirtualEnvironmentPath = virtualEnvironmentPath;
            PythonVersion = pythonVersion;
            PythonExecutable = pythonExecutable;
            PythonPrefix = pythonPrefix;
            PythonBasePrefix = pythonBasePrefix;
        }

        public string PythonDllPath { get; }
        public string VirtualEnvironmentPath { get; }
        public string PythonVersion { get; }
        public string PythonExecutable { get; }
        public string PythonPrefix { get; }
        public string PythonBasePrefix { get; }
        public bool IsVirtualEnvironment => !string.Equals(PythonPrefix, PythonBasePrefix, StringComparison.OrdinalIgnoreCase);
    }
}
