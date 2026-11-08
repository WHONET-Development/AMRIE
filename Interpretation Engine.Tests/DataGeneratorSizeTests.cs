using System.Text;
using Microsoft.VisualBasic.FileIO;
using Interpretation_Engine.DataGenerator;

namespace Interpretation_Engine.Tests;

public sealed class DataGeneratorSizeTests
{
    [Theory]
    [InlineData("50MB", 52_428_800L, "50MB")]
    [InlineData("10GB", 10_737_418_240L, "10GB")]
    [InlineData("2gb", 2_147_483_648L, "2GB")]
    public void RequestedSizeUsesBinaryUnits(string text, long expectedBytes, string expectedDisplay)
    {
        Assert.True(RequestedInputSize.TryParse(text, out RequestedInputSize size));
        Assert.Equal(expectedBytes, size.ByteCount);
        Assert.Equal(expectedDisplay, size.DisplayText);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0MB")]
    [InlineData("-1GB")]
    [InlineData("1.5GB")]
    [InlineData("10TB")]
    [InlineData("50MB ")]
    [InlineData("9223372036854775807GB")]
    public void RequestedSizeRejectsInvalidValues(string text)
    {
        Assert.False(RequestedInputSize.TryParse(text, out _));
    }

    [Fact]
    public void CommandLineSupportsNamedSizeAndOutputDirectory()
    {
        string outputDirectory = Path.Combine(Path.GetTempPath(), "amrie-size-tests");

        Assert.True(GeneratorCommandLine.TryParse(
            ["--target-size", "50MB", "--output-directory", outputDirectory],
            out GeneratorOptions? options,
            out string error));

        Assert.Empty(error);
        Assert.NotNull(options);
        Assert.Equal(Path.GetFullPath(outputDirectory), options.OutputDirectory);
        Assert.Equal(52_428_800L, options.TargetSize?.ByteCount);
    }

    [Fact]
    public void ScenarioExtensionIsDeterministicDenseAndStopsAtRowBoundary()
    {
        using TemporaryDirectory directory = new();
        string firstPath = Path.Combine(directory.Path, "first.txt");
        string secondPath = Path.Combine(directory.Path, "second.txt");
        string[] headers = ["ROW_IDX", "ORGANISM", "AMC_NM", "AMP_NM", "OXA_ND1"];
        string[] baseLines = [string.Join('|', headers), "0|spn|||\r\n"];
        File.WriteAllText(firstPath, string.Join("\r\n", baseLines), new UTF8Encoding(false));
        File.WriteAllText(secondPath, string.Join("\r\n", baseLines), new UTF8Encoding(false));

        ScenarioProfile[] profiles =
        [
            new("spn",
            [
                new ScenarioField(2, "AMC_NM", [new ScenarioProbe("2", "AT"), new ScenarioProbe("4", "ABOVE")]),
                new ScenarioField(3, "AMP_NM", [new ScenarioProbe("1", "BELOW"), new ScenarioProbe("2", "AT")])
            ]),
            new("psd",
            [
                new ScenarioField(4, "OXA_ND1", [new ScenarioProbe("17", "BELOW"), new ScenarioProbe("18", "AT")])
            ])
        ];
        long baseBytes = new FileInfo(firstPath).Length;
        long targetBytes = baseBytes + 1_000;

        ScenarioExtensionSummary first = ScenarioFileExtender.Append(firstPath, headers, profiles, targetBytes, 1);
        ScenarioExtensionSummary second = ScenarioFileExtender.Append(secondPath, headers, profiles, targetBytes, 1);

        Assert.True(first.RowsAdded > profiles.Length);
        Assert.True(first.ActualFileBytes >= targetBytes);
        Assert.InRange(first.BytesOverTarget, 0, first.MaximumRowBytes - 1);
        Assert.Equal(first.RowsAdded, second.RowsAdded);
        Assert.Equal(first.MeasurementsAdded, second.MeasurementsAdded);
        Assert.Equal(first.ProbeKindCounts, second.ProbeKindCounts);
        Assert.Equal(File.ReadAllBytes(firstPath), File.ReadAllBytes(secondPath));
        Assert.Equal(3, first.ReferenceOutputCount);

        using TextFieldParser parser = new(firstPath, Encoding.UTF8)
        {
            HasFieldsEnclosedInQuotes = true
        };
        parser.SetDelimiters("|");
        Assert.Equal(headers, parser.ReadFields());
        HashSet<string> rowIds = new(StringComparer.Ordinal);
        while (!parser.EndOfData)
        {
            string[] row = parser.ReadFields()!;
            Assert.Equal(headers.Length, row.Length);
            Assert.True(rowIds.Add(row[0]));
            if (row[0] == "0")
                continue;

            if (row[1] == "spn")
            {
                Assert.Contains(row[2], new[] { "2", "4" });
                Assert.Contains(row[3], new[] { "1", "2" });
                Assert.Empty(row[4]);
            }
            else
            {
                Assert.Equal("psd", row[1]);
                Assert.Empty(row[2]);
                Assert.Empty(row[3]);
                Assert.Contains(row[4], new[] { "17", "18" });
            }
        }
        Assert.Equal(first.RowsAdded + 1, rowIds.Count);
    }

    [Fact]
    public void ScenarioExtensionDoesNothingWhenBaseAlreadyMeetsTarget()
    {
        using TemporaryDirectory directory = new();
        string inputFile = Path.Combine(directory.Path, "input.txt");
        File.WriteAllText(inputFile, "base\r\n", new UTF8Encoding(false));
        byte[] before = File.ReadAllBytes(inputFile);

        ScenarioExtensionSummary summary = ScenarioFileExtender.Append(
            inputFile,
            ["ROW_IDX", "ORGANISM"],
            [],
            before.Length,
            0);

        Assert.Equal(0, summary.RowsAdded);
        Assert.Equal(0, summary.MeasurementsAdded);
        Assert.Equal(before, File.ReadAllBytes(inputFile));

        ScenarioExtensionSummary belowTarget = ScenarioFileExtender.Append(
            inputFile,
            ["ROW_IDX", "ORGANISM"],
            [],
            before.Length - 1,
            0);

        Assert.Equal(1, belowTarget.BytesOverTarget);
        Assert.Equal(before, File.ReadAllBytes(inputFile));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
