using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace JXServer.DataExporter.Legacy;

/// <summary>
/// Byte-preserving Lua 4.0 lexer operating on Latin1-decoded source strings so raw GBK/TCVN3 bytes
/// and <c>\ddd</c> decimal escapes remain intact.
/// </summary>
public static class Lua4Lexer
{
    public static List<Lua4Token> Tokenize(string src)
    {
        var tokens = new List<Lua4Token>(Math.Max(64, src.Length / 4));
        int i = 0;
        int line = 1;
        int len = src.Length;

        // Skip UTF-8 BOM if present in Latin1 (EF BB BF) or Unicode (FEFF)
        if (len >= 3 && src[0] == '\u00EF' && src[1] == '\u00BB' && src[2] == '\u00BF')
        {
            i = 3;
        }
        else if (len >= 1 && src[0] == '\uFEFF')
        {
            i = 1;
        }

        while (i < len)
        {
            char c = src[i];

            if (c is ' ' or '\t' or '\r' or '\f' or '\v' or '\0')
            {
                i++;
                continue;
            }

            if (c == '\n')
            {
                line++;
                i++;
                continue;
            }

            // Comments: -- ...
            if (c == '-' && i + 1 < len && src[i + 1] == '-')
            {
                i += 2;
                if (i + 1 < len && src[i] == '[' && src[i + 1] == '[')
                {
                    i += 2;
                    while (i < len)
                    {
                        if (src[i] == '\n')
                        {
                            line++;
                        }

                        if (i + 1 < len && src[i] == ']' && src[i + 1] == ']')
                        {
                            i += 2;
                            break;
                        }

                        i++;
                    }

                    continue;
                }

                while (i < len && src[i] != '\n')
                {
                    i++;
                }

                continue;
            }

            // Long string [[ ... ]]
            if (c == '[' && i + 1 < len && src[i + 1] == '[')
            {
                int strLine = line;
                i += 2;
                if (i < len && src[i] == '\n')
                {
                    line++;
                    i++;
                }

                var sb = new StringBuilder();
                while (i < len)
                {
                    if (i + 1 < len && src[i] == ']' && src[i + 1] == ']')
                    {
                        i += 2;
                        break;
                    }

                    if (src[i] == '\n')
                    {
                        line++;
                    }

                    sb.Append(src[i++]);
                }

                tokens.Add(new Lua4Token(Lua4TokenKind.String, sb.ToString(), 0, strLine));
                continue;
            }

            // Quoted string "..." or '...'
            if (c is '"' or '\'')
            {
                char quote = c;
                int strLine = line;
                i++;
                var sb = new StringBuilder();
                while (i < len && src[i] != quote)
                {
                    if (src[i] == '\n')
                    {
                        line++;
                    }

                    if (src[i] == '\\' && i + 1 < len)
                    {
                        i++;
                        char esc = src[i];
                        if (esc is >= '0' and <= '9')
                        {
                            int val = esc - '0';
                            i++;
                            if (i < len && src[i] is >= '0' and <= '9')
                            {
                                val = (val * 10) + (src[i] - '0');
                                i++;
                                if (i < len && src[i] is >= '0' and <= '9')
                                {
                                    int cand = (val * 10) + (src[i] - '0');
                                    if (cand <= 255)
                                    {
                                        val = cand;
                                        i++;
                                    }
                                }
                            }

                            sb.Append((char)(val & 0xFF));
                            continue;
                        }

                        sb.Append(esc switch
                        {
                            'a' => '\a',
                            'b' => '\b',
                            'f' => '\f',
                            'n' => '\n',
                            'r' => '\r',
                            't' => '\t',
                            'v' => '\v',
                            '\\' => '\\',
                            '"' => '"',
                            '\'' => '\'',
                            _ => esc,
                        });
                        i++;
                        continue;
                    }

                    sb.Append(src[i++]);
                }

                if (i < len && src[i] == quote)
                {
                    i++;
                }

                tokens.Add(new Lua4Token(Lua4TokenKind.String, sb.ToString(), 0, strLine));
                continue;
            }

            // Numbers
            if ((c is >= '0' and <= '9') || (c == '.' && i + 1 < len && src[i + 1] is >= '0' and <= '9'))
            {
                int start = i;
                if (c == '0' && i + 1 < len && (src[i + 1] == 'x' || src[i + 1] == 'X'))
                {
                    i += 2;
                    while (i < len && IsHexDigit(src[i]))
                    {
                        i++;
                    }

                    string hexStr = src[(start + 2)..i];
                    long hexVal = long.TryParse(hexStr, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long hv) ? hv : 0;
                    tokens.Add(new Lua4Token(Lua4TokenKind.Number, src[start..i], hexVal, line));
                    continue;
                }

                while (i < len && src[i] is >= '0' and <= '9')
                {
                    i++;
                }

                if (i < len && src[i] == '.')
                {
                    i++;
                    while (i < len && src[i] is >= '0' and <= '9')
                    {
                        i++;
                    }
                }

                if (i < len && (src[i] == 'e' || src[i] == 'E'))
                {
                    i++;
                    if (i < len && (src[i] == '+' || src[i] == '-'))
                    {
                        i++;
                    }

                    while (i < len && src[i] is >= '0' and <= '9')
                    {
                        i++;
                    }
                }

                string numStr = src[start..i];
                double dVal = double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double dv) ? dv : 0.0;
                tokens.Add(new Lua4Token(Lua4TokenKind.Number, numStr, dVal, line));
                continue;
            }

            // Lua 4.0 upvalue: %identifier (otherwise Modulo operator)
            if (c == '%')
            {
                if (i + 1 < len && IsIdentStart(src[i + 1]))
                {
                    i++;
                    int start = i;
                    while (i < len && IsIdentPart(src[i]))
                    {
                        i++;
                    }

                    tokens.Add(new Lua4Token(Lua4TokenKind.UpvalueIdentifier, src[start..i], 0, line));
                    continue;
                }

                tokens.Add(new Lua4Token(Lua4TokenKind.Percent, "%", 0, line));
                i++;
                continue;
            }

            // Identifiers / Keywords (including high-byte GBK identifiers used in some Kingsoft scripts)
            if (IsIdentStart(c))
            {
                int start = i;
                while (i < len && IsIdentPart(src[i]))
                {
                    i++;
                }

                string ident = src[start..i];
                Lua4TokenKind kw = ident switch
                {
                    "and" => Lua4TokenKind.And,
                    "break" => Lua4TokenKind.Break,
                    "do" => Lua4TokenKind.Do,
                    "else" => Lua4TokenKind.Else,
                    "elseif" => Lua4TokenKind.ElseIf,
                    "end" => Lua4TokenKind.End,
                    "for" => Lua4TokenKind.For,
                    "function" => Lua4TokenKind.Function,
                    "if" => Lua4TokenKind.If,
                    "in" => Lua4TokenKind.In,
                    "local" => Lua4TokenKind.Local,
                    "nil" => Lua4TokenKind.Nil,
                    "not" => Lua4TokenKind.Not,
                    "or" => Lua4TokenKind.Or,
                    "repeat" => Lua4TokenKind.Repeat,
                    "return" => Lua4TokenKind.Return,
                    "then" => Lua4TokenKind.Then,
                    "until" => Lua4TokenKind.Until,
                    "while" => Lua4TokenKind.While,
                    _ => Lua4TokenKind.Identifier,
                };
                tokens.Add(new Lua4Token(kw, ident, 0, line));
                continue;
            }

            // Operators & Punctuation
            switch (c)
            {
                case '+':
                    tokens.Add(new Lua4Token(Lua4TokenKind.Plus, "+", 0, line));
                    i++;
                    break;
                case '-':
                    tokens.Add(new Lua4Token(Lua4TokenKind.Minus, "-", 0, line));
                    i++;
                    break;
                case '*':
                    tokens.Add(new Lua4Token(Lua4TokenKind.Star, "*", 0, line));
                    i++;
                    break;
                case '/':
                    tokens.Add(new Lua4Token(Lua4TokenKind.Slash, "/", 0, line));
                    i++;
                    break;
                case '^':
                    tokens.Add(new Lua4Token(Lua4TokenKind.Caret, "^", 0, line));
                    i++;
                    break;
                case '(':
                    tokens.Add(new Lua4Token(Lua4TokenKind.LParen, "(", 0, line));
                    i++;
                    break;
                case ')':
                    tokens.Add(new Lua4Token(Lua4TokenKind.RParen, ")", 0, line));
                    i++;
                    break;
                case '{':
                    tokens.Add(new Lua4Token(Lua4TokenKind.LBrace, "{", 0, line));
                    i++;
                    break;
                case '}':
                    tokens.Add(new Lua4Token(Lua4TokenKind.RBrace, "}", 0, line));
                    i++;
                    break;
                case '[':
                    tokens.Add(new Lua4Token(Lua4TokenKind.LBracket, "[", 0, line));
                    i++;
                    break;
                case ']':
                    tokens.Add(new Lua4Token(Lua4TokenKind.RBracket, "]", 0, line));
                    i++;
                    break;
                case ';':
                    tokens.Add(new Lua4Token(Lua4TokenKind.Semicolon, ";", 0, line));
                    i++;
                    break;
                case ':':
                    tokens.Add(new Lua4Token(Lua4TokenKind.Colon, ":", 0, line));
                    i++;
                    break;
                case ',':
                    tokens.Add(new Lua4Token(Lua4TokenKind.Comma, ",", 0, line));
                    i++;
                    break;
                case '=':
                    if (i + 1 < len && src[i + 1] == '=')
                    {
                        tokens.Add(new Lua4Token(Lua4TokenKind.Eq, "==", 0, line));
                        i += 2;
                    }
                    else
                    {
                        tokens.Add(new Lua4Token(Lua4TokenKind.Assign, "=", 0, line));
                        i++;
                    }

                    break;
                case '~':
                    if (i + 1 < len && src[i + 1] == '=')
                    {
                        tokens.Add(new Lua4Token(Lua4TokenKind.Neq, "~=", 0, line));
                        i += 2;
                    }
                    else
                    {
                        i++;
                    }

                    break;
                case '<':
                    if (i + 1 < len && src[i + 1] == '=')
                    {
                        tokens.Add(new Lua4Token(Lua4TokenKind.Le, "<=", 0, line));
                        i += 2;
                    }
                    else
                    {
                        tokens.Add(new Lua4Token(Lua4TokenKind.Lt, "<", 0, line));
                        i++;
                    }

                    break;
                case '>':
                    if (i + 1 < len && src[i + 1] == '=')
                    {
                        tokens.Add(new Lua4Token(Lua4TokenKind.Ge, ">=", 0, line));
                        i += 2;
                    }
                    else
                    {
                        tokens.Add(new Lua4Token(Lua4TokenKind.Gt, ">", 0, line));
                        i++;
                    }

                    break;
                case '.':
                    if (i + 2 < len && src[i + 1] == '.' && src[i + 2] == '.')
                    {
                        tokens.Add(new Lua4Token(Lua4TokenKind.Dots, "...", 0, line));
                        i += 3;
                    }
                    else if (i + 1 < len && src[i + 1] == '.')
                    {
                        tokens.Add(new Lua4Token(Lua4TokenKind.Concat, "..", 0, line));
                        i += 2;
                    }
                    else
                    {
                        tokens.Add(new Lua4Token(Lua4TokenKind.Dot, ".", 0, line));
                        i++;
                    }

                    break;
                default:
                    // Skip stray control characters
                    i++;
                    break;
            }
        }

        tokens.Add(new Lua4Token(Lua4TokenKind.Eof, string.Empty, 0, line));
        return tokens;
    }

    private static bool IsIdentStart(char c) =>
        (c is >= 'a' and <= 'z') || (c is >= 'A' and <= 'Z') || c == '_' || c >= '\u0080';

    private static bool IsIdentPart(char c) =>
        IsIdentStart(c) || (c is >= '0' and <= '9');

    private static bool IsHexDigit(char c) =>
        (c is >= '0' and <= '9') || (c is >= 'a' and <= 'f') || (c is >= 'A' and <= 'F');
}
