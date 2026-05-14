using System;
using System.Collections.Generic;

namespace McpUnity.Server
{
    /// <summary>
    /// Simple JSON parser that supports Dictionary<string, object>
    /// </summary>
    public class SimpleJsonParser
    {
        private readonly string _json;
        private int _pos;
        private int _depth = 0;
        private const int MaxDepth = 64;
        private readonly System.Text.StringBuilder _sb = new System.Text.StringBuilder(256);

        public SimpleJsonParser(string json)
        {
            _json = json;
            _pos = 0;
        }

        public Dictionary<string, object> ParseObject()
        {
            SkipWhitespace();
            if (_pos >= _json.Length || _json[_pos] != '{') return null;
            _depth++;
            if (_depth > MaxDepth)
                throw new InvalidOperationException($"JSON nesting too deep (max: {MaxDepth})");
            _pos++; // skip '{'

            var result = new Dictionary<string, object>();

            SkipWhitespace();
            if (_pos < _json.Length && _json[_pos] == '}')
            {
                _pos++;
                _depth--;
                return result;
            }

            while (_pos < _json.Length)
            {
                SkipWhitespace();

                // Parse key
                var key = ParseString();
                if (key == null) break;

                SkipWhitespace();
                if (_pos >= _json.Length || _json[_pos] != ':') break;
                _pos++; // skip ':'

                SkipWhitespace();
                var value = ParseValue();
                result[key] = value;

                SkipWhitespace();
                if (_pos >= _json.Length) break;

                if (_json[_pos] == '}')
                {
                    _pos++;
                    _depth--;
                    return result;
                }

                if (_json[_pos] == ',')
                {
                    _pos++;
                    continue;
                }

                break;
            }

            _depth--;
            return result;
        }

        private object ParseValue()
        {
            SkipWhitespace();
            if (_pos >= _json.Length) return null;

            char c = _json[_pos];

            if (c == '"') return ParseString();
            if (c == '{') return ParseObject();
            if (c == '[') return ParseArray();
            if (c == 't' || c == 'f') return ParseBool();
            if (c == 'n') return ParseNull();
            if (c == '-' || char.IsDigit(c)) return ParseNumber();

            return null;
        }

        private string ParseString()
        {
            if (_pos >= _json.Length || _json[_pos] != '"') return null;
            _pos++; // skip opening quote

            _sb.Clear();
            while (_pos < _json.Length)
            {
                char c = _json[_pos];
                if (c == '"')
                {
                    _pos++;
                    return _sb.ToString();
                }
                if (c == '\\' && _pos + 1 < _json.Length)
                {
                    _pos++;
                    char escaped = _json[_pos];
                    switch (escaped)
                    {
                        case 'n': _sb.Append('\n'); break;
                        case 'r': _sb.Append('\r'); break;
                        case 't': _sb.Append('\t'); break;
                        case '"': _sb.Append('"'); break;
                        case '\\': _sb.Append('\\'); break;
                        case 'u':
                            if (_pos + 4 < _json.Length)
                            {
                                var hex = _json.Substring(_pos + 1, 4);
                                if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber,
                                    System.Globalization.CultureInfo.InvariantCulture, out int codePoint))
                                {
                                    // FIX-#386: handle UTF-16 surrogate pairs. A high surrogate
                                    // (0xD800-0xDBFF) followed by \uXXXX low surrogate (0xDC00-0xDFFF)
                                    // must be combined into a single codepoint or emitted as the
                                    // pair so the resulting string round-trips through .NET strings.
                                    if (codePoint >= 0xD800 && codePoint <= 0xDBFF
                                        && _pos + 10 < _json.Length
                                        && _json[_pos + 5] == '\\' && _json[_pos + 6] == 'u')
                                    {
                                        var lowHex = _json.Substring(_pos + 7, 4);
                                        if (int.TryParse(lowHex, System.Globalization.NumberStyles.HexNumber,
                                            System.Globalization.CultureInfo.InvariantCulture, out int lowCp)
                                            && lowCp >= 0xDC00 && lowCp <= 0xDFFF)
                                        {
                                            _sb.Append((char)codePoint);
                                            _sb.Append((char)lowCp);
                                            _pos += 10; // 4 hex + \u + 4 hex
                                            break;
                                        }
                                    }
                                    _sb.Append((char)codePoint);
                                    _pos += 4;
                                }
                                else
                                {
                                    _sb.Append('u');
                                }
                            }
                            else
                            {
                                _sb.Append('u');
                            }
                            break;
                        case '/': _sb.Append('/'); break;
                        case 'b': _sb.Append('\b'); break;
                        case 'f': _sb.Append('\f'); break;
                        default: _sb.Append(escaped); break;
                    }
                }
                else
                {
                    _sb.Append(c);
                }
                _pos++;
            }
            return _sb.ToString();
        }

        private List<object> ParseArray()
        {
            if (_pos >= _json.Length || _json[_pos] != '[') return null;
            _depth++;
            if (_depth > MaxDepth)
                throw new InvalidOperationException($"JSON nesting too deep (max: {MaxDepth})");
            _pos++; // skip '['

            var result = new List<object>();

            SkipWhitespace();
            if (_pos < _json.Length && _json[_pos] == ']')
            {
                _pos++;
                _depth--;
                return result;
            }

            while (_pos < _json.Length)
            {
                SkipWhitespace();
                var value = ParseValue();
                result.Add(value);

                SkipWhitespace();
                if (_pos >= _json.Length) break;

                if (_json[_pos] == ']')
                {
                    _pos++;
                    _depth--;
                    return result;
                }

                if (_json[_pos] == ',')
                {
                    _pos++;
                    continue;
                }

                break;
            }

            _depth--;
            return result;
        }

        private object ParseNumber()
        {
            int start = _pos;
            if (_json[_pos] == '-') _pos++;

            while (_pos < _json.Length && char.IsDigit(_json[_pos])) _pos++;

            bool isFloat = false;
            if (_pos < _json.Length && _json[_pos] == '.')
            {
                isFloat = true;
                _pos++;
                while (_pos < _json.Length && char.IsDigit(_json[_pos])) _pos++;
            }

            if (_pos < _json.Length && (_json[_pos] == 'e' || _json[_pos] == 'E'))
            {
                isFloat = true;
                _pos++;
                if (_pos < _json.Length && (_json[_pos] == '+' || _json[_pos] == '-')) _pos++;
                while (_pos < _json.Length && char.IsDigit(_json[_pos])) _pos++;
            }

            string numStr = _json.Substring(start, _pos - start);

            if (isFloat)
            {
                if (double.TryParse(numStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double d))
                    return d;
            }
            else
            {
                if (int.TryParse(numStr, out int i)) return i;
                if (long.TryParse(numStr, out long l)) return l;
                if (double.TryParse(numStr, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double d))
                    return d;
            }

            return null;
        }

        private bool ParseBool()
        {
            if (_pos + 4 <= _json.Length &&
                _json[_pos] == 't' && _json[_pos + 1] == 'r' &&
                _json[_pos + 2] == 'u' && _json[_pos + 3] == 'e')
            {
                _pos += 4;
                return true;
            }
            if (_pos + 5 <= _json.Length &&
                _json[_pos] == 'f' && _json[_pos + 1] == 'a' &&
                _json[_pos + 2] == 'l' && _json[_pos + 3] == 's' && _json[_pos + 4] == 'e')
            {
                _pos += 5;
                return false;
            }
            return false;
        }

        private object ParseNull()
        {
            if (_pos + 4 <= _json.Length &&
                _json[_pos] == 'n' && _json[_pos + 1] == 'u' &&
                _json[_pos + 2] == 'l' && _json[_pos + 3] == 'l')
            {
                _pos += 4;
                return null;
            }
            return null;
        }

        private void SkipWhitespace()
        {
            while (_pos < _json.Length && char.IsWhiteSpace(_json[_pos]))
                _pos++;
        }
    }
}
