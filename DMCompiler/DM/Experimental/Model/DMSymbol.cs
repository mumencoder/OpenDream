using OpenDreamShared.Compiler;
using OpenDreamShared.Compiler.DM;
using OpenDreamShared.Dream;

namespace DMCompiler.DM.Experimental {

    public interface IDMSymbol {
    }

    public class ObjectFieldDefinition : IDMSymbol {
        public StorageLocation Storage;
        public DMValue InitialValue;
        public bool IsConst;

        public ObjectFieldDefinition(DMASTObjectVarDefinition varDef) {
            IsConst = varDef.IsConst;
        }
    }

    public class LocalDefinition : IDMSymbol {
        public StorageLocation Storage;
        public DMValue InitialValue;
        public bool IsConst;

        public LocalDefinition(DMASTObjectVarDefinition varDef) {
            IsConst = varDef.IsConst;
        }
    }

    public class ProcDefinition : IDMSymbol {
        public StorageLocation Storage;

        public ProcDefinition(DMASTProcDefinition procDef) {
        }
    }

}
