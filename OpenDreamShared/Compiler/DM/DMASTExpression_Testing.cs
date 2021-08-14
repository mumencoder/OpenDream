using System;
using OpenDreamShared.Compiler.DM;

namespace OpenDreamShared.Compiler.DM.Testing {
    public partial class DMParser : Parser<Token> {
        public DMASTExpression Expression() {
            Whitespace();
            return ExpressionAssign();
        }

        public DMASTExpression ExpressionAssign() {
            DMASTExpression expression = ExpressionTernary();

            if (expression != null) {
                Token token = Current();
                TokenType[] assignTypes = new TokenType[] {
                    TokenType.DM_Equals,
                    TokenType.DM_PlusEquals,
                    TokenType.DM_MinusEquals,
                    TokenType.DM_BarEquals,
                    TokenType.DM_AndEquals,
                    TokenType.DM_StarEquals,
                    TokenType.DM_SlashEquals,
                    TokenType.DM_LeftShiftEquals,
                    TokenType.DM_RightShiftEquals,
                    TokenType.DM_XorEquals,
                    TokenType.DM_ModulusEquals
                };

                Whitespace();
                if (Check(assignTypes)) {
                    Whitespace();
                    DMASTExpression value = ExpressionAssign();

                    if (value != null) {
                        switch (token.Type) {
                            case TokenType.DM_Equals: return new DMASTAssign(expression, value);
                            case TokenType.DM_PlusEquals: return new DMASTAppend(expression, value);
                            case TokenType.DM_MinusEquals: return new DMASTRemove(expression, value);
                            case TokenType.DM_BarEquals: return new DMASTCombine(expression, value);
                            case TokenType.DM_AndEquals: return new DMASTMask(expression, value);
                            case TokenType.DM_StarEquals: return new DMASTMultiplyAssign(expression, value);
                            case TokenType.DM_SlashEquals: return new DMASTDivideAssign(expression, value);
                            case TokenType.DM_LeftShiftEquals: return new DMASTLeftShiftAssign(expression, value);
                            case TokenType.DM_RightShiftEquals: return new DMASTRightShiftAssign(expression, value);
                            case TokenType.DM_XorEquals: return new DMASTXorAssign(expression, value);
                            case TokenType.DM_ModulusEquals: return new DMASTModulusAssign(expression, value);
                        }
                    }
                    else {
                        Error("Expected a value");
                    }
                }
            }

            return expression;
        }

        public DMASTExpression ExpressionTernary() {
            DMASTExpression a = ExpressionIn();

            if (a != null && Check(TokenType.DM_Question)) {
                DMASTExpression b = ExpressionTernary();
                if (b == null) Error("Expected an expression");
                Consume(TokenType.DM_Colon, "Expected ':'");
                DMASTExpression c = ExpressionTernary();
                if (c == null) Error("Expected an expression");

                return new DMASTTernary(a, b, c);
            }

            return a;
        }

        public DMASTExpression ExpressionIn() {
            DMASTExpression value = ExpressionStep();

            if (value != null && Check(TokenType.DM_In)) {
                Whitespace();
                DMASTExpression list = ExpressionStep();

                return new DMASTExpressionIn(value, list);
            }

            return value;
        }

        public DMASTExpression ExpressionStep() {
            DMASTExpression value = ExpressionTo();

            if (value != null && Check(TokenType.DM_Step)) {
                Whitespace();
                DMASTExpression list = ExpressionTo();

                return new Testing.DMASTExpressionStep(value, list);
            }

            return value;
        }

        public DMASTExpression ExpressionTo() {
            DMASTExpression value = ExpressionOr();

            if (value != null && Check(TokenType.DM_To)) {
                Whitespace();
                DMASTExpression list = ExpressionOr();

                return new Testing.DMASTExpressionTo(value, list);
            }

            return value;
        }

        public DMASTExpression ExpressionOr() {
            DMASTExpression a = ExpressionAnd();

            if (a != null && Check(TokenType.DM_BarBar)) {
                Whitespace();
                DMASTExpression b = ExpressionOr();
                if (b == null) Error("Expected a second value");

                return new DMASTOr(a, b);
            }

            return a;
        }

        public DMASTExpression ExpressionAnd() {
            DMASTExpression a = ExpressionBinaryOr();

            if (a != null && Check(TokenType.DM_AndAnd)) {
                Whitespace();
                DMASTExpression b = ExpressionAnd();
                if (b == null) Error("Expected a second value");

                return new DMASTAnd(a, b);
            }

            return a;
        }

        public DMASTExpression ExpressionBinaryOr() {
            DMASTExpression a = ExpressionBinaryXor();

            if (a != null && Check(TokenType.DM_Bar)) {
                Whitespace();
                DMASTExpression b = ExpressionBinaryOr();
                if (b == null) Error("Expected an expression");

                return new DMASTBinaryOr(a, b);
            }

            return a;
        }

        public DMASTExpression ExpressionBinaryXor() {
            DMASTExpression a = ExpressionBinaryAnd();

            if (a != null && Check(TokenType.DM_Xor)) {
                Whitespace();
                DMASTExpression b = ExpressionBinaryXor();
                if (b == null) Error("Expected an expression");

                return new DMASTBinaryXor(a, b);
            }

            return a;
        }

        public DMASTExpression ExpressionBinaryAnd() {
            DMASTExpression a = ExpressionComparison();

            if (a != null && Check(TokenType.DM_And)) {
                Whitespace();
                DMASTExpression b = ExpressionBinaryAnd();

                if (b == null) Error("Expected an expression");
                return new DMASTBinaryAnd(a, b);
            }

            return a;
        }

        public DMASTExpression ExpressionComparison() {
            DMASTExpression expression = ExpressionBitShift();

            if (expression != null) {
                Token token = Current();
                Whitespace();
                if (Check(new TokenType[] { TokenType.DM_EqualsEquals, TokenType.DM_ExclamationEquals, TokenType.DM_TildeEquals, TokenType.DM_TildeNot })) {
                    Whitespace();
                    DMASTExpression b = ExpressionComparison();

                    if (b == null) Error("Expected an expression to compare to");
                    switch (token.Type) {
                        case TokenType.DM_EqualsEquals: return new DMASTEqual(expression, b);
                        case TokenType.DM_ExclamationEquals: return new DMASTNotEqual(expression, b);
                        // TODO: add the AST nodes in for this feature
                        case TokenType.DM_TildeEquals: return new DMASTNotEqual(expression, b);
                        case TokenType.DM_TildeNot: return new DMASTNotEqual(expression, b);
                    }
                }
            }

            return expression;
        }

        public DMASTExpression ExpressionBitShift() {
            DMASTExpression a = ExpressionComparisonLtGt();

            if (a != null) {
                if (Check(TokenType.DM_LeftShift)) {
                    Whitespace();
                    DMASTExpression b = ExpressionBitShift();
                    if (b == null) Error("Expected an expression");

                    return new DMASTLeftShift(a, b);
                }
                else if (Check(TokenType.DM_RightShift)) {
                    Whitespace();
                    DMASTExpression b = ExpressionBitShift();
                    if (b == null) Error("Expected an expression");

                    return new DMASTRightShift(a, b);
                }
            }

            return a;
        }

        public DMASTExpression ExpressionComparisonLtGt() {
            DMASTExpression a = ExpressionAdditionSubtraction();

            if (a != null) {
                Token token = Current();
                TokenType[] types = new TokenType[] {
                    TokenType.DM_LessThan,
                    TokenType.DM_LessThanEquals,
                    TokenType.DM_GreaterThan,
                    TokenType.DM_GreaterThanEquals
                };

                Whitespace();
                if (Check(types)) {
                    Whitespace();
                    DMASTExpression b = ExpressionComparisonLtGt();
                    if (b == null) Error("Expected an expression");

                    switch (token.Type) {
                        case TokenType.DM_LessThan: return new DMASTLessThan(a, b);
                        case TokenType.DM_LessThanEquals: return new DMASTLessThanOrEqual(a, b);
                        case TokenType.DM_GreaterThan: return new DMASTGreaterThan(a, b);
                        case TokenType.DM_GreaterThanEquals: return new DMASTGreaterThanOrEqual(a, b);
                    }
                }
            }

            return a;
        }

        public DMASTExpression ExpressionAdditionSubtraction() {
            DMASTExpression a = ExpressionMultiplicationDivisionModulus();

            if (a != null) {
                Token token = Current();
                TokenType[] types = new TokenType[] {
                    TokenType.DM_Plus,
                    TokenType.DM_Minus,
                };

                Whitespace();
                while (Check(types)) {
                    Whitespace();
                    DMASTExpression b = ExpressionMultiplicationDivisionModulus();
                    if (b == null) Error("Expected an expression");

                    switch (token.Type) {
                        case TokenType.DM_Plus: a = new DMASTAdd(a, b); break;
                        case TokenType.DM_Minus: a = new DMASTSubtract(a, b); break;
                    }

                    token = Current();
                }
            }

            return a;
        }

        public DMASTExpression ExpressionMultiplicationDivisionModulus() {
            DMASTExpression a = ExpressionPower();

            if (a != null) {
                Token token = Current();
                TokenType[] types = new TokenType[] {
                    TokenType.DM_Star,
                    TokenType.DM_Slash,
                    TokenType.DM_Modulus
                };

                Whitespace();
                while (Check(types)) {
                    Whitespace();
                    DMASTExpression b = ExpressionPower();
                    if (b == null) Error("Expected an expression");

                    switch (token.Type) {
                        case TokenType.DM_Star: a = new DMASTMultiply(a, b); break;
                        case TokenType.DM_Slash: a = new DMASTDivide(a, b); break;
                        case TokenType.DM_Modulus: a = new DMASTModulus(a, b); break;
                    }

                    token = Current();
                }
            }

            return a;
        }

        public DMASTExpression ExpressionPower() {
            DMASTExpression a = ExpressionUnary();

            if (a != null && Check(TokenType.DM_StarStar)) {
                Whitespace();
                DMASTExpression b = ExpressionUnary();
                if (b == null) Error("Expected an expression");

                return new DMASTPower(a, b);
            }

            return a;
        }


        public DMASTExpression ExpressionUnary() {
            if (Check(TokenType.DM_Exclamation)) {
                Whitespace();
                DMASTExpression expression = ExpressionUnary();
                if (expression == null) Error("Expected an expression");

                return new DMASTNot(expression);
            }
            else if (Check(TokenType.DM_Tilde)) {
                Whitespace();
                DMASTExpression expression = ExpressionUnary();
                if (expression == null) Error("Expected an expression");

                return new DMASTBinaryNot(expression);
            }
            else if (Check(TokenType.DM_PlusPlus)) {
                Whitespace();
                DMASTExpression expression = ExpressionSign();
                if (expression == null) Error("Expected an expression");

                return new DMASTPreIncrement(expression);
            }
            else if (Check(TokenType.DM_MinusMinus)) {
                Whitespace();
                DMASTExpression expression = ExpressionSign();
                if (expression == null) Error("Expected an expression");

                return new DMASTPreDecrement(expression);
            }
            else {
                DMASTExpression expression = ExpressionSign();

                if (expression != null) {
                    if (Check(TokenType.DM_PlusPlus)) {
                        Whitespace();
                        expression = new DMASTPostIncrement(expression);
                    }
                    else if (Check(TokenType.DM_MinusMinus)) {
                        Whitespace();
                        expression = new DMASTPostDecrement(expression);
                    }
                }

                return expression;
            }
        }

        public DMASTExpression ExpressionSign() {
            Token token = Current();

            Whitespace();
            if (Check(new TokenType[] { TokenType.DM_Plus, TokenType.DM_Minus })) {
                Whitespace();
                DMASTExpression expression = ExpressionDereference();

                if (expression == null) Error("Expected an expression");
                if (token.Type == TokenType.DM_Minus) {
                    if (expression is DMASTConstantInteger) {
                        int value = ((DMASTConstantInteger)expression).Value;

                        return new DMASTConstantInteger(-value);
                    }
                    else if (expression is DMASTConstantFloat) {
                        float value = ((DMASTConstantFloat)expression).Value;

                        return new DMASTConstantFloat(-value);
                    }

                    return new DMASTNegate(expression);
                }
                else {
                    return expression;
                }
            }
            else {
                return ExpressionDereference();
            }
        }

        /*
        public DMASTExpression ExpressionConditionalDereference() {
            Whitespace();
            DMASTExpression a = ExpressionDereference();
            Whitespace();
            TokenType[] types = new TokenType[] {
                    TokenType.DM_QuestionPeriod,
                    TokenType.DM_QuestionColon
            };
            var previous_expr = a;
            Token op = Current();
            while (Check(types)) {
                if (op.Type == TokenType.DM_QuestionPeriod) {
                    if (op.LeadingWhitespace) { ReuseToken(op); return previous_expr; }
                    Whitespace();
                    var b = ExpressionDereference();
                    switch (b) {
                        case DMASTIdentifier id: previous_expr = new DMASTDereferenceIdentifier(previous_expr, DMASTDereference.Type.Direct, true, id); break;
                        case DMASTProcCall call: {
                                previous_expr = new DMASTDereferenceProc(previous_expr, DMASTDereference.Type.Direct, true, call); break;
                            }
                        default: ReuseToken(op); return previous_expr;
                    }
                }
                else if (op.Type == TokenType.DM_QuestionColon) {
                    if (op.LeadingWhitespace) { ReuseToken(op); return previous_expr; }
                    Whitespace();
                    var b = ExpressionDereference();
                    switch (b) {
                        case DMASTIdentifier id: previous_expr = new DMASTDereferenceIdentifier(previous_expr, DMASTDereference.Type.Search, true, id); break;
                        case DMASTProcCall call: {
                                previous_expr = new DMASTDereferenceProc(previous_expr, DMASTDereference.Type.Search, true, call); break;
                            }
                        default: ReuseToken(op); return previous_expr;
                    }
                }
                else {
                    return previous_expr;
                }
                op = Current();
            }
            return previous_expr;
        }
        */

        public DMASTExpression ExpressionDereference() {
            Whitespace();
            DMASTExpression a = ExpressionExplicitParenthesis();
            Whitespace();
            TokenType[] types = new TokenType[] {
                TokenType.DM_QuestionPeriod,
                TokenType.DM_QuestionColon,
                TokenType.DM_Period,
                TokenType.DM_Colon,
                TokenType.DM_LeftBracket
            };
            var previous_expr = a;
            Token op = Current();
            while (previous_expr != null && Check(types)) {
                if (previous_expr is DMASTExpressionConstant || previous_expr is DMASTStringFormat) {
                    ReuseToken(op); return previous_expr;
                }

                if (op.Type == TokenType.DM_QuestionPeriod) {
                    if (op.LeadingWhitespace || op.TrailingWhitespace) { ReuseToken(op); return previous_expr; }
                    SavePosition();
                    var b = ExpressionExplicitParenthesis();
                    switch (b) {
                        case DMASTIdentifier id: previous_expr = new DMASTDereferenceIdentifier(previous_expr, DMASTDereference.Type.Direct, true, id); break;
                        case DMASTProcCall call: {
                                previous_expr = new DMASTDereferenceProc(previous_expr, DMASTDereference.Type.Direct, true, call); break;
                            }
                        default: RestorePosition(); ReuseToken(op); return previous_expr;
                    }
                    AcceptPosition();
                }
                else if (op.Type == TokenType.DM_QuestionColon) {
                    if (op.LeadingWhitespace || op.TrailingWhitespace) { ReuseToken(op); return previous_expr; }
                    SavePosition();
                    var b = ExpressionExplicitParenthesis();
                    switch (b) {
                        case DMASTIdentifier id: previous_expr = new DMASTDereferenceIdentifier(previous_expr, DMASTDereference.Type.Search, true, id); break;
                        case DMASTProcCall call: {
                                previous_expr = new DMASTDereferenceProc(previous_expr, DMASTDereference.Type.Search, true, call); break;
                            }
                        default: RestorePosition(); ReuseToken(op); return previous_expr;
                    }
                    AcceptPosition();
                }
                else if (op.Type == TokenType.DM_Period) {
                    if (op.LeadingWhitespace || op.TrailingWhitespace) { ReuseToken(op); return previous_expr; }
                    SavePosition();
                    var b = ExpressionExplicitParenthesis();
                    switch (b) {
                        case DMASTIdentifier id: previous_expr = new DMASTDereferenceIdentifier(previous_expr, DMASTDereference.Type.Direct, false, id); break;
                        case DMASTProcCall call: {
                                previous_expr = new DMASTDereferenceProc(previous_expr, DMASTDereference.Type.Direct, false, call); break;
                            }
                        default: RestorePosition(); ReuseToken(op); return previous_expr;
                    }
                    AcceptPosition();
                }
                else if (op.Type == TokenType.DM_Colon) {
                    if (op.LeadingWhitespace || op.TrailingWhitespace) { ReuseToken(op); return previous_expr; }
                    SavePosition();
                    var b = ExpressionExplicitParenthesis();
                    switch (b) {
                        case DMASTIdentifier id: previous_expr = new DMASTDereferenceIdentifier(previous_expr, DMASTDereference.Type.Search, false, id); break;
                        case DMASTProcCall call: {
                                previous_expr = new DMASTDereferenceProc(previous_expr, DMASTDereference.Type.Search, false, call); break;
                            }
                        default: RestorePosition(); ReuseToken(op); return previous_expr;
                    }
                    AcceptPosition();
                }
                else if (op.Type == TokenType.DM_LeftBracket) {
                    Whitespace();
                    var inner = Expression();
                    Whitespace();
                    Consume(TokenType.DM_RightBracket, "Expected ']' got " + Current().Type);
                    previous_expr = new DMASTListIndex(previous_expr, inner);
                }
                else {
                    return previous_expr;
                }
                op = Current();
            }
            return previous_expr;
        }

        public DMASTExpression ExpressionExplicitParenthesis() {
            if (Check(TokenType.DM_LeftParenthesis)) {
                var expr = Expression();
                Consume(TokenType.DM_RightParenthesis, "Expected ')'");
                return expr;
            }
            else {
                return ExpressionPrimary();
            }
        }

        public DMASTPathExpression ExpressionPath(int explicitStatus) {
            TokenType[] types = new TokenType[] {
                    TokenType.DM_Colon,
                    TokenType.DM_Slash,
                    TokenType.DM_Period
                };

            var previous_expr = ConstPath(explicitStatus);
            Token op = Current();
            SavePosition();
            while (previous_expr != null && Check(types)) {
                if (previous_expr is not Testing.DMASTPathExpression) { ReuseToken(op); return previous_expr; }
                if (op.Type == TokenType.DM_Slash) {
                    if (op.LeadingWhitespace) { ReuseToken(op); return previous_expr; }
                    //Console.WriteLine("hmm1!" + PrintDebugTokens());
                    var b = ConstPath(0);
                    //if (b != null) { Console.WriteLine(b.PrintNodes()); }
                    //else { Console.WriteLine("null"); }
                    //Console.WriteLine("hmm2!" + PrintDebugTokens());
                    if (b is not Testing.DMASTPathExpression) {
                        AcceptPosition();
                    //    Console.WriteLine("hmm3!" + PrintDebugTokens());
                    //    Console.WriteLine("ast is" + previous_expr.PrintNodes());
                        return previous_expr; }
                    previous_expr = new Testing.DMASTDirectPath(previous_expr as DMASTPathExpression, b as DMASTPathExpression);
                }
                else if (op.Type == TokenType.DM_Colon) {
                    if (op.LeadingWhitespace || op.TrailingWhitespace) { ReuseToken(op); return previous_expr; }
                    var b = ConstPath(0);
                    if (b is not Testing.DMASTPathExpression) { RestorePosition(); ReuseToken(op); return previous_expr; }
                    previous_expr = new Testing.DMASTDownwardPath(previous_expr as DMASTPathExpression, b as DMASTPathExpression);
                }
                else if (op.Type == TokenType.DM_Period) {
                    if (op.LeadingWhitespace || op.TrailingWhitespace) { ReuseToken(op); return previous_expr; }
                    var b = ConstPath(0);
                    if (b is not Testing.DMASTPathExpression) { RestorePosition(); ReuseToken(op); return previous_expr; }
                    previous_expr = new Testing.DMASTUpwardPath(previous_expr as DMASTPathExpression, b as DMASTPathExpression);
                }
                else {
                    return previous_expr;
                }
                AcceptPosition();
                SavePosition();
                op = Current();
            }
            return previous_expr;
        }
    }
}
