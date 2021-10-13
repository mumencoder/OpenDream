
using System.Collections.Generic;

namespace DMCompiler.DM {
    class DMScope {
        public Dictionary<string, DMVariable> Variables = new();
        public DMScope ParentScope;

        public DMScope() { }

        public DMScope(DMScope parentScope) {
            ParentScope = parentScope;
        }
    }
}
