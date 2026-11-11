using AMR_Engine;
using CsvHelper;
using System.ComponentModel;
using System.Text;

namespace Interpretation_Engine.Tests;

public sealed class BoundedFileProcessingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompatibilityEntryUsesTheSameBoundsOutputAndCancellation(bool cancelDuringWriting)
    {
        using TestDirectory directory = new();
        string input = WriteSmallInput(directory);
        Action<object?, DoWorkEventArgs>[] entries = [IO_Library.InterpretDataFileBounded, IO_Library.InterpretDataFile];
        foreach (var interpret in entries)
        {
            using BackgroundWorker worker = new() { WorkerSupportsCancellation = true, WorkerReportsProgress = true };
            var arguments = Arguments(directory, input, worker);
            arguments.BatchRowLimit = 1;
            List<FileProcessingProgress> progress = [];
            arguments.ProgressObserver = sample =>
            {
                progress.Add(sample);
                if (cancelDuringWriting && sample.Stage == "writing")
                    worker.CancelAsync();
            };
            File.WriteAllText(arguments.OutputFile, "previous output");
            DoWorkEventArgs work = new(arguments);
            interpret(null, work);
            Assert.Equal(cancelDuringWriting, work.Cancel);
            Assert.Equal(cancelDuringWriting ? "previous output"
                : "ROW_IDX\tORGANISM\tAMC_NM\tAMC_NM_INTERP" + Environment.NewLine
                    + "0\tspn\t4\tI" + Environment.NewLine + "1\tspn\t2\tS" + Environment.NewLine,
                File.ReadAllText(arguments.OutputFile));
            Assert.All(progress, sample => Assert.InRange(sample.BatchRows, 0, 1));
            Assert.Equal(2, progress[^1].RowsProcessed);
            Assert.Equal(2, progress[^1].MeasurementsProcessed);
            AssertNoStagingFiles(directory);
        }
    }

    [Fact]
    public void ResourceHeaderOverloadsPreserveExactNamesAndDuplicateErrors()
    {
        string[] headers = ["WHONET_ORG_CODE", "quoted\tname", ""];
        var expected = IO_Library.GetResourceHeaders(headers);
        var actual = IO_Library.GetResourceHeaders(IO_Library.ToLine(headers, '\t'));
        Assert.Equal(expected.Keys, actual.Keys);
        foreach (var header in expected)
            Assert.Equal(header.Value, actual[header.Key]);
        Assert.Throws<ArgumentException>(() => IO_Library.GetResourceHeaders("same\tsame"));
        Assert.Throws<ArgumentException>(() => IO_Library.GetResourceHeaders(new[] { "same", "same" }));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void LateMeasurementsAndExpertRulesMatchAcrossRowAndCharacterBoundaries(bool horizontal, bool comments)
    {
        using TestDirectory directory = new();
        string input = directory.File("input.txt");
        string config = directory.File("config.json");
        File.WriteAllText(input, "ROW_IDX|ORGANISM|AMC_NM|FOX_ND30|PEN_NM|NOTE\r\n"
            + "0|spn||||early\r\n1|spn|4|||\"two\"\"quotes\"\r\n"
            + "2|sau|4|10|1|\"two\r\nlines\"\r\n3|||||last\r\n", new UTF8Encoding(false));
        File.WriteAllText(config, System.Text.Json.JsonSerializer.Serialize(new
        {
            GuidelineYear = 2026, HorizontalAntibioticResults = horizontal,
            IncludeInterpretationComments = comments, EnabledExpertInterpretationRules = new[] { "MRS" }
        }));
        string resistant = comments ? "R!" : "R";
        string[][] rows = horizontal
            ? [ ["ROW_IDX", "ORGANISM", "AMC_NM", "AMC_NM_INTERP", "FOX_ND30", "FOX_ND30_INTERP", "PEN_NM", "PEN_NM_INTERP", "NOTE"],
                ["0", "spn", "", "", "", "", "", "", "early"],
                ["1", "spn", "4", "I", "", "", "", "", "two\"quotes"],
                ["2", "sau", "4", resistant, "10", resistant, "1", resistant, "two\r\nlines"],
                ["3", "", "", "", "", "", "", "", "last"] ]
            : [ ["ROW_IDX", "ORGANISM", "NOTE", "ANTIBIOTIC_CODE", "ANTIBIOTIC_MEASUREMENT", "ANTIBIOTIC_INTERPRETATION"],
                ["0", "spn", "early", "", "", ""], ["1", "spn", "two\"quotes", "AMC_NM", "4", "I"],
                ["2", "sau", "two\r\nlines", "AMC_NM", "4", resistant],
                ["2", "sau", "two\r\nlines", "FOX_ND30", "10", resistant],
                ["2", "sau", "two\r\nlines", "PEN_NM", "1", resistant], ["3", "", "last", "", "", ""] ];
        string expected = string.Join(Environment.NewLine, rows.Select(row => IO_Library.ToLine(row, '\t'))) + Environment.NewLine;
        foreach ((int rowLimit, long characterLimit) in new[] { (1, 4194304L), (2, 32L), (2048, 10L) })
        {
            string output = directory.File($"output-{rowLimit}-{characterLimit}.txt");
            List<FileProcessingProgress> progress = [];
            var arguments = new FileInterpretationParameters(input, '|', -1, config, output, null)
            {
                BatchRowLimit = rowLimit, BatchCharacterLimit = characterLimit, ProgressObserver = progress.Add
            };
            IO_Library.InterpretDataFileBounded(null, new DoWorkEventArgs(arguments));
            Assert.Equal(expected, File.ReadAllText(output));
            Assert.All(progress, sample => Assert.InRange(sample.BatchRows, 0, rowLimit));
            Assert.All(progress.Where(sample => sample.BatchRows > 1), sample =>
                Assert.InRange(sample.BatchCharacters, 0, characterLimit));
            Assert.Equal(4, progress[^1].RowsProcessed);
            Assert.Equal(4, progress[^1].MeasurementsProcessed);
            AssertNoStagingFiles(directory);
        }
    }

    [Fact]
    public void CancellationDuringOutputWritingKeepsPriorOutput()
    {
        using TestDirectory directory = new();
        using BackgroundWorker worker = new() { WorkerSupportsCancellation = true, WorkerReportsProgress = true };
        var arguments = Arguments(directory, WriteSmallInput(directory), worker);
        File.WriteAllText(arguments.OutputFile, "previous output");
        worker.ProgressChanged += (_, progress) =>
        {
            if (progress.ProgressPercentage >= 89)
                worker.CancelAsync();
        };
        DoWorkEventArgs work = new(arguments);
        IO_Library.InterpretDataFileBounded(null, work);
        Assert.True(work.Cancel);
        Assert.Equal("previous output", File.ReadAllText(arguments.OutputFile));
        AssertNoStagingFiles(directory);
    }

    [Fact]
    public void WindowsLockedDestinationIsNotReplacedOnPublishFailure()
    {
        if (!OperatingSystem.IsWindows())
            return;
        using TestDirectory directory = new();
        var arguments = Arguments(directory, WriteSmallInput(directory));
        File.WriteAllText(arguments.OutputFile, "previous output");
        using FileStream locked = new(arguments.OutputFile, FileMode.Open, FileAccess.Read, FileShare.Read);
        Assert.Throws<UnauthorizedAccessException>(() => IO_Library.InterpretDataFileBounded(null, new DoWorkEventArgs(arguments)));
        Assert.Equal("previous output", File.ReadAllText(arguments.OutputFile));
        AssertNoStagingFiles(directory);
    }

    [Fact]
    public void OversizedRowIsProcessedAloneWithoutTruncation()
    {
        using TestDirectory directory = new();
        string note = new('x', 10000);
        string input = directory.File("input.txt");
        File.WriteAllText(input, "ROW_IDX|ORGANISM|AMC_NM|NOTE\r\n0|spn|4|short\r\n1|spn|2|" + note + "\r\n2|spn|8|short\r\n");
        List<FileProcessingProgress> samples = [];
        var arguments = Arguments(directory, input);
        arguments.BatchRowLimit = 2;
        arguments.BatchCharacterLimit = 32;
        arguments.ProgressObserver = samples.Add;
        IO_Library.InterpretDataFileBounded(null, new DoWorkEventArgs(arguments));
        Assert.Contains("1\tspn\t2\tS\t" + note + Environment.NewLine, File.ReadAllText(arguments.OutputFile));
        var oversized = samples.Where(sample => sample.BatchCharacters > 32).ToArray();
        Assert.NotEmpty(oversized);
        Assert.All(oversized, sample => Assert.Equal(1, sample.BatchRows));
        AssertNoStagingFiles(directory);
    }

    [Theory]
    [InlineData("interpreting")]
    [InlineData("writing")]
    [InlineData("publishing")]
    public void CancellationKeepsExistingOutputAndCleansBothStages(string stage)
    {
        using TestDirectory directory = new();
        string input = WriteSmallInput(directory);
        using BackgroundWorker worker = new() { WorkerSupportsCancellation = true, WorkerReportsProgress = true };
        Assert.False(worker.IsBusy);
        var arguments = Arguments(directory, input, worker);
        File.WriteAllText(arguments.OutputFile, "previous output");
        arguments.BatchRowLimit = 1;
        arguments.ProgressObserver = progress =>
        {
            if (progress.Stage == stage && (stage != "interpreting" || progress.RowsProcessed >= 1))
                worker.CancelAsync();
        };
        DoWorkEventArgs work = new(arguments);
        IO_Library.InterpretDataFileBounded(null, work);
        Assert.True(work.Cancel);
        Assert.Equal("previous output", File.ReadAllText(arguments.OutputFile));
        AssertNoStagingFiles(directory);
    }

    [Fact]
    public void LateMalformedRecordKeepsPreviousOutputAndOriginalLineNumber()
    {
        using TestDirectory directory = new();
        string input = WriteSmallInput(directory);
        File.AppendAllText(input, "\"unterminated\r\n");
        var arguments = Arguments(directory, input);
        arguments.BatchRowLimit = 1;
        File.WriteAllText(arguments.OutputFile, "previous output");
        var error = Assert.Throws<CsvFormatException>(() => IO_Library.InterpretDataFileBounded(null, new DoWorkEventArgs(arguments)));
        Assert.Equal(4, error.LineNumber);
        Assert.Equal("previous output", File.ReadAllText(arguments.OutputFile));
        AssertNoStagingFiles(directory);
    }

    [Fact]
    public void StageFailurePropagatesAndPreservesDestination()
    {
        using TestDirectory directory = new();
        var arguments = Arguments(directory, WriteSmallInput(directory));
        File.WriteAllText(arguments.OutputFile, "previous output");
        arguments.ProgressObserver = progress =>
        {
            if (progress.Stage == "writing")
                throw new IOException("test stage failure");
        };
        Assert.Equal("test stage failure", Assert.Throws<IOException>(() =>
            IO_Library.InterpretDataFileBounded(null, new DoWorkEventArgs(arguments))).Message);
        Assert.Equal("previous output", File.ReadAllText(arguments.OutputFile));
        AssertNoStagingFiles(directory);
    }

    [Fact]
    public void InputAndOutputMayAliasAfterInputHasClosed()
    {
        using TestDirectory directory = new();
        string input = WriteSmallInput(directory);
        var arguments = new FileInterpretationParameters(input, '|', -1, SampleConfiguration, input, null)
            { BatchRowLimit = 1 };
        IO_Library.InterpretDataFileBounded(null, new DoWorkEventArgs(arguments));
        Assert.Equal("ROW_IDX\tORGANISM\tAMC_NM\tAMC_NM_INTERP" + Environment.NewLine
            + "0\tspn\t4\tI" + Environment.NewLine + "1\tspn\t2\tS" + Environment.NewLine, File.ReadAllText(input));
        AssertNoStagingFiles(directory);
    }

    [Fact]
    public void ProgressIsMonotonicAndRepeatedJobsPublishIdenticalBytes()
    {
        using TestDirectory directory = new();
        string input = WriteSmallInput(directory);
        using BackgroundWorker worker = new() { WorkerSupportsCancellation = true, WorkerReportsProgress = true };
        List<int> percentages = [];
        worker.ProgressChanged += (_, progress) => percentages.Add(progress.ProgressPercentage);
        var arguments = Arguments(directory, input, worker);
        arguments.BatchRowLimit = 1;
        IO_Library.InterpretDataFileBounded(null, new DoWorkEventArgs(arguments));
        byte[] expected = File.ReadAllBytes(arguments.OutputFile);
        Assert.Equal(0, percentages[0]);
        Assert.Equal(100, percentages[^1]);
        Assert.Equal(percentages.Order(), percentages);
        IO_Library.InterpretDataFileBounded(null, new DoWorkEventArgs(arguments));
        Assert.Equal(expected, File.ReadAllBytes(arguments.OutputFile));
        AssertNoStagingFiles(directory);
    }

    [Fact]
    public async Task ConcurrentJobsDoNotShareStagingFilesOrRowState()
    {
        using TestDirectory directory = new();
        string input = WriteSmallInput(directory);
        var arguments = Enumerable.Range(0, 4).Select(index => new FileInterpretationParameters(
            input, '|', -1, SampleConfiguration, directory.File($"output-{index}.txt"), null)
            { BatchRowLimit = 1 }).ToArray();
        await Task.WhenAll(arguments.Select(argument => Task.Run(() => IO_Library.InterpretDataFileBounded(null, new DoWorkEventArgs(argument)))));
        byte[] expected = File.ReadAllBytes(arguments[0].OutputFile);
        Assert.All(arguments, argument => Assert.Equal(expected, File.ReadAllBytes(argument.OutputFile)));
        AssertNoStagingFiles(directory);
    }

    [Fact]
    public void CollidingInputColumnsKeepInputPrecedenceAndExactLateHeaderInsertion()
    {
        using TestDirectory directory = new();
        string input = directory.File("input.txt");
        File.WriteAllText(input, "ROW_IDX|ORGANISM|FOX_ND30|FOX_ND30_INTERP|AMC_NM|AMC_NM_INTERP\r\n"
            + "0||10||4|existing\r\n1|unknown|10|preserved|4|own\r\n");
        var arguments = Arguments(directory, input);
        arguments.BatchRowLimit = 1;
        IO_Library.InterpretDataFileBounded(null, new DoWorkEventArgs(arguments));
        Assert.Equal("ROW_IDX\tORGANISM\tFOX_ND30\tFOX_ND30_INTERP\tFOX_ND30_INTERP_INTERP\tFOX_ND30_INTERP\tAMC_NM\tAMC_NM_INTERP\tAMC_NM_INTERP" + Environment.NewLine
            + "0\t\t10\t\t\t\t4\texisting\texisting" + Environment.NewLine
            + "1\tunknown\t10\tpreserved\t\tpreserved\t4\town\town" + Environment.NewLine,
            File.ReadAllText(arguments.OutputFile));
        AssertNoStagingFiles(directory);
    }

    [Fact]
    public void RowStageCleanupFailureCannotPublishOverPreviousOutput()
    {
        if (!OperatingSystem.IsWindows())
            return;
        using TestDirectory directory = new();
        var arguments = Arguments(directory, WriteSmallInput(directory));
        File.WriteAllText(arguments.OutputFile, "previous output");
        string? rowsPath = null;
        arguments.ProgressObserver = progress =>
        {
            if (progress.Stage == "writing")
            {
                rowsPath = Assert.Single(Directory.GetFiles(directory.Path, ".amrie-*.rows.tmp"));
                File.SetAttributes(rowsPath, FileAttributes.ReadOnly);
            }
        };
        try
        {
            Assert.Throws<UnauthorizedAccessException>(() => IO_Library.InterpretDataFileBounded(null, new DoWorkEventArgs(arguments)));
            Assert.Equal("previous output", File.ReadAllText(arguments.OutputFile));
            Assert.Empty(Directory.GetFiles(directory.Path, ".amrie-*.output.tmp"));
        }
        finally
        {
            if (rowsPath is not null && File.Exists(rowsPath))
                File.SetAttributes(rowsPath, FileAttributes.Normal);
        }
    }

    private static string SampleConfiguration => Path.Combine(Constants.SystemRootPath, "Resources", "SampleConfig.json");
    private static FileInterpretationParameters Arguments(TestDirectory directory, string input, BackgroundWorker? worker = null) =>
        new(input, '|', -1, SampleConfiguration, directory.File("output.txt"), worker);
    private static string WriteSmallInput(TestDirectory directory)
    {
        string input = directory.File("input.txt");
        File.WriteAllText(input, "ROW_IDX|ORGANISM|AMC_NM\r\n0|spn|4\r\n1|spn|2\r\n", new UTF8Encoding(false));
        return input;
    }
    private static void AssertNoStagingFiles(TestDirectory directory) => Assert.Empty(Directory.GetFiles(directory.Path, ".amrie-*.tmp"));

    private sealed class TestDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"AMRIE-BoundedTests-{Guid.NewGuid():N}");
        public TestDirectory() => Directory.CreateDirectory(Path);
        public string File(string name) => System.IO.Path.Combine(Path, name);
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
