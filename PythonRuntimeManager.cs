using NINA.Core.Utility;
using Python.Runtime;
using System;
using System.IO;

namespace NINA.Plugin.Python {

    internal static class PythonRuntimeManager {
        private const string PythonDllEnvironmentVariable = "PYTHONNET_PYDLL";
        private const string DefaultPythonVersionFolder = "Python312";
        private const string DefaultPythonDllFileName = "python312.dll";

        private static readonly object syncRoot = new object();
        private static bool initializedByPlugin;
        private static IntPtr allowThreadsState = IntPtr.Zero;

        public static void Execute(Action action) {
            lock (syncRoot) {
                EnsureInitialized();

                using (Py.GIL()) {
                    action();
                }
            }
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

            Runtime.PythonDLL = ResolvePythonDllPath();

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

        private static string ResolvePythonDllPath() {
            if (!string.IsNullOrWhiteSpace(Runtime.PythonDLL)) {
                return Runtime.PythonDLL;
            }

            string environmentPath = Environment.GetEnvironmentVariable(PythonDllEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(environmentPath)) {
                return environmentPath;
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                "Python",
                DefaultPythonVersionFolder,
                DefaultPythonDllFileName);
        }
    }
}
