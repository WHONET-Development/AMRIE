using AMR_Engine;
using Interpretation_Engine.Performance;
using System.Text.Json;

namespace Interpretation_Engine.Tests;

public sealed class FileMemorySamplerTests
{
    [Fact]
    public void SamplingIsOptInAndRequiresOneFileModePass()
    {
        Assert.True(PerformanceCommandLine.TryParse(["--input", "input.txt"], out var defaults, out string error), error);
        Assert.Null(defaults!.MemoryReportFile);
        Assert.False(PerformanceCommandLine.TryParse(["--input", "input.txt", "--memory-report", "trace.jsonl"], out _, out error));
        Assert.Contains("--mode file", error);
        Assert.False(PerformanceCommandLine.TryParse(["--input", "input.txt", "--mode", "file", "--runs", "2", "--memory-report", "trace.jsonl"], out _, out error));
        Assert.Contains("--runs 1", error);
    }

    [Fact]
    public void InstrumentedFileRunReportsActualCountsAndStreamsTypedMemorySamples()
    {
        string directory = Path.Combine(Path.GetTempPath(), "AMRIE-MemoryTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string input = Path.Combine(directory, "input.txt");
            string trace = Path.Combine(directory, "trace.jsonl");
            File.WriteAllText(input, "ROW_IDX|ORGANISM|AMC_NM\r\n0|spn|4\r\n1|spn|2\r\n");
            PerformanceOptions options = new(input, PerformanceMode.EngineFile,
                Path.Combine(Constants.SystemRootPath, "Resources", "SampleConfig.json"),
                null, null, null, '|', 1024, null, 1, 128L * 1024 * 1024, trace);
            PassReport pass = Assert.Single(PerformanceRunner.Run(options).Runs);
            Assert.Equal(trace, pass.MemorySamplesFile);
            Assert.NotNull(pass.FileProcessing);
            Assert.Equal(2, pass.FileProcessing.ActualRows);
            Assert.Equal(2, pass.FileProcessing.ActualMeasurements);
            Assert.Equal(2, pass.FileProcessing.MaxBatchRows);
            Assert.Equal(10, pass.FileProcessing.MaxBatchCharacters);
            Assert.True(pass.FileProcessing.MemorySamples >= 1);
            string[] lines = File.ReadAllLines(trace);
            Assert.Equal(pass.FileProcessing.MemorySamples, lines.LongLength);
            foreach (string line in lines)
            {
                using JsonDocument sample = JsonDocument.Parse(line);
                Assert.True(sample.RootElement.GetProperty("WorkingSetBytes").GetInt64() > 0);
                Assert.True(sample.RootElement.GetProperty("PrivateBytes").GetInt64() > 0);
                Assert.True(sample.RootElement.GetProperty("ManagedBytes").GetInt64() > 0);
                Assert.True(sample.RootElement.GetProperty("RowsProcessed").GetInt64() >= 0);
                Assert.InRange(sample.RootElement.GetProperty("BreakpointCache").GetProperty("Entries").GetInt32(),
                    0, AntibioticSpecificInterpretationRules.MaxBreakpointCacheEntries);
            }
            Assert.Throws<IOException>(() => PerformanceRunner.Run(options));
            Assert.Equal(lines, File.ReadAllLines(trace));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
