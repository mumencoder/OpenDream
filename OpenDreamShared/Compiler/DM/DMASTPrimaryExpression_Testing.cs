
using System;
using System.Collections.Generic;
using OpenDreamShared.Dream.Procs;
using System.Text;
using OpenDreamShared.Compiler.DMPreprocessor;
using OpenDreamShared.Dream;
using DereferenceType = OpenDreamShared.Compiler.DM.DMASTDereference.DereferenceType;
using Dereference = OpenDreamShared.Compiler.DM.DMASTDereference.Dereference;
using OpenDreamShared.Compiler;

namespace OpenDreamShared.Compiler.DM.Testing {
    public partial class DMParser : Parser<Token> {
        public DMASTExpression ExpressionPrimary() {
            Whitespace();
            DMASTExpression primary = null;
            primary = Constant();
            if (primary != null) {
                return primary;
            }
            if (Check(TokenType.DM_List)) {
                DMASTCallParameter[] values = ProcCall();
                return new DMASTList(values);
            }
            if (Check(TokenType.DM_NewList)) {
                Whitespace();
                DMASTCallParameter[] values = ProcCall(false);

                return new DMASTNewList(values);
            }
            if (Check(TokenType.DM_New)) {
                Whitespace();
                DMASTCallParameter[] parameters = null;
                if (Check(TokenType.DM_LeftParenthesis)) {
                    parameters = CallParameters(true);
                    Consume(TokenType.DM_RightParenthesis, "Expected ')'");
                    return new DMASTNewInferred(parameters);
                }
                else {
                    Testing.DMASTPathExpression path_expr = ExpressionPath(0) as Testing.DMASTPathExpression;
                    if (Check(TokenType.DM_LeftParenthesis)) {
                        parameters = CallParameters(true);
                        Consume(TokenType.DM_RightParenthesis, "Expected ')'");
                    }
                    return new Testing.DMASTNewTyped(path_expr, parameters);
                }
            }
            if (Check(TokenType.DM_Call)) {
                Whitespace();
                DMASTCallParameter[] callParameters = ProcCall();
                if (callParameters == null || callParameters.Length < 1 || callParameters.Length > 2) Error("Call must have 2 parameters");
                Whitespace();
                DMASTCallParameter[] procParameters = ProcCall();
                if (procParameters == null) Error("Expected proc parameters");

                return new DMASTCall(callParameters, procParameters);
            }

            primary = ExpressionExplicitPath();
            if (primary != null) {
                return primary;
            }

            DMASTCallable callable = null;
            if (Check(TokenType.DM_SuperProc)) { callable = new DMASTCallableSuper(); }
            else if (Check(TokenType.DM_Period)) { callable = new DMASTCallableSelf(); }
            if (callable != null) {
                DMASTCallParameter[] callParameters = ProcCall();
                if (callParameters != null) {
                    primary = new DMASTProcCall(callable, callParameters);
                }
                else {
                    primary = callable;
                }
                return primary;
            }

            DMASTIdentifier id = Identifier();
            if (id != null) {
                if (id.Identifier == "pick") {
                    var pickFn = PickFunction();
                    if (pickFn != null) {
                        return pickFn;
                    }
                }
                Whitespace();
                DMASTCallParameter[] callParameters = ProcCall();
                if (callParameters != null) {
                    Whitespace();
                    var specProc = SpecialProc(id, callParameters);
                    if (specProc != null) { return specProc; }
                    else {
                        var callableproc = new DMASTCallableProcIdentifier(id.Identifier);
                        primary = new DMASTProcCall(callableproc, callParameters);
                        return primary;
                    }
                }
                return id;
            }
            return null;
        }

        public DMASTExpression SpecialProc(DMASTIdentifier identifier, DMASTCallParameter[] callParameters) {
            if (identifier != null && identifier.Identifier == "input") {
                DMValueType types = AsTypes(defaultType: DMValueType.Text);
                Whitespace();
                DMASTExpression list = null;

                if (Check(TokenType.DM_In)) {
                    Whitespace();
                    list = Expression();
                }

                return new DMASTInput(callParameters, types, list);
            }
            else if (identifier != null && identifier.Identifier == "initial") {
                if (callParameters.Length != 1) Error("initial() requires 1 argument");

                return new DMASTInitial(callParameters[0].Value);
            }
            else if (identifier != null && identifier.Identifier == "issaved") {
                if (callParameters.Length != 1) Error("issaved() requires 1 argument");

                return new DMASTIsSaved(callParameters[0].Value);
            }
            else if (identifier != null && identifier.Identifier == "istype") {
                if (callParameters.Length == 1) {
                    return new DMASTImplicitIsType(callParameters[0].Value);
                }
                else if (callParameters.Length == 2) {
                    return new DMASTIsType(callParameters[0].Value, callParameters[1].Value);
                }
                else {
                    Error("istype() requires 1 or 2 arguments");
                    return null;
                }
            }
            else if (identifier != null && identifier.Identifier == "text") {
                if (callParameters.Length == 0) Error("text() requires at least 1 argument");

                if (callParameters[0].Value is DMASTConstantString constantString) {
                    if (callParameters.Length > 1) Error("text() expected 1 argument");
                    return constantString;
                }
                else if (callParameters[0].Value is DMASTStringFormat formatText) {
                    if (formatText == null) Error("text()'s first argument must be a string format");

                    List<int> emptyValueIndices = new();
                    for (int i = 0; i < formatText.InterpolatedValues.Length; i++) {
                        if (formatText.InterpolatedValues[i] == null) emptyValueIndices.Add(i);
                    }

                    if (callParameters.Length != emptyValueIndices.Count + 1) Error("text() was given an invalid amount of arguments for the string");
                    for (int i = 0; i < emptyValueIndices.Count; i++) {
                        int emptyValueIndex = emptyValueIndices[i];

                        formatText.InterpolatedValues[emptyValueIndex] = callParameters[i + 1].Value;
                    }

                    return formatText;
                }
                else {
                    Error("text() expected a string as the first argument");
                    return null;
                }
            }
            else if (identifier != null && identifier.Identifier == "locate") {
                if (callParameters.Length > 3) Error("locate() was given too many arguments");
                if (callParameters.Length == 3) { //locate(X, Y, Z)
                    return new DMASTLocateCoordinates(callParameters[0].Value, callParameters[1].Value, callParameters[2].Value);
                }
                else {
                    DMASTExpression container = null;
                    if (Check(TokenType.DM_In)) {
                        Whitespace();
                        container = Expression();
                        if (container == null) Error("Expected a container for locate()");
                    }
                    DMASTExpression type = null;
                    if (callParameters.Length == 2) {
                        type = callParameters[0].Value;
                        container = callParameters[1].Value;
                    }
                    else if (callParameters.Length == 1) {
                        type = callParameters[0].Value;
                    }
                    return new DMASTLocate(type, container);
                }
            }
            return null;
        }


        public Testing.DMASTPathExpression ExpressionExplicitPath() {
            return ExpressionPath(1);
        }

        public Testing.DMASTPathExpression ConstPath(int explicitStatus) {
            SavePosition();
            DreamPath.PathType pathType = DreamPath.PathType.Relative;
//            Console.WriteLine("check1 " + Current().ShortString() + " " + explicitStatus);
            Token op = Current();
            if (Check(TokenType.DM_Slash)) {
                if (op.TrailingWhitespace || explicitStatus == -1) { RestorePosition(); return null; }
                pathType = DreamPath.PathType.Absolute;
            }
            else if (Check(TokenType.DM_Colon)) {
                if (op.TrailingWhitespace || explicitStatus == -1) { RestorePosition(); return null; }
                pathType = DreamPath.PathType.DownwardSearch;
            }
            else if (Check(TokenType.DM_Period)) {
                if (op.TrailingWhitespace || explicitStatus == -1) { RestorePosition(); return null; }
                // .proc is just a thing.
                if (Current().Type == TokenType.DM_Proc) {
                    explicitStatus = 0;
                }
                pathType = DreamPath.PathType.UpwardSearch;
            }
            else if (explicitStatus == 1) { RestorePosition(); return null; }

  //          Console.WriteLine("check2 " + Current().ShortString() + " " + explicitStatus);
            var ident = PathElement();
  //          Console.WriteLine("check3 " + Current().ShortString() + " " + explicitStatus);
            if (ident == null && pathType == DreamPath.PathType.Absolute) {
                AcceptPosition();
                return new Testing.DMASTConstPath(new DreamPath("/"));
            }
            else if (ident == null) {
                RestorePosition(); return null;
            }
            else {
                DreamPath path = new DreamPath(pathType, new string[] { ident });
                if (Check(TokenType.DM_LeftCurlyBracket)) {
                    List<DMASTModifiedType.ModifiedProperty> modtypes = new();
                    while (!Check(TokenType.DM_RightCurlyBracket)) {
                        var modident = Identifier();
                        if (modident == null) {
                            Error("expected identifier in modified type");
                        }
                        if (!Check(TokenType.DM_Equals)) {
                            Error("expected = after modified type identifier");
                        }
                        var expr = Expression();
                        if (Current().Type != TokenType.DM_Semicolon && Current().Type != TokenType.DM_RightCurlyBracket) {
                            Error("expected } or ; after modified type property");
                        }
                        if (Current().Type == TokenType.DM_Semicolon) { Advance(); }
                        modtypes.Add(new DMASTModifiedType.ModifiedProperty(modident, expr));
                    }
                    Check(TokenType.Newline);
                    AcceptPosition();
                    return new DMASTModifiedType(path, modtypes.ToArray());
                }
                AcceptPosition();
                return new Testing.DMASTConstPath(path);
            }
        }

        public DMASTPath Path(bool expression = false) {
            Token firstToken = Current();
            DreamPath.PathType pathType = DreamPath.PathType.Relative;
            bool hasPathTypeToken = true;

            if (Check(TokenType.DM_Slash)) {
                pathType = DreamPath.PathType.Absolute;
            }
            else if (Check(TokenType.DM_Colon)) {
                pathType = DreamPath.PathType.DownwardSearch;
            }
            else if (Check(TokenType.DM_Period)) {
                pathType = DreamPath.PathType.UpwardSearch;
            }
            else {
                hasPathTypeToken = false;

                if (expression) return null;
            }

            string pathElement = PathElement();
            if (pathElement != null) {
                List<string> pathElements = new() { pathElement };

                while (pathElement != null && Check(TokenType.DM_Slash)) {
                    pathElement = PathElement();

                    if (pathElement != null) {
                        pathElements.Add(pathElement);
                    }
                }

                var path = new DreamPath(pathType, pathElements.ToArray());
                return new DMASTPath(path);
            }
            else if (hasPathTypeToken) {
                if (expression) ReuseToken(firstToken);

                return null;
            }

            return null;
        }

        public string PathElement() {
            TokenType[] validPathElementTokens = {
                TokenType.DM_Identifier,
                TokenType.DM_Var,
                TokenType.DM_Proc,
                TokenType.DM_List,
                TokenType.DM_NewList,
                TokenType.DM_Step
            };

            Token elementToken = Current();
            if (Check(validPathElementTokens)) {
                return elementToken.Text;
            }
            else {
                return null;
            }
        }

        public DMASTCallable Callable() {
            if (Check(TokenType.DM_SuperProc)) return new DMASTCallableSuper();
            if (Check(TokenType.DM_Period)) return new DMASTCallableSelf();

            return null;
        }

        public DMASTIdentifier Identifier() {
            Token token = Current();

            if (Check(new TokenType[] { TokenType.DM_Identifier, TokenType.DM_Step })) {
                return new DMASTIdentifier(token.Text);
            }

            return null;
        }

        public DM.DMASTDereference Dereference() {
            Token leftToken = Current();

            if (Check(TokenType.DM_Identifier)) {
                Token dereferenceToken = Current();
                TokenType[] dereferenceTokenTypes = {
                    TokenType.DM_Period,
                    TokenType.DM_QuestionPeriod,
                    TokenType.DM_Colon,
                    TokenType.DM_QuestionColon,
                };

                if (Check(dereferenceTokenTypes)) {
                    List<Dereference> dereferences = new();
                    DMASTIdentifier identifier = Identifier();

                    if (identifier != null) {
                        do {
                            DereferenceType type;
                            bool conditional;
                            switch (dereferenceToken.Type) {
                                case TokenType.DM_Period:
                                type = DereferenceType.Direct;
                                conditional = false;
                                break;
                                case TokenType.DM_QuestionPeriod:
                                type = DereferenceType.Direct;
                                conditional = true;
                                break;
                                case TokenType.DM_Colon:
                                type = DereferenceType.Search;
                                conditional = false;
                                break;
                                case TokenType.DM_QuestionColon:
                                type = DereferenceType.Search;
                                conditional = true;
                                break;
                                default:
                                throw new InvalidOperationException();
                            }

                            dereferences.Add(new Dereference(type, conditional, identifier.Identifier));

                            dereferenceToken = Current();
                            if (Check(dereferenceTokenTypes)) {
                                identifier = Identifier();
                                if (identifier == null) Error("Expected an identifier");
                            }
                            else {
                                identifier = null;
                            }
                        } while (identifier != null);

                        return new DM.DMASTDereference(new DMASTIdentifier(leftToken.Text), dereferences.ToArray());
                    }
                    else {
                        ReuseToken(dereferenceToken);
                        ReuseToken(leftToken);
                    }
                }
                else {
                    ReuseToken(leftToken);
                }
            }

            return null;
        }

        public DMASTCallParameter[] ProcCall(bool includeEmptyParameters = true) {
            if (Check(TokenType.DM_LeftParenthesis)) {
                DMASTCallParameter[] callParameters = CallParameters(includeEmptyParameters);
                if (callParameters == null) callParameters = new DMASTCallParameter[0];
                Consume(TokenType.DM_RightParenthesis, "Expected ')'");

                return callParameters;
            }

            return null;
        }

        public DMASTCallParameter[] CallParameters(bool includeEmpty) {
            List<DMASTCallParameter> parameters = new List<DMASTCallParameter>();
            do {
                if (Current().Type == TokenType.DM_RightParenthesis) { break; }
                if (Current().Type == TokenType.DM_Comma) {
                    Advance();
                    parameters.Add(new DMASTCallParameter(new DMASTConstantNull()));
                    continue;
                }
                DMASTCallParameter parameter = CallParameter();
                if (parameter != null) {
                    parameters.Add(parameter);
                }
                else if (Check(TokenType.DM_IndeterminateArgs)) {
                    ;
                }
                else {
                    Error("Parameter expected");
                }
                if (Current().Type == TokenType.DM_Comma) { Advance(); continue; }
                else if (Current().Type == TokenType.DM_RightParenthesis) { break; }
                else { Error("invalid arg separator"); }


            } while (true);
            if (parameters.Count > 0) {
                return parameters.ToArray();
            }
            else {
                return null;
            }
        }

        public DMASTCallParameter[] CallParametersOld(bool includeEmpty) {
            List<DMASTCallParameter> parameters = new List<DMASTCallParameter>();
            DMASTCallParameter parameter = CallParameter();

            while (parameter != null) {
                parameters.Add(parameter);

                if (Check(TokenType.DM_Comma)) {
                    Whitespace();
                    parameter = CallParameter();

                    if (parameter == null) {
                        if (includeEmpty) parameter = new DMASTCallParameter(new DMASTConstantNull());
                        else while (Check(TokenType.DM_Comma)) Whitespace();
                    }
                }
                else {
                    parameter = null;
                }
            }

            if (parameters.Count > 0) {
                return parameters.ToArray();
            }
            else {
                return null;
            }
        }

        public DMASTCallParameter CallParameter() {
            DMASTExpression expression = Expression();

            if (expression != null) {
                DMASTAssign assign = expression as DMASTAssign;

                if (assign != null) {
                    if (assign.Expression is DMASTConstantString) {
                        return new DMASTCallParameter(assign.Value, ((DMASTConstantString)assign.Expression).Value);
                    }
                    else if (assign.Expression is DMASTIdentifier) {
                        return new DMASTCallParameter(assign.Value, ((DMASTIdentifier)assign.Expression).Identifier);
                    }
                }

                return new DMASTCallParameter(expression);
            }

            return null;
        }

        public DMASTExpression Constant() {
            Whitespace();
            Token constantToken = Current();

            switch (constantToken.Type) {
                case TokenType.DM_Integer: Advance(); return new DMASTConstantInteger((int)constantToken.Value);
                case TokenType.DM_Float: Advance(); return new DMASTConstantFloat((float)constantToken.Value);
                case TokenType.DM_Resource: Advance(); return new DMASTConstantResource((string)constantToken.Value);
                case TokenType.DM_Null: Advance(); return new DMASTConstantNull();
                case TokenType.DM_RawString: Advance(); return new DMASTConstantString((string)constantToken.Value);
                case TokenType.DM_String: {
                        string tokenValue = (string)constantToken.Value;
                        StringBuilder stringBuilder = new StringBuilder();
                        List<DMASTExpression> interpolationValues = new();
                        Advance();

                        int bracketNesting = 0;
                        StringBuilder insideBrackets = new StringBuilder();
                        StringFormatTypes currentInterpolationType = StringFormatTypes.Stringify;
                        for (int i = 0; i < tokenValue.Length; i++) {
                            char c = tokenValue[i];

                            if (bracketNesting > 0) {
                                insideBrackets.Append(c);
                            }

                            if (c == '[') {
                                bracketNesting++;
                            }
                            else if (c == ']' && bracketNesting > 0) {
                                bracketNesting--;

                                if (bracketNesting == 0) { //End of expression
                                    insideBrackets.Remove(insideBrackets.Length - 1, 1); //Remove the ending bracket

                                    string insideBracketsText = insideBrackets.ToString();
                                    if (insideBracketsText != String.Empty) {
                                        DMPreprocessorLexer preprocLexer = new DMPreprocessorLexer(constantToken.SourceFile, insideBracketsText);
                                        List<Token> preprocTokens = new();
                                        Token preprocToken;
                                        do {
                                            preprocToken = preprocLexer.GetNextToken();
                                            preprocToken.SourceFile = constantToken.SourceFile;
                                            preprocToken.Line = constantToken.Line;
                                            preprocToken.Column = constantToken.Column;
                                            if (preprocToken.Type != TokenType.EndOfFile) {
                                                preprocTokens.Add(preprocToken);
                                            }
                                        } while (preprocToken.Type != TokenType.EndOfFile);

                                        // TODO this should use the factory pattern so filters can be set elsewhere
                                        preprocTokens.Add(new Token(TokenType.EndOfFile, "\0", "", 0, 0, '\0'));
                                        DMLexer expressionLexer = new TokenWhitespaceFilter( new DMLexer(constantToken.SourceFile, preprocTokens) );
                                        DMParser expressionParser = new DMParser(expressionLexer);

                                        expressionParser.Whitespace(true);
                                        DMASTExpression expression = expressionParser.Expression();
                                        if (expression == null) Error("Expected an expression");
                                        if (expressionParser.Errors.Count > 0) Errors.AddRange(expressionParser.Errors);
                                        if (expressionParser.Warnings.Count > 0) Warnings.AddRange(expressionParser.Warnings);

                                        interpolationValues.Add(expression);
                                    }
                                    else {
                                        interpolationValues.Add(null);
                                    }

                                    stringBuilder.Append(StringFormatCharacter);
                                    stringBuilder.Append((char)currentInterpolationType);

                                    currentInterpolationType = StringFormatTypes.Stringify;
                                    insideBrackets.Clear();
                                }
                            }
                            else if (c == '\\' && bracketNesting == 0) {
                                string escapeSequence = String.Empty;

                                do {
                                    c = tokenValue[++i];
                                    escapeSequence += c;

                                    if (escapeSequence == "[" || escapeSequence == "]") {
                                        stringBuilder.Append(escapeSequence);
                                        break;
                                    }
                                    else if (escapeSequence == "\"" || escapeSequence == "\\" || escapeSequence == "'") {
                                        stringBuilder.Append(escapeSequence);
                                        break;
                                    }
                                    else if (escapeSequence == "n") {
                                        stringBuilder.Append('\n');
                                        break;
                                    }
                                    else if (escapeSequence == "t") {
                                        stringBuilder.Append('\t');
                                        break;
                                    }
                                    else if (escapeSequence == "ref") {
                                        currentInterpolationType = StringFormatTypes.Ref;
                                        break;
                                    }
                                    else if (DMLexer.ValidEscapeSequences.Contains(escapeSequence)) { //Unimplemented escape sequence
                                        break;
                                    }
                                } while (c != ' ');

                                if (!DMLexer.ValidEscapeSequences.Contains(escapeSequence)) {
                                    Error("Invalid escape sequence \"\\" + escapeSequence + "\"");
                                }
                            }
                            else if (bracketNesting == 0) {
                                stringBuilder.Append(c);
                            }
                        }

                        if (bracketNesting > 0) Error("Expected ']'");

                        string stringValue = stringBuilder.ToString();
                        if (interpolationValues.Count == 0) {
                            return new DMASTConstantString(stringValue);
                        }
                        else {
                            return new DMASTStringFormat(stringValue, interpolationValues.ToArray());
                        }
                    }
                default: return null;
            }
        }
    }
}
