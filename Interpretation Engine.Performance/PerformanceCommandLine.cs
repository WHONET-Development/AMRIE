using System.Globalization;

namespace Interpretation_Engine.Performance;

internal enum PerformanceMode
{
    EngineFile,
    BoundedStream
}

internal sealed record PerformanceOptions(
    string InputFile,
    PerformanceMode Mode,
    string ConfigFile,
    string? OutputFile,
    string? ReportFile,
    string? ManifestFile,
    char Delimiter,
    int BatchSize,
    int? MaxRows,
    int Runs,
    long MaxWholeFileBytes,
    string? MemoryReportFile = null);

internal static class PerformanceCommandLine
{
    private const int DefaultBatchSize = 1024;
    private const int DefaultWholeFileLimitMegabytes = 128;

    public const string Usage = """
        Usage: Interpretation Engine.Performance --input <path> [options]

        Options:
          --mode <stream|file>          Bounded streaming (default) or the engine's whole-file API.
          --config <path>               Engine configuration JSON; defaults to Resources\SampleConfig.json.
          --manifest <path>             Generator manifest for input hash/size metadata.
          --output <path>               Write interpreted tab-delimited output. Stream mode discards output if omitted.
          --report <path>               Write the JSON measurement report without overwriting an existing file.
          --memory-report <path>        Stream 250-ms unforced memory/cache samples as JSONL; file mode, one pass only.
          --delimiter <char>            Input delimiter (default: |).
          --batch-size <count>          Rows retained per bounded batch (default: 1024; maximum: 10000).
          --max-rows <count>            Limit stream mode to a prefix; cannot be used with --mode file.
          --runs <count>                Number of passes (default: 1).
          --max-whole-file-mb <count>   Safety limit for --mode file (default: 128 MiB).
          --help                        Show this help.

        Examples:
          dotnet run --project "Interpretation Engine.Performance" -- --input "C:\\data\\base.txt" --mode file --manifest "C:\\data\\manifest.json"
          dotnet run --project "Interpretation Engine.Performance" -- --input "C:\\data\\50gb.txt" --mode stream --batch-size 1024 --manifest "C:\\data\\manifest.json"
        """;

    public static bool TryParse(string[] args, out PerformanceOptions? options, out string error)
    {
        options = null;
        error = string.Empty;

        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < args.Length; index++)
        {
            string name = args[index];
            if (!name.StartsWith("--", StringComparison.Ordinal))
            {
                error = $"Unexpected argument '{name}'.{Environment.NewLine}{Usage}";
                return false;
            }

            if (name.Equals("--help", StringComparison.OrdinalIgnoreCase))
            {
                error = Usage;
                return false;
            }

            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                error = $"Option '{name}' requires a value.{Environment.NewLine}{Usage}";
                return false;
            }

            if (!values.TryAdd(name, args[++index]))
            {
                error = $"Option '{name}' was specified more than once.";
                return false;
            }
        }

        if (!values.TryGetValue("--input", out string? inputFile) || string.IsNullOrWhiteSpace(inputFile))
        {
            error = $"An input file is required.{Environment.NewLine}{Usage}";
            return false;
        }

        PerformanceMode mode = PerformanceMode.BoundedStream;
        if (values.TryGetValue("--mode", out string? modeValue))
        {
            if (modeValue.Equals("stream", StringComparison.OrdinalIgnoreCase))
                mode = PerformanceMode.BoundedStream;
            else if (modeValue.Equals("file", StringComparison.OrdinalIgnoreCase))
                mode = PerformanceMode.EngineFile;
            else
            {
                error = "Mode must be 'stream' or 'file'.";
                return false;
            }
        }

        if (mode == PerformanceMode.EngineFile && values.ContainsKey("--max-rows"))
        {
            error = "--max-rows is available only in bounded stream mode.";
            return false;
        }

        if (mode == PerformanceMode.BoundedStream && values.ContainsKey("--max-whole-file-mb"))
        {
            error = "--max-whole-file-mb is available only in file mode.";
            return false;
        }

        if (!TryPositiveInteger(values, "--batch-size", DefaultBatchSize, 10_000, out int batchSize, out error)
            || !TryPositiveInteger(values, "--runs", 1, 100, out int runs, out error))
            return false;

        int? maxRows = null;
        if (values.TryGetValue("--max-rows", out string? maxRowsValue))
        {
            if (!TryPositiveInteger(maxRowsValue, "--max-rows", int.MaxValue, out int parsedMaxRows, out error))
                return false;
            maxRows = parsedMaxRows;
        }

        if (!TryPositiveInteger(values, "--max-whole-file-mb", DefaultWholeFileLimitMegabytes, 1_048_576,
                out int wholeFileLimitMegabytes, out error))
            return false;

        string delimiterValue = values.GetValueOrDefault("--delimiter", "|");
        if (delimiterValue.Length != 1)
        {
            error = "--delimiter must be exactly one character.";
            return false;
        }

        string inputPath = Path.GetFullPath(inputFile);
        string configPath = Path.GetFullPath(values.GetValueOrDefault(
            "--config", Path.Combine(AppContext.BaseDirectory, "Resources", "SampleConfig.json")));
        string? outputPath = GetOptionalFullPath(values, "--output");
        string? reportPath = GetOptionalFullPath(values, "--report");
        string? manifestPath = GetOptionalFullPath(values, "--manifest");
        string? memoryReportPath = GetOptionalFullPath(values, "--memory-report");
        if (memoryReportPath is not null && (mode != PerformanceMode.EngineFile || runs != 1))
        {
            error = "--memory-report requires --mode file with --runs 1.";
            return false;
        }

        options = new PerformanceOptions(
            inputPath,
            mode,
            configPath,
            outputPath,
            reportPath,
            manifestPath,
            delimiterValue[0],
            batchSize,
            maxRows,
            runs,
            (long)wholeFileLimitMegabytes * 1024 * 1024,
            memoryReportPath);
        return true;
    }

    private static string? GetOptionalFullPath(Dictionary<string, string> values, string name) =>
        values.TryGetValue(name, out string? value) ? Path.GetFullPath(value) : null;

    private static bool TryPositiveInteger(
        Dictionary<string, string> values,
        string name,
        int defaultValue,
        int maximum,
        out int parsedValue,
        out string error)
    {
        if (!values.TryGetValue(name, out string? value))
        {
            parsedValue = defaultValue;
            error = string.Empty;
            return true;
        }

        return TryPositiveInteger(value, name, maximum, out parsedValue, out error);
    }

    private static bool TryPositiveInteger(
        string value,
        string name,
        int maximum,
        out int parsedValue,
        out string error)
    {
        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out parsedValue)
            && parsedValue > 0
            && parsedValue <= maximum)
        {
            error = string.Empty;
            return true;
        }

        error = $"Option '{name}' must be an integer from 1 through {maximum.ToString(CultureInfo.InvariantCulture)}.";
        return false;
    }
}
