using CsvHelper;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AMR_Engine
{
    internal sealed class DelimitedFileReader : IDisposable
    {
        private readonly CsvReader reader;
        private readonly CompatibleTextReader normalizer;

        public DelimitedFileReader(string path, char delimiter)
            : this(OpenText(path, delimiter), delimiter)
        {
        }

        internal DelimitedFileReader(TextReader source, char delimiter)
        {
            ValidateDelimiter(delimiter);
            char separator = delimiter > '\0' && delimiter <= '\u007f' ? delimiter : '\u001f';
            string whitespace = " \t\v\f".Replace(separator.ToString(), string.Empty);
            normalizer = new CompatibleTextReader(source, delimiter, separator);
            reader = CsvReader.Create(normalizer, new CsvReaderOptions
            {
                Separator = separator,
                HasHeader = false,
                Mode = CsvMode.Rfc4180,
                Quote = '"',
                Escape = separator == '\\' ? '\u001b' : '\\',
                TrimWhitespace = true,
                TrimInsideQuotes = false,
                WhitespaceCharacters = whitespace,
                SkipEmptyLines = true,
                AllowLineBreaksInQuotes = true,
                MalformedData = CsvMalformedDataHandling.Throw,
                FieldCountMismatch = CsvFieldCountHandling.Allow,
                MissingField = CsvMissingFieldHandling.Throw,
                TrackLineNumbers = true,
                DetectSeparatorHint = false,
                PoolStrings = false
            });
        }

        public string[] Fields { get; private set; }

        public bool Read()
        {
            Fields = null;
            try
            {
                if (!reader.Read())
                    return false;
                normalizer.ResolveLineNumber(reader.LineNumber);
                string[] fields = new string[reader.FieldCount];
                for (int index = 0; index < fields.Length; index++)
                    fields[index] = CompatibleTextReader.Decode(reader.GetString(index)).Trim();
                Fields = fields;
                return true;
            }
            catch (CsvFormatException exception)
            {
                long line = normalizer.ResolveLineNumber(exception.LineNumber);
                throw new MalformedLineException($"Line {line}: {exception.Message}", line);
            }
        }

        public string[] ReadFields() => Read() ? Fields : null;

        public void Dispose() => reader.Dispose();

        private static StreamReader OpenText(string path, char delimiter)
        {
            ValidateDelimiter(delimiter);
            return new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        }

        private static void ValidateDelimiter(char delimiter)
        {
            if (delimiter == '\r' || delimiter == '\n')
                throw new ArgumentException("A delimiter cannot be a line break.", nameof(delimiter));
            if (delimiter == '"')
                throw new InvalidOperationException("A delimiter cannot be a quote when quoted fields are enabled.");
        }

        // Normalize only syntax that CsvHelper cannot represent directly. Field values are decoded after GetString.
        private sealed class CompatibleTextReader : TextReader
        {
            private const char Marker = '\ue000';
            private enum FieldState { Start, Unquoted, Quoted, AfterQuote }

            private readonly TextReader source;
            private readonly char delimiter;
            private readonly char separator;
            private readonly char[] input = new char[8192];
            private int inputIndex;
            private int inputLength;
            private char[] line = new char[8192];
            private int lineIndex;
            private int lineLength;
            private long sourceLine;
            private long parsedLine;
            private readonly Queue<(long Parsed, long Source)> lineNumbers = new Queue<(long, long)>();
            private int pending = -1;
            private FieldState state;
            private bool quotePadding;
            private StringBuilder leadingWhitespace;
            private int leadingIndex;
            private bool flushingLeading;

            public CompatibleTextReader(TextReader source, char delimiter, char separator)
            {
                this.source = source;
                this.delimiter = delimiter;
                this.separator = separator;
            }

            public override int Read(char[] buffer, int index, int count)
            {
                ArgumentNullException.ThrowIfNull(buffer);
                return Read(buffer.AsSpan(index, count));
            }

#if NETFRAMEWORK
            public int Read(Span<char> buffer)
#else
            public override int Read(Span<char> buffer)
#endif
            {
                int written = 0;
                while (written < buffer.Length)
                {
                    if (pending >= 0)
                    {
                        buffer[written++] = (char)pending;
                        pending = -1;
                        continue;
                    }
                    if (flushingLeading)
                    {
                        char leading = leadingWhitespace[leadingIndex++];
                        if (leading == '\ufeff')
                        {
                            buffer[written++] = Marker;
                            pending = 'b';
                        }
                        else
                            buffer[written++] = leading;
                        if (leadingIndex == leadingWhitespace.Length)
                        {
                            leadingWhitespace.Clear();
                            flushingLeading = false;
                        }
                        continue;
                    }
                    if (lineIndex == lineLength)
                    {
                        if (!ReadDataLine())
                        {
                            if (leadingWhitespace != null && leadingWhitespace.Length > 0)
                            {
                                BeginLeadingFlush();
                                continue;
                            }
                            if (state == FieldState.AfterQuote && quotePadding)
                            {
                                buffer[written++] = separator;
                                state = FieldState.Start;
                                continue;
                            }
                            break;
                        }
                    }

                    char value = line[lineIndex++];
                    bool literalQuote = false;
                    if (state == FieldState.Start && leadingWhitespace != null && leadingWhitespace.Length > 0
                        && value != '"' && (value == delimiter || value == '\r' || value == '\n' || !IsQuoteWhitespace(value)))
                    {
                        lineIndex--;
                        state = FieldState.Unquoted;
                        BeginLeadingFlush();
                        continue;
                    }
                    if (state != FieldState.Quoted && value == delimiter)
                    {
                        buffer[written++] = separator;
                        state = FieldState.Start;
                        quotePadding = false;
                        continue;
                    }
                    if (state != FieldState.Quoted && (value == '\r' || value == '\n'))
                    {
                        buffer[written++] = value;
                        state = FieldState.Start;
                        quotePadding = false;
                        continue;
                    }

                    switch (state)
                    {
                        case FieldState.Start:
                            if (IsQuoteWhitespace(value))
                            {
                                if (!char.IsWhiteSpace(value) || leadingWhitespace != null && leadingWhitespace.Length > 0)
                                {
                                    if (leadingWhitespace == null)
                                        leadingWhitespace = new StringBuilder();
                                    leadingWhitespace.Append(value);
                                }
                                continue;
                            }
                            leadingWhitespace?.Clear();
                            state = value == '"' ? FieldState.Quoted : FieldState.Unquoted;
                            break;
                        case FieldState.Unquoted:
                            literalQuote = value == '"';
                            break;
                        case FieldState.Quoted:
                            if (value == '"')
                            {
                                state = FieldState.AfterQuote;
                                quotePadding = false;
                            }
                            break;
                        case FieldState.AfterQuote:
                            if (IsQuoteWhitespace(value))
                            {
                                value = separator == ' ' ? '\t' : ' ';
                                quotePadding = true;
                            }
                            else
                                state = value == '"' ? FieldState.Quoted : FieldState.Unquoted;
                            break;
                    }

                    if (value == Marker || value == '\ufeff' || literalQuote || (delimiter != separator && value == separator))
                    {
                        buffer[written++] = Marker;
                        pending = value == Marker ? 'e' : value == '\ufeff' ? 'b' : literalQuote ? 'q' : 's';
                    }
                    else
                        buffer[written++] = value;
                }
                return written;
            }

            private bool ReadDataLine()
            {
                do
                {
                    lineIndex = 0;
                    lineLength = 0;
                    int value;
                    while ((value = ReadInput()) >= 0)
                    {
                        AppendToLine((char)value);
                        if (value == '\r')
                        {
                            if (PeekInput() == '\n')
                                AppendToLine((char)ReadInput());
                            break;
                        }
                        if (value == '\n')
                            break;
                    }
                    if (lineLength == 0)
                        return false;
                    sourceLine++;
                    // TextFieldParser skipped blank physical lines even within multiline quoted fields.
                } while (line.AsSpan(0, lineLength).Trim().IsEmpty);
                lineNumbers.Enqueue((++parsedLine, sourceLine));
                return true;
            }

            private int PeekInput()
            {
                if (inputIndex == inputLength)
                {
                    inputLength = source.Read(input, 0, input.Length);
                    inputIndex = 0;
                }
                return inputLength == 0 ? -1 : input[inputIndex];
            }

            private int ReadInput()
            {
                int value = PeekInput();
                if (value >= 0)
                    inputIndex++;
                return value;
            }

            private void AppendToLine(char value)
            {
                if (lineLength == line.Length)
                    Array.Resize(ref line, checked(line.Length * 2));
                line[lineLength++] = value;
            }

            public long ResolveLineNumber(long parsed)
            {
                if (parsed == 0)
                    return 0;
                while (lineNumbers.Count > 1 && lineNumbers.Peek().Parsed < parsed)
                    lineNumbers.Dequeue();
                if (lineNumbers.Count == 0 || lineNumbers.Peek().Parsed != parsed)
                    throw new InvalidDataException("Delimited-text line tracking is inconsistent.");
                return lineNumbers.Peek().Source;
            }

            private void BeginLeadingFlush()
            {
                leadingIndex = 0;
                flushingLeading = true;
            }

            // Legacy quote padding differs from modern String.Trim (notably U+200B, U+FEFF, U+202F and U+205F).
            private static bool IsQuoteWhitespace(char value) => value == '\t' || value == '\v' || value == '\f'
                || value == ' ' || value == '\u0085' || value == '\u00a0' || value == '\u1680'
                || value >= '\u2000' && value <= '\u200b' || value == '\u2028' || value == '\u2029'
                || value == '\u3000' || value == '\ufeff';

            public static string Decode(string value)
            {
                int first = value.IndexOf(Marker);
                if (first < 0)
                    return value;

                StringBuilder decoded = new StringBuilder(value.Length);
                decoded.Append(value, 0, first);
                for (int index = first; index < value.Length; index++)
                {
                    char current = value[index];
                    if (current == Marker)
                    {
                        char code = value[++index];
                        current = code switch
                        {
                            'e' => Marker,
                            'b' => '\ufeff',
                            'q' => '"',
                            's' => '\u001f',
                            _ => throw new InvalidDataException("Invalid delimited-text compatibility escape.")
                        };
                    }
                    decoded.Append(current);
                }
                return decoded.ToString();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                    source.Dispose();
                base.Dispose(disposing);
            }
        }
    }
}
