using System;
using System.Collections.Generic;

namespace OpenDreamShared.Compiler.DM.Testing {
    public partial class DMParser : Parser<Token> {

        public DMASTExpression Identifier(string id) {
            var idexpr = Identifier();
            if (idexpr.Identifier != id) {
                return null;
            }
            return idexpr;
        }

        public DMASTExpression PickFunction() {
            if (Check(TokenType.DM_LeftParenthesis)) {
                List<DMASTPickEntry> entries = new();

                while (!Check(TokenType.DM_RightParenthesis)) {
                    var expr_1 = Expression();

                    if (Check(TokenType.DM_Comma)) {
                        entries.Add(new DMASTPickEntry(null, expr_1));
                    }
                    else if (Current().Type == TokenType.DM_Semicolon || Current().Type == TokenType.Newline) {
                        Advance();
                        var val_expr = Expression();
                        entries.Add(new DMASTPickEntry(expr_1, val_expr));
                    }
                    else if (Check(TokenType.DM_RightParenthesis)) {
                        break;
                    }
                    else {
                        Error("Expected ) in pick function");
                    }
                }
                return new DMASTPickFunction(entries.ToArray());
            }
            return null;
        }
    }
}
