
using System;
using OpenDreamShared.Compiler.DM;

namespace OpenDreamShared.Compiler.DM {
    public partial interface DMASTVisitor : ASTVisitor {
        public void Visit(Testing.DMASTProcStatementTryCatch node) { throw new NotImplementedException(); }
        public void Visit(Testing.DMASTProcStatementEmpty node) { throw new NotImplementedException(); }
        public void Visit(Testing.DMASTExpressionTo node) { throw new NotImplementedException(); }
        public void Visit(Testing.DMASTExpressionStep node) { throw new NotImplementedException(); }
        public void Visit(Testing.DMASTProcStatementContinue node) { throw new NotImplementedException(); }
        public void Visit(Testing.DMASTProcStatementBreak node) { throw new NotImplementedException(); }
        public void Visit(Testing.DMASTPickFunction node) { throw new NotImplementedException(); }
        public void Visit(Testing.DMASTProcStatementLabel node) { throw new NotImplementedException(); }
        public void Visit(Testing.DMASTDereferenceIdentifier node) { throw new NotImplementedException(); }
        public void Visit(Testing.DMASTDereferenceProc node) { throw new NotImplementedException(); }
    }
}

namespace OpenDreamShared.Compiler.DM.Testing {

    public class DMASTProcStatementTryCatch : DMASTProcStatement {
        public DMASTProcBlockInner TryBlock;
        public DMASTProcBlockInner CatchBlock;
        public DMASTProcStatementVarDeclaration CatchVar;

        public DMASTProcStatementTryCatch(DMASTProcBlockInner t, DMASTProcBlockInner c, DMASTProcStatementVarDeclaration cv) {
            TryBlock = t;
            CatchBlock = c;
            CatchVar = cv;
        }
        public void Visit(DM.DMASTVisitor visitor) {
            visitor.Visit(this);
        }
    }

    public class DMASTProcStatementEmpty : DMASTProcStatement {
        public void Visit(DM.DMASTVisitor visitor) {
            visitor.Visit(this);
        }

    }
    public class DMASTExpressionTo : DMASTExpression {
        public DMASTExpression Value;
        public DMASTExpression List;

        public DMASTExpressionTo(DMASTExpression value, DMASTExpression list) {
            Value = value;
            List = list;
        }

        public void Visit(DM.DMASTVisitor visitor) {
            visitor.Visit(this);
        }
    }

    public class DMASTExpressionStep : DMASTExpression {
        public DMASTExpression Value;
        public DMASTExpression List;

        public DMASTExpressionStep(DMASTExpression value, DMASTExpression list) {
            Value = value;
            List = list;
        }

        public void Visit(DM.DMASTVisitor visitor) {
            visitor.Visit(this);
        }
    }

    public class DMASTProcStatementBreak : DMASTProcStatement {
        public DMASTIdentifier Identifier;
        public DMASTProcStatementBreak(DMASTIdentifier ident) {
            Identifier = ident;
        }
        public void Visit(DM.DMASTVisitor visitor) {
            visitor.Visit(this);
        }
    }

    public class DMASTProcStatementContinue : DMASTProcStatement {
        public DMASTIdentifier Identifier;
        public DMASTProcStatementContinue(DMASTIdentifier ident) {
            Identifier = ident;
        }
        public void Visit(DM.DMASTVisitor visitor) {
            visitor.Visit(this);
        }
    }

    public class DMASTPickFunction : DMASTExpression {
        public DMASTPickEntry[] Entries;

        public DMASTPickFunction(DMASTPickEntry[] entries) {
            Entries = entries;
        }

        public void Visit(DM.DMASTVisitor visitor) {
            visitor.Visit(this);
        }

    }

    public class DMASTProcStatementLabel : DMASTProcStatement {
        public DMASTIdentifier Identifier;
        public DMASTProcBlockInner InnerBlock;

        public DMASTProcStatementLabel(DMASTIdentifier ident, DMASTProcBlockInner inner) {
            Identifier = ident;
            InnerBlock = inner;
        }
        public void Visit(DM.DMASTVisitor visitor) {
            visitor.Visit(this);
        }
    }

    public class DMASTPickEntry {
        public DMASTExpression ProbExpr;
        public DMASTExpression ValueExpr;

        public DMASTPickEntry(DMASTExpression probExpr, DMASTExpression valueExpr) {
            ProbExpr = probExpr;
            ValueExpr = valueExpr;
        }
    }
    public class DMASTDereference {
        public enum Type {
            Direct,
            Search,
        }
        public DMASTDereference.Type DerefType;
        public DMASTExpression Expression;
        public bool Conditional;

        public DMASTDereference(DMASTExpression expression, DMASTDereference.Type type, bool conditional) {
            DerefType = type;
            Expression = expression;
            Conditional = conditional;
        }

    }

    public class DMASTDereferenceIdentifier : DMASTDereference, DMASTCallable {
        public DMASTIdentifier Property;
        public DMASTDereferenceIdentifier(DMASTExpression expression, DMASTDereference.Type ty, bool conditional, DMASTIdentifier property) : base(expression, ty, conditional) {
            Property = property;
        }
        public void Visit(DM.DMASTVisitor visitor) {
            visitor.Visit(this);
        }
    }
    public class DMASTDereferenceProc : DMASTDereference, DMASTCallable {
        public DMASTProcCall Proc;
        public DMASTDereferenceProc(DMASTExpression expression, DMASTDereference.Type ty, bool conditional, DMASTProcCall proc) : base(expression, ty, conditional) {
            Proc = proc;
        }

        public void Visit(DM.DMASTVisitor visitor) {
            visitor.Visit(this);
        }
    }
}
