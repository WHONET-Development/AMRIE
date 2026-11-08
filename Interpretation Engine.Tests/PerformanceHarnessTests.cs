using AMR_Engine;
using Interpretation_Engine.Performance;
using System.ComponentModel;
using System.Text;

namespace Interpretation_Engine.Tests;

public sealed class PerformanceHarnessTests
{
    [Fact]
    public void CommandLineDefaultsToBoundedStreamAndRejectsUnsafeRowLimitInFileMode()
    {
        Assert.True(PerformanceCommandLine.TryParse(["--input", "input.txt"], out PerformanceOptions? options, out string error), error);
        Assert.NotNull(options);
        Assert.Equal(PerformanceMode.BoundedStream, options.Mode);
        Assert.Equal(1024, options.BatchSize);
        Assert.Equal(128L * 1024 * 1024, options.MaxWholeFileBytes);

        Assert.False(PerformanceCommandLine.TryParse(
            ["--input", "input.txt", "--mode", "file", "--max-rows", "10"],
            out _,
            out error));
        Assert.Contains("only in bounded stream mode", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WholeFileModeRefusesAnInputAboveItsConfiguredSafetyLimit()
    {
        using TemporaryDirectory directory = new();
        string inputFile = Path.Combine(directory.Path, "input.txt");
        string configFile = Path.Combine(Constants.SystemRootPath, "Resources", "SampleConfig.json");
        File.WriteAllText(inputFile, "ROW_IDX|ORGANISM\r\n0|spn\r\n", new UTF8Encoding(false));

        PerformanceOptions options = new(
            inputFile,
            PerformanceMode.EngineFile,
            configFile,
            null,
            null,
            null,
            '|',
            1024,
            null,
            1,
            1);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => PerformanceRunner.Run(options));
        Assert.Contains("Use --mode stream", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BoundedStreamRejectsVerticalOutputConfiguration()
    {
        using TemporaryDirectory directory = new();
        string inputFile = Path.Combine(directory.Path, "input.txt");
        string configFile = Path.Combine(directory.Path, "vertical-config.json");
        File.WriteAllText(inputFile, "ROW_IDX|ORGANISM|AMP_NM\r\n0|spn|4\r\n", new UTF8Encoding(false));
        File.WriteAllText(configFile, "{\"GuidelineYear\":2026,\"HorizontalAntibioticResults\":false}", new UTF8Encoding(false));

        PerformanceOptions options = new(
            inputFile,
            PerformanceMode.BoundedStream,
            configFile,
            null,
            null,
            null,
            '|',
            1,
            null,
            1,
            128L * 1024 * 1024);

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() => PerformanceRunner.Run(options));
        Assert.Contains("horizontal antibiotic results", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BoundedStreamOutputMatchesTheEngineFileApiOnSmallInput()
    {
        using TemporaryDirectory directory = new();
        string inputFile = Path.Combine(directory.Path, "input.txt");
        string configFile = Path.Combine(Constants.SystemRootPath, "Resources", "SampleConfig.json");
        string engineOutput = Path.Combine(directory.Path, "engine-output.txt");
        string streamOutput = Path.Combine(directory.Path, "stream-output.txt");
        File.WriteAllText(inputFile,
            "ROW_IDX|ORGANISM|AMP_NM|AMC_NM\r\n0|spn|4|2\r\n1|eco||1\r\n",
            new UTF8Encoding(false));

        FileInterpretationParameters arguments = new(inputFile, '|', -1, configFile, engineOutput, null);
        IO_Library.InterpretDataFile(null, new DoWorkEventArgs(arguments));

        PerformanceOptions options = new(
            inputFile,
            PerformanceMode.BoundedStream,
            configFile,
            streamOutput,
            null,
            null,
            '|',
            1,
            null,
            1,
            128L * 1024 * 1024);
        BaselineReport report = PerformanceRunner.Run(options);

        Assert.Equal(File.ReadAllText(engineOutput), File.ReadAllText(streamOutput));
        PassReport pass = Assert.Single(report.Runs);
        Assert.Equal(2, pass.RowsProcessed);
        Assert.Equal(3, pass.MeasurementsProcessed);
        Assert.Equal(2, pass.KeyScanRows);
        Assert.Equal(new FileInfo(streamOutput).Length, pass.OutputBytes);
    }

    [Fact]
    public void WholeFileApiPreservesQuotedAndRepeatedMeasurementValues()
    {
        using TemporaryDirectory directory = new();
        string inputFile = Path.Combine(directory.Path, "input.txt");
        string configFile = Path.Combine(Constants.SystemRootPath, "Resources", "SampleConfig.json");
        string outputFile = Path.Combine(directory.Path, "output.txt");
        File.WriteAllText(inputFile,
            "ROW_IDX|ORGANISM|AMC_NM\r\n0|spn|4\r\n1|spn|\"4\"\r\n2|spn|2\r\n3|spn|8\r\n",
            new UTF8Encoding(false));

        FileInterpretationParameters arguments = new(inputFile, '|', -1, configFile, outputFile, null);
        IO_Library.InterpretDataFile(null, new DoWorkEventArgs(arguments));

        string expectedOutput = string.Join(Environment.NewLine,
        [
            "ROW_IDX\tORGANISM\tAMC_NM\tAMC_NM_INTERP",
            "0\tspn\t4\tI",
            "1\tspn\t4\tI",
            "2\tspn\t2\tS",
            "3\tspn\t8\tR"
        ]) + Environment.NewLine;
        Assert.Equal(expectedOutput, File.ReadAllText(outputFile));
    }

    [Fact]
    public void WholeFileApiPreservesMeasurementsLongerThanCanonicalizationLimit()
    {
        using TemporaryDirectory directory = new();
        string inputFile = Path.Combine(directory.Path, "input.txt");
        string configFile = Path.Combine(Constants.SystemRootPath, "Resources", "SampleConfig.json");
        string outputFile = Path.Combine(directory.Path, "output.txt");
        File.WriteAllText(inputFile,
            "ROW_IDX|ORGANISM|AMC_NM\r\n0|spn|123456789\r\n",
            new UTF8Encoding(false));

        FileInterpretationParameters arguments = new(inputFile, '|', -1, configFile, outputFile, null);
        IO_Library.InterpretDataFile(null, new DoWorkEventArgs(arguments));

        Assert.Contains("0\tspn\t123456789\t", File.ReadAllText(outputFile), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void WholeFileApiPreservesSparseRowsAndExpertComments(bool horizontal, bool comments)
    {
        using TemporaryDirectory directory = new();
        string inputFile = Path.Combine(directory.Path, "input.txt");
        string configFile = Path.Combine(directory.Path, "config.json");
        string outputFile = Path.Combine(directory.Path, "output.txt");
        string multilineNote = "two" + Environment.NewLine + "lines";
        File.WriteAllText(inputFile, string.Join(Environment.NewLine,
        [
            "ROW_IDX|ORGANISM|AMC_NM|FOX_ND30|PEN_NM|X_01_NM|NOTE",
            "0|spn|4||2|4|\" leading|note \"",
            "1|sau|4|10|1||\"two\"\"quotes\"",
            "2||||||",
            "3||4||||\"" + multilineNote + "\"",
            "4|spn|\"   \"||||empty measurements"
        ]) + Environment.NewLine, new UTF8Encoding(false));
        File.WriteAllText(configFile, System.Text.Json.JsonSerializer.Serialize(new
        {
            GuidelineYear = 2026,
            HorizontalAntibioticResults = horizontal,
            IncludeInterpretationComments = comments,
            EnabledExpertInterpretationRules = new[] { "MRS" }
        }));

        IO_Library.InterpretDataFile(null, new DoWorkEventArgs(
            new FileInterpretationParameters(inputFile, '|', -1, configFile, outputFile, null)));

        string resistant = comments ? "R!" : "R";
        string[][] expectedRows = horizontal
            ? [
                ["ROW_IDX", "ORGANISM", "AMC_NM", "AMC_NM_INTERP", "FOX_ND30", "FOX_ND30_INTERP", "PEN_NM", "PEN_NM_INTERP", "X_01_NM", "X_01_NM_INTERP", "NOTE"],
                ["0", "spn", "4", "I", "", "", "2", "S", "4", "", "leading|note"],
                ["1", "sau", "4", resistant, "10", resistant, "1", resistant, "", "", "two\"quotes"],
                ["2", "", "", "", "", "", "", "", "", "", ""],
                ["3", "", "4", "", "", "", "", "", "", "", multilineNote],
                ["4", "spn", "", "", "", "", "", "", "", "", "empty measurements"]
            ]
            : [
                ["ROW_IDX", "ORGANISM", "NOTE", "ANTIBIOTIC_CODE", "ANTIBIOTIC_MEASUREMENT", "ANTIBIOTIC_INTERPRETATION"],
                ["0", "spn", "leading|note", "AMC_NM", "4", "I"],
                ["0", "spn", "leading|note", "PEN_NM", "2", "S"],
                ["0", "spn", "leading|note", "X_01_NM", "4", ""],
                ["1", "sau", "two\"quotes", "AMC_NM", "4", resistant],
                ["1", "sau", "two\"quotes", "FOX_ND30", "10", resistant],
                ["1", "sau", "two\"quotes", "PEN_NM", "1", resistant],
                ["2", "", "", "", "", ""],
                ["3", "", multilineNote, "AMC_NM", "4", ""],
                ["4", "spn", "empty measurements", "", "", ""]
            ];
        string expectedOutput = string.Join(Environment.NewLine,
            expectedRows.Select(row => IO_Library.ToLine(row, '\t'))) + Environment.NewLine;
        Assert.Equal(expectedOutput, File.ReadAllText(outputFile));
    }

    [Fact]
    public void WholeFileApiPreservesRowsAcrossDictionaryGrowthBoundaries()
    {
        using TemporaryDirectory directory = new();
        string inputFile = Path.Combine(directory.Path, "input.txt");
        string configFile = Path.Combine(directory.Path, "config.json");
        string outputFile = Path.Combine(directory.Path, "output.txt");
        int[] fieldCounts = [3, 7, 17, 37, 89, 197, 431];
        string[] antibiotics = Enumerable.Range(1, fieldCounts.Max() - 2).Select(i => $"X_{i}_NM").ToArray();
        string[] headers = ["ROW_IDX", "ORGANISM", .. antibiotics];
        List<string> inputLines = [IO_Library.ToLine(headers, '|')];
        List<string> expectedLines = [IO_Library.ToLine(
            headers.Take(2).Concat(antibiotics.SelectMany(a => new[] { a, a + "_INTERP" })), '\t')];
        for (int rowIndex = 0; rowIndex < fieldCounts.Length; rowIndex++)
        {
            int measurements = fieldCounts[rowIndex] - 2;
            string[] values = [rowIndex.ToString(System.Globalization.CultureInfo.InvariantCulture), "unknown",
                .. antibiotics.Select((_, i) => i < measurements ? "1" : "")];
            inputLines.Add(IO_Library.ToLine(values, '|'));
            expectedLines.Add(IO_Library.ToLine(values.Take(2).Concat(
                antibiotics.SelectMany((_, i) => new[] { i < measurements ? "1" : "", "" })), '\t'));
        }
        File.WriteAllText(inputFile, string.Join(Environment.NewLine, inputLines) + Environment.NewLine, new UTF8Encoding(false));
        File.WriteAllText(configFile, "{\"GuidelineYear\":2026,\"UseIntrinsicResistanceRules\":false,\"EnabledExpertInterpretationRules\":[]}");

        IO_Library.InterpretDataFile(null, new DoWorkEventArgs(
            new FileInterpretationParameters(inputFile, '|', -1, configFile, outputFile, null)));

        Assert.Equal(string.Join(Environment.NewLine, expectedLines) + Environment.NewLine, File.ReadAllText(outputFile));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(17)]
    [InlineData(37)]
    [InlineData(89)]
    [InlineData(197)]
    [InlineData(431)]
    public void ResultDictionaryPreservesContentsAndIdentityAcrossRepeatedCalls(int measurementCount)
    {
        Dictionary<string, string> row = new() { [Constants.KeyFields.ORGANISM] = "unknown" };
        for (int i = 0; i < measurementCount; i++)
            row.Add($"X_{i}_NM", "1");
        KeyValuePair<string, string>[] originalValues = row.ToArray();
        IsolateInterpretation interpretation = new(row, row.Keys.ToList(), [], [],
            guidelineYear: 2026, useIntrinsicResistanceRules: false);

        Dictionary<string, string> results = interpretation.GetAllInterpretations();

        Assert.Equal(measurementCount, results.Count);
        Assert.All(row.Keys.Where(k => k != Constants.KeyFields.ORGANISM), field =>
        {
            Assert.True(results.ContainsKey(field));
            Assert.Equal(string.Empty, results[field]);
        });
        results.Add("CALLER_VALUE", "kept");
        Assert.Same(results, interpretation.GetAllInterpretations());
        Assert.Equal("kept", results["CALLER_VALUE"]);
        Assert.Equal(measurementCount + 1, results.Count);
        Assert.Equal(originalValues, row.ToArray());
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"AMRIE-PerformanceTests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }

    [Fact]
    public void WholeFileApiPreservesHeaderSearchShortRowsAndBlankExtraFields()
    {
        using TemporaryDirectory directory = new();
        string input = Path.Combine(directory.Path, "input.txt");
        string output = Path.Combine(directory.Path, "output.txt");
        string config = Path.Combine(Constants.SystemRootPath, "Resources", "SampleConfig.json");
        File.WriteAllText(input, "\r\n|||\r\nROW_IDX|ORGANISM|AMC_NM\r\n0|spn|4\r\nshort|spn\r\n1|spn|2||\r\n|||\r\n");
        IO_Library.InterpretDataFile(null, new DoWorkEventArgs(new FileInterpretationParameters(input, '|', -1, config, output, null)));
        Assert.Equal("ROW_IDX\tORGANISM\tAMC_NM\tAMC_NM_INTERP" + Environment.NewLine
            + "0\tspn\t4\tI" + Environment.NewLine + "1\tspn\t2\tS" + Environment.NewLine, File.ReadAllText(output));
    }

    [Fact]
    public void WholeFileApiStillRejectsNonblankExtraFields()
    {
        using TemporaryDirectory directory = new();
        string input = Path.Combine(directory.Path, "input.txt");
        string config = Path.Combine(Constants.SystemRootPath, "Resources", "SampleConfig.json");
        File.WriteAllText(input, "ROW_IDX|ORGANISM|AMC_NM\r\n0|spn|4|extra\r\n");
        Assert.Throws<IndexOutOfRangeException>(() => IO_Library.InterpretDataFile(null, new DoWorkEventArgs(
            new FileInterpretationParameters(input, '|', -1, config, Path.Combine(directory.Path, "output.txt"), null))));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \r\n\t\r\n")]
    public void WholeFileApiStillRejectsInputWithoutAnyRecords(string text)
    {
        using TemporaryDirectory directory = new();
        string input = Path.Combine(directory.Path, "input.txt");
        string config = Path.Combine(Constants.SystemRootPath, "Resources", "SampleConfig.json");
        File.WriteAllText(input, text);
        Assert.Throws<ArgumentNullException>(() => IO_Library.InterpretDataFile(null, new DoWorkEventArgs(
            new FileInterpretationParameters(input, '|', -1, config, Path.Combine(directory.Path, "output.txt"), null))));
    }

    [Fact]
    public void WholeFileApiChecksCancellationBeforeReadingMalformedData()
    {
        using TemporaryDirectory directory = new();
        string input = Path.Combine(directory.Path, "input.txt");
        string output = Path.Combine(directory.Path, "output.txt");
        string config = Path.Combine(Constants.SystemRootPath, "Resources", "SampleConfig.json");
        File.WriteAllText(input, "ROW_IDX|ORGANISM|AMC_NM\r\n\"unterminated\r\n");
        using BackgroundWorker worker = new() { WorkerSupportsCancellation = true, WorkerReportsProgress = true };
        worker.CancelAsync();
        DoWorkEventArgs arguments = new(new FileInterpretationParameters(input, '|', -1, config, output, worker));
        IO_Library.InterpretDataFile(null, arguments);
        Assert.True(arguments.Cancel);
        Assert.False(File.Exists(output));
    }
}
