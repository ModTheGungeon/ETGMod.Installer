using System;
using System.Collections.Generic;

namespace ETGModInstaller {
    
    public struct Token {
        public enum TokenType {
            String,
            Separator
        }

        public Token(TokenType type, string content, int column, int row) {
            Type = type;
            Content = content;
            Column = column;
            Row = row;
        }

        public TokenType Type;
        public string Content;
        public int Column;
        public int Row;
    }

    class LexerException : Exception {
        public LexerException(int column, int row, string reason)
            : base($"Failed lexing at column {column}, row {row}: {reason}") {}
    }

    public static class CharExt {
        public static bool IsWhitespace(this char ch) {
            if (ch == ' ' || ch == '\n' || ch == '\t' || ch == '\r') return true;
            return false;
        }
    }

    public class Lexer {
        public Lexer(string content) {
            _Content = content;
            _Current = content[_Position];
        }

        private string _Content;
        private char _Current;
        private int _Position = 0;
        private int _Column = 1;
        private int _Row = 1;

        char Advance() {
            _Position += 1;
            if (_Position >= _Content.Length) {
                _Current = '\0';
                return _Current;
            }
            _Current = _Content[_Position];
            _Column += 1;
            if (_Current == '\n') {
                _Column = 0;
                _Row += 1;
            }
            return _Current;
        }

        char Peek() {
            return _Content[_Position + 1];
        }

        void SkipWhitespace() {
            while (_Current != '\0' && _Current.IsWhitespace()) Advance();
        }

        void Error(string reason) {
            throw new LexerException(_Column, _Row, reason);
        }

        Token? ReadString() {
            if (_Current == '"') {
                string final = "";
                bool escaped = false;
                while (true) {
                    Advance();
                    if (!escaped && _Current == '"') break;
                    if (_Current == '\0') Error("Unterminated quoted string.");

                    if (!escaped && _Current == '\\') {
                        escaped = true;
                        continue;
                    }

                    escaped = false;
                    final += _Current;
                }
                Advance();
                return new Token(Token.TokenType.String, final, _Column, _Row);
            }
            return null;
        }

        Token? ReadSeparator() {
            if (_Current == '=') {
                var tok = new Token(Token.TokenType.Separator, _Current.ToString(), _Column, _Row);
                Advance();
                return tok;
            }
            return null;
        }

        public Token[] Lex() {
            var toks = new List<Token>();

            while (true) {
                SkipWhitespace();
                if (_Current == '\0') break;
                var tok = ReadString();
                if (tok == null) tok = ReadSeparator();
                if (tok == null) Error($"Unknown token {_Current}");

                toks.Add(tok.Value);
            }

            return toks.ToArray();
        }
    }
}
