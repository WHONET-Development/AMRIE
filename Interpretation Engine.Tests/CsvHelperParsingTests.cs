using AMR_Engine;
using CsvHelper;
using Microsoft.VisualBasic.FileIO;
using System.Text;

namespace Interpretation_Engine.Tests;

public sealed class CsvHelperParsingTests
{
    private static readonly string[] ResourceNames =
    [
        "Antibiotics.txt",
        "Breakpoints.txt",
        "ExpectedResistancePhenotypes.txt",
        "ExpertInterpretationRules.txt",
        "Organisms.txt",
        "QC_Ranges.txt",
        "SampleUserDefinedBreakpoints.txt",
        "SampleInputFile.txt"
    ];

    public static IEnumerable<object[]> DirectCsvHelperResourceBaselines()
    {
        foreach (string name in ResourceNames)
        {
            yield return [name, false];
            yield return [name, true];
        }
    }

    [Theory]
    [MemberData(nameof(DirectCsvHelperResourceBaselines))]
    public void DirectCsvHelperMatchesLegacyForResources(string name, bool useReaderOptions)
    {
        string path = Path.Combine(Constants.SystemRootPath, "Resources", name);
        using TextFieldParser legacy = new(path, Encoding.UTF8);
        legacy.SetDelimiters("\t");
        using CsvReader candidate = CsvReader.Create(
            new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true),
            CreateCsvOptions('\t', useReaderOptions));

        int record = 0;
        while (!legacy.EndOfData)
        {
            string[] expected = legacy.ReadFields()!;
            record++;
            Assert.True(candidate.Read(),
                $"{name}: CsvHelper ended before record {record} (legacy line {legacy.LineNumber}).");

            string[] actual = new string[candidate.FieldCount];
            for (int index = 0; index < actual.Length; index++)
            {
                actual[index] = candidate.GetString(index);
                if (useReaderOptions)
                    actual[index] = actual[index].Trim();
            }

            Assert.True(expected.SequenceEqual(actual, StringComparer.Ordinal),
                $"{name}: record {record} (legacy line {legacy.LineNumber}) differs. "
                + $"Expected {System.Text.Json.JsonSerializer.Serialize(expected)}, "
                + $"actual {System.Text.Json.JsonSerializer.Serialize(actual)}.");
        }

        Assert.False(candidate.Read(), $"{name}: CsvHelper returned records after legacy EOF.");
    }

    [Theory]
    [InlineData(',')]
    [InlineData('\t')]
    [InlineData(';')]
    [InlineData('|')]
    public void DirectCsvHelperParsesQuotedFieldsWithSupportedDelimiters(char delimiter)
    {
        string text = $"alpha{delimiter}\"two{delimiter}fields\"{delimiter}\"say \"\"hi\"\"\"\r\nshort\r\n";
        using CsvReader parser = CsvReader.Create(
            new StringReader(text), IO_Library.CreateCsvReaderOptions(delimiter));

        Assert.True(parser.Read());
        Assert.Equal(new[] { "alpha", $"two{delimiter}fields", "say \"hi\"" }, IO_Library.GetFields(parser));
        Assert.True(parser.Read());
        Assert.Equal(new[] { "short" }, IO_Library.GetFields(parser));
        Assert.False(parser.Read());
    }

    [Theory]
    [InlineData("utf-8")]
    [InlineData("utf-16")]
    [InlineData("utf-16BE")]
    [InlineData("utf-32")]
    public void DirectCsvHelperDetectsByteOrderMarks(string encodingName)
    {
        Encoding encoding = Encoding.GetEncoding(encodingName);
        using MemoryStream stream = new();
        stream.Write(encoding.GetPreamble());
        stream.Write(encoding.GetBytes("HEADER|VALUE\r\nx|\u00e9\r\n"));
        stream.Position = 0;
        using CsvReader parser = CsvReader.Create(
            new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true),
            IO_Library.CreateCsvReaderOptions('|'));

        Assert.True(parser.Read());
        Assert.Equal(new[] { "HEADER", "VALUE" }, IO_Library.GetFields(parser));
        Assert.True(parser.Read());
        Assert.Equal(new[] { "x", "\u00e9" }, IO_Library.GetFields(parser));
        Assert.False(parser.Read());
    }

    [Fact]
    public void DirectCsvHelperPreservesQuotedMultilineFields()
    {
        using CsvReader parser = CsvReader.Create(
            new StringReader("first|\"line one\r\nline two\"|last\r\n"),
            IO_Library.CreateCsvReaderOptions('|'));

        Assert.True(parser.Read());
        Assert.Equal(new[] { "first", "line one\r\nline two", "last" }, IO_Library.GetFields(parser));
        Assert.False(parser.Read());
    }

    [Theory]
    [InlineData('\t')]
    [InlineData(',')]
    [InlineData(';')]
    [InlineData('|')]
    public void FileInterpretationParametersAcceptsSupportedDelimiters(char delimiter)
    {
        FileInterpretationParameters parameters = new("input.txt", delimiter, -1, "config.json", "output.txt", null);

        Assert.Equal(delimiter, parameters.Delimiter);
    }

    [Theory]
    [InlineData('^')]
    [InlineData(' ')]
    [InlineData('"')]
    [InlineData('\n')]
    [InlineData('\u00a7')]
    public void FileInterpretationParametersRejectsUnsupportedDelimiters(char delimiter)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            new FileInterpretationParameters("input.txt", delimiter, -1, "config.json", "output.txt", null));

        Assert.Equal("delimiter_", exception.ParamName);
        Assert.Contains("Supported delimiters", exception.Message);
    }

    [Theory]
    [InlineData("TAB", '\t')]
    [InlineData(",", ',')]
    [InlineData(";", ';')]
    [InlineData("|", '|')]
    public void CliDelimiterTokenParserAcceptsSupportedValues(string token, char expected)
    {
        Assert.Equal(expected, Constants.Delimiters.ParseFileDelimiterToken(token));
    }

    [Theory]
    [InlineData("")]
    [InlineData("TABX")]
    [InlineData("||")]
    [InlineData("^")]
    public void CliDelimiterTokenParserRejectsUnsupportedValues(string token)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => Constants.Delimiters.ParseFileDelimiterToken(token));

        Assert.Contains("Supported delimiters", exception.Message);
    }

    [Fact]
    public void DirectCsvHelperRejectsMalformedQuotedInputWithLineNumber()
    {
        using CsvReader parser = CsvReader.Create(
            new StringReader("header|value\r\n\r\n\"unterminated|x\r\n"),
            IO_Library.CreateCsvReaderOptions('|'));
        Assert.True(parser.Read());

        CsvFormatException exception = Assert.Throws<CsvFormatException>(() => parser.Read());

        Assert.Equal(3, exception.LineNumber);
    }

    private static CsvReaderOptions CreateCsvOptions(char delimiter, bool useReaderOptions) => useReaderOptions
        ? IO_Library.CreateCsvReaderOptions(delimiter)
        : new CsvReaderOptions { Separator = delimiter, HasHeader = false };
}