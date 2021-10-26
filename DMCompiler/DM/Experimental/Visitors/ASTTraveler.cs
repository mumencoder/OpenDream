
using System.Collections.Generic;
using OpenDreamShared.Compiler;
using OpenDreamShared.Compiler.DM;
using OpenDreamShared.Dream;

namespace DMCompiler.DM.Experimental {
    public class ASTTraveler {

        // Tree
        protected DMTree _treeRoot = new DMTree();
        protected DMTree _currentObjectPath;

        // Scopes
        public DMScope GlobalScope = new();

        // Objects

        // Procs

        // Runtime
        protected RuntimeContext _runContext = new RuntimeContext();

        // Expressions
        protected DreamPath? _inferredPath;

        protected virtual void Visit(DMASTObjectVarDefinition varDef) { }
        protected virtual void Visit(DMASTObjectVarOverride varOverride) { }
        protected virtual void Visit(DMASTProcDefinition varOverride) { }

        public void Travel(DMASTFile file) {
            Travel(file.BlockInner);
        }
        public void Travel(DMASTBlockInner blockInner) {
            foreach (DMASTStatement statement in blockInner.Statements) {
                Travel((dynamic)statement);
            }
        }
        public void Travel(DMASTObjectDefinition objectDef) {
            _currentObjectPath = _treeRoot.GetTree(objectDef.Path);
            Travel(objectDef.InnerBlock);
        }

        public void Travel(DMASTObjectVarDefinition varDef) {
            _currentObjectPath = varDef.ObjectPath != null ? _treeRoot.GetTree(varDef.ObjectPath.Value) : _treeRoot;
            try {
                Visit(varDef);
            }
            catch (CompileErrorException e) {
                Program.Error(e.Error);
            }
        }
        public void Travel(DMASTObjectVarOverride varOverride) {
            _currentObjectPath = _treeRoot.GetTree(varOverride.ObjectPath);
            try {
                Visit(varOverride);
            }
            catch (CompileErrorException e) {
                Program.Error(e.Error);
            }
        }
        public void Travel(DMASTProcDefinition procDef) {
            _currentObjectPath = _treeRoot.GetTree(procDef.ObjectPath);
            try {
                Visit(procDef);
            }
            catch (CompileErrorException e) {
                Program.Error(e.Error);
            }
        }

    }
}
