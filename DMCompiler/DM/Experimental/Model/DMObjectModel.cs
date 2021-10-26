
using System.Collections.Generic;
using OpenDreamShared.Compiler;
using OpenDreamShared.Compiler.DM;
using OpenDreamShared.Dream;

namespace DMCompiler.DM.Experimental {
    public class DMObjectModel {
        public DMScope Scope = new();
        public DMObjectModel Parent;
        
        public void DefineOverride(DMASTObjectVarOverride ident) {
        }

        public void DefineProc(string name, DMProcModel proc) {
        }

        public void DefineVerb(DMProcModel proc) {
        }

        public IDMSymbol GetFieldIdent(string name) {
            return null;
        }

        public StorageLocation NewFieldStorage() {
            return new FieldStorageLocation(this);
        }
    }
}
