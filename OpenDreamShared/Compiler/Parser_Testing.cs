using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Linq;

namespace OpenDreamShared.Compiler.DM.Testing {
    public class Parser<SourceType> {
        public List<CompilerError> Errors = new();
        public List<CompilerWarning> Warnings = new();

        private Lexer<SourceType> _lexer;
        private Token _currentToken;
        private Queue<Token> _previousDebugTokens = new();

        private Stack<Token> _tokenStack = new();

        public Parser(Lexer<SourceType> lexer) {
            _lexer = lexer;

            Advance();
        }

        protected Token Current() {
            return _currentToken;
        }
        protected virtual Token Advance() {
            if (_tokenStack.Count > 0) {
                _currentToken = _tokenStack.Pop();
            }
            else {
                _currentToken = _lexer.GetNextToken();

                _previousDebugTokens.Enqueue(_currentToken);
                if (_previousDebugTokens.Count > 50) {
                    _previousDebugTokens.Dequeue();
                }

                if (_currentToken.Type == TokenType.Error) {
                    string msg = _currentToken.Value + " " + _currentToken.SourceFile + " " + _currentToken.Line + " " + _currentToken.Column;
                    System.Console.WriteLine(msg);
                    Error(msg);
                    Advance();
                }
                else if (_currentToken.Type == TokenType.Warning) {
                    Warning((string)_currentToken.Value);
                    Advance();
                }
            }

            if (_lookahead.Count > 0) {
                _lookahead.Peek().Push(_currentToken);
            }

            return Current();
        }

        protected void ReuseToken(Token token) {
            _tokenStack.Push(_currentToken);
            _currentToken = token;
        }

        private Stack<Stack<Token>> _lookahead = new();
        protected void SavePosition() {
            _lookahead.Push(new Stack<Token>());
            _lookahead.Peek().Push(_currentToken);
        }
        protected void RestorePosition() {
            var stack = _lookahead.Pop();
            while (stack.Count > 1) {
                Token token = stack.Pop();
                _tokenStack.Push(token);
            }
            _currentToken = stack.Pop();
        }
        protected void AcceptPosition() {
            foreach (var token in _lookahead.Pop()) {
                if (_lookahead.Count > 0) {
                    _lookahead.Peek().Push(token);
                }
            }
        }

        protected bool Check(TokenType type, [CallerMemberName] string member = "") {
            Token t = Current();
            if (Current().Type == type) {
                Advance();
                return true;
            }

            return false;
        }

        protected bool Check(IEnumerable<TokenType> types, [CallerMemberName] string member = "") {
            Token t = Current();
            TokenType currentType = Current().Type;
            foreach (TokenType type in types) {
                if (currentType == type) {
                    Advance();

                    return true;
                }
            }

            return false;
        }

        protected bool Peek(IEnumerable<TokenType> types, [CallerMemberName] string member = "") {
            Token t = Current();
            TokenType currentType = Current().Type;
            foreach (TokenType type in types) {
                if (currentType == type) {
                    return true;
                }
            }
            return false;
        }

        protected void Consume(TokenType type, string errorMessage) {
            Token t = Current();
            if (!Check(type)) {
                Error(errorMessage);
            }
        }

        protected void Consume(TokenType[] types, string errorMessage) {
            Token t = Current();
            foreach (TokenType type in types) {
                if (Check(type)) { return; }
            }
            Error(errorMessage);
        }

        protected string PrintToken(Token token) {
            var typetext = "";
            if (token.Type == TokenType.DM_Whitespace) {
                typetext = "";
            }
            else if (token.Type == TokenType.DM_Identifier) {
                typetext = "";
            }
            else {
                typetext = token.Type.ToString() + " ";
            }
            string tokentext = token.Text.Replace("\n", "\\n").Replace("\t", "\\t").Replace("\r", "");
            return "(" + typetext + tokentext + ") ";
        }

        protected string PrintDebugTokens(int num = 6) {
            string msg = "";
            msg += "Current token: " + PrintToken(_currentToken);
            msg += "\ntoken stack: ";
            foreach (var token in _tokenStack) {
                msg += PrintToken(token);
            }
            msg += "\nDebug tokens: ";
            var i = 0;
            foreach (var token in _previousDebugTokens) {
                msg += PrintToken(token);
                if (i == num) { break; }
            }
            return msg + '\n';

        }
        protected void Error(string message) {
            message += '\n' + PrintDebugTokens();

            Errors.Add(new CompilerError(_currentToken, message));
            Fatal();
        }

        protected void Warning(string message) {
            Warnings.Add(new CompilerWarning(_currentToken, message));
        }

        protected void Fatal() {
            foreach (var err in Errors) {
                System.Console.WriteLine(err);
            }
            throw new System.Exception("Cannot continue parse");
        }
    }
}
