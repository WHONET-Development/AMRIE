using AMR_Engine;
using CsvHelper;
using Microsoft.VisualBasic.FileIO;
using System.Text;

namespace Interpretation_Engine.Tests;

public sealed class DelimitedFileReaderTests
{
    public static IEnumerable<object[]> AcceptedRecords()
    {
        yield return ["\r\n \t\r\nA|B\r\n||\r\n\"\"\r\n", '|', new string[][] { ["A", "B"], ["", "", ""], [""] }];
        yield return [" a | \" b \" |\"x|y\"|\"two\"\"quotes\"|\r\n", '|', new string[][] { ["a", "b", "x|y", "two\"quotes", ""] }];
        yield return ["a\"b|c\r\n", '|', new string[][] { ["a\"b", "c"] }];
        yield return ["\"one\ntwo\"|x\n", '|', new string[][] { ["one\ntwo", "x"] }];
        yield return ["\"one\rtwo\"|x\r", '|', new string[][] { ["one\rtwo", "x"] }];
        yield return ["\"one\r\ntwo\"|x\r\n", '|', new string[][] { ["one\r\ntwo", "x"] }];
        yield return ["\u00a0\u2003\" left\u00a0 \"\u2003|\u00a0right\u2003|\r\n", '|', new string[][] { ["left", "right", ""] }];
        yield return ["left\u00a7\"x\u00a7y\"\u00a7\r\n", '\u00a7', new string[][] { ["left", "x\u00a7y", ""] }];
        yield return ["a b \r\n", ' ', new string[][] { ["a", "b", ""] }];
        yield return ["\t\r\nA\tB\t\r\n", '\t', new string[][] { ["A", "B", ""] }];
        yield return ["a|b\r\nx\r\nx|y|z\r\n", '|', new string[][] { ["a", "b"], ["x"], ["x", "y", "z"] }];
        yield return ["\"x\"   |\"\"|last", '|', new string[][] { ["x", "", "last"] }];
        yield return ["", '|', Array.Empty<string[]>()];
        yield return [" \r\n\t\r\n", '|', Array.Empty<string[]>()];
        yield return ["left\u00a7literal\u001f\ue000eqs\"quote\u00a7\"\u001f\ue000\u00a7\"\r\n", '\u00a7', new string[][] { ["left", "literal\u001f\ue000eqs\"quote", "\u001f\ue000\u00a7"] }];
        yield return ["\"\r\n\r\n\"|x\r\n", '|', new string[][] { ["", "x"] }];
        yield return ["\u200b \ufeffunquoted|\u200b \ufeff\r\n\u200b\r\n\u202f\u205f\r\n", '|', new string[][] { ["\u200b \ufeffunquoted", "\u200b \ufeff"], ["\u200b"] }];
        yield return ["\u200b\ufeff\"value\"\u200b\ufeff|x\r\n", '|', new string[][] { ["value", "x"] }];
        yield return ["\"one\r\r \t\r\ntwo\"|x\r\n", '|', new string[][] { ["one\rtwo", "x"] }];
        yield return ["\"value\" \u200b", '|', new string[][] { ["value", ""] }];
        yield return ["\ufeff|x\r\n", '|', new string[][] { ["\ufeff", "x"] }];
    }

    [Theory]
    [MemberData(nameof(AcceptedRecords))]
    public void ParserMatchesCharacterizedRecords(string text, char delimiter, string[][] expected)
    {
        string[][] actual = ReadLegacy(new StringReader(text), delimiter);
        Assert.Equal(expected.Length, actual.Length);
        for (int index = 0; index < expected.Length; index++)
            Assert.Equal(expected[index], actual[index]);

        using DelimitedFileReader parser = new(new StringReader(text), delimiter);
        List<string[]> candidate = [];
        while (parser.Read())
            candidate.Add(parser.Fields);
        Assert.Equal(expected.Length, candidate.Count);
        for (int index = 0; index < expected.Length; index++)
            Assert.Equal(expected[index], candidate[index]);
    }

    [Theory]
    [InlineData("\"unterminated|x")]
    [InlineData("\"value\"junk|x\r\n")]
    [InlineData("\"value\"\"|x\r\n")]
    [InlineData("\"value\" \"other\"|x\r\n")]
    [InlineData("a\"b|\"value\"junk\r\n")]
    [InlineData("\"value\"\u202f|x\r\n")]
    [InlineData("\"value\"\u205f|x\r\n")]
    public void ParserRejectsMalformedQuotedRecords(string text)
    {
        Assert.Throws<MalformedLineException>(() => ReadLegacy(new StringReader(text), '|'));
        using DelimitedFileReader parser = new(new StringReader(text), '|');
        Assert.Throws<MalformedLineException>(() => parser.Read());
    }

    [Theory]
    [InlineData("utf-8")]
    [InlineData("utf-16")]
    [InlineData("utf-16BE")]
    [InlineData("utf-32")]
    public void ParserDetectsByteOrderMarks(string encodingName)
    {
        Encoding encoding = Encoding.GetEncoding(encodingName);
        using MemoryStream stream = new();
        stream.Write(encoding.GetPreamble());
        stream.Write(encoding.GetBytes("HEADER|VALUE\r\nx|\u00e9\r\n"));
        stream.Position = 0;
        using TextFieldParser parser = new(stream, Encoding.UTF8);
        parser.SetDelimiters("|");
        Assert.Equal(new[] { "HEADER", "VALUE" }, parser.ReadFields());
        Assert.Equal(new[] { "x", "\u00e9" }, parser.ReadFields());

        using MemoryStream candidateStream = new();
        candidateStream.Write(encoding.GetPreamble());
        candidateStream.Write(encoding.GetBytes("HEADER|VALUE\r\nx|\u00e9\r\n"));
        candidateStream.Position = 0;
        using DelimitedFileReader candidate = new(new StreamReader(candidateStream, Encoding.UTF8, true), '|');
        Assert.Equal(new[] { "HEADER", "VALUE" }, candidate.ReadFields());
        Assert.Equal(new[] { "x", "\u00e9" }, candidate.ReadFields());
    }

    [Fact]
    public void CsvHelperDefaultsRecoverMalformedQuotesAndRejectUnicodeOptions()
    {
        using CsvReader recovering = CsvReader.Create(new StringReader("\"value\"junk|x\r\n"),
            new CsvReaderOptions { Separator = '|', HasHeader = false });
        Assert.True(recovering.Read());
        Assert.Equal("valuejunk", recovering.GetString(0));
        Assert.Throws<ArgumentException>(() => CsvReader.Create(new StringReader(""),
            new CsvReaderOptions { Separator = '\u00a7', HasHeader = false }));
        Assert.Throws<ArgumentException>(() => CsvReader.Create(new StringReader(""),
            new CsvReaderOptions { Separator = '|', HasHeader = false, WhitespaceCharacters = " \u00a0" }));
    }

    [Fact]
    public void ParserSupportsLegacySeparatorsAndUnicodeWhitespace()
    {
        IEnumerable<char> delimiters = Enumerable.Range(0, 128).Select(value => (char)value)
            .Where(value => value != '\r' && value != '\n' && value != '"')
            .Concat(new[] { '\u00a7', '\u00a0', '\u2003', '\ue000', '\u03b1' });
        foreach (char delimiter in delimiters)
        {
            string text = $"left{delimiter}\"x{delimiter}y\"{delimiter}\r\n";
            string[][] legacy = ReadLegacy(new StringReader(text), delimiter);
            using DelimitedFileReader candidate = new(new StringReader(text), delimiter);
            Assert.Equal(Assert.Single(legacy), candidate.ReadFields());
            Assert.False(candidate.Read());
        }

        foreach (char whitespace in Enumerable.Range(0, char.MaxValue + 1)
            .Select(value => (char)value).Where(char.IsWhiteSpace).Where(value => value != '\r' && value != '\n'))
        {
            string text = $"{whitespace}\"{whitespace}value{whitespace}\"{whitespace}|x\r\n";
            string[][] legacy = ReadLegacy(new StringReader(text), '|');
            using DelimitedFileReader candidate = new(new StringReader(text), '|');
            Assert.Equal(Assert.Single(legacy), candidate.ReadFields());
        }
    }

    [Fact]
    public void FieldsOwnTheirValuesAcrossReadsAndBufferGrowth()
    {
        string largeValue = new('x', 200_000);
        string text = $"first|\"{largeValue}\"\r\nsecond|\"other\"\r\n";
        using DelimitedFileReader parser = new(new StringReader(text), '|');
        string[] first = parser.ReadFields();
        Assert.Equal(new[] { "second", "other" }, parser.ReadFields());
        Assert.False(parser.Read());
        Assert.Equal(new[] { "first", largeValue }, first);
    }

    [Theory]
    [InlineData("Antibiotics.txt")]
    [InlineData("Breakpoints.txt")]
    [InlineData("ExpectedResistancePhenotypes.txt")]
    [InlineData("ExpertInterpretationRules.txt")]
    [InlineData("Organisms.txt")]
    [InlineData("QC_Ranges.txt")]
    [InlineData("SampleUserDefinedBreakpoints.txt")]
    public void EveryResourceFieldMatchesLegacyParser(string name)
    {
        string path = Path.Combine(Constants.SystemRootPath, "Resources", name);
        using TextFieldParser legacy = new(path, Encoding.UTF8);
        legacy.SetDelimiters("\t");
        using DelimitedFileReader candidate = new(path, '\t');
        while (!legacy.EndOfData)
            Assert.Equal(legacy.ReadFields(), candidate.ReadFields());
        Assert.False(candidate.Read());
    }

    [Fact]
    public void MalformedLineNumbersIncludeSkippedPhysicalLines()
    {
        const string text = "\r\n \t\nheader|value\r\n\r\n\"one\n\t\n two\"junk|x\r\n";
        using TextFieldParser legacy = new(new StringReader(text));
        legacy.SetDelimiters("|");
        legacy.ReadFields();
        MalformedLineException expected = Assert.Throws<MalformedLineException>(() => legacy.ReadFields());
        using DelimitedFileReader candidate = new(new StringReader(text), '|');
        candidate.ReadFields();
        MalformedLineException actual = Assert.Throws<MalformedLineException>(() => candidate.ReadFields());
        Assert.Equal(expected.LineNumber, actual.LineNumber);
    }

    [Fact]
    public void DeterministicSyntaxCombinationsMatchLegacyAcceptanceAndFields()
    {
        char[] alphabet = ['a', 'b', '|', '"', ' ', '\t', '\r', '\n', '\u00a0', '\u200b', '\ufeff', '\u202f', '\u205f', '\ue000', '\u001f'];
        Random random = new(3400);
        List<string> differences = [];
        for (int sample = 0; sample < 5000; sample++)
        {
            string text = new(Enumerable.Range(0, random.Next(40))
                .Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray());
            string[][]? legacy = null;
            string[][]? candidate = null;
            try
            {
                legacy = ReadLegacy(new StringReader(text), '|');
            }
            catch (MalformedLineException)
            {
            }
            using (DelimitedFileReader parser = new(new StringReader(text), '|'))
            {
                try
                {
                    List<string[]> rows = [];
                    while (parser.Read())
                        rows.Add(parser.Fields);
                    candidate = rows.ToArray();
                }
                catch (MalformedLineException)
                {
                }
            }
            string description = System.Text.Json.JsonSerializer.Serialize(text);
            bool matches = (legacy is null) == (candidate is null);
            if (legacy is not null && candidate is not null)
            {
                matches = legacy.Length == candidate.Length && legacy.Zip(candidate)
                    .All(pair => pair.First.SequenceEqual(pair.Second, StringComparer.Ordinal));
            }
            if (!matches && differences.Count < 20)
                differences.Add(description + " legacy=" + System.Text.Json.JsonSerializer.Serialize(legacy)
                    + " candidate=" + System.Text.Json.JsonSerializer.Serialize(candidate));
        }
        Assert.True(differences.Count == 0, string.Join(Environment.NewLine, differences));
    }

    private static string[][] ReadLegacy(TextReader reader, char delimiter)
    {
        using TextFieldParser parser = new(reader);
        parser.SetDelimiters(delimiter.ToString());
        parser.HasFieldsEnclosedInQuotes = true;
        List<string[]> rows = [];
        while (!parser.EndOfData)
            rows.Add(parser.ReadFields()!);
        return rows.ToArray();
    }
}
