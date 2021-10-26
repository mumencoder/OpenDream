
using System.Collections.Generic;
using OpenDreamShared.Compiler;
using OpenDreamShared.Compiler.DM;
using OpenDreamShared.Dream;

namespace DMCompiler.DM.Experimental {
    public class DMScope {
        public Dictionary<string, IDMSymbol> Identifiers;

        public void Add(string name, IDMSymbol ident) {
            Identifiers[name] = ident;
        }
    }
}
