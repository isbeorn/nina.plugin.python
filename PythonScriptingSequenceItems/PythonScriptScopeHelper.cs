using NINA.Sequencer.Logic;
using Python.Runtime;
using System.Collections.Generic;

namespace NINA.Plugin.Python.PythonScriptingTestCategory {

    internal static class PythonScriptScopeHelper {
        internal const string SymbolSnapshotVariableName = "__nina_symbols_snapshot";
        internal const string SymbolVariableNamesVariableName = "__nina_symbol_variable_names";
        internal const string SymbolFunctionInvokerVariableName = "__nina_symbol_function_invoker";
        internal const string SymbolFunctionNamesVariableName = "__nina_symbol_function_names";
        internal const string SymbolFunctionVariableNamesVariableName = "__nina_symbol_function_variable_names";

        private static readonly ISet<string> reservedVariableNames = new HashSet<string> {
            "symbols",
            "symbolFunctions"
        };

        public static void RegisterSymbolSnapshot(PyModule scope, ISymbolBroker symbolBroker) {
            using var symbolValues = new PyDict();
            var symbolVariableNames = new HashSet<string>();

            foreach (var symbol in symbolBroker.GetSymbols()) {
                string variableName = ToSymbolVariableName(symbol);
                if (reservedVariableNames.Contains(variableName) || !symbolVariableNames.Add(variableName)) {
                    continue;
                }

                using var value = ToPythonValue(symbol.Value);
                symbolValues.SetItem(variableName, value);
            }

            using var variableNames = new PyList();
            foreach (var variableName in symbolVariableNames) {
                using var name = variableName.ToPython();
                variableNames.Append(name);
            }

            using var functionNames = new PyList();
            using var functionVariableNames = new PyList();
            var symbolFunctionNames = new HashSet<string>();
            var symbolFunctionVariableNames = new HashSet<string>();

            foreach (var function in symbolBroker.GetFunctions()) {
                string functionName = ToSymbolFunctionName(function);
                if (!symbolFunctionNames.Add(functionName)) {
                    continue;
                }

                using var name = functionName.ToPython();
                functionNames.Append(name);

                if (reservedVariableNames.Contains(functionName) || symbolVariableNames.Contains(functionName)) {
                    continue;
                }

                if (symbolFunctionVariableNames.Add(functionName)) {
                    functionVariableNames.Append(name);
                }
            }

            using var functionInvoker = new PythonSymbolFunctionInvoker(symbolBroker).ToPython();
            scope.Set(SymbolSnapshotVariableName, symbolValues);
            scope.Set(SymbolVariableNamesVariableName, variableNames);
            scope.Set(SymbolFunctionInvokerVariableName, functionInvoker);
            scope.Set(SymbolFunctionNamesVariableName, functionNames);
            scope.Set(SymbolFunctionVariableNamesVariableName, functionVariableNames);
        }

        private static string ToSymbolVariableName(Symbol symbol) {
            string category = SymbolBroker.SanitizeIdentifier(symbol.Category);
            string key = SymbolBroker.SanitizeIdentifier(symbol.Key);
            return $"{category}_{key}";
        }

        private static string ToSymbolFunctionName(SymbolFunction function) {
            string category = SymbolBroker.SanitizeIdentifier(function.Category);
            string key = SymbolBroker.SanitizeIdentifier(function.Key);
            return $"{category}_{key}";
        }

        private static PyObject ToPythonValue(object value) {
            return value == null
                ? Runtime.None
                : value.ToPython();
        }
    }
}
