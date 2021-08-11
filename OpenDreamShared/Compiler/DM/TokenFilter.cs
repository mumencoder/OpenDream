
using System.Text;
using System.Collections.Generic;

namespace OpenDreamShared.Compiler.DM {
    public class TokenWhitespaceFilter : DMLexer {

        private DMLexer _innerlexer;
        private Token _lastToken;
        private Token _currentToken;
        private Token _nextToken;

        public TokenWhitespaceFilter(DMLexer lexer) : base(lexer.SourceName, new List<Token>(lexer.Source)) {
            _innerlexer = lexer;
            _nextToken = _innerlexer.GetNextToken();
        }

        public override Token GetNextToken() {
            do {
                _lastToken = _currentToken;
                _currentToken = _nextToken;
                _nextToken = _innerlexer.GetNextToken();
            } while (_currentToken.Type == TokenType.DM_Whitespace);

            if (_lastToken != null && _lastToken.Type == TokenType.DM_Whitespace) {
                _currentToken.LeadingWhitespace = true;
            }
            if (_nextToken != null && _nextToken.Type == TokenType.DM_Whitespace) {
                _currentToken.TrailingWhitespace = true;
            }
            return _currentToken;
        }
    }

    public class TokenLogFilter : DMLexer {
        private DMLexer _innerlexer;
        private System.IO.Stream _stream;

        public TokenLogFilter(DMLexer lexer, System.IO.Stream stream) : base(lexer.SourceName, new List<Token>(lexer.Source)) {
            _innerlexer = lexer;
            _stream = stream;
        }

        public override Token GetNextToken() {
            Token t = base.GetNextToken();
            var s = t.LeadingWhitespace + t.ShortString() + t.TrailingWhitespace + '\n';
            _stream.Write(Encoding.ASCII.GetBytes(s), 0, s.Length);
            return t;
        }


    }

}
