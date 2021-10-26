
using System.Collections.Generic;
using OpenDreamShared.Compiler;
using OpenDreamShared.Compiler.DM;
using OpenDreamShared.Dream;

namespace DMCompiler.DM.Experimental {
    class DeclarationWalker : ASTTraveler {

        protected override void Visit(DMASTObjectVarDefinition varDef) {
            if (varDef.IsToplevel) {
                LocalDefinition symbol = new LocalDefinition(varDef);
                symbol.Storage = _runContext.NewRegister();
                symbol.InitialValue = new DMExpressionValue(varDef.Value);
                GlobalScope.Add(varDef.Name, symbol);
            }
            else if (varDef.IsGlobal) {
                ObjectFieldDefinition symbol = new ObjectFieldDefinition(varDef);
                symbol.Storage = _runContext.NewRegister();
                symbol.InitialValue = new DMExpressionValue(varDef.Value);
                _currentObject.Scope.Add(varDef.Name, symbol);
            }
            else {
                ObjectFieldDefinition symbol = new ObjectFieldDefinition(varDef);
                symbol.Storage = _currentObject.NewFieldStorage();
                symbol.InitialValue = new DMExpressionValue(varDef.Value);
                _currentObject.Scope.Add(varDef.Name, symbol);
            }
            _inferredPath = varDef.Type;
        }
        protected override void Visit(DMASTObjectVarOverride varOverride) {
            if (varOverride.VarName == "parent_type") {
                DMASTConstantPath parentType = varOverride.Value as DMASTConstantPath;

                if (parentType == null) throw new CompileErrorException("Expected a constant path");
                _currentObject.Parent = _rootObject.GetDMObject(parentType.Value.Path);
            }
            else {
                IDMSymbol id = _currentObject.GetFieldIdent(varOverride.VarName);
                _currentObject.DefineOverride(varOverride);
            }
        }
        protected override void Visit(DMASTProcDefinition procDef) {
            if (!procDef.IsOverride && _currentObject.HasProc(procDef.Name)) {
                throw new CompileErrorException("Type " + procDef.ObjectPath + " already has a proc named \"" + procDef.Name + "\"");
            }
            ProcDefinition proc = new ProcDefinition(procDef);
            proc.Storage = _runContext.NewRegister();
            if (procDef.IsToplevel) {
                GlobalScope.Add(procDef.Name, proc);
            }
            else if (procDef.IsVerb) {
                _currentObject.Scope.Add(procDef.Name, proc);
                _currentObject.DefineVerb(_currentProc);
            }
            else {
                _currentObject.Scope.Add(procDef.Name, proc);
            }
        }
    }
}
