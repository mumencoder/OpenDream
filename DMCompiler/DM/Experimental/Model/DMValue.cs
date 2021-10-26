using OpenDreamShared.Compiler;
using OpenDreamShared.Compiler.DM;
using OpenDreamShared.Dream;

namespace DMCompiler.DM.Experimental {
    public class DMValue {
    }

    public class DMExpressionValue : DMValue {
        public DMASTExpression Expression;

        public DMExpressionValue(DMASTExpression expr) {
            Expression = expr;
        }
    }
}
