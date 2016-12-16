using System;
using System.Collections.Generic;

namespace ETGModInstaller {
    public class ParserException : Exception {
        public ParserException(Token token, string message)
            : base($"Failed parsing token '{token.Content}' ({token.Row}:{token.Column}): {message}") {}
    }

    public class Parser {
        enum State {
            Key,
            Separator,
            Value
        }

        public static string SanitizeString(string str) {
            return str.Replace("\\", "\\\\").Replace("\"", "\\\\");
        }

        public static string CreateEntry(string key, string value) {
            return $"\"{SanitizeString(key)}\" = \"{SanitizeString(value)}\"";
        }

        public static Dictionary<String, String> Parse(Lexer lexer) {
            var dict = new Dictionary<String, String>();
            var tokens = lexer.Lex();

            var state = State.Key;
            string current_key = null;

            for (int i = 0; i < tokens.Length; i++) {
                var token = tokens[i];

                switch (state) {
                case State.Key:
                    if (token.Type == Token.TokenType.Separator) {
                        throw new ParserException(token, "Expected string, got separator");
                    }
                    current_key = token.Content;
                    state = State.Separator;
                    break;
                case State.Separator:
                    if (token.Type == Token.TokenType.String) {
                        throw new ParserException(token, "Expected separator, got string");
                    }
                    state = State.Value;
                    break;
                case State.Value:
                    if (token.Type == Token.TokenType.Separator) {
                        throw new ParserException(token, "Expected string, got separator");
                    }
                    if (current_key == null) throw new ParserException(token, "Tried to set value without current_key");
                    dict[current_key] = token.Content;
                    current_key = null;
                    state = State.Key;
                    break;
                }
            }

            return dict;
        }

        public static Dictionary<String, String> Parse(string input) {
            return Parse(new Lexer(input));
        }
    }
}
