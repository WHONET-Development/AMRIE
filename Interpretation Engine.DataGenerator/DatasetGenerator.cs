using AMR_Engine;
using Microsoft.VisualBasic.FileIO;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Interpretation_Engine.DataGenerator;

internal sealed class DatasetGenerator
{
    private const int GuidelineYear = 2026;
    private const decimal ProbeStep = 0.0001M;
    private const char PipeDelimiter = '|';
    private const long SidecarSpaceReserveBytes = 256L * 1024L * 1024L;
    private static readonly HashSet<string> IncludedGuidelines = new(StringComparer.Ordinal)
    {
        Antibiotic.GuidelineNames.CLSI,
        Antibiotic.GuidelineNames.EUCAST
    };
    private static readonly IReadOnlyDictionary<string, string> BreakpointAgentCatalogAliases =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CFC"] = "CEC",
            ["FIX"] = "CFM"
        };
    private static readonly string[] BreakpointTypes =
    [
        Breakpoint.BreakpointTypes.Human,
        Breakpoint.BreakpointTypes.Animal,
        Breakpoint.BreakpointTypes.ECOFF
    ];

    public GenerationManifest Generate(string outputDirectory, RequestedInputSize? targetSize = null)
    {
        Directory.CreateDirectory(outputDirectory);
        string inputFile = Path.Combine(outputDirectory, "2026-clsi-eucast-boundary-input.txt");
        bool publishAfterValidation = targetSize.HasValue;
        if (publishAfterValidation && File.Exists(inputFile))
            throw new IOException($"Refusing to overwrite existing generated input '{inputFile}'.");
        if (targetSize is RequestedInputSize requestedSize)
            EnsureFreeSpace(outputDirectory, requestedSize.ByteCount);

        string workingInputFile = publishAfterValidation
            ? inputFile + ".partial-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)
            : inputFile;

        try
        {
            GenerationManifest manifest = GenerateCore(outputDirectory, inputFile, workingInputFile, targetSize);
            if (publishAfterValidation)
                File.Move(workingInputFile, inputFile);

            File.WriteAllText(manifest.ManifestFile,
                JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }),
                new UTF8Encoding(false));
            return manifest;
        }
        finally
        {
            if (publishAfterValidation && File.Exists(workingInputFile))
                File.Delete(workingInputFile);
        }
    }

    private GenerationManifest GenerateCore(
        string outputDirectory,
        string inputFile,
        string workingInputFile,
        RequestedInputSize? targetSize)
    {
        string resourcesDirectory = Path.Combine(Constants.SystemRootPath, "Resources");
        string sampleInputPath = Path.Combine(resourcesDirectory, "SampleInputFile.txt");
        string breakpointResourcePath = Path.Combine(resourcesDirectory, "Breakpoints.txt");

        if (!File.Exists(sampleInputPath) || !File.Exists(breakpointResourcePath))
            throw new FileNotFoundException("The engine resources were not copied beside the generator.", resourcesDirectory);

        List<string> sampleHeaders = ReadHeaders(sampleInputPath, PipeDelimiter);
        if (!sampleHeaders.Contains(Constants.KeyFields.ORGANISM, StringComparer.Ordinal)
            || !sampleHeaders.Contains("ROW_IDX", StringComparer.Ordinal))
            throw new InvalidDataException("SampleInputFile.txt must contain ROW_IDX and ORGANISM columns.");

        List<IndexedBreakpoint> breakpointRows = Breakpoint.Breakpoints
            .Select((breakpoint, index) => new IndexedBreakpoint(breakpoint, index))
            .Where(item => item.Breakpoint.YEAR == GuidelineYear
                && IncludedGuidelines.Contains(item.Breakpoint.GUIDELINES)
                && BreakpointTypes.Contains(item.Breakpoint.BREAKPOINT_TYPE, StringComparer.Ordinal))
            .ToList();
        if (breakpointRows.Count == 0)
            throw new InvalidDataException($"No {GuidelineYear} CLSI/EUCAST breakpoints were loaded.");

        Organism[] organisms = Organism.AllOrganisms
            .GroupBy(organism => organism.WHONET_ORG_CODE, StringComparer.Ordinal)
            .Select(group => group.FirstOrDefault(organism => Organism.CurrentOrganisms.ContainsKey(organism.WHONET_ORG_CODE)) ?? group.First())
            .OrderBy(organism => organism.WHONET_ORG_CODE, StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, Antibiotic[]> antibioticsByCode = Antibiotic.AllAntibiotics
            .GroupBy(antibiotic => antibiotic.WHONET_ABX_CODE, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);

        Dictionary<PairKey, ProbeSet> directCases = new();
        HashSet<ThresholdKey> thresholds = new();
        int breakpointRowOrganismIncidences = 0;
        int expandedBreakpointAgentIncidences = 0;
        int atcPrefixExpansions = 0;

        foreach (IndexedBreakpoint indexedBreakpoint in breakpointRows)
        {
            Breakpoint breakpoint = indexedBreakpoint.Breakpoint;
            if (breakpoint.TEST_METHOD != Antibiotic.TestMethods.Disk
                && breakpoint.TEST_METHOD != Antibiotic.TestMethods.MIC)
                throw new InvalidDataException($"Unsupported test method '{breakpoint.TEST_METHOD}' at breakpoint row {indexedBreakpoint.RowIndex}.");

            ResolvedAgent[] agents = ResolveAgents(breakpoint, antibioticsByCode, out bool wasAtcPrefix);
            if (wasAtcPrefix)
                atcPrefixExpansions++;

            foreach (Organism organism in organisms)
            {
                if (!MatchesOrganism(organism, breakpoint.ORGANISM_CODE_TYPE, breakpoint.ORGANISM_CODE))
                    continue;

                breakpointRowOrganismIncidences++;
                foreach (ResolvedAgent agent in agents)
                {
                    expandedBreakpointAgentIncidences++;
                    PairKey key = new(breakpoint.GUIDELINES, organism.WHONET_ORG_CODE, agent.TestField);
                    if (!directCases.TryGetValue(key, out ProbeSet? probeSet))
                    {
                        probeSet = new ProbeSet(key);
                        directCases.Add(key, probeSet);
                    }

                    AddBreakpointProbes(probeSet, breakpoint, indexedBreakpoint.RowIndex, key, thresholds);
                }
            }
        }

        if (directCases.Count == 0)
            throw new InvalidDataException("No organism/test pairs matched the 2026 breakpoint resources.");

        foreach ((PairKey key, ProbeSet probeSet) in directCases)
        {
            if (probeSet.Probes.Count == 0)
                AddProbe(probeSet, key.TestField, Antibiotic.TestMethods.GetTestMethodFromCode(GetTestMethodCode(key.TestField)),
                    "20", new ProbeOrigin(-1, "", "NO_NUMERIC_CUTOFF", null));
        }

        int directMeasurementCount = directCases.Sum(pair => pair.Value.Probes.Count);
        Dictionary<PairKey, ProbeSet> allCases = new(directCases);
        HashSet<string> etestFields = new(StringComparer.Ordinal);
        int etestAliasPairCount = 0;

        foreach ((PairKey key, ProbeSet directProbeSet) in directCases.ToArray())
        {
            string breakpointAgentCode = GetAntibioticCode(key.TestField);
            string catalogAgentCode = BreakpointAgentCatalogAliases.GetValueOrDefault(breakpointAgentCode, breakpointAgentCode);
            if (GetTestMethodCode(key.TestField) != Antibiotic.TestMethodCodes.MIC
                || !antibioticsByCode.TryGetValue(catalogAgentCode, out Antibiotic[]? catalogEntries))
                continue;

            bool guidelineSupportsAgent = key.Guideline == Antibiotic.GuidelineNames.CLSI
                ? catalogEntries.Any(antibiotic => antibiotic.CLSI)
                : key.Guideline == Antibiotic.GuidelineNames.EUCAST
                    && catalogEntries.Any(antibiotic => antibiotic.EUCAST);
            if (!guidelineSupportsAgent || !catalogEntries.Any(antibiotic => !string.IsNullOrWhiteSpace(antibiotic.LOINCETEST)))
                continue;

            string etestField = key.TestField[..^1] + Antibiotic.TestMethodCodes.ETest;
            PairKey aliasKey = key with { TestField = etestField };
            if (!allCases.TryGetValue(aliasKey, out ProbeSet? etestProbeSet))
            {
                etestProbeSet = new ProbeSet(aliasKey);
                allCases.Add(aliasKey, etestProbeSet);
                etestAliasPairCount++;
            }

            etestFields.Add(etestField);
            foreach ((string value, ProbeOrigin origin) in directProbeSet.Probes)
            {
                string probeKind = origin.ProbeKind == "NO_NUMERIC_CUTOFF"
                    ? "ETEST_ALIAS_NO_NUMERIC_CUTOFF"
                    : "ETEST_ALIAS_" + origin.ProbeKind;
                etestProbeSet.Probes.TryAdd(value, origin with { ProbeKind = probeKind });
            }
        }

        int etestMeasurementCount = allCases
            .Where(pair => pair.Key.TestField.EndsWith(Antibiotic.TestMethodCodes.ETest))
            .Sum(pair => pair.Value.Probes.Count);
        int totalMeasurementCount = allCases.Sum(pair => pair.Value.Probes.Count);
        string[] testFields = allCases.Keys
            .Select(key => key.TestField)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(field => field, StringComparer.Ordinal)
            .ToArray();

        List<string> headers = sampleHeaders.ToList();
        HashSet<string> headerSet = new(headers, StringComparer.Ordinal);
        foreach (string field in testFields)
            if (headerSet.Add(field))
                headers.Add(field);

        string caseMapFile = Path.Combine(outputDirectory, "2026-clsi-eucast-cell-map.tsv.gz");
        ScenarioProfile[] scenarioProfiles = BuildScenarioProfiles(allCases, headers);
        MatrixWriteResult matrixWrite = WriteBoundaryMatrix(workingInputFile, caseMapFile, headers, allCases);
        OutcomeSummary outcomes = InterpretGeneratedMatrix(workingInputFile, headers);
        long baseInputFileBytes = new FileInfo(workingInputFile).Length;
        ScenarioExtensionSummary extension = targetSize is RequestedInputSize requestedSize
            ? ScenarioFileExtender.Append(workingInputFile, headers, scenarioProfiles, requestedSize.ByteCount, matrixWrite.RowCount)
            : ScenarioExtensionSummary.Empty(baseInputFileBytes, targetSize?.ByteCount ?? baseInputFileBytes);

        string intrinsicFile = Path.Combine(outputDirectory, "2026-intrinsic-rule-cases.txt");
        RuleCaseSummary intrinsicRules = GenerateIntrinsicRuleCases(intrinsicFile, sampleHeaders);
        string expertFile = Path.Combine(outputDirectory, "2026-expert-rule-cases.txt");
        string expertBreakpointsFile = Path.Combine(outputDirectory, "2026-expert-rule-user-breakpoints.txt");
        ExpertRuleSummary expertRules = GenerateExpertRuleCases(expertFile, expertBreakpointsFile, sampleHeaders, breakpointResourcePath);

        ValidateOutcomeCoverage(outcomes, intrinsicRules, expertRules);
        ValidateRoundingBoundaries();

        long inputFileBytes = new FileInfo(workingInputFile).Length;
        long caseMapFileBytes = new FileInfo(caseMapFile).Length;
        long intrinsicFileBytes = new FileInfo(intrinsicFile).Length;
        long expertFileBytes = new FileInfo(expertFile).Length;
        long expertBreakpointsFileBytes = new FileInfo(expertBreakpointsFile).Length;
        long projectedAppendBytes = CalculateProjectedAppendBytes(sampleInputPath, sampleHeaders, headers, inputFileBytes);
        long projectedAppendGrowthBytes = projectedAppendBytes - new FileInfo(sampleInputPath).Length;

        GenerationManifest manifest = new()
        {
            ManifestFile = Path.Combine(outputDirectory, "2026-clsi-eucast-manifest.json"),
            InputFile = inputFile,
            InputFileBytes = inputFileBytes,
            InputSha256 = ComputeSha256(workingInputFile),
            BaseInputFileBytes = baseInputFileBytes,
            RequestedSize = targetSize?.DisplayText ?? string.Empty,
            RequestedSizeBytes = targetSize?.ByteCount ?? 0,
            InputSizeOvershootBytes = extension.BytesOverTarget,
            SizeExpansionAlgorithm = extension.RowsAdded > 0 ? ScenarioFileExtender.AlgorithmVersion : string.Empty,
            SizeExpansionRows = extension.RowsAdded,
            SizeExpansionMeasurements = extension.MeasurementsAdded,
            SizeExpansionMaximumRowBytes = extension.MaximumRowBytes,
            SizeExpansionProbeKindCounts = extension.ProbeKindCounts,
            SizeExpansionReferenceRowsValidated = extension.ReferenceRowsValidated,
            SizeExpansionReferenceOutputCount = extension.ReferenceOutputCount,
            SizeExpansionReferenceOutcomeCounts = extension.ReferenceOutcomeCounts,
            CaseMapFile = caseMapFile,
            CaseMapFileBytes = caseMapFileBytes,
            CaseMapSha256 = ComputeSha256(caseMapFile),
            ResourceSha256 = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["Antibiotics.txt"] = ComputeSha256(Path.Combine(resourcesDirectory, "Antibiotics.txt")),
                ["Breakpoints.txt"] = ComputeSha256(breakpointResourcePath),
                ["Organisms.txt"] = ComputeSha256(Path.Combine(resourcesDirectory, "Organisms.txt")),
                ["SampleInputFile.txt"] = ComputeSha256(sampleInputPath)
            },
            GuidelineYear = GuidelineYear,
            BreakpointResourceRows = breakpointRows.Count,
            BreakpointRowOrganismIncidences = breakpointRowOrganismIncidences,
            ExpandedBreakpointAgentIncidences = expandedBreakpointAgentIncidences,
            AtcPrefixBreakpointRows = atcPrefixExpansions,
            BreakpointAgentCatalogAliases = new SortedDictionary<string, string>(
                breakpointRows
                    .Select(item => item.Breakpoint.WHONET_ABX_CODE)
                    .Distinct(StringComparer.Ordinal)
                    .Where(BreakpointAgentCatalogAliases.ContainsKey)
                    .ToDictionary(code => code, code => BreakpointAgentCatalogAliases[code], StringComparer.Ordinal),
                StringComparer.Ordinal),
            UnmappedBreakpointAgentCodes = breakpointRows
                .Select(item => item.Breakpoint.WHONET_ABX_CODE)
                .Distinct(StringComparer.Ordinal)
                .Where(code => !antibioticsByCode.ContainsKey(code)
                    && !BreakpointAgentCatalogAliases.ContainsKey(code)
                    && !Antibiotic.AllAntibiotics.Any(antibiotic => antibiotic.ATC_CODE.StartsWith(code, StringComparison.Ordinal)))
                .OrderBy(code => code, StringComparer.Ordinal)
                .ToArray(),
            OrganismCodes = directCases.Keys.Select(key => key.OrganismCode).Distinct(StringComparer.Ordinal).Count(),
            DirectPairCount = directCases.Count,
            DirectTestFieldCount = directCases.Keys.Select(key => key.TestField).Distinct(StringComparer.Ordinal).Count(),
            DirectMeasurementCount = directMeasurementCount,
            ThresholdInstanceCount = thresholds.Count,
            EtestAliasPairCount = etestAliasPairCount,
            EtestAliasFieldCount = etestFields.Count,
            EtestMeasurementCount = etestMeasurementCount,
            TotalMeasurementCount = totalMeasurementCount + extension.MeasurementsAdded,
            IsolateRows = matrixWrite.RowCount + extension.RowsAdded,
            BaseIsolateRows = matrixWrite.RowCount,
            ColumnCount = headers.Count,
            NewTestFieldCount = headers.Count - sampleHeaders.Count,
            MatrixCellMapRows = matrixWrite.CellMapRows,
            ProjectedAppendBytes = projectedAppendBytes,
            ProjectedAppendGrowthBytes = projectedAppendGrowthBytes,
            ReferenceOutputSha256 = outcomes.OutputSha256,
            ReferenceOutputCount = outcomes.ResultCount,
            ReferenceOutputScope = "comprehensive base matrix",
            OutcomeCounts = outcomes.OutcomeCounts,
            UnmappedHistoricalOrganismCodes = directCases.Keys
                .Select(key => key.OrganismCode)
                .Distinct(StringComparer.Ordinal)
                .Where(code => !Organism.CurrentOrganisms.ContainsKey(code) && !Organism.MergedOrganisms.ContainsKey(code))
                .OrderBy(code => code, StringComparer.Ordinal)
                .ToArray(),
            IntrinsicRuleFile = intrinsicFile,
            IntrinsicRuleFileBytes = intrinsicFileBytes,
            IntrinsicRuleCaseRows = intrinsicRules.RowCount,
            IntrinsicRulePairs = intrinsicRules.PairCount,
            IntrinsicRuleResistantOutputs = intrinsicRules.ResistantOutputCount,
            ExpertRuleFile = expertFile,
            ExpertRuleFileBytes = expertFileBytes,
            ExpertRuleCaseRows = expertRules.RowCount,
            ExpertRulesExercised = expertRules.RulesExercised,
            ExpertRuleResistantOutputs = expertRules.ResistantOutputCount,
            ExpertRuleUserBreakpointsFile = expertBreakpointsFile,
            ExpertRuleUserBreakpointsFileBytes = expertBreakpointsFileBytes,
            ExpertRuleUserBreakpointCount = expertRules.UserBreakpointCount,
            EtestRoundingBoundaryCount = ValidateRoundingBoundaries()
        };

        return manifest;
    }

    private static List<string> ReadHeaders(string filePath, char delimiter)
    {
        using TextFieldParser parser = new(filePath, Encoding.UTF8)
        {
            HasFieldsEnclosedInQuotes = true
        };
        parser.SetDelimiters(delimiter.ToString());
        return parser.ReadFields()?.ToList() ?? throw new InvalidDataException($"No header found in '{filePath}'.");
    }

    private static ScenarioProfile[] BuildScenarioProfiles(
        Dictionary<PairKey, ProbeSet> cases,
        List<string> headers)
    {
        Dictionary<string, int> headerIndices = headers
            .Select((header, index) => (header, index))
            .ToDictionary(item => item.header, item => item.index, StringComparer.Ordinal);

        return cases.Values
            .GroupBy(probeSet => probeSet.Key.OrganismCode, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(organismGroup =>
            {
                ScenarioField[] fields = organismGroup
                    .GroupBy(probeSet => probeSet.Key.TestField, StringComparer.Ordinal)
                    .OrderBy(fieldGroup => headerIndices[fieldGroup.Key])
                    .Select(fieldGroup =>
                    {
                        ScenarioProbe[] probes = fieldGroup
                            .SelectMany(probeSet => probeSet.Probes)
                            .GroupBy(probe => probe.Key, StringComparer.Ordinal)
                            .Select(probeGroup => probeGroup
                                .OrderBy(probe => NormalizeScenarioKind(probe.Value.ProbeKind), StringComparer.Ordinal)
                                .ThenBy(probe => probe.Value.BreakpointRowIndex)
                                .ThenBy(probe => probe.Value.CutoffName, StringComparer.Ordinal)
                                .First())
                            .OrderBy(probe => NormalizeScenarioKind(probe.Value.ProbeKind), StringComparer.Ordinal)
                            .ThenBy(probe => probe.Value.BreakpointRowIndex)
                            .ThenBy(probe => probe.Value.CutoffName, StringComparer.Ordinal)
                            .ThenBy(probe => probe.Key, StringComparer.Ordinal)
                            .Select(probe => new ScenarioProbe(probe.Key, probe.Value.ProbeKind))
                            .ToArray();

                        return new ScenarioField(headerIndices[fieldGroup.Key], fieldGroup.Key, probes);
                    })
                    .ToArray();
                return new ScenarioProfile(organismGroup.Key, fields);
            })
            .ToArray();
    }

    private static string NormalizeScenarioKind(string probeKind)
    {
        const string etestAliasPrefix = "ETEST_ALIAS_";
        return probeKind.StartsWith(etestAliasPrefix, StringComparison.Ordinal)
            ? probeKind[etestAliasPrefix.Length..]
            : probeKind;
    }

    private static ResolvedAgent[] ResolveAgents(
        Breakpoint breakpoint,
        Dictionary<string, Antibiotic[]> antibioticsByCode,
        out bool wasAtcPrefix)
    {
        wasAtcPrefix = false;
        if (antibioticsByCode.ContainsKey(breakpoint.WHONET_ABX_CODE))
        {
            if (string.IsNullOrWhiteSpace(breakpoint.WHONET_TEST))
                throw new InvalidDataException($"Breakpoint for {breakpoint.WHONET_ABX_CODE} has no WHONET_TEST code.");
            return [new ResolvedAgent(breakpoint.WHONET_ABX_CODE, breakpoint.WHONET_TEST)];
        }

        Antibiotic[] classMembers = Antibiotic.AllAntibiotics
            .Where(antibiotic => !string.IsNullOrWhiteSpace(antibiotic.ATC_CODE)
                && antibiotic.ATC_CODE.StartsWith(breakpoint.WHONET_ABX_CODE, StringComparison.Ordinal))
            .ToArray();
        if (classMembers.Length == 0)
        {
            if (IsolateInterpretation.ValidAntibioticFieldNameRegex.IsMatch(breakpoint.WHONET_TEST)
                && GetAntibioticCode(breakpoint.WHONET_TEST) == breakpoint.WHONET_ABX_CODE)
                return [new ResolvedAgent(breakpoint.WHONET_ABX_CODE, breakpoint.WHONET_TEST)];

            throw new InvalidDataException($"Breakpoint agent '{breakpoint.WHONET_ABX_CODE}' is neither a catalog agent, a valid exact test field, nor an ATC prefix.");
        }

        int suffixSeparator = breakpoint.WHONET_TEST.IndexOf('_');
        if (suffixSeparator < 0)
            throw new InvalidDataException($"Cannot expand class-level breakpoint test '{breakpoint.WHONET_TEST}'.");

        wasAtcPrefix = true;
        string suffix = breakpoint.WHONET_TEST[suffixSeparator..];
        return classMembers
            .OrderBy(antibiotic => antibiotic.WHONET_ABX_CODE, StringComparer.Ordinal)
            .Select(antibiotic => new ResolvedAgent(antibiotic.WHONET_ABX_CODE, antibiotic.WHONET_ABX_CODE + suffix))
            .ToArray();
    }

    private static bool MatchesOrganism(Organism organism, string type, string code) => type switch
    {
        nameof(Organism.WHONET_ORG_CODE) => organism.WHONET_ORG_CODE == code,
        nameof(Organism.SEROVAR_GROUP) => organism.SEROVAR_GROUP == code,
        nameof(Organism.SPECIES_GROUP) => organism.SPECIES_GROUP == code,
        nameof(Organism.GENUS_CODE) => organism.GENUS_CODE == code,
        nameof(Organism.GENUS_GROUP) => organism.GENUS_GROUP == code,
        nameof(Organism.FAMILY_CODE) => organism.FAMILY_CODE == code,
        nameof(Organism.ANAEROBE) => organism.ANAEROBE && code == Constants.OrganismGroups.Anaerobes,
        _ when type == Constants.OrganismGroups.AnaerobePlusSubkingdomCode => organism.ANAEROBE
            && code == (organism.SUBKINGDOM_CODE == Constants.TestResultCodes.Positive
                ? Constants.OrganismGroups.GramPositiveAnaerobes
                : Constants.OrganismGroups.GramNegativeAnaerobes),
        "ALL" => code == "ALL",
        _ => false
    };

    private static void AddBreakpointProbes(
        ProbeSet probeSet,
        Breakpoint breakpoint,
        int breakpointRowIndex,
        PairKey key,
        HashSet<ThresholdKey> thresholds)
    {
        bool hasNumericCutoff = false;
        string testMethod = breakpoint.TEST_METHOD;

        if (breakpoint.BREAKPOINT_TYPE == Breakpoint.BreakpointTypes.ECOFF)
        {
            if (breakpoint.ECV_ECOFF > 0M)
            {
                AddThresholdProbes(probeSet, testMethod, key, breakpoint, breakpointRowIndex,
                    "ECV_ECOFF", breakpoint.ECV_ECOFF, thresholds);
                hasNumericCutoff = true;
            }
        }
        else
        {
            if (breakpoint.R > 0M)
            {
                AddThresholdProbes(probeSet, testMethod, key, breakpoint, breakpointRowIndex,
                    "R", breakpoint.R, thresholds);
                hasNumericCutoff = true;
            }

            if (breakpoint.S > 0M)
            {
                AddThresholdProbes(probeSet, testMethod, key, breakpoint, breakpointRowIndex,
                    "S", breakpoint.S, thresholds);
                hasNumericCutoff = true;
            }

            if (breakpoint.R > 0M && breakpoint.S > 0M && breakpoint.R != breakpoint.S)
            {
                decimal midpoint = (breakpoint.R + breakpoint.S) / 2M;
                if (testMethod == Antibiotic.TestMethods.Disk)
                {
                    AddDiskProbe(probeSet, key.TestField, breakpointRowIndex, "INTERVAL", midpoint,
                        (decimal)Math.Floor(midpoint));
                    AddDiskProbe(probeSet, key.TestField, breakpointRowIndex, "INTERVAL", midpoint,
                        (decimal)Math.Ceiling(midpoint));
                }
                else
                    AddProbe(probeSet, key.TestField, testMethod, Format(midpoint),
                        new ProbeOrigin(breakpointRowIndex, "R/S", "INTERVAL", midpoint));
            }
        }

        if (!hasNumericCutoff && probeSet.Probes.Count == 0)
            AddProbe(probeSet, key.TestField, testMethod, "20",
                new ProbeOrigin(breakpointRowIndex, "", "NO_NUMERIC_CUTOFF", null));
    }

    private static void AddThresholdProbes(
        ProbeSet probeSet,
        string testMethod,
        PairKey key,
        Breakpoint breakpoint,
        int breakpointRowIndex,
        string cutoffName,
        decimal cutoff,
        HashSet<ThresholdKey> thresholds)
    {
        thresholds.Add(new ThresholdKey(key.Guideline, key.OrganismCode, key.TestField,
            breakpoint.BREAKPOINT_TYPE, cutoffName, cutoff));

        if (testMethod == Antibiotic.TestMethods.Disk)
        {
            decimal floor = Math.Floor(cutoff);
            decimal ceiling = Math.Ceiling(cutoff);
            AddDiskProbe(probeSet, key.TestField, breakpointRowIndex, cutoffName, cutoff, floor - 1M);
            AddDiskProbe(probeSet, key.TestField, breakpointRowIndex, cutoffName, cutoff, floor);
            AddDiskProbe(probeSet, key.TestField, breakpointRowIndex, cutoffName, cutoff, ceiling);
            AddDiskProbe(probeSet, key.TestField, breakpointRowIndex, cutoffName, cutoff, ceiling + 1M);
            return;
        }

        decimal below = cutoff - ProbeStep;
        if (below <= 0M)
            below = cutoff / 2M;

        AddProbe(probeSet, key.TestField, testMethod, Format(below),
            new ProbeOrigin(breakpointRowIndex, cutoffName, "BELOW", cutoff));
        AddProbe(probeSet, key.TestField, testMethod, Format(cutoff),
            new ProbeOrigin(breakpointRowIndex, cutoffName, "AT", cutoff));
        AddProbe(probeSet, key.TestField, testMethod, Format(cutoff + ProbeStep),
            new ProbeOrigin(breakpointRowIndex, cutoffName, "ABOVE", cutoff));

        foreach (string modifier in new[] { "<", "<=", ">", ">=", "=" })
            AddProbe(probeSet, key.TestField, testMethod, modifier + Format(cutoff),
                new ProbeOrigin(breakpointRowIndex, cutoffName, "MODIFIER_" + modifier, cutoff));
    }

    private static void AddDiskProbe(
        ProbeSet probeSet,
        string testField,
        int breakpointRowIndex,
        string cutoffName,
        decimal cutoff,
        decimal candidate)
    {
        if (candidate < Constants.Disk.MinimumDiskMeasurement
            || candidate > Constants.Disk.MaximumDiskMeasurement
            || decimal.Truncate(candidate) != candidate)
            return;

        string probeKind = candidate < cutoff ? "BELOW" : candidate > cutoff ? "ABOVE" : "AT";
        AddProbe(probeSet, testField, Antibiotic.TestMethods.Disk, Format(candidate),
            new ProbeOrigin(breakpointRowIndex, cutoffName, probeKind, cutoff));
    }

    private static void AddProbe(ProbeSet probeSet, string testField, string testMethod, string value, ProbeOrigin origin)
    {
        decimal numericResult = 0M;
        string modifier = string.Empty;
        if (!InterpretationLibrary.ParseResult(testMethod, value, ref numericResult, ref modifier))
            throw new InvalidDataException($"Generated invalid {testMethod} measurement '{value}' for {testField}.");

        probeSet.Probes.TryAdd(value, origin);
    }

    private static MatrixWriteResult WriteBoundaryMatrix(
        string inputFile,
        string caseMapFile,
        List<string> headers,
        Dictionary<PairKey, ProbeSet> cases)
    {
        Dictionary<string, int> headerIndices = headers
            .Select((header, index) => (header, index))
            .ToDictionary(item => item.header, item => item.index, StringComparer.Ordinal);
        string organismHeader = Constants.KeyFields.ORGANISM;
        string rowIndexHeader = "ROW_IDX";
        List<OrganismCases> organismCases = cases.Values
            .GroupBy(probeSet => probeSet.Key.OrganismCode, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                FieldCases[] fields = group
                    .OrderBy(probeSet => probeSet.Key.TestField, StringComparer.Ordinal)
                    .Select(probeSet => new FieldCases(probeSet.Key.TestField,
                        probeSet.Probes.OrderBy(probe => probe.Key, StringComparer.Ordinal).ToArray()))
                    .ToArray();
                return new OrganismCases(group.Key, fields, fields.Max(field => field.Probes.Length));
            })
            .ToList();

        int rowCount = organismCases.Sum(organism => organism.RowCount);
        long cellMapRows = 0;
        long rowIndex = 0;
        using (StreamWriter inputWriter = new(inputFile, false, new UTF8Encoding(false)))
        using (StreamWriter mapWriter = new(
            new GZipStream(File.Create(caseMapFile), CompressionLevel.Optimal),
            new UTF8Encoding(false)))
        {
            inputWriter.NewLine = "\r\n";
            mapWriter.NewLine = "\r\n";
            inputWriter.WriteLine(IO_Library.ToLine(headers, PipeDelimiter));
            mapWriter.WriteLine(IO_Library.ToLine(
                ["ROW_IDX", "ORGANISM", "TEST_FIELD", "MEASUREMENT", "BREAKPOINT_ROW_INDEX", "CUTOFF", "PROBE_KIND", "THRESHOLD"],
                Constants.Delimiters.TabChar));

            foreach (OrganismCases organism in organismCases)
            {
                for (int slot = 0; slot < organism.RowCount; slot++)
                {
                    string[] row = new string[headers.Count];
                    Array.Fill(row, string.Empty);
                    string rowIndexValue = rowIndex.ToString(CultureInfo.InvariantCulture);
                    row[headerIndices[rowIndexHeader]] = rowIndexValue;
                    row[headerIndices[organismHeader]] = organism.OrganismCode;

                    foreach (FieldCases field in organism.Fields)
                    {
                        if (slot >= field.Probes.Length)
                            continue;

                        (string value, ProbeOrigin origin) = field.Probes[slot];
                        row[headerIndices[field.TestField]] = value;
                        mapWriter.WriteLine(IO_Library.ToLine(
                        [
                            rowIndexValue,
                            organism.OrganismCode,
                            field.TestField,
                            value,
                            origin.BreakpointRowIndex.ToString(CultureInfo.InvariantCulture),
                            origin.CutoffName,
                            origin.ProbeKind,
                            origin.Threshold is decimal threshold ? Format(threshold) : string.Empty
                        ], Constants.Delimiters.TabChar));
                        cellMapRows++;
                    }

                    inputWriter.WriteLine(IO_Library.ToLine(row, PipeDelimiter));
                    rowIndex++;
                }
            }
        }

        return new MatrixWriteResult(rowCount, cellMapRows);
    }

    private static OutcomeSummary InterpretGeneratedMatrix(string inputFile, List<string> expectedHeaders)
    {
        SortedDictionary<string, long> outcomeCounts = new(StringComparer.Ordinal);
        HashSet<(string Organism, string TestField)> coveredFields = new();
        using IncrementalHash outputHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using TextFieldParser parser = new(inputFile, Encoding.UTF8)
        {
            HasFieldsEnclosedInQuotes = true
        };
        parser.SetDelimiters(PipeDelimiter.ToString());

        string[] headers = parser.ReadFields() ?? throw new InvalidDataException("Generated input has no header.");
        if (!headers.SequenceEqual(expectedHeaders, StringComparer.Ordinal))
            throw new InvalidDataException("Generated input header differs from the expected header.");

        int organismIndex = Array.IndexOf(headers, Constants.KeyFields.ORGANISM);
        int rowIndexIndex = Array.IndexOf(headers, "ROW_IDX");
        long rowCount = 0;
        long measurementCount = 0;
        long resultCount = 0;

        while (!parser.EndOfData)
        {
            string[] values = parser.ReadFields() ?? throw new InvalidDataException("Unexpected end of generated input.");
            if (values.Length != headers.Length)
                throw new InvalidDataException($"Row {rowCount} has {values.Length} fields; expected {headers.Length}.");
            if (string.IsNullOrWhiteSpace(values[organismIndex]))
                throw new InvalidDataException($"Row {rowCount} has no organism code.");

            Dictionary<string, string> rowValues = new(StringComparer.Ordinal);
            for (int index = 0; index < values.Length; index++)
            {
                if (string.IsNullOrWhiteSpace(values[index]))
                    continue;

                rowValues.Add(headers[index], values[index]);
                if (!IsolateInterpretation.ValidAntibioticFieldNameRegex.IsMatch(headers[index]))
                    continue;

                string testMethod = Antibiotic.TestMethods.GetTestMethodFromCode(GetTestMethodCode(headers[index]));
                decimal numericResult = 0M;
                string modifier = string.Empty;
                if (!InterpretationLibrary.ParseResult(testMethod, values[index], ref numericResult, ref modifier))
                    throw new InvalidDataException($"Invalid parsed measurement '{values[index]}' in row {rowCount}, field {headers[index]}.");

                measurementCount++;
                coveredFields.Add((values[organismIndex], headers[index]));
            }

            IsolateInterpretation isolate = new(
                rowValues,
                headers.ToList(),
                [],
                [],
                guidelineYear: GuidelineYear,
                prioritizedBreakpointTypes: BreakpointTypes.ToList(),
                prioritizedSitesOfInfection: Constants.SitesOfInfection.DefaultOrder.ToList(),
                roundHalfDilutions: true,
                useIntrinsicResistanceRules: false);
            Dictionary<string, string> interpretations = isolate.GetAllInterpretations();
            if (interpretations.Count != rowValues.Keys.Count(IsolateInterpretation.ValidAntibioticFieldNameRegex.IsMatch))
                throw new InvalidDataException($"Engine omitted one or more measured fields in row {rowCount}.");

            string rowIdentifier = values[rowIndexIndex];
            foreach ((string field, string result) in interpretations.OrderBy(result => result.Key, StringComparer.Ordinal))
            {
                string outcome = string.IsNullOrEmpty(result) ? "(blank)" : result;
                outcomeCounts[outcome] = outcomeCounts.GetValueOrDefault(outcome) + 1;
                string record = string.Concat(rowIdentifier, "\t", field, "\t", outcome, "\n");
                outputHash.AppendData(Encoding.UTF8.GetBytes(record));
                resultCount++;
            }

            rowCount++;
        }

        if (rowCount == 0 || measurementCount != resultCount)
            throw new InvalidDataException($"Generated input verification failed: rows={rowCount}, measurements={measurementCount}, results={resultCount}.");

        return new OutcomeSummary(
            rowCount,
            measurementCount,
            resultCount,
            coveredFields.Count,
            Convert.ToHexString(outputHash.GetHashAndReset()).ToLowerInvariant(),
            outcomeCounts);
    }

    private static RuleCaseSummary GenerateIntrinsicRuleCases(string outputFile, List<string> sampleHeaders)
    {
        Dictionary<string, Dictionary<string, string>> rows = new(StringComparer.Ordinal);
        int pairCount = 0;
        string[] guidelines = [Antibiotic.GuidelineNames.CLSI, Antibiotic.GuidelineNames.EUCAST];
        List<string> antibioticCodes = Antibiotic.AllAntibiotics
            .Select(antibiotic => antibiotic.WHONET_ABX_CODE)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (Organism organism in Organism.CurrentOrganisms.Values.OrderBy(org => org.WHONET_ORG_CODE, StringComparer.Ordinal))
        {
            foreach (ExpectedResistancePhenotypeRule rule in ExpectedResistancePhenotypeRule.GetApplicableExpectedResistanceRules(
                organism.WHONET_ORG_CODE, guidelines.ToList(), antibioticCodes))
            {
                char guidelineCode = Antibiotic.GuidelineCodes.AllCodes.Single(code =>
                    Antibiotic.GuidelineNames.GetGuidelineFromCode(code) == rule.GUIDELINE);
                string field = $"{rule.ABX_CODE}_{guidelineCode}{Antibiotic.TestMethodCodes.MIC}";
                if (!rows.TryGetValue(organism.WHONET_ORG_CODE, out Dictionary<string, string>? row))
                {
                    row = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        [Constants.KeyFields.ORGANISM] = organism.WHONET_ORG_CODE
                    };
                    rows.Add(organism.WHONET_ORG_CODE, row);
                }

                if (row.TryAdd(field, "1"))
                    pairCount++;
            }
        }

        if (pairCount == 0)
            throw new InvalidDataException("No intrinsic CLSI/EUCAST rule cases were generated.");

        string[] headers = BuildHeaders(sampleHeaders, rows.Values.SelectMany(row => row.Keys));
        WriteScenarioRows(outputFile, headers, rows, includeRuleCode: false);
        OutcomeSummary outcomes = InterpretScenarioFile(outputFile, headers, useIntrinsicRules: true, useExpertRules: false);
        long resistantCount = outcomes.OutcomeCounts.GetValueOrDefault(Constants.InterpretationCodes.Resistant + Constants.InterpretationCodes.Asterisk);
        if (resistantCount != pairCount)
            throw new InvalidDataException($"Intrinsic-rule output count {resistantCount} did not match generated pair count {pairCount}.");

        return new RuleCaseSummary(outcomes.RowCount, pairCount, resistantCount);
    }

    private static ExpertRuleSummary GenerateExpertRuleCases(
        string outputFile,
        string userBreakpointsFile,
        List<string> sampleHeaders,
        string breakpointResourcePath)
    {
        List<ExpertCase> cases = new();
        Dictionary<(string Organism, string TestField), Breakpoint> userBreakpoints = new();

        foreach (ExpertInterpretationRule rule in ExpertInterpretationRule.ExpertInterpretationRules)
        {
            Organism? organism = Organism.CurrentOrganisms.Values
                .OrderBy(candidate => candidate.WHONET_ORG_CODE, StringComparer.Ordinal)
                .FirstOrDefault(candidate => MatchesOrganism(candidate, rule.ORGANISM_CODE_TYPE, rule.ORGANISM_CODE));
            if (organism is null)
                continue;

            Dictionary<string, string> values = new(StringComparer.Ordinal)
            {
                [Constants.KeyFields.ORGANISM] = organism.WHONET_ORG_CODE,
                ["RULE_CODE"] = rule.RULE_CODE
            };
            HashSet<string> antibioticCodes = new(rule.AFFECTED_ANTIBIOTICS, StringComparer.Ordinal);
            antibioticCodes.UnionWith(rule.ANTIBIOTIC_EXCEPTIONS.Where(code => !string.IsNullOrWhiteSpace(code)));
            antibioticCodes.UnionWith(rule.RULE_CRITERIA
                .Where(criterion => IsolateInterpretation.ValidAntibioticCode.IsMatch(criterion.TestName))
                .Select(criterion => criterion.TestName));
            if (rule.RULE_CRITERIA.Any(criterion => criterion.TestName == ExpertInterpretationRule.PROF_CLASS.CEPH3))
                antibioticCodes.UnionWith(Antibiotic.CEPH3_AntibioticCodes);

            foreach (string antibioticCode in antibioticCodes.OrderBy(code => code, StringComparer.Ordinal))
            {
                string field = $"{antibioticCode}_{Antibiotic.GuidelineCodes.CLSI}{Antibiotic.TestMethodCodes.MIC}";
                bool requiredForRule = rule.RULE_CRITERIA.Any(criterion =>
                    criterion.TestName == antibioticCode
                    || criterion.TestName == ExpertInterpretationRule.PROF_CLASS.CEPH3
                        && Antibiotic.CEPH3_AntibioticCodes.Contains(antibioticCode));
                values[field] = requiredForRule ? "4" : "1";
                userBreakpoints.TryAdd((organism.WHONET_ORG_CODE, field), CreateUserBreakpoint(organism.WHONET_ORG_CODE, field, antibioticCode));
            }

            foreach (ExpertRuleCriterion criterion in rule.RULE_CRITERIA.Where(criterion =>
                !IsolateInterpretation.ValidAntibioticCode.IsMatch(criterion.TestName)
                && criterion.TestName != ExpertInterpretationRule.PROF_CLASS.CEPH3))
                values[criterion.TestName] = criterion.TestResult;

            List<Breakpoint> ruleBreakpoints = userBreakpoints.Values
                .Where(breakpoint => breakpoint.ORGANISM_CODE == organism.WHONET_ORG_CODE)
                .ToList();
            IsolateInterpretation isolate = new(
                values,
                values.Keys.ToList(),
                [rule.RULE_CODE],
                ruleBreakpoints,
                guidelineYear: GuidelineYear,
                prioritizedBreakpointTypes: [Breakpoint.BreakpointTypes.Human],
                prioritizedSitesOfInfection: Constants.SitesOfInfection.DefaultOrder.ToList(),
                roundHalfDilutions: false,
                useIntrinsicResistanceRules: false);
            Dictionary<string, string> interpretations = isolate.GetAllInterpretations();

            foreach (string antibioticCode in rule.AFFECTED_ANTIBIOTICS)
            {
                string affectedField = $"{antibioticCode}_{Antibiotic.GuidelineCodes.CLSI}{Antibiotic.TestMethodCodes.MIC}";
                if (!interpretations.TryGetValue(affectedField, out string? result)
                    || result != Constants.InterpretationCodes.Resistant + Constants.InterpretationCodes.ExclamationPoint)
                    throw new InvalidDataException($"Expert rule {rule.RULE_CODE} did not override {affectedField}.");
            }

            foreach (string antibioticCode in rule.ANTIBIOTIC_EXCEPTIONS.Where(code => !string.IsNullOrWhiteSpace(code)))
            {
                string exceptionField = $"{antibioticCode}_{Antibiotic.GuidelineCodes.CLSI}{Antibiotic.TestMethodCodes.MIC}";
                if (!interpretations.TryGetValue(exceptionField, out string? result)
                    || result != Constants.InterpretationCodes.Susceptible)
                    throw new InvalidDataException($"Expert rule {rule.RULE_CODE} exception {exceptionField} was not preserved.");
            }

            cases.Add(new ExpertCase(rule.RULE_CODE, organism.WHONET_ORG_CODE, values, interpretations));
        }

        if (cases.Count == 0)
            throw new InvalidDataException("No expert-rule test cases were generated.");

        IEnumerable<string> allFields = cases.SelectMany(testCase => testCase.Values.Keys);
        string[] headers = BuildHeaders(sampleHeaders, allFields.Append("RULE_CODE"));
        WriteExpertCases(outputFile, headers, cases);
        WriteUserBreakpoints(userBreakpointsFile, breakpointResourcePath, userBreakpoints.Values);

        SortedDictionary<string, long> outputCounts = new(StringComparer.Ordinal);
        foreach (ExpertCase testCase in cases)
        foreach (string result in testCase.Interpretations.Values)
        {
            string outcome = string.IsNullOrEmpty(result) ? "(blank)" : result;
            outputCounts[outcome] = outputCounts.GetValueOrDefault(outcome) + 1;
        }

        long resistantCount = outputCounts.GetValueOrDefault(Constants.InterpretationCodes.Resistant + Constants.InterpretationCodes.ExclamationPoint);
        long expectedResistantCount = cases.Sum(testCase => testCase.Interpretations.Values.Count(result =>
            result == Constants.InterpretationCodes.Resistant + Constants.InterpretationCodes.ExclamationPoint));
        if (resistantCount != expectedResistantCount || resistantCount == 0)
            throw new InvalidDataException("Expert-rule output coverage was incomplete.");

        return new ExpertRuleSummary(cases.Count, cases.Select(testCase => testCase.RuleCode).Distinct(StringComparer.Ordinal).Count(),
            resistantCount, userBreakpoints.Count, outputCounts);
    }

    private static string[] BuildHeaders(List<string> sampleHeaders, IEnumerable<string> additionalHeaders)
    {
        List<string> headers = sampleHeaders.ToList();
        HashSet<string> headerSet = new(headers, StringComparer.Ordinal);
        foreach (string header in additionalHeaders.OrderBy(header => header, StringComparer.Ordinal))
            if (headerSet.Add(header))
                headers.Add(header);
        return headers.ToArray();
    }

    private static void WriteScenarioRows(
        string outputFile,
        string[] headers,
        Dictionary<string, Dictionary<string, string>> rows,
        bool includeRuleCode)
    {
        Dictionary<string, int> headerIndices = headers
            .Select((header, index) => (header, index))
            .ToDictionary(item => item.header, item => item.index, StringComparer.Ordinal);

        using StreamWriter writer = new(outputFile, false, new UTF8Encoding(false))
        {
            NewLine = "\r\n"
        };
        writer.WriteLine(IO_Library.ToLine(headers, PipeDelimiter));

        int rowIndex = 0;
        foreach ((string organismCode, Dictionary<string, string> inputValues) in rows.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            string[] row = new string[headers.Length];
            Array.Fill(row, string.Empty);
            row[headerIndices["ROW_IDX"]] = rowIndex.ToString(CultureInfo.InvariantCulture);
            foreach ((string field, string value) in inputValues)
                row[headerIndices[field]] = value;
            if (includeRuleCode && !inputValues.ContainsKey("RULE_CODE"))
                row[headerIndices["RULE_CODE"]] = string.Empty;
            writer.WriteLine(IO_Library.ToLine(row, PipeDelimiter));
            rowIndex++;
        }
    }

    private static OutcomeSummary InterpretScenarioFile(
        string inputFile,
        IReadOnlyList<string> expectedHeaders,
        bool useIntrinsicRules,
        bool useExpertRules)
    {
        SortedDictionary<string, long> outcomes = new(StringComparer.Ordinal);
        using TextFieldParser parser = new(inputFile, Encoding.UTF8)
        {
            HasFieldsEnclosedInQuotes = true
        };
        parser.SetDelimiters(PipeDelimiter.ToString());
        string[] headers = parser.ReadFields() ?? throw new InvalidDataException($"No header in {inputFile}.");
        if (!headers.SequenceEqual(expectedHeaders, StringComparer.Ordinal))
            throw new InvalidDataException($"Unexpected header in {inputFile}.");

        long rows = 0;
        long results = 0;
        while (!parser.EndOfData)
        {
            string[] fields = parser.ReadFields() ?? throw new InvalidDataException($"Unexpected end of {inputFile}.");
            if (fields.Length != headers.Length)
                throw new InvalidDataException($"Malformed row {rows} in {inputFile}.");
            Dictionary<string, string> input = new(StringComparer.Ordinal);
            for (int index = 0; index < headers.Length; index++)
                if (!string.IsNullOrWhiteSpace(fields[index]))
                    input.Add(headers[index], fields[index]);

            IsolateInterpretation isolate = new(
                input,
                headers.ToList(),
                useExpertRules ? ExpertInterpretationRule.ExpertInterpretationRules.Select(rule => rule.RULE_CODE).ToList() : [],
                [],
                guidelineYear: GuidelineYear,
                prioritizedBreakpointTypes: BreakpointTypes.ToList(),
                prioritizedSitesOfInfection: Constants.SitesOfInfection.DefaultOrder.ToList(),
                roundHalfDilutions: false,
                useIntrinsicResistanceRules: useIntrinsicRules);
            Dictionary<string, string> interpretations = isolate.GetAllInterpretations();
            foreach (string result in interpretations.Values)
            {
                string outcome = string.IsNullOrEmpty(result) ? "(blank)" : result;
                outcomes[outcome] = outcomes.GetValueOrDefault(outcome) + 1;
                results++;
            }
            rows++;
        }

        return new OutcomeSummary(rows, results, results, results, string.Empty, outcomes);
    }

    private static void WriteExpertCases(string outputFile, IReadOnlyList<string> headers, List<ExpertCase> cases)
    {
        Dictionary<string, int> headerIndices = headers
            .Select((header, index) => (header, index))
            .ToDictionary(item => item.header, item => item.index, StringComparer.Ordinal);

        using StreamWriter writer = new(outputFile, false, new UTF8Encoding(false))
        {
            NewLine = "\r\n"
        };
        writer.WriteLine(IO_Library.ToLine(headers, PipeDelimiter));

        int rowIndex = 0;
        foreach (ExpertCase testCase in cases.OrderBy(testCase => testCase.RuleCode, StringComparer.Ordinal))
        {
            string[] row = new string[headers.Count];
            Array.Fill(row, string.Empty);
            row[headerIndices["ROW_IDX"]] = rowIndex.ToString(CultureInfo.InvariantCulture);
            foreach ((string field, string value) in testCase.Values)
                row[headerIndices[field]] = value;
            writer.WriteLine(IO_Library.ToLine(row, PipeDelimiter));
            rowIndex++;
        }
    }

    private static void WriteUserBreakpoints(
        string outputFile,
        string breakpointResourcePath,
        IEnumerable<Breakpoint> breakpoints)
    {
        List<string> headers = ReadHeaders(breakpointResourcePath, Constants.Delimiters.TabChar);
        using StreamWriter writer = new(outputFile, false, new UTF8Encoding(false))
        {
            NewLine = "\r\n"
        };
        writer.WriteLine(IO_Library.ToLine(headers, Constants.Delimiters.TabChar));

        foreach (Breakpoint breakpoint in breakpoints.OrderBy(bp => bp.ORGANISM_CODE, StringComparer.Ordinal)
            .ThenBy(bp => bp.WHONET_TEST, StringComparer.Ordinal))
        {
            Dictionary<string, string> row = new(StringComparer.Ordinal)
            {
                [nameof(Breakpoint.GUIDELINES)] = Antibiotic.GuidelineNames.UserDefined,
                [nameof(Breakpoint.YEAR)] = breakpoint.YEAR.ToString(CultureInfo.InvariantCulture),
                [nameof(Breakpoint.TEST_METHOD)] = breakpoint.TEST_METHOD,
                [nameof(Breakpoint.POTENCY)] = breakpoint.POTENCY,
                [nameof(Breakpoint.ORGANISM_CODE)] = breakpoint.ORGANISM_CODE,
                [nameof(Breakpoint.ORGANISM_CODE_TYPE)] = breakpoint.ORGANISM_CODE_TYPE,
                [nameof(Breakpoint.BREAKPOINT_TYPE)] = breakpoint.BREAKPOINT_TYPE,
                [nameof(Breakpoint.HOST)] = breakpoint.HOST,
                [nameof(Breakpoint.SITE_OF_INFECTION)] = breakpoint.SITE_OF_INFECTION,
                [nameof(Breakpoint.REFERENCE_TABLE)] = breakpoint.REFERENCE_TABLE,
                [nameof(Breakpoint.REFERENCE_SEQUENCE)] = breakpoint.REFERENCE_SEQUENCE,
                [nameof(Breakpoint.WHONET_ABX_CODE)] = breakpoint.WHONET_ABX_CODE,
                [nameof(Breakpoint.WHONET_TEST)] = breakpoint.WHONET_TEST,
                [nameof(Breakpoint.R)] = Format(breakpoint.R),
                [nameof(Breakpoint.I)] = breakpoint.I,
                [nameof(Breakpoint.SDD)] = breakpoint.SDD,
                [nameof(Breakpoint.S)] = Format(breakpoint.S),
                [nameof(Breakpoint.ECV_ECOFF)] = Format(breakpoint.ECV_ECOFF),
                [nameof(Breakpoint.ECV_ECOFF_TENTATIVE)] = breakpoint.ECV_ECOFF_TENTATIVE ? "X" : string.Empty,
                [nameof(Breakpoint.DATE_ENTERED)] = string.Empty,
                [nameof(Breakpoint.DATE_MODIFIED)] = string.Empty,
                [nameof(Breakpoint.COMMENTS)] = string.Empty
            };
            writer.WriteLine(IO_Library.ToLine(headers.Select(header => row.GetValueOrDefault(header, string.Empty)),
                Constants.Delimiters.TabChar));
        }
    }

    private static Breakpoint CreateUserBreakpoint(string organismCode, string field, string antibioticCode) => new(
        Antibiotic.GuidelineNames.UserDefined,
        GuidelineYear,
        Antibiotic.TestMethods.MIC,
        string.Empty,
        organismCode,
        nameof(Organism.WHONET_ORG_CODE),
        Breakpoint.BreakpointTypes.Human,
        Breakpoint.BreakpointTypes.Human,
        string.Empty,
        string.Empty,
        string.Empty,
        antibioticCode,
        field,
        4M,
        "I",
        string.Empty,
        2M,
        0M,
        false,
        DateTime.MinValue,
        DateTime.MinValue,
        string.Empty);

    private static void ValidateOutcomeCoverage(
        OutcomeSummary breakpointOutcomes,
        RuleCaseSummary intrinsicRules,
        ExpertRuleSummary expertRules)
    {
        string[] requiredBreakpointOutcomes =
        [
            Constants.InterpretationCodes.Susceptible,
            Constants.InterpretationCodes.Resistant,
            Constants.InterpretationCodes.Intermediate,
            Constants.InterpretationCodes.SusceptibleDoseDependent,
            Constants.InterpretationCodes.NonSusceptible,
            Constants.InterpretationCodes.WildType,
            Constants.InterpretationCodes.NonWildType
        ];
        List<string> missing = requiredBreakpointOutcomes
            .Where(outcome => !breakpointOutcomes.OutcomeCounts.ContainsKey(outcome))
            .ToList();
        if (!breakpointOutcomes.OutcomeCounts.Keys.Any(outcome => outcome.Contains(Constants.InterpretationCodes.QuestionMark, StringComparison.Ordinal)))
            missing.Add("uncertain modifier result");
        if (!breakpointOutcomes.OutcomeCounts.ContainsKey("(blank)"))
            missing.Add("blank/uninterpretable");
        if (intrinsicRules.ResistantOutputCount == 0)
            missing.Add("R*");
        if (expertRules.ResistantOutputCount == 0)
            missing.Add("R!");
        if (missing.Count > 0)
            throw new InvalidDataException("The generated corpus did not exercise expected outcomes: " + string.Join(", ", missing));
    }

    private static int ValidateRoundingBoundaries()
    {
        decimal[] dilutions =
        [
            0.001M, 0.002M, 0.004M, 0.008M, 0.015M, 0.016M, 0.03M, 0.032M,
            0.06M, 0.064M, 0.12M, 0.125M, 0.25M, 0.5M, 1M, 2M, 4M, 8M,
            16M, 32M, 64M, 128M, 256M, 512M, 1024M, 2048M
        ];
        decimal[] fixedConcentrations = [500M, 1000M];
        for (int index = 0; index < dilutions.Length; index++)
        {
            decimal boundary = dilutions[index];
            if (InterpretationLibrary.Round_ETestHalfDilutionsUp(boundary) != boundary)
                throw new InvalidDataException($"ETest rounding changed a supported dilution boundary {boundary}.");
            if (index + 1 < dilutions.Length
                && InterpretationLibrary.Round_ETestHalfDilutionsUp(boundary + 0.000001M) != dilutions[index + 1])
                throw new InvalidDataException($"ETest rounding did not advance after dilution boundary {boundary}.");
        }

        foreach (decimal concentration in fixedConcentrations)
            if (InterpretationLibrary.Round_ETestHalfDilutionsUp(concentration) != concentration)
                throw new InvalidDataException($"ETest rounding changed fixed concentration {concentration}.");

        return dilutions.Length + fixedConcentrations.Length;
    }

    private static long CalculateProjectedAppendBytes(
        string sampleInputPath,
        List<string> sampleHeaders,
        List<string> finalHeaders,
        long generatedInputFileBytes)
    {
        int additionalColumns = finalHeaders.Count - sampleHeaders.Count;
        long paddedExistingRowsBytes = 0;
        foreach (string line in File.ReadLines(sampleInputPath).Skip(1))
            paddedExistingRowsBytes += Encoding.UTF8.GetByteCount(line) + additionalColumns + 2;
        return generatedInputFileBytes + paddedExistingRowsBytes;
    }

    private static string ComputeSha256(string filePath)
    {
        using FileStream stream = new(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            FileOptions.SequentialScan);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static void EnsureFreeSpace(string outputDirectory, long requestedBytes)
    {
        string fullPath = Path.GetFullPath(outputDirectory);
        string driveRoot = Path.GetPathRoot(fullPath)
            ?? throw new IOException($"Cannot determine the output drive for '{fullPath}'.");
        DriveInfo drive = new(driveRoot);
        if (!drive.IsReady)
            throw new IOException($"The output drive '{drive.Name}' is not ready.");

        if (requestedBytes > long.MaxValue - SidecarSpaceReserveBytes)
            throw new IOException("Requested size exceeds the generator's supported file-size range.");
        long minimumFreeBytes = checked(requestedBytes + SidecarSpaceReserveBytes);
        if (drive.AvailableFreeSpace < minimumFreeBytes)
            throw new IOException($"Insufficient free space on '{drive.Name}': {minimumFreeBytes:N0} bytes required, {drive.AvailableFreeSpace:N0} available.");
    }

    private static string GetAntibioticCode(string testField)
    {
        Match match = IsolateInterpretation.ValidAntibioticFieldNameRegex.Match(testField);
        if (!match.Success)
            throw new InvalidDataException($"Invalid antibiotic test field '{testField}'.");
        return match.Groups[1].Value;
    }

    private static char GetTestMethodCode(string testField)
    {
        Match match = IsolateInterpretation.ValidAntibioticFieldNameRegex.Match(testField);
        if (!match.Success || match.Groups[3].Value.Length == 0)
            throw new InvalidDataException($"Invalid antibiotic test field '{testField}'.");
        return match.Groups[3].Value[0];
    }

    private static string Format(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private readonly record struct IndexedBreakpoint(Breakpoint Breakpoint, int RowIndex);
    private readonly record struct PairKey(string Guideline, string OrganismCode, string TestField);
    private readonly record struct ThresholdKey(
        string Guideline,
        string OrganismCode,
        string TestField,
        string BreakpointType,
        string CutoffName,
        decimal Threshold);
    private readonly record struct ResolvedAgent(string AntibioticCode, string TestField);
    private readonly record struct ProbeOrigin(int BreakpointRowIndex, string CutoffName, string ProbeKind, decimal? Threshold);
    private sealed class ProbeSet
    {
        public ProbeSet(PairKey key)
        {
            Key = key;
        }

        public PairKey Key { get; }
        public Dictionary<string, ProbeOrigin> Probes { get; } = new(StringComparer.Ordinal);
    }

    private sealed record FieldCases(string TestField, KeyValuePair<string, ProbeOrigin>[] Probes);
    private sealed record OrganismCases(string OrganismCode, FieldCases[] Fields, int RowCount);
    private sealed record MatrixWriteResult(int RowCount, long CellMapRows);
    private sealed record OutcomeSummary(
        long RowCount,
        long MeasurementCount,
        long ResultCount,
        long CoveredFieldCount,
        string OutputSha256,
        SortedDictionary<string, long> OutcomeCounts);
    private sealed record RuleCaseSummary(long RowCount, int PairCount, long ResistantOutputCount);
    private sealed record ExpertCase(string RuleCode, string OrganismCode, Dictionary<string, string> Values, Dictionary<string, string> Interpretations);
    private sealed record ExpertRuleSummary(int RowCount, int RulesExercised, long ResistantOutputCount, int UserBreakpointCount, SortedDictionary<string, long> OutcomeCounts);
}

internal sealed class GenerationManifest
{
    public string ManifestFile { get; init; } = string.Empty;
    public string InputFile { get; init; } = string.Empty;
    public long InputFileBytes { get; init; }
    public string InputSha256 { get; init; } = string.Empty;
    public long BaseInputFileBytes { get; init; }
    public string RequestedSize { get; init; } = string.Empty;
    public long RequestedSizeBytes { get; init; }
    public long InputSizeOvershootBytes { get; init; }
    public string SizeExpansionAlgorithm { get; init; } = string.Empty;
    public long SizeExpansionRows { get; init; }
    public long SizeExpansionMeasurements { get; init; }
    public long SizeExpansionMaximumRowBytes { get; init; }
    public SortedDictionary<string, long> SizeExpansionProbeKindCounts { get; init; } = new(StringComparer.Ordinal);
    public long SizeExpansionReferenceRowsValidated { get; init; }
    public long SizeExpansionReferenceOutputCount { get; init; }
    public SortedDictionary<string, long> SizeExpansionReferenceOutcomeCounts { get; init; } = new(StringComparer.Ordinal);
    public string CaseMapFile { get; init; } = string.Empty;
    public long CaseMapFileBytes { get; init; }
    public string CaseMapSha256 { get; init; } = string.Empty;
    public SortedDictionary<string, string> ResourceSha256 { get; init; } = new(StringComparer.Ordinal);
    public int GuidelineYear { get; init; }
    public int BreakpointResourceRows { get; init; }
    public int BreakpointRowOrganismIncidences { get; init; }
    public int ExpandedBreakpointAgentIncidences { get; init; }
    public int AtcPrefixBreakpointRows { get; init; }
    public SortedDictionary<string, string> BreakpointAgentCatalogAliases { get; init; } = new(StringComparer.Ordinal);
    public string[] UnmappedBreakpointAgentCodes { get; init; } = [];
    public int OrganismCodes { get; init; }
    public int DirectPairCount { get; init; }
    public int DirectTestFieldCount { get; init; }
    public int DirectMeasurementCount { get; init; }
    public int ThresholdInstanceCount { get; init; }
    public int EtestAliasPairCount { get; init; }
    public int EtestAliasFieldCount { get; init; }
    public int EtestMeasurementCount { get; init; }
    public long TotalMeasurementCount { get; init; }
    public long IsolateRows { get; init; }
    public long BaseIsolateRows { get; init; }
    public int ColumnCount { get; init; }
    public int NewTestFieldCount { get; init; }
    public long MatrixCellMapRows { get; init; }
    public long ProjectedAppendBytes { get; init; }
    public long ProjectedAppendGrowthBytes { get; init; }
    public string ReferenceOutputSha256 { get; init; } = string.Empty;
    public long ReferenceOutputCount { get; init; }
    public string ReferenceOutputScope { get; init; } = string.Empty;
    public SortedDictionary<string, long> OutcomeCounts { get; init; } = new(StringComparer.Ordinal);
    public string[] UnmappedHistoricalOrganismCodes { get; init; } = [];
    public string IntrinsicRuleFile { get; init; } = string.Empty;
    public long IntrinsicRuleFileBytes { get; init; }
    public long IntrinsicRuleCaseRows { get; init; }
    public int IntrinsicRulePairs { get; init; }
    public long IntrinsicRuleResistantOutputs { get; init; }
    public string ExpertRuleFile { get; init; } = string.Empty;
    public long ExpertRuleFileBytes { get; init; }
    public int ExpertRuleCaseRows { get; init; }
    public int ExpertRulesExercised { get; init; }
    public long ExpertRuleResistantOutputs { get; init; }
    public string ExpertRuleUserBreakpointsFile { get; init; } = string.Empty;
    public long ExpertRuleUserBreakpointsFileBytes { get; init; }
    public int ExpertRuleUserBreakpointCount { get; init; }
    public int EtestRoundingBoundaryCount { get; init; }
}
