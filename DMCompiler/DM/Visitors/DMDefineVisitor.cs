
using DMCompiler.Compiler.DM;
using System;

namespace DMCompiler.DM.Visitors {
    public class DMDefineVisitor : DMASTVisitor {
        Action<DMASTNode> Handler;

        public DMDefineVisitor(Action<DMASTNode> handler) {
            Handler = handler;
        }

        public void VisitFile(DMASTFile dmFile) {
            dmFile.BlockInner.Visit(this);
        }

        public void VisitBlockInner(DMASTBlockInner blockInner) {
            foreach (DMASTStatement statement in blockInner.Statements) {
                statement.Visit(this);
            }
        }

        public void VisitObjectDefinition(DMASTObjectDefinition statement) {
            Handler(statement);
            statement.InnerBlock?.Visit(this);
        }

        public void VisitObjectVarDefinition(DMASTObjectVarDefinition objectVarDefinition) {
            Handler(objectVarDefinition);
        }

        public void VisitMultipleObjectVarDefinitions(DMASTMultipleObjectVarDefinitions multipleObjectVarDefinitions) {
            foreach (DMASTObjectVarDefinition varDefinition in multipleObjectVarDefinitions.VarDefinitions) {
                varDefinition.Visit(this);
            }
        }

        public void VisitObjectVarOverride(DMASTObjectVarOverride objectVarOverride) {
            Handler(objectVarOverride);
        }

        public void VisitProcDefinition(DMASTProcDefinition procDefinition) {
            Handler(procDefinition);
        }
    }
}
