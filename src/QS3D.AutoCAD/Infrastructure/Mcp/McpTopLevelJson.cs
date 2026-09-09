using System.Collections;
using System.Globalization;
using System.Text;

namespace QS3D.AutoCAD.Infrastructure.Mcp;

internal static class McpJson
{
    internal static object? Parse(string json) => new Parser(json ?? string.Empty).ParseDocument();

    internal static Dictionary<string, object?> ParseObject(string json)
    {
        var value = Parse(json);
        return value as Dictionary<string, object?>
            ?? throw new InvalidOperationException("Expected a JSON object.");
    }

    internal static string Serialize(object? value)
    {
        var builder = new StringBuilder();
        Write(builder, value);
        return builder.ToString();
    }

    private static void Write(StringBuilder builder, object? value)
    {
        switch (value)
        {
            case null:
                builder.Append("null");
                return;
            case string text:
                WriteString(builder, text);
                return;
            case bool flag:
                builder.Append(flag ? "true" : "false");
                return;
            case byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
                builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            case IDictionary<string, object?> map:
                builder.Append('{');
                var firstProperty = true;
                foreach (var pair in map)
                {
                    if (!firstProperty) builder.Append(',');
                    firstProperty = false;
                    WriteString(builder, pair.Key);
                    builder.Append(':');
                    Write(builder, pair.Value);
                }
                builder.Append('}');
                return;
            case IDictionary dictionary:
                builder.Append('{');
                var first = true;
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Key is not string key) continue;
                    if (!first) builder.Append(',');
                    first = false;
                    WriteString(builder, key);
                    builder.Append(':');
                    Write(builder, entry.Value);
                }
                builder.Append('}');
                return;
            case IEnumerable enumerable:
                builder.Append('[');
                var firstItem = true;
                foreach (var item in enumerable)
                {
                    if (!firstItem) builder.Append(',');
                    firstItem = false;
                    Write(builder, item);
                }
                builder.Append(']');
                return;
            default:
                WriteString(builder, Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty);
                return;
        }
    }

    private static void WriteString(StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if (ch < 0x20) builder.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                    else builder.Append(ch);
                    break;
            }
        }
        builder.Append('"');
    }

    private sealed class Parser
    {
        private const int MaxDepth = 64;
        private readonly string _text;
        private int _index;

        internal Parser(string text) => _text = text;

        internal object? ParseDocument()
        {
            SkipWhitespace();
            var value = ParseValue(0);
            SkipWhitespace();
            if (_index != _text.Length) throw Error("Unexpected trailing JSON content.");
            return value;
        }

        private object? ParseValue(int depth)
        {
            if (depth > MaxDepth) throw Error("JSON nesting is too deep.");
            SkipWhitespace();
            if (_index >= _text.Length) throw Error("Unexpected end of JSON.");
            return _text[_index] switch
            {
                '{' => ParseObject(depth + 1),
                '[' => ParseArray(depth + 1),
                '"' => ParseString(),
                't' => ParseLiteral("true", true),
                'f' => ParseLiteral("false", false),
                'n' => ParseLiteral("null", null),
                '-' or >= '0' and <= '9' => ParseNumber(),
                _ => throw Error("Unexpected JSON token.")
            };
        }

        private Dictionary<string, object?> ParseObject(int depth)
        {
            Expect('{');
            var result = new Dictionary<string, object?>(StringComparer.Ordinal);
            SkipWhitespace();
            if (TryConsume('}')) return result;
            while (true)
            {
                SkipWhitespace();
                if (_index >= _text.Length || _text[_index] != '"') throw Error("JSON object property name must be a string.");
                var name = ParseString();
                if (!result.TryAdd(name, null)) throw Error("Duplicate JSON property: " + name);
                SkipWhitespace();
                Expect(':');
                result[name] = ParseValue(depth);
                SkipWhitespace();
                if (TryConsume('}')) return result;
                Expect(',');
            }
        }

        private List<object?> ParseArray(int depth)
        {
            Expect('[');
            var result = new List<object?>();
            SkipWhitespace();
            if (TryConsume(']')) return result;
            while (true)
            {
                result.Add(ParseValue(depth));
                SkipWhitespace();
                if (TryConsume(']')) return result;
                Expect(',');
            }
        }

        private string ParseString()
        {
            Expect('"');
            var builder = new StringBuilder();
            while (_index < _text.Length)
            {
                var ch = _text[_index++];
                if (ch == '"') return builder.ToString();
                if (ch != '\\')
                {
                    if (ch < 0x20) throw Error("Control character is not allowed in a JSON string.");
                    builder.Append(ch);
                    continue;
                }
                if (_index >= _text.Length) throw Error("Unterminated JSON escape.");
                var escape = _text[_index++];
                switch (escape)
                {
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case '/': builder.Append('/'); break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u': builder.Append(ParseUnicodeEscape()); break;
                    default: throw Error("Invalid JSON escape sequence.");
                }
            }
            throw Error("Unterminated JSON string.");
        }

        private char ParseUnicodeEscape()
        {
            if (_index + 4 > _text.Length) throw Error("Incomplete JSON unicode escape.");
            var token = _text.Substring(_index, 4);
            _index += 4;
            return (char)int.Parse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        private object? ParseLiteral(string literal, object? value)
        {
            if (_index + literal.Length > _text.Length
                || !string.Equals(_text.Substring(_index, literal.Length), literal, StringComparison.Ordinal))
                throw Error("Invalid JSON literal.");
            _index += literal.Length;
            return value;
        }

        private object ParseNumber()
        {
            var start = _index;
            if (_text[_index] == '-') _index++;
            ReadDigits();
            var floating = false;
            if (_index < _text.Length && _text[_index] == '.')
            {
                floating = true;
                _index++;
                ReadDigits();
            }
            if (_index < _text.Length && (_text[_index] == 'e' || _text[_index] == 'E'))
            {
                floating = true;
                _index++;
                if (_index < _text.Length && (_text[_index] == '+' || _text[_index] == '-')) _index++;
                ReadDigits();
            }
            var token = _text.Substring(start, _index - start);
            if (!floating && long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)) return integer;
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && !double.IsNaN(number) && !double.IsInfinity(number)) return number;
            throw Error("Invalid or non-finite JSON number.");
        }

        private void ReadDigits()
        {
            var start = _index;
            while (_index < _text.Length && _text[_index] >= '0' && _text[_index] <= '9') _index++;
            if (_index == start) throw Error("JSON number requires digits.");
        }

        private bool TryConsume(char expected)
        {
            SkipWhitespace();
            if (_index >= _text.Length || _text[_index] != expected) return false;
            _index++;
            return true;
        }

        private void Expect(char expected)
        {
            SkipWhitespace();
            if (_index >= _text.Length || _text[_index] != expected) throw Error("Expected '" + expected + "'.");
            _index++;
        }

        private void SkipWhitespace()
        {
            while (_index < _text.Length && char.IsWhiteSpace(_text[_index])) _index++;
        }

        private FormatException Error(string message) => new(message + " Offset " + _index.ToString(CultureInfo.InvariantCulture) + ".");
    }
}

internal static class McpTopLevelJson
{
    internal static Dictionary<string, object?> ParseObject(string body) => McpJson.ParseObject(string.IsNullOrWhiteSpace(body) ? "{}" : body);

    internal static bool HasProperty(string body, string property) => ParseObject(body).ContainsKey(property);

    internal static string ExtractString(string body, string property)
    {
        var map = ParseObject(body);
        return map.TryGetValue(property, out var value) && value is string text ? text : string.Empty;
    }

    internal static bool ExtractBoolean(string body, string property)
    {
        var map = ParseObject(body);
        return map.TryGetValue(property, out var value) && value is bool flag && flag;
    }

    internal static bool TryGetBoolean(string body, string property, out bool value)
    {
        var map = ParseObject(body);
        if (map.TryGetValue(property, out var raw) && raw is bool flag) { value = flag; return true; }
        value = false;
        return false;
    }

    internal static double RequireDouble(string body, string property)
    {
        var map = ParseObject(body);
        if (!map.TryGetValue(property, out var value)) throw new InvalidOperationException(property + " is required.");
        var number = ConvertNumber(value, property);
        if (double.IsNaN(number) || double.IsInfinity(number)) throw new InvalidOperationException(property + " must be finite.");
        return number;
    }

    internal static double OptionalDouble(string body, string property, double fallback)
    {
        var map = ParseObject(body);
        return map.TryGetValue(property, out var value) ? ConvertNumber(value, property) : fallback;
    }

    internal static int OptionalInt(string body, string property, int fallback, int minimum, int maximum)
    {
        var map = ParseObject(body);
        if (!map.TryGetValue(property, out var value)) return fallback;
        var number = ConvertNumber(value, property);
        if (Math.Truncate(number) != number || number < minimum || number > maximum)
            throw new InvalidOperationException(property + " must be an integer between " + minimum + " and " + maximum + ".");
        return checked((int)number);
    }

    internal static Dictionary<string, object?>? ExtractObject(string body, string property)
    {
        var map = ParseObject(body);
        return map.TryGetValue(property, out var value) ? value as Dictionary<string, object?> : null;
    }

    internal static List<object?>? ExtractArray(string body, string property)
    {
        var map = ParseObject(body);
        return map.TryGetValue(property, out var value) ? value as List<object?> : null;
    }

    internal static string ToJson(object? value) => McpJson.Serialize(value);

    private static double ConvertNumber(object? value, string property)
    {
        if (value is long integer) return integer;
        if (value is double number) return number;
        if (value is int intValue) return intValue;
        throw new InvalidOperationException(property + " must be a number.");
    }
}