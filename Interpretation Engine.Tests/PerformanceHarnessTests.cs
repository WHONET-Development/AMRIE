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
}
