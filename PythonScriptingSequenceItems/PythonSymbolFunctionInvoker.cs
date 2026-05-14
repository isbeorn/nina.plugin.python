using NCalc.Handlers;
using NINA.Sequencer.Logic;
using Python.Runtime;
using System;
using System.Collections.Generic;

namespace NINA.Plugin.Python.PythonScriptingTestCategory {

    internal sealed class PythonSymbolFunctionInvoker {
        private readonly ISymbolBroker symbolBroker;

        public PythonSymbolFunctionInvoker(ISymbolBroker symbolBroker) {
            this.symbolBroker = symbolBroker ?? throw new ArgumentNullException(nameof(symbolBroker));
        }

        public object Invoke(string name, PyObject arguments) {
            var functionArgs = new FunctionArgs(Guid.NewGuid(), ToNCalcExpressions(arguments));
            symbolBroker.InvokeFunction(name, functionArgs, out var result, out _);
            return result;
        }

        private static NCalc.Expression[] ToNCalcExpressions(PyObject arguments) {
            if (arguments == null || arguments.IsNone()) {
                return [];
            }

            int count = checked((int)arguments.Length());
            var expressions = new NCalc.Expression[count];

            for (int i = 0; i < count; i++) {
                using var item = arguments.GetItem(i);
                expressions[i] = CreateArgumentExpression(i, ToManagedValue(item));
            }

            return expressions;
        }

        private static NCalc.Expression CreateArgumentExpression(int index, object value) {
            string parameterName = $"arg{index}";
            return new NCalc.Expression(parameterName) {
                Parameters = new Dictionary<string, object> {
                    [parameterName] = value
                }
            };
        }

        private static object ToManagedValue(PyObject value) {
            return value == null || value.IsNone()
                ? null
                : value.AsManagedObject(typeof(object));
        }
    }
}
