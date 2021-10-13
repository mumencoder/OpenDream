using OpenDreamShared.Dream;
using OpenDreamShared.Compiler.DM;

namespace DMCompiler.DM {
    class DMVariable {
        public DreamPath? Type;
        public string Name;
        public bool IsGlobal;
        public bool IsConst;
        public DMExpression InitialValue = null;
        public DMExpression Value = null;
        public int localId = -1;

        public DMVariable(DMASTObjectVarDefinition decl) {
            Type = decl.Type;
            Name = decl.Name;
            IsGlobal = decl.IsGlobal;
            IsConst = decl.IsConst;
        }

        public DMVariable(DMASTProcStatementVarDeclaration decl) {
            Type = decl.Type;
            Name = decl.Name;
            IsGlobal = decl.IsGlobal;
            IsConst = decl.IsConst;
        }
        public DMVariable(DreamPath? type, string name, bool isGlobal = false, bool isConst = false) {
            Type = type;
            Name = name;
            IsGlobal = isGlobal;
            IsConst = isConst;
        }
    }
}
