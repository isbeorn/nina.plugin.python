using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Sequencer.Container;
using NINA.Sequencer.Logic;
using NINA.Sequencer.SequenceItem.Expressions;
using Python.Runtime;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NINA.Plugin.Python.PythonScriptingTestCategory {

    internal sealed class PythonSequenceVariableStore {
        private readonly ISequenceContainer context;

        public PythonSequenceVariableStore(ISequenceContainer context) {
            this.context = context;
        }

        public bool Contains(string name) {
            return FindVariable(name) != null;
        }

        public object Get(string name) {
            var variable = FindVariable(name);
            if (variable == null) {
                throw new KeyNotFoundException($"N.I.N.A. sequencer variable '{name}' is not in scope.");
            }

            return GetVariableValue(variable);
        }

        public IReadOnlyList<string> Keys() {
            return GetVariablesInScope().Select(x => x.Identifier).ToList();
        }

        public void Set(string name, PyObject value) {
            var variable = FindVariable(name);
            if (variable == null) {
                throw new SequenceEntityFailedException($"N.I.N.A. sequencer variable '{name}' is not in scope.");
            }

            if (!variable.Executed) {
                throw new SequenceEntityFailedException($"N.I.N.A. sequencer variable '{name}' has not been executed.");
            }

            if (variable.Expr == null) {
                throw new SequenceEntityFailedException($"N.I.N.A. sequencer variable '{name}' does not have an expression.");
            }

            string oldDefinition = variable.Expr?.Definition;
            string newDefinition = ToExpressionDefinition(value);
            variable.Expr.Error = null;
            variable.Expr.Definition = newDefinition;
            variable.Expr.Evaluate();

            if (variable.Expr.Error != null && !Expression.JustWarnings(variable.Expr.Error)) {
                throw new SequenceEntityFailedException($"The value '{newDefinition}' was invalid for N.I.N.A. sequencer variable '{name}'.");
            }

            Logger.Info("Python SetVariable: " + name + " from " + oldDefinition + " to " + variable.Expr.Definition);
            UserSymbol.SymbolDirty(variable);
        }

        internal IEnumerable<Variable> GetVariablesInScope() {
            var variables = new Dictionary<string, Variable>();

            ISequenceContainer current = context;
            while (current != null) {
                AddSequenceVariables(current, variables);
                current = current.Parent;
            }

            AddSequenceVariables(UserSymbol.GlobalSymbols, variables);
            return variables.Values;
        }

        private static void AddSequenceVariables(ISequenceContainer context, IDictionary<string, Variable> variables) {
            if (context == null || !UserSymbol.SymbolCache.TryGetValue(context, out var symbols)) {
                return;
            }

            foreach (var symbol in symbols.Values) {
                if (symbol is not Variable variable
                    || string.IsNullOrWhiteSpace(variable.Identifier)
                    || variables.ContainsKey(variable.Identifier)
                    || !UserSymbol.IsAttachedToRoot(variable)) {
                    continue;
                }

                variables.Add(variable.Identifier, variable);
            }
        }

        private Variable FindVariable(string name) {
            if (string.IsNullOrWhiteSpace(name)) {
                return null;
            }

            return UserSymbol.FindSymbol(name, context) is Variable variable && UserSymbol.IsAttachedToRoot(variable)
                ? variable
                : null;
        }

        private static object GetVariableValue(Variable variable) {
            if (variable?.Expr == null || !variable.Executed) {
                return null;
            }

            variable.Expr.Evaluate();

            if (variable.Expr.StringValue != null) {
                return variable.Expr.StringValue;
            }

            return double.IsNaN(variable.Expr.Value)
                ? null
                : variable.Expr.Value;
        }

        private static string ToExpressionDefinition(PyObject value) {
            object managedValue = value == null || value.IsNone()
                ? null
                : value.AsManagedObject(typeof(object));

            return managedValue switch {
                null => throw new ArgumentException("N.I.N.A. sequencer variables cannot be set to None."),
                bool boolValue => boolValue ? "1" : "0",
                string stringValue => QuoteString(stringValue),
                char charValue => QuoteString(charValue.ToString()),
                DateTime dateTime => CoreUtil.ToUnixSeconds(dateTime).ToString(CultureInfo.InvariantCulture),
                DateTimeOffset dateTimeOffset => dateTimeOffset.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                IConvertible convertible => ConvertConvertible(convertible),
                _ => throw new ArgumentException($"Unsupported N.I.N.A. sequencer variable value type: {managedValue.GetType().Name}")
            };
        }

        private static string ConvertConvertible(IConvertible value) {
            double numericValue = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            if (double.IsNaN(numericValue) || double.IsInfinity(numericValue)) {
                throw new ArgumentException("N.I.N.A. sequencer variables cannot be set to NaN or infinity.");
            }

            return numericValue.ToString(CultureInfo.InvariantCulture);
        }

        private static string QuoteString(string value) {
            return "'" + value.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
        }
    }
}
