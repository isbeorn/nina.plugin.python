using NINA.Sequencer.Logic;
using System;

namespace NINA.Plugin.Python.PythonScriptingTestCategory {

    internal sealed class PythonSymbolProviderHelper {
        private readonly ISymbolBroker symbolBroker;

        public PythonSymbolProviderHelper(ISymbolBroker symbolBroker) {
            this.symbolBroker = symbolBroker ?? throw new ArgumentNullException(nameof(symbolBroker));
        }

        public ISymbolProvider GetOrCreate(string name) {
            if (string.IsNullOrWhiteSpace(name)) {
                throw new ArgumentException("Symbol provider name cannot be empty.", nameof(name));
            }

            foreach (var provider in symbolBroker.GetMyProviders()) {
                if (string.Equals(provider.GetProviderName(), name, StringComparison.OrdinalIgnoreCase)) {
                    return provider;
                }
            }

            return symbolBroker.RegisterSymbolProvider(name);
        }
    }
}
