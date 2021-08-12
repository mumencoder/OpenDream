
using System;
using System.Collections.Generic;
using OpenDreamShared.Dream.Procs;
using System.Text;
using OpenDreamShared.Compiler.DMPreprocessor;

namespace OpenDreamShared.Compiler.DM.Testing {
    public partial class DMParser : Parser<Token> {
        public DMASTExpression ExpressionPrimary() {
            Whitespace();
            DMASTExpression primary = Constant();
            if (primary != null) {
                return primary;
            }
            DMASTPath const_path = Path(true);
            if (const_path != null) {
                primary = new DMASTConstantPath(const_path);

                while (Check(TokenType.DM_Period)) {
                    DMASTPath search = Path();
                    if (search == null) Error("Expected a path for an upward search");

                    primary = new DMASTUpwardPathSearch((DMASTExpressionConstant)primary, search);
                }
                return primary;
            }

            if (Check(TokenType.DM_List)) {
                DMASTCallParameter[] values = ProcCall(false);
                return new DMASTList(values);
            }
            if (Check(TokenType.DM_New)) {
                Whitespace();
                DMASTCallParameter[] parameters = null;
                if (Check(TokenType.DM_LeftParenthesis)) {
                    Whitespace();
                    parameters = CallParameters(true);
                    Consume(TokenType.DM_RightParenthesis, "Expected ')'");
                    Whitespace();
                    return new DMASTNewInferred(parameters);
                }
                else {
                    // NOTE: I dont know how this syntax works, maybe the deref cannot be a full expression
                    DM.DMASTDereference dereference = Dereference();
                    DMASTIdentifier identifier = (dereference == null) ? Identifier() : null;
                    DMASTPath path = (dereference == null && identifier == null) ? Path(true) : null;
                    Whitespace();
                    if (Check(TokenType.DM_LeftParenthesis)) {
                        Whitespace();
                        parameters = CallParameters(true);
                        Consume(TokenType.DM_RightParenthesis, "Expected ')'");
                        Whitespace();
                    }
                    if (dereference != null) {
                        return new DMASTNewDereference(dereference, parameters);
                    }
                    else if (identifier != null) {
                        return new DMASTNewIdentifier(identifier, parameters);
                    }
                    else if (path != null) {
                        return new DMASTNewPath(path, parameters);
                    }
                    else {
                        return new DMASTNewInferred(parameters);
                    }
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
                                            preprocTokens.Add(preprocToken);
                                        } while (preprocToken.Type != TokenType.EndOfFile);

                                        // TODO this should use the factory pattern so filters can be set elsewhere
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
