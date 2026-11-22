// A language file as the generator reads it: one JSON object of key/text pairs, nothing nested.
//
// Read by hand rather than with System.Text.Json: an analyzer brings no dependencies of its own
// into the compiler, and a file this plain needs twenty lines of reading. Reading it here also
// gives each key its line, so an error points at the line to fix, and a key written twice is an
// error rather than the second one quietly winning.

using System.Collections.Generic;
using System.Text;

namespace Karpo.Strings.Generator;

/// <summary>One key and its text, and where the key stands in the file (0-based line and column).</summary>
internal sealed class Entry
{
    public Entry(string key, string text, int line, int column, int start, int length)
    {
        Key = key;
        Text = text;
        Line = line;
        Column = column;
        Start = start;
        Length = length;
    }

    public string Key { get; }

    public string Text { get; }

    public int Line { get; }

    public int Column { get; }

    public int Start { get; }

    public int Length { get; }
}

/// <summary>A file that doesn't read: what is wrong, and where.</summary>
internal sealed class JsonError
{
    public JsonError(string message, int line, int column, int start)
    {
        Message = message;
        Line = line;
        Column = column;
        Start = start;
    }

    public string Message { get; }

    public int Line { get; }

    public int Column { get; }

    public int Start { get; }
}

internal static class FlatJson
{
    /// <summary>The pairs of <paramref name="text"/>, in the file's order; null and <paramref name="error"/> for a file that doesn't read.</summary>
    public static List<Entry>? Read(string text, out JsonError? error)
    {
        var reader = new Reader(text);
        var entries = new List<Entry>();
        error = null;
        try
        {
            reader.Skip();
            if (reader.Peek() == '﻿')
            {
                reader.Next();
                reader.Skip();
            }
            reader.Expect('{');
            reader.Skip();
            if (reader.Peek() == '}')
            {
                reader.Next();
            }
            else
            {
                while (true)
                {
                    reader.Skip();
                    var (line, column, start) = reader.Here();
                    var key = reader.String();
                    var length = reader.Position - start;
                    reader.Skip();
                    reader.Expect(':');
                    reader.Skip();
                    if (reader.Peek() != '"')
                    {
                        throw reader.Fail("a text is written in double quotes; nested objects, numbers and lists are not read");
                    }
                    var value = reader.String();
                    entries.Add(new Entry(key, value, line, column, start, length));
                    reader.Skip();
                    var next = reader.Next();
                    if (next == '}')
                    {
                        break;
                    }
                    if (next != ',')
                    {
                        throw reader.Fail("expected ',' or '}'", back: 1);
                    }
                }
            }
            reader.Skip();
            if (!reader.AtEnd)
            {
                throw reader.Fail("nothing may follow the object");
            }
            return entries;
        }
        catch (ReadFailure failure)
        {
            error = failure.Error;
            return null;
        }
    }

    private sealed class ReadFailure : System.Exception
    {
        public ReadFailure(JsonError error)
            : base(error.Message) => Error = error;

        public JsonError Error { get; }
    }

    private sealed class Reader
    {
        private readonly string text;
        private int line;
        private int lineStart;

        public Reader(string text) => this.text = text;

        public int Position { get; private set; }

        public bool AtEnd => Position >= text.Length;

        public (int Line, int Column, int Start) Here() => (line, Position - lineStart, Position);

        public char Peek() => AtEnd ? '\0' : text[Position];

        public char Next()
        {
            if (AtEnd)
            {
                throw Fail("the file ends too soon");
            }
            var c = text[Position++];
            if (c == '\n')
            {
                line++;
                lineStart = Position;
            }
            return c;
        }

        public void Skip()
        {
            while (!AtEnd && (Peek() is ' ' or '\t' or '\r' or '\n'))
            {
                Next();
            }
        }

        public void Expect(char wanted)
        {
            if (AtEnd || Peek() != wanted)
            {
                throw Fail($"expected '{wanted}'");
            }
            Next();
        }

        public string String()
        {
            Expect('"');
            var result = new StringBuilder();
            while (true)
            {
                var c = Next();
                if (c == '"')
                {
                    return result.ToString();
                }
                if (c is '\n' or '\r')
                {
                    throw Fail("a text may not run over a line end; write \\n", back: 1);
                }
                if (c != '\\')
                {
                    result.Append(c);
                    continue;
                }
                var escape = Next();
                switch (escape)
                {
                    case '"': result.Append('"'); break;
                    case '\\': result.Append('\\'); break;
                    case '/': result.Append('/'); break;
                    case 'b': result.Append('\b'); break;
                    case 'f': result.Append('\f'); break;
                    case 'n': result.Append('\n'); break;
                    case 'r': result.Append('\r'); break;
                    case 't': result.Append('\t'); break;
                    case 'u':
                        var code = 0;
                        for (var i = 0; i < 4; i++)
                        {
                            var digit = Next();
                            var value = digit >= '0' && digit <= '9' ? digit - '0'
                                : digit >= 'a' && digit <= 'f' ? digit - 'a' + 10
                                : digit >= 'A' && digit <= 'F' ? digit - 'A' + 10
                                : -1;
                            if (value < 0)
                            {
                                throw Fail("\\u takes four hex digits", back: 1);
                            }
                            code = (code * 16) + value;
                        }
                        result.Append((char)code);
                        break;
                    default:
                        throw Fail($"'\\{escape}' is no JSON escape", back: 2);
                }
            }
        }

        public System.Exception Fail(string message, int back = 0)
        {
            var at = System.Math.Max(lineStart, Position - back);
            return new ReadFailure(new JsonError(message, line, at - lineStart, at));
        }
    }
}
