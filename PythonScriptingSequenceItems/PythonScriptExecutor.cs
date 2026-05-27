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
            _nina_sequence_variable_store = globals().get("__nina_sequence_variable_store")
            _nina_sequence_variable_direct_names = globals().get("__nina_sequence_variable_direct_names", {})
            _nina_script_filename = __nina_script_filename
            _nina_symbol_names = frozenset(globals().get("__nina_symbol_variable_names", ()))
            _nina_symbol_function_names = frozenset(globals().get("__nina_symbol_function_names", ()))
            _nina_symbol_function_variable_names = frozenset(globals().get("__nina_symbol_function_variable_names", ()))
            _nina_sequence_variable_names = frozenset(globals().get("__nina_sequence_variable_names", ()))
            _nina_protected_helper_names = ("symbols", "symbolFunctions", "variables", "setVariable", "getSymbolProvider")
            symbols = MappingProxyType(__nina_symbol_values)

            class _nina_sequence_variables:
                def __contains__(self, name):
                    return _nina_sequence_variable_store.Contains(name)

                def __getitem__(self, name):
                    return _nina_sequence_variable_store.Get(name)

                def __iter__(self):
                    return iter(self.keys())

                def __len__(self):
                    return len(self.keys())

                def get(self, name, default=None):
                    if name in self:
                        return self[name]

                    return default

                def items(self):
                    return tuple((name, self[name]) for name in self.keys())

                def keys(self):
                    return tuple(_nina_sequence_variable_store.Keys())

                def values(self):
                    return tuple(self[name] for name in self.keys())

            variables = _nina_sequence_variables()

            class _nina_sequence_variable_aliases:
                def __getitem__(self, alias):
                    return variables[_nina_sequence_variable_direct_names[alias]]

            __nina_sequence_variable_direct_values = _nina_sequence_variable_aliases()

            def setVariable(variable, value):
                _nina_sequence_variable_store.Set(variable, value)

            def getSymbolProvider(name):
                return __nina_symbol_provider_helper.GetOrCreate(name)

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
                        f"{name} is a protected N.I.N.A. injected value or function.",
                        (_nina_script_filename, getattr(node, "lineno", 1), getattr(node, "col_offset", 0) + 1, ""))

                def _nina_is_read_only_name(self, name):
                    return name in _nina_symbol_names or name in _nina_symbol_function_variable_names or name in _nina_sequence_variable_names or name in _nina_protected_helper_names

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

                    if node.id in _nina_sequence_variable_names:
                        if isinstance(node.ctx, ast.Load):
                            return ast.copy_location(
                                ast.Subscript(
                                    value=ast.Name(id="__nina_sequence_variable_direct_values", ctx=ast.Load()),
                                    slice=ast.Constant(value=node.id),
                                    ctx=node.ctx),
                                node)

                        if isinstance(node.ctx, (ast.Store, ast.Del)):
                            self._nina_reject(node.id, node)

                    if node.id in _nina_protected_helper_names and isinstance(node.ctx, (ast.Store, ast.Del)):
                        self._nina_reject(node.id, node)

                    return node

                def visit_Call(self, node):
                    is_set_variable_call = isinstance(node.func, ast.Name) and node.func.id == "setVariable"
                    if is_set_variable_call and node.args and isinstance(node.args[0], ast.Name) and node.args[0].id in _nina_sequence_variable_names:
                        direct_name = node.args[0].id
                        node.args[0] = ast.copy_location(
                            ast.Constant(value=_nina_sequence_variable_direct_names[direct_name]),
                            node.args[0])

                    self.generic_visit(node)
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
