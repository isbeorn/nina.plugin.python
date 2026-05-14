using Python.Runtime;
using System;

namespace NINA.Plugin.Python.PythonScriptingTestCategory {

    internal static class PythonScriptExecutor {
        private const string InlineScriptFileName = "<nina-python-script>";

        private const string WrappedExecScript = """
            import ast
            import sys
            from types import MappingProxyType

            __nina_previous_trace = sys.gettrace()
            __nina_trace_callback = globals().get("__nina_trace_line")
            __nina_symbol_values = globals().get("__nina_symbols_snapshot", {})
            _nina_script_filename = __nina_script_filename
            _nina_symbol_names = frozenset(globals().get("__nina_symbol_variable_names", ()))
            _nina_symbol_function_names = frozenset(globals().get("__nina_symbol_function_names", ()))
            _nina_symbol_function_variable_names = frozenset(globals().get("__nina_symbol_function_variable_names", ()))
            symbols = MappingProxyType(__nina_symbol_values)

            def _nina_make_symbol_function(name):
                def _nina_symbol_function(*args):
                    return __nina_symbol_function_invoker.Invoke(name, args)

                return _nina_symbol_function

            __nina_symbol_function_values = {
                name: _nina_make_symbol_function(name)
                for name in _nina_symbol_function_names
            }
            symbolFunctions = MappingProxyType(__nina_symbol_function_values)

            class __nina_symbol_name_guard(ast.NodeTransformer):
                def _nina_reject(self, name, node):
                    raise SyntaxError(
                        f"{name} is a read-only N.I.N.A. symbol value or function.",
                        (_nina_script_filename, getattr(node, "lineno", 1), getattr(node, "col_offset", 0) + 1, ""))

                def _nina_is_read_only_name(self, name):
                    return name in _nina_symbol_names or name in _nina_symbol_function_variable_names or name in ("symbols", "symbolFunctions")

                def visit_Name(self, node):
                    if node.id in _nina_symbol_names:
                        if isinstance(node.ctx, ast.Load):
                            return ast.copy_location(
                                ast.Subscript(
                                    value=ast.Name(id="__nina_symbols_snapshot", ctx=ast.Load()),
                                    slice=ast.Constant(value=node.id),
                                    ctx=node.ctx),
                                node)

                        if isinstance(node.ctx, (ast.Store, ast.Del)):
                            self._nina_reject(node.id, node)

                    if node.id in _nina_symbol_function_variable_names:
                        if isinstance(node.ctx, ast.Load):
                            return ast.copy_location(
                                ast.Subscript(
                                    value=ast.Name(id="__nina_symbol_function_values", ctx=ast.Load()),
                                    slice=ast.Constant(value=node.id),
                                    ctx=node.ctx),
                                node)

                        if isinstance(node.ctx, (ast.Store, ast.Del)):
                            self._nina_reject(node.id, node)

                    if node.id in ("symbols", "symbolFunctions") and isinstance(node.ctx, (ast.Store, ast.Del)):
                        self._nina_reject(node.id, node)

                    return node

                def visit_Global(self, node):
                    for name in node.names:
                        if self._nina_is_read_only_name(name):
                            self._nina_reject(name, node)

                    return node

                def visit_Nonlocal(self, node):
                    for name in node.names:
                        if self._nina_is_read_only_name(name):
                            self._nina_reject(name, node)

                    return node

                def visit_FunctionDef(self, node):
                    self._nina_reject_binding_name(node.name, node)
                    self.generic_visit(node)
                    return node

                def visit_AsyncFunctionDef(self, node):
                    self._nina_reject_binding_name(node.name, node)
                    self.generic_visit(node)
                    return node

                def visit_ClassDef(self, node):
                    self._nina_reject_binding_name(node.name, node)
                    self.generic_visit(node)
                    return node

                def visit_Import(self, node):
                    for alias in node.names:
                        name = alias.asname or alias.name.split(".", 1)[0]
                        self._nina_reject_binding_name(name, node)

                    return node

                def visit_ImportFrom(self, node):
                    for alias in node.names:
                        name = alias.asname or alias.name
                        self._nina_reject_binding_name(name, node)

                    return node

                def visit_ExceptHandler(self, node):
                    if node.name is not None:
                        self._nina_reject_binding_name(node.name, node)

                    self.generic_visit(node)
                    return node

                def visit_arg(self, node):
                    self._nina_reject_binding_name(node.arg, node)
                    return node

                def _nina_reject_binding_name(self, name, node):
                    if self._nina_is_read_only_name(name):
                        self._nina_reject(name, node)

            def __nina_trace(frame, event, arg):
                if frame.f_code.co_filename != __nina_script_filename:
                    return None

                if event == "line":
                    try:
                        __nina_trace_callback(frame.f_lineno)
                    except Exception:
                        pass

                return __nina_trace

            __nina_tree = ast.parse(__nina_script_source, __nina_script_filename, "exec")
            __nina_tree = __nina_symbol_name_guard().visit(__nina_tree)
            ast.fix_missing_locations(__nina_tree)
            __nina_code = compile(__nina_tree, __nina_script_filename, "exec")

            if __nina_trace_callback is None:
                exec(__nina_code, globals(), locals())
            else:
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

            scope.Set("__nina_script_source", scriptToExecute.Script.ToPython());
            scope.Set("__nina_script_filename", ResolveScriptFileName(scriptToExecute).ToPython());

            if (reportLine != null) {
                scope.Set("__nina_trace_line", reportLine.ToPython());
            }

            scope.Exec(WrappedExecScript);
        }

        private static string ResolveScriptFileName(PythonScriptExecutionSource scriptToExecute) {
            return string.IsNullOrWhiteSpace(scriptToExecute.FilePath)
                ? InlineScriptFileName
                : scriptToExecute.FilePath;
        }
    }
}
