
using System;
using OpenDreamShared.Compiler.DM;

namespace OpenDreamShared.Compiler.DM {
    public partial interface DMASTVisitor : ASTVisitor {

        public void Visit(Testing.DMASTDereferenceIdentifier dereference) { throw new NotImplementedException(); }
        public void Visit(Testing.DMASTDereferenceProc dereferenceProc) { throw new NotImplementedException(); }
    }
}

namespace OpenDreamShared.Compiler.DM.Testing {
 
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
