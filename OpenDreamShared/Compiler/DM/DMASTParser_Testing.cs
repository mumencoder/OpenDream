using System;
using System.Collections.Generic;
using OpenDreamShared.Dream;
using DereferenceType = OpenDreamShared.Compiler.DM.DMASTDereference.DereferenceType;
using Dereference = OpenDreamShared.Compiler.DM.DMASTDereference.Dereference;
using OpenDreamShared.Dream.Procs;
using System.Text;
using OpenDreamShared.Compiler.DMPreprocessor;

namespace OpenDreamShared.Compiler.DM.Testing {
    public partial class DMParser : Parser<Token> {
        public static char StringFormatCharacter = (char)0xFF;

        private DreamPath _currentPath = DreamPath.Root;

        // set this to null when not using
        private string _debugPath = null;
        protected bool DebugPath = false;
        protected string DebugTokenType = "path";
        List<TokenType> DebugTokenIndentTypes = new() { TokenType.Newline, TokenType.DM_Indent };

        protected bool DebugToken = false;
        private int DebugTokenCount = 0;

        public DMParser(DMLexer lexer) : base(lexer) { }

        protected override Token Advance() {
            Token t = base.Advance();

            if (DebugTokenType == "path" && DebugPath) {
                DebugToken = true;
            }
            else if (DebugTokenType == "indent" && Peek(DebugTokenIndentTypes)) {
                DebugToken = true;
            }
            if (DebugToken) {
                System.Console.Write(System.String.Format("{0,-40}", PrintToken(t)));
                DebugTokenCount += 1;
                if (DebugTokenCount >= 4) {
                    System.Console.Write("\n");
                    DebugTokenCount = 0;
                }
            }
            DebugToken = false;
            return t;
        }

        public DMASTFile File() {
            DMASTBlockInner blockInner = BlockInner();
            Newline();
            Consume(TokenType.EndOfFile, "Expected EOF");

            return new DMASTFile(blockInner);
        }

        public DMASTBlockInner BlockInner() {
            DMASTStatement statement = Statement();

            if (statement != null) {
                List<DMASTStatement> statements = new() { statement };

                while (Delimiter()) {
                    Whitespace();
                    statement = Statement();

                    if (statement != null) {
                        Whitespace();
                        statements.Add(statement);
                    }
                }

                return new DMASTBlockInner(statements.ToArray());
            }
            else {
                return null;
            }
        }

        public DMASTStatement Statement() {
            DMASTPath path = Path();

            if (path != null) {
                DreamPath oldPath = _currentPath;
                DMASTStatement statement;

                Whitespace();

                _currentPath = _currentPath.Combine(path.Path);
                if (_currentPath.PathString == _debugPath) {
                    DebugPath = true;
                }
                if (DebugPath) {
                    Console.WriteLine("\n");
                }
                //Proc definition
                if (Check(TokenType.DM_LeftParenthesis)) {
                    DMASTDefinitionParameter[] parameters = DefinitionParameters();
                    Consume(TokenType.DM_RightParenthesis, "Expected ')'");
                    if (DebugPath) {
                        Console.WriteLine("\n");
                    }

                    DMASTProcBlockInner procBlock = ProcBlock();
                    if (procBlock == null) {
                        DMASTProcStatement procStatement = ProcStatement();

                        if (procStatement != null) {
                            procBlock = new DMASTProcBlockInner(new DMASTProcStatement[] { procStatement });
                        }
                    }

                    statement = new DMASTProcDefinition(_currentPath, parameters, procBlock);
                }
                else {
                    DMASTBlockInner block = Block();

                    //Object definition
                    if (block != null) {
                        statement = new DMASTObjectDefinition(_currentPath, block);
                    }
                    else {
                        //Var definition(s)
                        if (_currentPath.FindElement("var") != -1) {
                            DreamPath varPath = _currentPath;
                            List<DMASTObjectVarDefinition> varDefinitions = new();

                            while (true) {
                                DMASTExpression value;
                                if (Check(TokenType.DM_Equals)) {
                                    value = Expression();
                                    if (value == null) Error("Expected an expression");
                                }
                                else {
                                    value = new DMASTConstantNull();
                                }
                                AsTypes();
                                varDefinitions.Add(new DMASTObjectVarDefinition(varPath, value));
                                if (Current().Type == TokenType.DM_LeftBracket) {
                                    while (Check(TokenType.DM_LeftBracket)) {
                                        DMASTExpression size = Constant();
                                        if (!Check(TokenType.DM_RightBracket)) {
                                            Error("Expected ] after constant int");
                                        }
                                    }
                                    // TODO change AST node
                                }
                                else if (Check(TokenType.DM_Comma)) {
                                DMASTPath newVarPath = Path();
                                if (newVarPath == null) Error("Expected a var definition");
                                if (newVarPath.Path.Elements.Length > 1) Error("Invalid var name"); //TODO: This is valid DM

                                varPath = _currentPath.AddToPath("../" + newVarPath.Path.PathString);
                                }
                                else {
                                    break;
                                }
                            }

                            if (varDefinitions.Count == 1) {
                                statement = varDefinitions[0];
                            }
                            else {
                                statement = new DMASTMultipleObjectVarDefinitions(varDefinitions.ToArray());
                            }
                        }
                        else {
                            //Var override
                            if (Check(TokenType.DM_Equals)) {
                                Whitespace();
                                DMASTExpression value = Expression();
                                if (value == null) Error("Expected an expression");

                                statement = new DMASTObjectVarOverride(_currentPath, value);
                            }
                            else {
                                //Empty object definition
                                statement = new DMASTObjectDefinition(_currentPath, null);
                            }
                        }
                    }
                }

                _currentPath = oldPath;
                DebugPath = false;
                return statement;
            }

            return null;
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

                return new DMASTPath(new DreamPath(pathType, pathElements.ToArray()));
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


        public DMASTBlockInner Block() {
            Token beforeBlockToken = Current();
            bool hasNewline = Newline();

            DMASTBlockInner block = BracedBlock();
            if (block == null) block = IndentedBlock();

            if (block == null && hasNewline) {
                ReuseToken(beforeBlockToken);
            }

            return block;
        }

        public DMASTBlockInner BracedBlock() {
            if (Check(TokenType.DM_LeftCurlyBracket)) {
                Whitespace();
                Newline();
                bool isIndented = Check(TokenType.DM_Indent);
                DMASTBlockInner blockInner = BlockInner();
                if (isIndented) Consume(TokenType.DM_Dedent, "Expected dedent");
                Consume(TokenType.DM_RightCurlyBracket, "Expected '}'");

                return blockInner;
            }

            return null;
        }

        public DMASTBlockInner IndentedBlock() {
            if (Check(TokenType.DM_Indent)) {
                DMASTBlockInner blockInner = BlockInner();

                if (blockInner != null) {
                    Newline();
                    Consume(TokenType.DM_Dedent, "Expected dedent");

                    return blockInner;
                }
            }

            return null;
        }

        public DMASTProcBlockInner ProcBlock() {
            Token beforeBlockToken = Current();
            bool hasNewline = Newline();

            DMASTProcBlockInner procBlock = BracedProcBlock();
            if (procBlock == null) procBlock = IndentedProcBlock();

            if (procBlock == null && hasNewline) {
                ReuseToken(beforeBlockToken);
            }

            return procBlock;
        }

        public DMASTProcBlockInner BracedProcBlock() {
            if (Check(TokenType.DM_LeftCurlyBracket)) {
                Whitespace();
                Newline();
                bool isIndented = Check(TokenType.DM_Indent);
                DMASTProcBlockInner procBlock = ProcBlockInner();
                if (isIndented) Consume(TokenType.DM_Dedent, "Expected dedent");
                Consume(TokenType.DM_RightCurlyBracket, "Expected '}'");

                return procBlock;
            }

            return null;
        }

        public DMASTProcBlockInner IndentedProcBlock() {
            if (Check(TokenType.DM_Indent)) {
                DMASTProcBlockInner procBlock = ProcBlockInner();
                Consume(TokenType.DM_Dedent, "Expected dedent");

                return procBlock;
            }

            return null;
        }

        public DMASTProcBlockInner ProcBlockInner() {
            DMASTProcStatement procStatement = ProcStatement();

            if (procStatement != null) {
                if (DebugPath) {
                    Console.WriteLine("\n");
                }
                List<DMASTProcStatement> procStatements = new List<DMASTProcStatement>() { procStatement };

                while (Delimiter()) {
                    Whitespace();
                    procStatement = ProcStatement();

                    if (procStatement != null) {
                        Whitespace();

                        procStatements.Add(procStatement);
                    }
                }

                return new DMASTProcBlockInner(procStatements.ToArray());
            }
            else {
                return null;
            }
        }

        public DMASTProcStatement ProcStatement() {
            DMASTExpression expression = Expression();
            DMASTProcStatement stmt_return = null;

            if (expression != null) {
                if (expression is DMASTIdentifier) {
                    Check(TokenType.DM_Colon);
                    // TODO: this needs more verification

                    var inner_block = ProcBlock();
                    if (inner_block == null) {
                        Error("Expected a proc block after label");
                    }

                    stmt_return = new Testing.DMASTProcStatementLabel((expression as DMASTIdentifier), inner_block);
                }
                else if (expression is DMASTLeftShift) {
                    DMASTLeftShift leftShift = (DMASTLeftShift)expression;
                    DMASTProcCall procCall = leftShift.B as DMASTProcCall;

                    if (procCall != null && procCall.Callable is DMASTCallableProcIdentifier) {
                        DMASTCallableProcIdentifier identifier = (DMASTCallableProcIdentifier)procCall.Callable;

                        if (identifier.Identifier == "browse") {
                            if (procCall.Parameters.Length != 1 && procCall.Parameters.Length != 2) Error("browse() requires 1 or 2 parameters");

                            DMASTExpression body = procCall.Parameters[0].Value;
                            DMASTExpression options = (procCall.Parameters.Length == 2) ? procCall.Parameters[1].Value : new DMASTConstantNull();
                            stmt_return = new DMASTProcStatementBrowse(leftShift.A, body, options);
                        }
                        else if (identifier.Identifier == "browse_rsc") {
                            if (procCall.Parameters.Length != 1 && procCall.Parameters.Length != 2) Error("browse_rsc() requires 1 or 2 parameters");

                            DMASTExpression file = procCall.Parameters[0].Value;
                            DMASTExpression filepath = (procCall.Parameters.Length == 2) ? procCall.Parameters[1].Value : new DMASTConstantNull();
                            stmt_return = new DMASTProcStatementBrowseResource(leftShift.A, file, filepath);
                        }
                        else if (identifier.Identifier == "output") {
                            if (procCall.Parameters.Length != 2) Error("output() requires 2 parameters");

                            DMASTExpression msg = procCall.Parameters[0].Value;
                            DMASTExpression control = procCall.Parameters[1].Value;
                            stmt_return = new DMASTProcStatementOutputControl(leftShift.A, msg, control);
                        }
                    }
                }
                if (stmt_return == null) {
                    stmt_return = new DMASTProcStatementExpression(expression);
                }
            }
            else {
                DMASTProcStatement procStatement = ProcVarDeclaration();
                if (procStatement == null) procStatement = Return();
                if (procStatement == null) procStatement = Break();
                if (procStatement == null) procStatement = Continue();
                if (procStatement == null) procStatement = Goto();
                if (procStatement == null) procStatement = Del();
                if (procStatement == null) procStatement = Set();
                if (procStatement == null) procStatement = Spawn();
                if (procStatement == null) procStatement = If();
                if (procStatement == null) procStatement = For();
                if (procStatement == null) procStatement = While();
                if (procStatement == null) procStatement = DoWhile();
                if (procStatement == null) procStatement = Switch();
                if (procStatement == null) procStatement = TryCatch();

                if (procStatement != null) {
                    Whitespace();
                    stmt_return = procStatement;
                }

            }
            if (DebugPath) {
                Console.WriteLine("\n");
            }
            if (_debugPath != null && _currentPath.PathString.StartsWith(_debugPath)) {
                Console.WriteLine("Statement: \n" + DMAST.PrintNodes(stmt_return));
            }
            return stmt_return;
        }

        public DMASTProcStatement ProcVarDeclaration(bool allowMultiple = true) {
            Token firstToken = Current();
            bool wasSlash = Check(TokenType.DM_Slash);

            if (Check(TokenType.DM_Var)) {
                if (wasSlash) Error("Unsupported root variable declaration");


                Whitespace();
                DMASTPath varPath = Path();
                if (varPath == null) Error("Expected a variable name");

                List<DMASTProcStatementVarDeclaration> varDeclarations = new();
                while (true) {
                    DMASTExpression value = null;
                    Whitespace();

                    //TODO: Multidimensional lists
                    if (Check(TokenType.DM_LeftBracket)) {
                        //Type information
                        if (varPath.Path.FindElement("list") != 0) {
                            varPath = new DMASTPath(DreamPath.List.Combine(varPath.Path));
                        }

                        Whitespace();
                        DMASTExpression size = Expression();
                        Consume(TokenType.DM_RightBracket, "Expected ']'");
                        Whitespace();

                        if (size is not null) {
                            value = new DMASTNewPath(new DMASTPath(DreamPath.List),
                                new[] { new DMASTCallParameter(size) });
                        }
                    }

                    if (Check(TokenType.DM_Equals)) {
                        if (value != null) Error("List doubly initialized");

                        Whitespace();
                        value = Expression();
                        if (value == null) Error("Expected an expression");
                    }

                    AsTypes();

                    varDeclarations.Add(new DMASTProcStatementVarDeclaration(varPath, value ?? new DMASTConstantNull()));
                    if (allowMultiple && Check(TokenType.DM_Comma)) {
                        Whitespace();
                        varPath = Path();
                        if (varPath == null) Error("Expected a var declaration");
                    }
                    else {
                        break;
                    }
                }

                if (varDeclarations.Count > 1) {
                    return new DMASTProcStatementMultipleVarDeclarations(varDeclarations.ToArray());
                }
                else {
                    return varDeclarations[0];
                }
            }
            else if (wasSlash) {
                ReuseToken(firstToken);
            }

            return null;
        }

        public DMASTProcStatementReturn Return() {
            if (Check(TokenType.DM_Return)) {
                Whitespace();
                DMASTExpression value = Expression();

                return new DMASTProcStatementReturn(value);
            }
            else {
                return null;
            }
        }

        public DMASTProcStatementBreak Break() {
            if (Check(TokenType.DM_Break)) {
                DMASTIdentifier label = Identifier();
                return new Testing.DMASTProcStatementBreak(label);
            }
            else {
                return null;
            }
        }

        public DMASTProcStatementContinue Continue() {
            if (Check(TokenType.DM_Continue)) {
                DMASTIdentifier label = Identifier();
                return new Testing.DMASTProcStatementContinue(label);
            }
            else {
                return null;
            }
        }

        public DMASTProcStatementGoto Goto() {
            if (Check(TokenType.DM_Goto)) {
                Whitespace();
                DMASTIdentifier label = Identifier();

                return new DMASTProcStatementGoto(label);
            }
            else {
                return null;
            }
        }

        public DMASTProcStatementDel Del() {
            if (Check(TokenType.DM_Del)) {
                Whitespace();
                bool hasParenthesis = Check(TokenType.DM_LeftParenthesis);
                Whitespace();
                DMASTExpression value = Expression();
                if (value == null) Error("Expected value to delete");
                if (hasParenthesis) Consume(TokenType.DM_RightParenthesis, "Expected ')'");

                return new DMASTProcStatementDel(value);
            }
            else {
                return null;
            }
        }

        public DMASTProcStatementSet Set() {
            if (Check(TokenType.DM_Set)) {
                Whitespace();
                Token attributeToken = Current();

                if (Check(TokenType.DM_Identifier)) {
                    Whitespace();
                    Consume(new TokenType[] { TokenType.DM_Equals, TokenType.DM_In }, "Expected '=' or 'in'");
                    Whitespace();
                    DMASTExpression value = Expression();
                    if (value == null) Error("Expected an expression");

                    return new DMASTProcStatementSet(attributeToken.Text, value);
                }
                else {
                    Error("Expected property name");
                }
            }

            return null;
        }

        public DMASTProcStatementSpawn Spawn() {
            if (Check(TokenType.DM_Spawn)) {
                Whitespace();
                Consume(TokenType.DM_LeftParenthesis, "Expected '('");
                Whitespace();

                DMASTExpression delay;
                if (Check(TokenType.DM_RightParenthesis)) {
                    //No parameters, default to zero
                    delay = new DMASTConstantInteger(0);
                }
                else {
                    delay = Expression();

                    if (delay == null) Error("Expected an expression");
                    Consume(TokenType.DM_RightParenthesis, "Expected ')'");
                }

                Whitespace();
                Newline();

                DMASTProcBlockInner body = ProcBlock();
                if (body == null) {
                    DMASTProcStatement statement = ProcStatement();

                    if (statement == null) Error("Expected body or statement");
                    body = new DMASTProcBlockInner(new DMASTProcStatement[] { statement });
                }

                return new DMASTProcStatementSpawn(delay, body);
            }
            else {
                return null;
            }
        }

        public DMASTProcStatementIf If() {
            if (Check(TokenType.DM_If)) {
                Consume(TokenType.DM_LeftParenthesis, "Expected '('");
                DMASTExpression condition = Expression();
                if (condition == null) Error("Expected a condition");
                Consume(TokenType.DM_RightParenthesis, "Expected ')'");
                Check(TokenType.DM_Colon);

                DMASTProcStatement procStatement = ProcStatement();
                DMASTProcBlockInner body;
                DMASTProcBlockInner elseBody = null;

                if (procStatement != null) {
                    body = new DMASTProcBlockInner(new DMASTProcStatement[] { procStatement });
                }
                else {
                    body = ProcBlock();
                }

                if (body == null) body = new DMASTProcBlockInner(new DMASTProcStatement[0]);
                Token afterIfBody = Current();
                bool newLineAfterIf = Newline();
                if (newLineAfterIf) Whitespace();
                if (Check(TokenType.DM_Else)) {
                    Check(TokenType.DM_Colon);
                    procStatement = ProcStatement();

                    if (procStatement != null) {
                        elseBody = new DMASTProcBlockInner(new DMASTProcStatement[] { procStatement });
                    }
                    else {
                        elseBody = ProcBlock();
                    }

                    if (elseBody == null) elseBody = new DMASTProcBlockInner(new DMASTProcStatement[0]);
                }
                else if (newLineAfterIf) {
                    ReuseToken(afterIfBody);
                }

                return new DMASTProcStatementIf(condition, body, elseBody);
            }
            else {
                return null;
            }
        }

        public DMASTProcStatementEmpty EmptyStatement() {
            if (Check(TokenType.DM_Semicolon)) {
                return new DMASTProcStatementEmpty();
            }
            return null;
        }
        public DMASTProcStatement For() {
            if (Check(TokenType.DM_For)) {
                Consume(TokenType.DM_LeftParenthesis, "Expected '('");
                DMASTProcStatement initializer = null;
                DMASTIdentifier variable;
                DMASTProcStatementVarDeclaration variableDeclaration = ProcVarDeclaration(allowMultiple: false) as DMASTProcStatementVarDeclaration;
                if (variableDeclaration != null) {
                    initializer = variableDeclaration;
                    variable = new DMASTIdentifier(variableDeclaration.Name);
                }
                else {
                    variable = Identifier();
                    if (variable == null) {
                        if (Current().Type == TokenType.DM_Comma || Current().Type == TokenType.DM_Semicolon) {
                            initializer = null;
                        }
                        else {
                            Error("Expected , or ; after null initializer");
                        }
                    }
                    else if (Check(TokenType.DM_Equals)) {
                        Whitespace();
                        DMASTExpression value = Expression();
                        if (value == null) Error("Expected an expression");

                        initializer = new DMASTProcStatementExpression(new DMASTAssign(variable, value));
                    }
                }

                Whitespace();
                AsTypes(); //TODO: Correctly handle
                Whitespace();

                if (Check(TokenType.DM_In)) {
                    Whitespace();
                    DMASTExpression enumerateValue = Expression();
                    DMASTExpression toValue = null;
                    DMASTExpression step = new DMASTConstantInteger(1);

                    if (Check(TokenType.DM_To)) {
                        Whitespace();

                        toValue = Expression();
                        if (toValue == null) Error("Expected an end to the range");

                        if (Check(TokenType.DM_Step)) {
                            Whitespace();

                            step = Expression();
                            if (step == null) Error("Expected a step value");
                        }
                    }

                    Consume(TokenType.DM_RightParenthesis, "Expected ')'");
                    Whitespace();
                    Newline();

                    DMASTProcBlockInner body = ProcBlock();
                    if (body == null) {
                        DMASTProcStatement statement = ProcStatement();

                        if (statement == null) Error("Expected body or statement");
                        body = new DMASTProcBlockInner(new DMASTProcStatement[] { statement });
                    }

                    if (toValue == null) {
                        return new DMASTProcStatementForList(initializer, variable, enumerateValue, body);
                    }
                    else {
                        return new DMASTProcStatementForRange(initializer, variable, enumerateValue, toValue, step, body);
                    }
                }
                else if (Check(new TokenType[] { TokenType.DM_Comma, TokenType.DM_Semicolon })) {
                    Whitespace();
                    DMASTExpression comparator = Expression();
                    Consume(new TokenType[] { TokenType.DM_Comma, TokenType.DM_Semicolon }, "Expected ','");
                    Whitespace();
                    DMASTExpression incrementor = Expression();
                    Consume(TokenType.DM_RightParenthesis, "Expected ')'");
                    Whitespace();
                    Newline();

                    DMASTProcBlockInner body = ProcBlock();
                    if (body == null) {
                        DMASTProcStatement statement = ProcStatement();

                        if (statement == null) {
                            statement = EmptyStatement();
                        }
                        if (statement == null) {
                            Error("Expected body or statement");
                        }
                        body = new DMASTProcBlockInner(new DMASTProcStatement[] { statement });
                    }

                    return new DMASTProcStatementForStandard(initializer, comparator, incrementor, body);
                }
                else if (variableDeclaration != null) {
                    var to_expr = variableDeclaration.Value as Testing.DMASTExpressionTo;
                    if (to_expr == null) { Error("Expected 'to' for variable range"); }


                    DMASTExpression rangeBegin = to_expr.Value;
                    DMASTExpression rangeEnd = to_expr.List;

                    DMASTExpression step = new DMASTConstantInteger(1);
                    if (Check(TokenType.DM_Step)) {
                        step = Expression();
                        if (step == null) Error("Expected a step value");
                    }
                    Consume(TokenType.DM_RightParenthesis, "Expected ')'");
                    Newline();

                    DMASTProcBlockInner body = ProcBlock();
                    if (body == null) {
                        DMASTProcStatement statement = ProcStatement();

                        if (statement == null) Error("Expected body or statement");
                        body = new DMASTProcBlockInner(new DMASTProcStatement[] { statement });
                    }

                    return new DMASTProcStatementForRange(initializer, variable, rangeBegin, rangeEnd, step, body);
                }
                else {
                    Error("Expected 'in'");
                }
            }

            return null;
        }

        public DMASTProcStatementWhile While() {
            if (Check(TokenType.DM_While)) {
                Whitespace();
                Consume(TokenType.DM_LeftParenthesis, "Expected '('");
                Whitespace();
                DMASTExpression conditional = Expression();
                if (conditional == null) Error("Expected conditional");
                Consume(TokenType.DM_RightParenthesis, "Expected ')'");
                Whitespace();
                DMASTProcBlockInner body = ProcBlock();

                if (body == null) {
                    DMASTProcStatement statement = ProcStatement();
                    if (statement == null) Error("Expected statement");

                    body = new DMASTProcBlockInner(new DMASTProcStatement[] { statement });
                }

                return new DMASTProcStatementWhile(conditional, body);
            }

            return null;
        }

        public DMASTProcStatementDoWhile DoWhile() {
            if (Check(TokenType.DM_Do)) {
                Whitespace();
                DMASTProcBlockInner body = ProcBlock();

                if (body == null) {
                    DMASTProcStatement statement = ProcStatement();
                    if (statement == null) Error("Expected statement");

                    body = new DMASTProcBlockInner(new DMASTProcStatement[] { statement });
                }

                Newline();
                Whitespace();
                Consume(TokenType.DM_While, "Expected 'while'");
                Whitespace();
                Consume(TokenType.DM_LeftParenthesis, "Expected '('");
                Whitespace();
                DMASTExpression conditional = Expression();
                if (conditional == null) Error("Expected conditional");
                Consume(TokenType.DM_RightParenthesis, "Expected ')'");
                Whitespace();

                return new DMASTProcStatementDoWhile(conditional, body);
            }

            return null;
        }

        public DMASTProcStatementSwitch Switch() {
            if (Check(TokenType.DM_Switch)) {
                Whitespace();
                Consume(TokenType.DM_LeftParenthesis, "Expected '('");
                Whitespace();
                DMASTExpression value = Expression();
                Consume(TokenType.DM_RightParenthesis, "Expected ')'");
                Whitespace();
                DMASTProcStatementSwitch.SwitchCase[] switchCases = SwitchCases();

                if (switchCases == null) Error("Expected switch cases");
                return new DMASTProcStatementSwitch(value, switchCases);
            }

            return null;
        }

        public DMASTProcStatementSwitch.SwitchCase[] SwitchCases() {
            Token beforeSwitchBlock = Current();
            bool hasNewline = Newline();

            DMASTProcStatementSwitch.SwitchCase[] switchCases = BracedSwitchInner();
            if (switchCases == null) switchCases = IndentedSwitchInner();

            if (switchCases == null && hasNewline) {
                ReuseToken(beforeSwitchBlock);
            }

            return switchCases;
        }

        public DMASTProcStatementSwitch.SwitchCase[] BracedSwitchInner() {
            return null; //TODO: Braced switch blocks
        }

        public DMASTProcStatementSwitch.SwitchCase[] IndentedSwitchInner() {
            if (Check(TokenType.DM_Indent)) {
                DMASTProcStatementSwitch.SwitchCase[] switchInner = SwitchInner();
                Consume(TokenType.DM_Dedent, "Expected dedent");

                return switchInner;
            }

            return null;
        }

        public DMASTProcStatementSwitch.SwitchCase[] SwitchInner() {
            List<DMASTProcStatementSwitch.SwitchCase> switchCases = new();
            DMASTProcStatementSwitch.SwitchCase switchCase = SwitchCase();

            if (switchCase != null) {
                do {
                    switchCases.Add(switchCase);
                    Newline();
                    switchCase = SwitchCase();
                } while (switchCase != null);
            }

            return switchCases.ToArray();
        }

        public DMASTProcStatementSwitch.SwitchCase SwitchCase() {
            if (Check(TokenType.DM_If)) {
                List<DMASTExpression> expressions = new();

                Whitespace();
                Consume(TokenType.DM_LeftParenthesis, "Expected '('");
                do {
                    Whitespace();
                    DMASTExpression expression = Expression();
                    if (expression == null) Error("Expected an expression");

                    if (Check(TokenType.DM_To)) {
                        Whitespace();
                        DMASTExpression rangeEnd = Expression();
                        if (rangeEnd == null) Error("Expected an upper limit");

                        expressions.Add(new DMASTSwitchCaseRange(expression, rangeEnd));
                    }
                    else {
                        expressions.Add(expression);
                    }
                } while (Check(TokenType.DM_Comma));
                Consume(TokenType.DM_RightParenthesis, "Expected ')'");
                Whitespace();
                DMASTProcBlockInner body = ProcBlock();

                if (body == null) {
                    DMASTProcStatement statement = ProcStatement();

                    if (statement != null) {
                        body = new DMASTProcBlockInner(new DMASTProcStatement[] { statement });
                    }
                    else {
                        body = new DMASTProcBlockInner(new DMASTProcStatement[0]);
                    }
                }

                return new DMASTProcStatementSwitch.SwitchCaseValues(expressions.ToArray(), body);
            }
            else if (Check(TokenType.DM_Else)) {
                Whitespace();
                DMASTProcBlockInner body = ProcBlock();

                if (body == null) {
                    DMASTProcStatement statement = ProcStatement();

                    if (statement != null) {
                        body = new DMASTProcBlockInner(new DMASTProcStatement[] { statement });
                    }
                    else {
                        body = new DMASTProcBlockInner(new DMASTProcStatement[0]);
                    }
                }

                return new DMASTProcStatementSwitch.SwitchCaseDefault(body);
            }

            return null;
        }

        public Testing.DMASTProcStatementTryCatch TryCatch() {
            if (Check(TokenType.DM_Try)) {
                var tryBlock = ProcBlock();
                DMASTProcStatement declExpr = null;
                Newline();
                if (!Check(TokenType.DM_Catch)) {
                    Error("Expected catch statement after try");
                }
                if (Check(TokenType.DM_LeftParenthesis)) {
                    declExpr = ProcVarDeclaration();
                    if (declExpr == null) {
                        Error("Expected variable declaration");
                        return null;
                    }
                    if (!Check(TokenType.DM_RightParenthesis)) {
                        Error("Expected )");
                        return null;
                    }
                }
                var catchBlock = ProcBlock();
                return new Testing.DMASTProcStatementTryCatch(tryBlock, catchBlock, declExpr as DMASTProcStatementVarDeclaration); 
            }
            return null;
        }

        public DMASTCallParameter[] ProcCall(bool includeEmptyParameters = true) {
            if (Check(TokenType.DM_LeftParenthesis)) {
                Whitespace();
                DMASTCallParameter[] callParameters = CallParameters(includeEmptyParameters);
                if (callParameters == null) callParameters = new DMASTCallParameter[0];
                Whitespace();
                Consume(TokenType.DM_RightParenthesis, "Expected ')'");

                return callParameters;
            }

            return null;
        }

        public DMASTCallParameter[] CallParameters(bool includeEmpty) {
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

        public DMASTDefinitionParameter[] DefinitionParameters() {
            List<DMASTDefinitionParameter> parameters = new();
            DMASTDefinitionParameter parameter = DefinitionParameter();

            if (parameter != null || Check(TokenType.DM_IndeterminateArgs)) {
                if (parameter != null) parameters.Add(parameter);

                while (Check(TokenType.DM_Comma)) {
                    Whitespace();

                    parameter = DefinitionParameter();
                    if (parameter != null) {
                        parameters.Add(parameter);
                    }
                    else if (!Check(TokenType.DM_IndeterminateArgs)) {
                        Error("Expected parameter definition");
                    }
                }
            }

            return parameters.ToArray();
        }

        public DMASTDefinitionParameter DefinitionParameter() {
            DMASTPath path = Path();

            if (path != null) {
                Whitespace();
                if (Check(TokenType.DM_LeftBracket)) {
                    Whitespace();
                    DMASTExpression expression = Expression();
                    if (expression != null && expression is not DMASTExpressionConstant) Error("Expected a constant expression");
                    Whitespace();
                    Consume(TokenType.DM_RightBracket, "Expected ']'");
                }

                DMASTExpression value = null;
                DMValueType type;
                DMASTExpression possibleValues = null;

                if (Check(TokenType.DM_Equals)) {
                    Whitespace();
                    value = Expression();
                }

                type = AsTypes();
                Whitespace();

                if (Check(TokenType.DM_In)) {
                    Whitespace();
                    possibleValues = Expression();
                }

                return new DMASTDefinitionParameter(path, value, type, possibleValues);
            }

            return null;
        }


        protected bool Newline() {
            bool hasNewline = Check(TokenType.Newline);

            while (Check(TokenType.Newline)) {
            }
            return hasNewline;
        }

        protected bool Whitespace(bool includeIndentation = false) {
            if (includeIndentation) {
                bool hadWhitespace = false;

                while (Check(new TokenType[] { TokenType.DM_Whitespace, TokenType.DM_Indent, TokenType.DM_Dedent })) hadWhitespace = true;
                return hadWhitespace;
            }
            else {
                return Check(TokenType.DM_Whitespace);
            }
        }

        private DMValueType AsTypes(DMValueType defaultType = DMValueType.Anything) {
            DMValueType type = DMValueType.Anything;

            if (Check(TokenType.DM_As)) {
                Whitespace();
                bool parenthetical = Check(TokenType.DM_LeftParenthesis);
                bool closed = false;
                Whitespace();

                do {
                    Token typeToken = Current();

                    if (parenthetical) {
                        closed = Check(TokenType.DM_RightParenthesis);
                        if (closed) break;
                    }

                    Consume(new TokenType[] { TokenType.DM_Identifier, TokenType.DM_Null }, "Expected value type");
                    switch (typeToken.Text) {
                        case "anything": type |= DMValueType.Anything; break;
                        case "null": type |= DMValueType.Null; break;
                        case "text": type |= DMValueType.Text; break;
                        case "obj": type |= DMValueType.Obj; break;
                        case "mob": type |= DMValueType.Mob; break;
                        case "turf": type |= DMValueType.Turf; break;
                        case "num": type |= DMValueType.Num; break;
                        case "message": type |= DMValueType.Message; break;
                        case "area": type |= DMValueType.Area; break;
                        case "color": type |= DMValueType.Color; break;
                        case "file": type |= DMValueType.File; break;
                        default: Error("Invalid value type '" + typeToken.Text + "'"); break;
                    }
                } while (Check(TokenType.DM_Bar));

                if (parenthetical && !closed) {
                    Whitespace();
                    Consume(TokenType.DM_RightParenthesis, "Expected closing parenthesis");
                }
            }
            else {
                return defaultType;
            }

            return type;
        }

        private bool Delimiter() {
            return Check(TokenType.DM_Semicolon) || Newline();
        }
    }
}
