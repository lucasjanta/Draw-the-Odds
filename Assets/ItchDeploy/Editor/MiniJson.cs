using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ItchDeploy.Editor
{
    internal static class MiniJson
    {
        public abstract class Value { }

        public sealed class Object : Value
        {
            public readonly Dictionary<string, Value> Members = new();

            public IEnumerable<string> Keys => Members.Keys;

            public Object GetObject(string key)
                => TryGet(key, out var v) && v is Object o ? o : null;

            public Array GetArray(string key)
                => TryGet(key, out var v) && v is Array a ? a : null;

            public string GetString(string key, string fallback = "")
                => TryGet(key, out var v) && v is Primitive p && p.Value is string s ? s : fallback;

            public bool GetBool(string key, bool fallback = false)
            {
                if (!TryGet(key, out var v) || v is not Primitive p) return fallback;
                return p.Value switch
                {
                    bool b => b,
                    string s => bool.TryParse(s, out var parsed) && parsed,
                    _ => false,
                };
            }

            public double GetNumber(string key, double fallback = 0)
                => TryGet(key, out var v) && v is Primitive p && p.Value is double d ? d : fallback;

            public bool TryGet(string key, out Value value)
                => Members.TryGetValue(key, out value);

            public Object Set(string key, Value value)
            {
                Members[key] = value ?? new Primitive { Value = null };
                return this;
            }

            public Object Set(string key, string value)
                => Set(key, new Primitive { Value = value });

            public Object Set(string key, bool value)
                => Set(key, new Primitive { Value = value });

            public Object Set(string key, double value)
                => Set(key, new Primitive { Value = value });
        }

        public sealed class Array : Value
        {
            public readonly List<Value> Items = new();
        }

        public sealed class Primitive : Value
        {
            public object Value; 
        }

        public static Value Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var p = new Parser(json);
            p.SkipWhitespace();
            var value = p.ParseValue();
            p.SkipWhitespace();
            return value;
        }

        public static string Serialize(Value value)
        {
            var builder = new StringBuilder();
            WriteValue(builder, value);
            return builder.ToString();
        }

        private static void WriteValue(StringBuilder builder, Value value)
        {
            switch (value)
            {
                case Object obj:
                    builder.Append('{');
                    var firstMember = true;
                    foreach (var pair in obj.Members)
                    {
                        if (!firstMember) builder.Append(',');
                        firstMember = false;
                        WriteString(builder, pair.Key);
                        builder.Append(':');
                        WriteValue(builder, pair.Value);
                    }
                    builder.Append('}');
                    return;
                case Array array:
                    builder.Append('[');
                    for (var index = 0; index < array.Items.Count; index++)
                    {
                        if (index > 0) builder.Append(',');
                        WriteValue(builder, array.Items[index]);
                    }
                    builder.Append(']');
                    return;
                case Primitive primitive:
                    WritePrimitive(builder, primitive.Value);
                    return;
                case null:
                    builder.Append("null");
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), "Unsupported JSON value.");
            }
        }

        private static void WritePrimitive(StringBuilder builder, object value)
        {
            switch (value)
            {
                case null:
                    builder.Append("null");
                    break;
                case string text:
                    WriteString(builder, text);
                    break;
                case bool boolean:
                    builder.Append(boolean ? "true" : "false");
                    break;
                case double number:
                    builder.Append(number.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case float number:
                    builder.Append(number.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case int number:
                    builder.Append(number.ToString(CultureInfo.InvariantCulture));
                    break;
                case long number:
                    builder.Append(number.ToString(CultureInfo.InvariantCulture));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), "Unsupported JSON primitive.");
            }
        }

        private static void WriteString(StringBuilder builder, string value)
        {
            builder.Append('"');
            foreach (var character in value ?? string.Empty)
            {
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < 0x20)
                            builder.Append("\\u").Append(((int)character).ToString("x4"));
                        else
                            builder.Append(character);
                        break;
                }
            }
            builder.Append('"');
        }

        private struct Parser
        {
            private readonly string s;
            private int i;

            public Parser(string source) { s = source; i = 0; }

            public Value ParseValue()
            {
                SkipWhitespace();
                if (i >= s.Length) throw new FormatException("Unexpected end of JSON.");
                var c = s[i];
                switch (c)
                {
                    case '{': return ParseObject();
                    case '[': return ParseArray();
                    case '"': return new Primitive { Value = ParseString() };
                    case 't': case 'f': return new Primitive { Value = ParseBool() };
                    case 'n': return new Primitive { Value = ParseNull() };
                    default: return new Primitive { Value = ParseNumber() };
                }
            }

            private Object ParseObject()
            {
                var obj = new Object();
                Assume('{');
                SkipWhitespace();
                if (Peek() == '}') { i++; return obj; }

                while (true)
                {
                    SkipWhitespace();

                    if (Peek() == '}') { i++; return obj; }

                    var key = ParseString();
                    SkipWhitespace();
                    Assume(':');
                    var value = ParseValue();
                    obj.Members[key] = value;
                    SkipWhitespace();
                    var next = Peek();
                    if (next == ',') { i++; continue; }
                    if (next == '}') { i++; return obj; }
                    throw new FormatException($"Expected ',' or '}}' at {i}.");
                }
            }

            private Array ParseArray()
            {
                var arr = new Array();
                Assume('[');
                SkipWhitespace();
                if (Peek() == ']') { i++; return arr; }

                while (true)
                {
                    arr.Items.Add(ParseValue());
                    SkipWhitespace();
                    var next = Peek();
                    if (next == ',') { i++; SkipWhitespace(); if (Peek() == ']') { i++; return arr; } continue; }
                    if (next == ']') { i++; return arr; }
                    throw new FormatException($"Expected ',' or ']' at {i}.");
                }
            }

            private string ParseString()
            {
                Assume('"');
                var sb = new StringBuilder();
                while (i < s.Length)
                {
                    var c = s[i++];
                    if (c == '"') return sb.ToString();
                    if (c == '\\')
                    {
                        if (i >= s.Length) break;
                        var esc = s[i++];
                        switch (esc)
                        {
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case '/': sb.Append('/'); break;
                            case 'b': sb.Append('\b'); break;
                            case 'f': sb.Append('\f'); break;
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case 'u':

                                if (i + 4 > s.Length)
                                    throw new FormatException(
                                        $"Incomplete \\u escape at {i}.");
                                var hex = s.Substring(i, 4);
                                if (!ushort.TryParse(
                                        hex,
                                        NumberStyles.HexNumber,
                                        CultureInfo.InvariantCulture,
                                        out var codePoint))
                                    throw new FormatException(
                                        $"'{hex}' is not a valid \\u escape at {i}.");
                                i += 4;
                                sb.Append((char)codePoint);
                                break;
                            default: sb.Append(esc); break;
                        }
                    }
                    else sb.Append(c);
                }
                return sb.ToString();
            }

            private double ParseNumber()
            {
                var start = i;
                while (i < s.Length)
                {
                    var c = s[i];
                    if (char.IsDigit(c) || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E')
                        i++;
                    else break;
                }

                var text = s.Substring(start, i - start);
                if (!double.TryParse(
                        text,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var number))
                {
                    throw new FormatException(text.Length == 0
                        ? $"Unexpected character '{s[start]}' at {start}."
                        : $"'{text}' is not a valid number at {start}.");
                }

                return number;
            }

            private bool ParseBool()
            {
                if (TryConsumeLiteral("true")) return true;
                if (TryConsumeLiteral("false")) return false;
                throw new FormatException($"Expected 'true' or 'false' at {i}.");
            }

            private object ParseNull()
            {
                if (!TryConsumeLiteral("null"))
                    throw new FormatException($"Expected 'null' at {i}.");
                return null;
            }

            private bool TryConsumeLiteral(string literal)
            {
                if (i + literal.Length > s.Length) return false;
                if (string.CompareOrdinal(s, i, literal, 0, literal.Length) != 0) return false;
                i += literal.Length;
                return true;
            }

            public void SkipWhitespace()
            {
                while (i < s.Length)
                {
                    var c = s[i];
                    if (c == ' ' || c == '\t' || c == '\n' || c == '\r') { i++; continue; }

                    if (c == '/' && i + 1 < s.Length && s[i + 1] == '/')
                    {
                        while (i < s.Length && s[i] != '\n') i++;
                        continue;
                    }
                    break;
                }
            }

            private char Peek()
            {
                if (i >= s.Length) return '\0';
                return s[i];
            }

            private void Assume(char c)
            {
                if (i >= s.Length || s[i] != c)
                    throw new FormatException($"Expected '{c}' at {i}.");
                i++;
            }
        }
    }
}
