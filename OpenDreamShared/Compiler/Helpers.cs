

using System.Collections.Generic;

namespace OpenDreamShared.Compiler {
    public partial class Parser<SourceType> {

        private Queue<Token> _previousDebugTokens = new();
        private void AddDebugToken(Token t) {
            _previousDebugTokens.Enqueue(t);

            if (_previousDebugTokens.Count > 12) {
                _previousDebugTokens.Dequeue();
            }
        }
        private string DebugTokensToString() {
            System.Text.StringBuilder sb = new();
            foreach (var token in _previousDebugTokens) {
                sb.Append(token.ShortString() + " ");
            }
            return sb.ToString();
        }
    }
    public partial class Token {
        public string ShortString() {
            string prefix = "";
            string inner = Text.Replace("\n", "\\n").Replace("\t", "\\t");
            switch (Type) {
                case TokenType.DM_Whitespace:
                case TokenType.DM_Preproc_Whitespace:
                case TokenType.DM_Preproc_Identifier:
                case TokenType.DM_Identifier: break;
                default: prefix = Type.ToString(); break;
            }
            return prefix + "(" + inner + ")";
        }
    }
}
