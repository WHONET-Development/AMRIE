using AMR_Engine;
using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Interpretation_Engine.Performance;

internal static class PerformanceRunner
{
    private const int FileHashBufferSize = 1024 * 1024;
    private const string InterpretationSuffix = "_INTERP";

    public static BaselineReport Run(PerformanceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.MemoryReportFile is not null && (options.Mode != PerformanceMode.EngineFile || options.Runs != 1))
            throw new InvalidOperationException("--memory-report requires --mode file with --runs 1.");
        if (!File.Exists(options.InputFile))
            throw new FileNotFoundException("The input data file was not found.", options.InputFile);
        if (!File.Exists(options.ConfigFile))
            throw new FileNotFoundException("The engine configuration file was not found.", options.ConfigFile);

        FileInfo inputInfo = new(options.InputFile);
        if (inputInfo.Length == 0)
            throw new InvalidDataException("The input data file is empty.");
        if (options.Mode == PerformanceMode.EngineFile && inputInfo.Length > options.MaxWholeFileBytes)
            throw new InvalidOperationException(
                $"Refusing the engine's whole-file API for a {inputInfo.Length:N0}-byte input; " +
                $"the configured safety limit is {options.MaxWholeFileBytes:N0} bytes. Use --mode stream or raise --max-whole-file-mb only when sufficient memory is available.");

        EnsureDistinctOutputPaths(options);
        DatasetMetadata? metadata = options.ManifestFile is null ? null : ReadManifest(options.ManifestFile, inputInfo.Length);
        string? outputFile = options.OutputFile;
        if (options.Mode == PerformanceMode.EngineFile && outputFile is null)
        {
            outputFile = Path.Combine(Path.GetTempPath(), $"amrie-performance-{Guid.NewGuid():N}.txt");
        }

        if (outputFile is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
            using FileStream _ = new(outputFile, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        }

        bool keepOutput = false;
        try
        {
            InputCounts? preflightCounts = null;
            if (options.Mode == PerformanceMode.EngineFile && metadata?.Rows is null)
                preflightCounts = CountInput(options.InputFile, options.Delimiter);

            InterpretationConfiguration? configuration = null;
            double? configurationLoadMilliseconds = null;
            if (options.Mode == PerformanceMode.BoundedStream)
            {
                Stopwatch configurationTimer = Stopwatch.StartNew();
                configuration = InterpretationConfiguration.ReadConfiguration(options.ConfigFile);
                configurationTimer.Stop();
                configurationLoadMilliseconds = configurationTimer.Elapsed.TotalMilliseconds;
                if (!configuration.HorizontalAntibioticResults)
                    throw new NotSupportedException("Bounded stream mode currently requires horizontal antibiotic results.");
            }

            DateTimeOffset startedUtc = DateTimeOffset.UtcNow;
            List<PassReport> runs = new(options.Runs);
            for (int runNumber = 1; runNumber <= options.Runs; runNumber++)
            {
                if (runNumber == 1)
                    AntibioticSpecificInterpretationRules.ClearBreakpoints();

                PassReport pass = options.Mode == PerformanceMode.EngineFile
                    ? RunEngineFile(options, runNumber, outputFile!, metadata, preflightCounts)
                    : RunBoundedStream(options, runNumber, outputFile, configuration!);
                runs.Add(pass);
            }

            keepOutput = options.OutputFile is not null;
            long? outputBytes = outputFile is not null && File.Exists(outputFile)
                ? new FileInfo(outputFile).Length
                : null;
            string? outputSha256 = outputFile is not null && File.Exists(outputFile)
                ? ComputeSha256(outputFile)
                : null;

            return new BaselineReport(
                options.Mode == PerformanceMode.EngineFile ? "engine-file" : "bounded-stream",
                options.Mode == PerformanceMode.EngineFile
                    ? "IO_Library.InterpretDataFileBounded"
                    : "TextFieldParser + IO_Library public per-isolate interpretation API",
                options.InputFile,
                inputInfo.Length,
                metadata?.Sha256,
                options.ManifestFile,
                options.ConfigFile,
                configurationLoadMilliseconds,
                options.Delimiter.ToString(),
                options.BatchSize,
                options.MaxRows,
                options.MaxWholeFileBytes,
                options.Runs,
                options.OutputFile,
                options.OutputFile is not null
                    ? "file"
                    : options.Mode == PerformanceMode.EngineFile ? "temporary-file-deleted" : "discard",
                startedUtc,
                GetEnvironmentReport(),
                runs);
        }
        finally
        {
            if (!keepOutput && outputFile is not null && File.Exists(outputFile))
                File.Delete(outputFile);
        }
    }

    private static PassReport RunEngineFile(
        PerformanceOptions options,
        int runNumber,
        string outputFile,
        DatasetMetadata? metadata,
        InputCounts? preflightCounts)
    {
        Process process = Process.GetCurrentProcess();
        long allocatedBefore = GC.GetTotalAllocatedBytes(true);
        Stopwatch totalTimer = Stopwatch.StartNew();

        FileInterpretationParameters arguments = new(
            options.InputFile,
            options.Delimiter,
            -1,
            options.ConfigFile,
            outputFile,
            null);
        using FileMemorySampler? sampler = options.MemoryReportFile is null
            ? null : new(options.MemoryReportFile, arguments.BatchCharacterLimit);
        if (sampler is not null)
            arguments.ProgressObserver = sampler.Observe;
        IO_Library.InterpretDataFileBounded(null, new DoWorkEventArgs(arguments));
        sampler?.Dispose();

        totalTimer.Stop();
        long allocatedBytes = GC.GetTotalAllocatedBytes(false) - allocatedBefore;
        long? outputBytes = File.Exists(outputFile) ? new FileInfo(outputFile).Length : null;
        string? outputSha256 = File.Exists(outputFile) ? ComputeSha256(outputFile) : null;
        long rows = metadata?.Rows ?? preflightCounts?.Rows ?? 0;
        long measurements = metadata?.Measurements ?? preflightCounts?.Measurements ?? 0;

        return new PassReport(
            runNumber,
            runNumber == 1 ? "first-run-cache-cleared" : "warm-cache-retained",
            null,
            null,
            null,
            null,
            null,
            totalTimer.Elapsed.TotalMilliseconds,
            rows,
            measurements,
            CalculateRate(rows, totalTimer.Elapsed.TotalSeconds),
            CalculateRate(measurements, totalTimer.Elapsed.TotalSeconds),
            allocatedBytes,
            process.PeakWorkingSet64,
            outputBytes,
            outputSha256,
            null,
            null)
        {
            MemorySamplesFile = options.MemoryReportFile,
            FileProcessing = sampler?.Report
        };
    }

    private static PassReport RunBoundedStream(
        PerformanceOptions options,
        int runNumber,
        string? outputFile,
        InterpretationConfiguration configuration)
    {
        Process process = Process.GetCurrentProcess();
        long allocatedBefore = GC.GetTotalAllocatedBytes(true);
        Stopwatch totalTimer = Stopwatch.StartNew();

        Stopwatch keyScanTimer = Stopwatch.StartNew();
        KeyScanResult scan = CollectInterpretationKeys(options.InputFile, options.Delimiter, options.MaxRows);
        keyScanTimer.Stop();

        Stopwatch preheatTimer = Stopwatch.StartNew();
        if (scan.Keys.Count > 0)
        {
            AntibioticSpecificInterpretationRules.PreheatBreakpointCache(
                configuration.UserDefinedBreakpoints ?? [],
                Convert.ToInt32(configuration.GuidelineYear, CultureInfo.InvariantCulture),
                configuration.PrioritizedBreakpointTypes,
                configuration.PrioritizedSitesOfInfection,
                scan.Keys);
        }
        preheatTimer.Stop();

        double inputParseMilliseconds = 0;
        double interpretationMilliseconds = 0;
        double outputMilliseconds = 0;
        long rowsProcessed = 0;
        long measurementsProcessed = 0;
        string[] headers;
        List<string> columnNames = [];
        List<string> outputHeaders = [];
        List<Dictionary<string, string>> batch = new(options.BatchSize);

        StreamWriter? outputWriter = null;
        TextWriter writer = TextWriter.Null;
        if (outputFile is not null)
        {
            outputWriter = new StreamWriter(new FileStream(outputFile, FileMode.Create, FileAccess.Write, FileShare.None), new UTF8Encoding(false));
            writer = outputWriter;
        }

        try
        {
            using TextFieldParser parser = OpenParser(options.InputFile, options.Delimiter);
            headers = ReadHeader(parser);
            columnNames = headers.ToList();
            outputHeaders = BuildOutputHeaders(headers, scan.ActiveFields);
            int organismColumn = Array.IndexOf(headers, Constants.KeyFields.ORGANISM);
            if (organismColumn < 0)
                throw new InvalidDataException($"Input header does not contain '{Constants.KeyFields.ORGANISM}'.");

            Stopwatch outputTimer = Stopwatch.StartNew();
            writer.WriteLine(IO_Library.ToLine(outputHeaders, Constants.Delimiters.TabChar));
            outputTimer.Stop();
            outputMilliseconds += outputTimer.Elapsed.TotalMilliseconds;

            while (!parser.EndOfData && (!options.MaxRows.HasValue || rowsProcessed < options.MaxRows.Value))
            {
                Stopwatch parseTimer = Stopwatch.StartNew();
                string[]? values = parser.ReadFields();
                if (values is null || values.All(string.IsNullOrWhiteSpace))
                {
                    parseTimer.Stop();
                    inputParseMilliseconds += parseTimer.Elapsed.TotalMilliseconds;
                    continue;
                }

                if (values.Length != headers.Length)
                    throw new InvalidDataException(
                        $"Input row {rowsProcessed + 2:N0} has {values.Length} fields; expected {headers.Length}.");

                Dictionary<string, string> row = new();
                for (int column = 0; column < values.Length; column++)
                    if (!string.IsNullOrWhiteSpace(values[column]))
                        row.Add(headers[column], values[column]);

                long rowMeasurementCount = row.Keys.LongCount(field => IsolateInterpretation.ValidAntibioticFieldNameRegex.IsMatch(field));
                measurementsProcessed += rowMeasurementCount;
                rowsProcessed++;
                batch.Add(row);
                parseTimer.Stop();
                inputParseMilliseconds += parseTimer.Elapsed.TotalMilliseconds;

                if (batch.Count >= options.BatchSize)
                    FlushBatch(batch, columnNames, outputHeaders, configuration, writer,
                        ref interpretationMilliseconds, ref outputMilliseconds);
            }

            if (batch.Count > 0)
                FlushBatch(batch, columnNames, outputHeaders, configuration, writer,
                    ref interpretationMilliseconds, ref outputMilliseconds);

            Stopwatch flushTimer = Stopwatch.StartNew();
            writer.Flush();
            flushTimer.Stop();
            outputMilliseconds += flushTimer.Elapsed.TotalMilliseconds;
        }
        finally
        {
            outputWriter?.Dispose();
        }

        totalTimer.Stop();
        long allocatedBytes = GC.GetTotalAllocatedBytes(false) - allocatedBefore;
        long? outputBytes = outputFile is not null && File.Exists(outputFile) ? new FileInfo(outputFile).Length : null;
        string? outputSha256 = outputFile is not null && File.Exists(outputFile) ? ComputeSha256(outputFile) : null;

        return new PassReport(
            runNumber,
            runNumber == 1 ? "first-run-cache-cleared" : "warm-cache-retained",
            keyScanTimer.Elapsed.TotalMilliseconds,
            inputParseMilliseconds,
            preheatTimer.Elapsed.TotalMilliseconds,
            interpretationMilliseconds,
            outputMilliseconds,
            totalTimer.Elapsed.TotalMilliseconds,
            rowsProcessed,
            measurementsProcessed,
            CalculateRate(rowsProcessed, totalTimer.Elapsed.TotalSeconds),
            CalculateRate(measurementsProcessed, totalTimer.Elapsed.TotalSeconds),
            allocatedBytes,
            process.PeakWorkingSet64,
            outputBytes,
            outputSha256,
            scan.Keys.Count,
            scan.RowsScanned);
    }

    private static void FlushBatch(
        List<Dictionary<string, string>> batch,
        List<string> columnNames,
        List<string> outputHeaders,
        InterpretationConfiguration configuration,
        TextWriter writer,
        ref double interpretationMilliseconds,
        ref double outputMilliseconds)
    {
        Dictionary<string, string>[] results = new Dictionary<string, string>[batch.Count];
        int guidelineYear = Convert.ToInt32(configuration.GuidelineYear, CultureInfo.InvariantCulture);
        Stopwatch interpretationTimer = Stopwatch.StartNew();
        Parallel.For(0, batch.Count, rowIndex =>
        {
            results[rowIndex] = new IsolateInterpretation(
                batch[rowIndex],
                columnNames,
                configuration.EnabledExpertInterpretationRules,
                configuration.UserDefinedBreakpoints ?? [],
                guidelineYear,
                configuration.PrioritizedBreakpointTypes,
                configuration.PrioritizedSitesOfInfection,
                configuration.RoundHalfDilutions,
                configuration.UseIntrinsicResistanceRules).GetAllInterpretations();
        });
        interpretationTimer.Stop();
        interpretationMilliseconds += interpretationTimer.Elapsed.TotalMilliseconds;

        Stopwatch outputTimer = Stopwatch.StartNew();
        for (int rowIndex = 0; rowIndex < batch.Count; rowIndex++)
        {
            Dictionary<string, string> row = batch[rowIndex];
            Dictionary<string, string> rowResults = results[rowIndex];
            List<string> outputValues = new(outputHeaders.Count);
            foreach (string outputHeader in outputHeaders)
            {
                if (outputHeader.EndsWith(InterpretationSuffix, StringComparison.Ordinal))
                {
                    string field = outputHeader[..^InterpretationSuffix.Length];
                    if (rowResults.TryGetValue(field, out string? interpretation))
                    {
                        if (!configuration.IncludeInterpretationComments)
                            interpretation = IsolateInterpretation.RemoveComments(interpretation);
                        outputValues.Add(interpretation);
                    }
                    else
                        outputValues.Add(string.Empty);
                }
                else
                    outputValues.Add(row.GetValueOrDefault(outputHeader, string.Empty));
            }

            writer.WriteLine(IO_Library.ToLine(outputValues, Constants.Delimiters.TabChar));
        }
        outputTimer.Stop();
        outputMilliseconds += outputTimer.Elapsed.TotalMilliseconds;

        batch.Clear();
    }

    private static KeyScanResult CollectInterpretationKeys(string inputFile, char delimiter, int? maxRows)
    {
        using TextFieldParser parser = OpenParser(inputFile, delimiter);
        string[] headers = ReadHeader(parser);
        int organismColumn = Array.IndexOf(headers, Constants.KeyFields.ORGANISM);
        if (organismColumn < 0)
            throw new InvalidDataException($"Input header does not contain '{Constants.KeyFields.ORGANISM}'.");

        int[] antibioticColumns = headers
            .Select((header, index) => (header, index))
            .Where(item => IsolateInterpretation.ValidAntibioticFieldNameRegex.IsMatch(item.header))
            .Select(item => item.index)
            .ToArray();
        HashSet<(string Organism, string Guideline, string Field)> keys = new();
        HashSet<string> activeFieldSet = new(StringComparer.Ordinal);
        List<string> activeFields = new();
        int rowsScanned = 0;
        while (!parser.EndOfData && (!maxRows.HasValue || rowsScanned < maxRows.Value))
        {
            string[]? values = parser.ReadFields();
            if (values is null || values.All(string.IsNullOrWhiteSpace))
                continue;
            if (values.Length != headers.Length)
                throw new InvalidDataException(
                    $"Input row {rowsScanned + 2:N0} has {values.Length} fields; expected {headers.Length}.");

            rowsScanned++;
            string organism = values[organismColumn].Trim();
            foreach (int column in antibioticColumns)
            {
                if (string.IsNullOrWhiteSpace(values[column]))
                    continue;
                string field = headers[column];
                if (activeFieldSet.Add(field))
                    activeFields.Add(field);
                if (organism.Length > 0)
                    keys.Add((organism, GetGuideline(field), field));
            }
        }

        List<Tuple<string, string, string>> result = keys
            .Select(key => new Tuple<string, string, string>(key.Organism, key.Guideline, key.Field))
            .ToList();
        return new KeyScanResult(rowsScanned, result, activeFields);
    }

    private static string GetGuideline(string antibioticField)
    {
        string[] parts = antibioticField.Split(Constants.Delimiters.Underscore);
        int guidelinePartIndex = parts[0].Equals("X", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
        if (parts.Length <= guidelinePartIndex || parts[guidelinePartIndex].Length == 0)
            throw new InvalidDataException($"Unable to determine the guideline for antibiotic field '{antibioticField}'.");
        return Antibiotic.GuidelineNames.GetGuidelineFromCode(parts[guidelinePartIndex][0]);
    }

    private static List<string> BuildOutputHeaders(string[] headers, IReadOnlyCollection<string> activeFields)
    {
        List<string> outputHeaders = new(headers.Length * 2);
        HashSet<string> activeFieldSet = new(activeFields, StringComparer.Ordinal);
        foreach (string header in headers)
        {
            outputHeaders.Add(header);
            if (activeFieldSet.Contains(header))
                outputHeaders.Add(header + InterpretationSuffix);
        }
        return outputHeaders;
    }

    private static InputCounts CountInput(string inputFile, char delimiter)
    {
        using TextFieldParser parser = OpenParser(inputFile, delimiter);
        string[] headers = ReadHeader(parser);
        int[] antibioticColumns = headers
            .Select((header, index) => (header, index))
            .Where(item => IsolateInterpretation.ValidAntibioticFieldNameRegex.IsMatch(item.header))
            .Select(item => item.index)
            .ToArray();

        long rows = 0;
        long measurements = 0;
        while (!parser.EndOfData)
        {
            string[]? values = parser.ReadFields();
            if (values is null || values.All(string.IsNullOrWhiteSpace))
                continue;
            if (values.Length != headers.Length)
                throw new InvalidDataException(
                    $"Input row {rows + 2:N0} has {values.Length} fields; expected {headers.Length}.");
            rows++;
            measurements += antibioticColumns.LongCount(column => !string.IsNullOrWhiteSpace(values[column]));
        }

        return new InputCounts(rows, measurements);
    }

    private static TextFieldParser OpenParser(string inputFile, char delimiter)
    {
        TextFieldParser parser = new(inputFile)
        {
            HasFieldsEnclosedInQuotes = true
        };
        parser.SetDelimiters(delimiter.ToString());
        return parser;
    }

    private static string[] ReadHeader(TextFieldParser parser)
    {
        while (!parser.EndOfData)
        {
            string[]? header = parser.ReadFields();
            if (header is not null && header.Any(field => !string.IsNullOrWhiteSpace(field)))
                return header;
        }

        throw new InvalidDataException("Input file does not contain a header row.");
    }

    private static DatasetMetadata ReadManifest(string manifestFile, long inputLength)
    {
        if (!File.Exists(manifestFile))
            throw new FileNotFoundException("The input manifest was not found.", manifestFile);

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestFile));
        JsonElement root = document.RootElement;
        long? manifestLength = GetInt64(root, "InputFileBytes");
        if (manifestLength.HasValue && manifestLength.Value != inputLength)
            throw new InvalidDataException(
                $"Manifest input size ({manifestLength.Value:N0}) does not match the data file ({inputLength:N0}).");

        return new DatasetMetadata(
            GetInt64(root, "IsolateRows"),
            GetInt64(root, "TotalMeasurementCount"),
            GetString(root, "InputSha256"));
    }

    private static long? GetInt64(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement value) && value.TryGetInt64(out long result)
            ? result
            : null;

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static void EnsureDistinctOutputPaths(PerformanceOptions options)
    {
        if (options.OutputFile is not null && File.Exists(options.OutputFile))
            throw new IOException($"Refusing to overwrite existing output '{options.OutputFile}'.");
        if (options.ReportFile is not null && File.Exists(options.ReportFile))
            throw new IOException($"Refusing to overwrite existing report '{options.ReportFile}'.");
        if (options.MemoryReportFile is not null && File.Exists(options.MemoryReportFile))
            throw new IOException($"Refusing to overwrite existing memory report '{options.MemoryReportFile}'.");

        StringComparer comparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        string[] paths = new[]
        {
            options.InputFile,
            options.ConfigFile,
            options.ManifestFile,
            options.OutputFile,
            options.ReportFile,
            options.MemoryReportFile
        }.Where(path => path is not null).Select(path => Path.GetFullPath(path!)).ToArray();
        if (paths.Distinct(comparer).Count() != paths.Length)
            throw new IOException("Input, output, and report paths must be different files.");
    }

    private static string ComputeSha256(string path)
    {
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileHashBufferSize, FileOptions.SequentialScan);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static EnvironmentReport GetEnvironmentReport()
    {
        string? cpuName = null;
        if (OperatingSystem.IsWindows())
        {
            using RegistryKey? processorKey = Registry.LocalMachine.OpenSubKey(
                @"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            cpuName = processorKey?.GetValue("ProcessorNameString") as string;
        }

        return new EnvironmentReport(
            RuntimeInformation.OSDescription,
            RuntimeInformation.FrameworkDescription,
#if DEBUG
            "Debug",
#else
            "Release",
#endif
            RuntimeInformation.ProcessArchitecture.ToString(),
            Environment.ProcessorCount,
            GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
            cpuName ?? Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"));
    }

    private static double CalculateRate(long count, double seconds) =>
        seconds <= 0 ? 0 : count / seconds;

    private sealed record KeyScanResult(int RowsScanned, List<Tuple<string, string, string>> Keys, List<string> ActiveFields);
    private sealed record InputCounts(long Rows, long Measurements);
    private sealed record DatasetMetadata(long? Rows, long? Measurements, string? Sha256);
}

internal sealed record BaselineReport(
    string Mode,
    string Api,
    string InputFile,
    long InputFileBytes,
    string? InputSha256,
    string? ManifestFile,
    string ConfigurationFile,
    double? ConfigurationLoadMilliseconds,
    string InputDelimiter,
    int BatchSize,
    int? MaxRows,
    long WholeFileSafetyLimitBytes,
    int RequestedRuns,
    string? OutputFile,
    string OutputSink,
    DateTimeOffset StartedUtc,
    EnvironmentReport Environment,
    IReadOnlyList<PassReport> Runs);

internal sealed record EnvironmentReport(
    string OperatingSystem,
    string Runtime,
    string BuildConfiguration,
    string ProcessArchitecture,
    int LogicalProcessorCount,
    long AvailableMemoryBytes,
    string? CpuName);

internal sealed record PassReport(
    int RunNumber,
    string CacheState,
    double? KeyScanMilliseconds,
    double? InputParseMilliseconds,
    double? BreakpointPreheatMilliseconds,
    double? InterpretationMilliseconds,
    double? OutputMilliseconds,
    double TotalMilliseconds,
    long RowsProcessed,
    long MeasurementsProcessed,
    double RowsPerSecond,
    double MeasurementsPerSecond,
    long AllocatedBytes,
    long PeakWorkingSetBytes,
    long? OutputBytes,
    string? OutputSha256,
    int? DistinctInterpretationKeys,
    int? KeyScanRows)
{
    public string? MemorySamplesFile { get; init; }
    public FileProcessingReport? FileProcessing { get; init; }
}
