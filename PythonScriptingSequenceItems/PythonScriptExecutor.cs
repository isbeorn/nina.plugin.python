using Python.Runtime;
using System;

namespace NINA.Plugin.Python.PythonScriptingTestCategory {

    internal static class PythonScriptExecutor {
        private const string InlineScriptFileName = "<nina-python-script>";

        private const string TracedExecScript = """
            import sys

            __nina_previous_trace = sys.gettrace()

            def __nina_trace(frame, event, arg):
                if frame.f_code.co_filename != __nina_script_filename:
                    return None

                if event == "line":
                    try:
                        __nina_trace_line(frame.f_lineno)
                    except Exception:
                        pass

                return __nina_trace

            __nina_code = compile(__nina_script_source, __nina_script_filename, "exec")

            try:
                sys.settrace(__nina_trace)
                exec(__nina_code, globals(), locals())
            finally:
                sys.settrace(__nina_previous_trace)
            """;

        public static void Execute(PyModule scope, PythonScriptExecutionSource scriptToExecute, Action<int> reportLine) {
            if (scope == null) {
                throw new ArgumentNullException(nameof(scope));
            }

            if (scriptToExecute == null) {
                throw new ArgumentNullException(nameof(scriptToExecute));
            }

            if (reportLine == null) {
                scope.Exec(scriptToExecute.Script);
                return;
            }

            scope.Set("__nina_script_source", scriptToExecute.Script.ToPython());
            scope.Set("__nina_script_filename", ResolveScriptFileName(scriptToExecute).ToPython());
            scope.Set("__nina_trace_line", reportLine.ToPython());

            scope.Exec(TracedExecScript);
        }

        private static string ResolveScriptFileName(PythonScriptExecutionSource scriptToExecute) {
            return string.IsNullOrWhiteSpace(scriptToExecute.FilePath)
                ? InlineScriptFileName
                : scriptToExecute.FilePath;
        }
    }
}
