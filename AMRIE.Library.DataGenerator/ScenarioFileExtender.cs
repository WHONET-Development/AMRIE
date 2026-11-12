using AMRIE.Library;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AMRIE.Library.DataGenerator;

internal sealed record ScenarioProbe(string Value, string ProbeKind)
{
    public int KindIndex { get; set; } = -1;
}

internal sealed record ScenarioField(int ColumnIndex, string TestField, ScenarioProbe[] Probes);

internal sealed record ScenarioProfile(string OrganismCode, ScenarioField[] Fields);

internal sealed record ScenarioExtensionSummary(
    long BaseFileBytes,
    long ActualFileBytes,
    long BytesOverTarget,
    long RowsAdded,
    long MeasurementsAdded,
    long MaximumRowBytes,
    SortedDictionary<string, long> ProbeKindCounts,
    long ReferenceRowsValidated,
    long ReferenceOutputCount,
    SortedDictionary<string, long> ReferenceOutcomeCounts)
{
    public static ScenarioExtensionSummary Empty(long fileBytes, long targetBytes) => new(
        fileBytes,
        fileBytes,
        Math.Max(0, fileBytes - targetBytes),
        0,
        0,
        0,
        new SortedDictionary<string, long>(StringComparer.Ordinal),
        0,
        0,
        new SortedDictionary<string, long>(StringComparer.Ordinal));
}

internal static class ScenarioFileExtender
{
    public const string AlgorithmVersion = "scenario-cycle-v1";
    private const int StreamBufferSize = 1024 * 1024;
    private const int GuidelineYear = 2026;
    private const long ProgressIntervalBytes = 1024L * 1024L * 1024L;
    private const string LineEnding = "\r\n";

    public static ScenarioExtensionSummary Append(
        string inputFile,
        IReadOnlyList<string> headers,
        IReadOnlyList<ScenarioProfile> profiles,
        long targetBytes,
        long firstRowIndex)
    {
        if (targetBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetBytes), "Target size must be a positive number of bytes.");

        long baseFileBytes = new FileInfo(inputFile).Length;
        if (baseFileBytes >= targetBytes)
            return ScenarioExtensionSummary.Empty(baseFileBytes, targetBytes);
        if (profiles.Count == 0)
            throw new InvalidDataException("Cannot extend the input file without organism scenario profiles.");

        int rowIndexColumn = FindColumn(headers, "ROW_IDX");
        int organismColumn = FindColumn(headers, Constants.KeyFields.ORGANISM);
        if (rowIndexColumn == organismColumn)
            throw new InvalidDataException("ROW_IDX and ORGANISM must occupy different input columns.");

        if (profiles.Select(profile => profile.OrganismCode).Distinct(StringComparer.Ordinal).Count() != profiles.Count)
            throw new InvalidDataException("Scenario profiles must have unique organism codes.");

        string[] probeKindNames = [.. profiles
            .SelectMany(profile => profile.Fields)
            .SelectMany(field => field.Probes)
            .Select(probe => probe.ProbeKind)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(kind => kind, StringComparer.Ordinal)];
        Dictionary<string, int> probeKindIndices = probeKindNames
            .Select((kind, index) => (kind, index))
            .ToDictionary(item => item.kind, item => item.index, StringComparer.Ordinal);
        foreach (ScenarioProbe probe in profiles.SelectMany(profile => profile.Fields).SelectMany(field => field.Probes))
            probe.KindIndex = probeKindIndices[probe.ProbeKind];

        ScenarioLayout[] layouts = [.. profiles.Select(profile => CreateLayout(profile, headers, rowIndexColumn, organismColumn))];

        string[] pipeRuns = [.. Enumerable.Range(0, headers.Count + 1).Select(length => new string('|', length))];
        List<string> headerList = [.. headers];
        StringBuilder line = new(Math.Max(headers.Count + 32, 1024));
        long[] probeKindCounts = new long[probeKindNames.Length];
        SortedDictionary<string, long> referenceOutcomeCounts = new(StringComparer.Ordinal);
        long referenceRowsValidated = 0;
        long referenceOutputCount = 0;
        long rowsAdded = 0;
        long measurementsAdded = 0;
        long maximumRowBytes = 0;
        long outputBytes = baseFileBytes;
        long nextProgressBytes = ProgressIntervalBytes;

        using FileStream outputStream = new(
            inputFile,
            FileMode.Append,
            FileAccess.Write,
            FileShare.None,
            StreamBufferSize,
            FileOptions.SequentialScan);
        using StreamWriter writer = new(outputStream, new UTF8Encoding(false), StreamBufferSize, leaveOpen: true)
        {
            NewLine = LineEnding
        };

        while (outputBytes < targetBytes)
        {
            long profileRowOrdinal = rowsAdded / layouts.Length;
            ScenarioLayout layout = layouts[(int)(rowsAdded % layouts.Length)];
            long rowIndex = checked(firstRowIndex + rowsAdded);
            string rowIdentifier = rowIndex.ToString(CultureInfo.InvariantCulture);
            Dictionary<string, string>? sampleValues = profileRowOrdinal == 0
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : null;
            int previousColumn = -1;
            line.Clear();

            foreach (ScenarioCell cell in layout.Cells)
            {
                int separatorCount = previousColumn < 0
                    ? cell.ColumnIndex
                    : cell.ColumnIndex - previousColumn;
                line.Append(pipeRuns[separatorCount]);

                switch (cell.Kind)
                {
                    case ScenarioCellKind.RowIndex:
                        line.Append(rowIdentifier);
                        sampleValues?.Add("ROW_IDX", rowIdentifier);
                        break;
                    case ScenarioCellKind.Organism:
                        line.Append(layout.OrganismCode);
                        sampleValues?.Add(Constants.KeyFields.ORGANISM, layout.OrganismCode);
                        break;
                    case ScenarioCellKind.Measurement:
                        ScenarioField field = cell.Field!;
                        int probeIndex = (int)(((profileRowOrdinal % field.Probes.Length)
                            + (cell.FieldOrdinal % field.Probes.Length)) % field.Probes.Length);
                        ScenarioProbe probe = field.Probes[probeIndex];
                        line.Append(probe.Value);
                        probeKindCounts[probe.KindIndex] = checked(probeKindCounts[probe.KindIndex] + 1);
                        sampleValues?.Add(field.TestField, probe.Value);
                        break;
                    default:
                        throw new InvalidDataException($"Unsupported scenario cell kind '{cell.Kind}'.");
                }

                previousColumn = cell.ColumnIndex;
            }

            line.Append(pipeRuns[headers.Count - previousColumn - 1]);
            long rowBytes = checked(line.Length + LineEnding.Length);
            maximumRowBytes = Math.Max(maximumRowBytes, rowBytes);
            writer.Write(line);
            writer.WriteLine();
            outputBytes = checked(outputBytes + rowBytes);
            rowsAdded = checked(rowsAdded + 1);
            measurementsAdded = checked(measurementsAdded + layout.Fields.Length);
            if (outputBytes >= nextProgressBytes)
            {
                Console.WriteLine($"Size expansion: {outputBytes:N0} / {targetBytes:N0} bytes.");
                nextProgressBytes = checked(nextProgressBytes + ProgressIntervalBytes);
            }

            if (sampleValues is not null)
            {
                IsolateInterpretation isolate = new(
                    sampleValues,
                    headerList,
                    [],
                    [],
                    guidelineYear: GuidelineYear,
                    prioritizedBreakpointTypes:
                    [
                        Breakpoint.BreakpointTypes.Human,
                        Breakpoint.BreakpointTypes.Animal,
                        Breakpoint.BreakpointTypes.ECOFF
                    ],
                    prioritizedSitesOfInfection: [.. Constants.SitesOfInfection.DefaultOrder],
                    roundHalfDilutions: true,
                    useIntrinsicResistanceRules: false);
                Dictionary<string, string> interpretations = isolate.GetAllInterpretations();
                if (interpretations.Count != layout.Fields.Length)
                    throw new InvalidDataException($"The engine omitted a field in scaled sample row '{rowIdentifier}'.");

                referenceRowsValidated++;
                foreach (string result in interpretations.Values)
                {
                    string outcome = string.IsNullOrEmpty(result) ? "(blank)" : result;
                    referenceOutcomeCounts[outcome] = referenceOutcomeCounts.GetValueOrDefault(outcome) + 1;
                    referenceOutputCount++;
                }
            }
        }

        writer.Flush();
        if (outputStream.Length != outputBytes)
            throw new InvalidDataException($"Scaled file byte count mismatch: tracked {outputBytes}, actual {outputStream.Length}.");

        SortedDictionary<string, long> scenarioCounts = new(StringComparer.Ordinal);
        for (int index = 0; index < probeKindNames.Length; index++)
            if (probeKindCounts[index] > 0)
                scenarioCounts.Add(probeKindNames[index], probeKindCounts[index]);

        return new ScenarioExtensionSummary(
            baseFileBytes,
            outputBytes,
            Math.Max(0, outputBytes - targetBytes),
            rowsAdded,
            measurementsAdded,
            maximumRowBytes,
            scenarioCounts,
            referenceRowsValidated,
            referenceOutputCount,
            referenceOutcomeCounts);
    }

    private static ScenarioLayout CreateLayout(
        ScenarioProfile profile,
        IReadOnlyList<string> headers,
        int rowIndexColumn,
        int organismColumn)
    {
        if (string.IsNullOrWhiteSpace(profile.OrganismCode) || profile.Fields.Length == 0)
            throw new InvalidDataException("Every scenario profile must have an organism and at least one test field.");
        EnsurePlainField(profile.OrganismCode, Constants.KeyFields.ORGANISM);

        List<ScenarioCell> cells =
        [
            new(rowIndexColumn, ScenarioCellKind.RowIndex, null, -1),
            new(organismColumn, ScenarioCellKind.Organism, null, -1)
        ];
        ScenarioField[] fields = [.. profile.Fields.OrderBy(field => field.ColumnIndex)];
        HashSet<int> occupiedColumns = [rowIndexColumn, organismColumn];

        for (int fieldOrdinal = 0; fieldOrdinal < fields.Length; fieldOrdinal++)
        {
            ScenarioField field = fields[fieldOrdinal];
            if (field.ColumnIndex < 0 || field.ColumnIndex >= headers.Count
                || headers[field.ColumnIndex] != field.TestField
                || !occupiedColumns.Add(field.ColumnIndex))
                throw new InvalidDataException($"Scenario test field '{field.TestField}' does not map to a unique input column.");
            if (field.Probes.Length == 0)
                throw new InvalidDataException($"Scenario test field '{field.TestField}' has no valid probe values.");

            Match match = IsolateInterpretation.ValidAntibioticFieldNameRegex.Match(field.TestField);
            if (!match.Success || match.Groups[3].Value.Length == 0)
                throw new InvalidDataException($"Invalid scenario test field '{field.TestField}'.");
            string method = Antibiotic.TestMethods.GetTestMethodFromCode(match.Groups[3].Value[0]);
            foreach (ScenarioProbe probe in field.Probes)
            {
                EnsurePlainField(probe.Value, field.TestField);
                decimal numericResult = 0M;
                string modifier = string.Empty;
                if (!InterpretationLibrary.ParseResult(method, probe.Value, ref numericResult, ref modifier))
                    throw new InvalidDataException($"Invalid scenario measurement '{probe.Value}' for '{field.TestField}'.");
            }

            cells.Add(new ScenarioCell(field.ColumnIndex, ScenarioCellKind.Measurement, field, fieldOrdinal));
        }

        cells.Sort((left, right) => left.ColumnIndex.CompareTo(right.ColumnIndex));
        return new ScenarioLayout(profile.OrganismCode, fields, [.. cells]);
    }

    private static int FindColumn(IReadOnlyList<string> headers, string header)
    {
        for (int index = 0; index < headers.Count; index++)
            if (headers[index] == header)
                return index;
        throw new InvalidDataException($"Required input column '{header}' was not found.");
    }

    private static void EnsurePlainField(string value, string field)
    {
        foreach (char character in value)
            if (character > 0x7F || character is '|' or '"' or '\r' or '\n')
                throw new InvalidDataException($"Scenario value for '{field}' must be plain ASCII without delimiters.");
    }

    private enum ScenarioCellKind
    {
        RowIndex,
        Organism,
        Measurement
    }

    private readonly record struct ScenarioCell(int ColumnIndex, ScenarioCellKind Kind, ScenarioField? Field, int FieldOrdinal);
    private sealed record ScenarioLayout(string OrganismCode, ScenarioField[] Fields, ScenarioCell[] Cells);
}
