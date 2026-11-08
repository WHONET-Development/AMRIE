using AMR_Engine;
using System.Globalization;

namespace Interpretation_Engine.Tests;

public class BreakpointMatrixTests
{
    [Fact]
    public void EveryApplicableResourceCombinationResolvesAndClassifiesThresholdBoundaries()
    {
        Dictionary<(string Type, string Code), List<Organism>> candidateOrganisms = CreateCandidateOrganisms();
        var combinations = Breakpoint.Breakpoints
            .Where(breakpoint => !string.IsNullOrWhiteSpace(breakpoint.WHONET_TEST))
            .Select(breakpoint => new
            {
                breakpoint.GUIDELINES,
                breakpoint.YEAR,
                breakpoint.WHONET_TEST,
                breakpoint.TEST_METHOD,
                breakpoint.ORGANISM_CODE_TYPE,
                breakpoint.ORGANISM_CODE,
                breakpoint.BREAKPOINT_TYPE,
                breakpoint.SITE_OF_INFECTION
            })
            .Distinct()
            .ToList();
        var executableCombinations = combinations.Where(combination =>
            ResourceCodeMatchesBreakpointMetadata(
                combination.WHONET_TEST, combination.GUIDELINES, combination.TEST_METHOD)).ToList();

        int directlySelectedCombinations = 0;
        int thresholdTestedCombinations = 0;
        List<string> unsupportedOrganismCombinations = new();
        foreach (var combination in executableCombinations)
        {
            candidateOrganisms.TryGetValue(
                (combination.ORGANISM_CODE_TYPE, combination.ORGANISM_CODE), out List<Organism>? candidates);
            Breakpoint resourceBreakpoint = Breakpoint.Breakpoints.First(breakpoint =>
                breakpoint.GUIDELINES == combination.GUIDELINES
                && breakpoint.YEAR == combination.YEAR
                && breakpoint.WHONET_TEST == combination.WHONET_TEST
                && breakpoint.TEST_METHOD == combination.TEST_METHOD
                && breakpoint.ORGANISM_CODE_TYPE == combination.ORGANISM_CODE_TYPE
                && breakpoint.ORGANISM_CODE == combination.ORGANISM_CODE
                && breakpoint.BREAKPOINT_TYPE == combination.BREAKPOINT_TYPE
                && breakpoint.SITE_OF_INFECTION == combination.SITE_OF_INFECTION);

            Organism? organism = null;
            string interpretationOrganismCode = string.Empty;
            Breakpoint? selected = null;
            bool useSyntheticBreakpoint = false;
            if (candidates != null)
            {
                foreach (Organism candidate in candidates)
                {
                    string candidateOrganismCode = combination.ORGANISM_CODE_TYPE == nameof(Organism.WHONET_ORG_CODE)
                        ? combination.ORGANISM_CODE
                        : candidate.WHONET_ORG_CODE;
                    Breakpoint? candidateBreakpoint = Breakpoint.GetApplicableBreakpoints(
                            candidateOrganismCode,
                            new List<Breakpoint>(),
                            prioritizedGuidelines: [combination.GUIDELINES],
                            prioritizedGuidelineYears: [combination.YEAR],
                            prioritizedBreakpointTypes: [combination.BREAKPOINT_TYPE],
                            prioritizedSitesOfInfection: Constants.SitesOfInfection.DefaultOrder.ToList(),
                            prioritizedWhonetAbxFullDrugCodes: [combination.WHONET_TEST],
                            returnFirstBreakpointOnly: true)
                        .FirstOrDefault();

                    if (candidateBreakpoint?.ORGANISM_CODE_TYPE == combination.ORGANISM_CODE_TYPE
                        && candidateBreakpoint.ORGANISM_CODE == combination.ORGANISM_CODE)
                    {
                        organism = candidate;
                        interpretationOrganismCode = candidateOrganismCode;
                        selected = candidateBreakpoint;
                        break;
                    }
                }
            }

            if (selected == null || organism == null)
            {
                useSyntheticBreakpoint = true;
                unsupportedOrganismCombinations.Add(
                    $"{combination.ORGANISM_CODE_TYPE}:{combination.ORGANISM_CODE}/{combination.WHONET_TEST}/{combination.GUIDELINES}/{combination.YEAR}");
                organism = candidates?.FirstOrDefault() ?? Organism.CurrentOrganisms.Values.First();
                interpretationOrganismCode = organism.WHONET_ORG_CODE;
                selected = CreateUserBreakpointFromResource(organism, resourceBreakpoint);
            }
            else
                directlySelectedCombinations++;

            Organism selectedOrganism = organism ?? throw new InvalidOperationException("No organism selected for the test case.");
            Breakpoint selectedBreakpoint = selected ?? throw new InvalidOperationException("No breakpoint selected for the test case.");
            string requestedMethod = TestMethodFromCode(resourceBreakpoint.WHONET_TEST);
            List<string> prioritizedSites = Constants.SitesOfInfection.DefaultOrder.ToList();
            if (useSyntheticBreakpoint)
                prioritizedSites.AddRange(resourceBreakpoint.SITES_OF_INFECTION
                    .Where(site => !string.IsNullOrWhiteSpace(site))
                    .Except(prioritizedSites));

            InterpretationConfiguration configuration = new()
            {
                GuidelineYear = combination.YEAR,
                EnabledExpertInterpretationRules = [],
                UseIntrinsicResistanceRules = false,
                RoundHalfDilutions = false,
                PrioritizedBreakpointTypes = [combination.BREAKPOINT_TYPE],
                PrioritizedSitesOfInfection = prioritizedSites,
                UserDefinedBreakpoints = useSyntheticBreakpoint ? [selectedBreakpoint] : []
            };

            foreach (decimal measurement in GetBoundaryMeasurements(selectedBreakpoint, requestedMethod))
            {
                string input = measurement.ToString(CultureInfo.InvariantCulture);
                string expected = ExpectedInterpretation(selectedBreakpoint, requestedMethod, measurement);
                string actual = IsolateInterpretation.GetSingleInterpretation(
                    configuration,
                    interpretationOrganismCode,
                    selectedBreakpoint.WHONET_TEST,
                    input);

                Assert.True(expected == actual,
                    $"Unexpected result for {selectedOrganism.WHONET_ORG_CODE}/{resourceBreakpoint.WHONET_TEST}/{resourceBreakpoint.GUIDELINES}/{resourceBreakpoint.YEAR}/{resourceBreakpoint.BREAKPOINT_TYPE} at {input} (R={selectedBreakpoint.R}, I={selectedBreakpoint.I}, S={selectedBreakpoint.S}, ECOFF={selectedBreakpoint.ECV_ECOFF}, method={requestedMethod}): expected '{expected}', got '{actual}'.");
            }
            thresholdTestedCombinations++;
        }

        Dictionary<string, int> unsupportedOrganismCounts = unsupportedOrganismCombinations
            .GroupBy(combination => combination.Split('/')[0])
            .ToDictionary(group => group.Key, group => group.Count());
        Dictionary<string, int> expectedUnsupportedOrganismCounts = new()
        {
            ["GENUS_CODE:ACI"] = 10,
            ["GENUS_CODE:KLE"] = 4,
            ["SEROVAR_GROUP:ECO"] = 6,
            ["SPECIES_GROUP:RGM"] = 48,
            ["WHONET_ORG_CODE:BSB"] = 4,
            ["WHONET_ORG_CODE:cps"] = 2,
            ["WHONET_ORG_CODE:csl"] = 12,
            ["WHONET_ORG_CODE:eco"] = 15,
            ["WHONET_ORG_CODE:efv"] = 4,
            ["WHONET_ORG_CODE:hin"] = 2,
            ["WHONET_ORG_CODE:hne"] = 12,
            ["WHONET_ORG_CODE:psd"] = 8,
            ["WHONET_ORG_CODE:sau"] = 19,
            ["WHONET_ORG_CODE:squ"] = 24,
            ["WHONET_ORG_CODE:sui"] = 24,
            ["WHONET_ORG_CODE:szo"] = 24,
            [":Gen"] = 39,
            ["ALL:Gen"] = 195
        };

        Assert.True(
            expectedUnsupportedOrganismCounts.OrderBy(pair => pair.Key)
                .SequenceEqual(unsupportedOrganismCounts.OrderBy(pair => pair.Key)),
            $"Unsupported organism selector counts changed: {string.Join(", ", unsupportedOrganismCounts.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value}"))}");
        Assert.Equal(50_048, executableCombinations.Count);
        Assert.Equal(49_596, directlySelectedCombinations);
        Assert.Equal(executableCombinations.Count, thresholdTestedCombinations);
    }

    [Fact]
    public void UniversalBreakpointRowsAreFallbacksToOrganismSpecificRows()
    {
        const string testCode = "PEN_EM";
        Breakpoint universalBreakpoint = Assert.Single(Breakpoint.Breakpoints.Where(breakpoint =>
            breakpoint.GUIDELINES == Antibiotic.GuidelineNames.EUCAST
            && breakpoint.YEAR == 2013
            && breakpoint.WHONET_TEST == testCode
            && breakpoint.ORGANISM_CODE_TYPE == "ALL"
            && breakpoint.ORGANISM_CODE == "ALL"));

        Breakpoint? specificBreakpoint = Breakpoint.GetApplicableBreakpoints(
                "spn",
                [],
                prioritizedGuidelines: [universalBreakpoint.GUIDELINES],
                prioritizedGuidelineYears: [universalBreakpoint.YEAR],
                prioritizedBreakpointTypes: [universalBreakpoint.BREAKPOINT_TYPE],
                prioritizedSitesOfInfection: Constants.SitesOfInfection.DefaultOrder.ToList(),
                prioritizedWhonetAbxFullDrugCodes: [testCode],
                returnFirstBreakpointOnly: true)
            .FirstOrDefault();
        Assert.NotNull(specificBreakpoint);
        Assert.Equal(nameof(Organism.WHONET_ORG_CODE), specificBreakpoint.ORGANISM_CODE_TYPE);
        Assert.Equal("spn", specificBreakpoint.ORGANISM_CODE);

        Breakpoint? fallbackBreakpoint = Organism.CurrentOrganisms.Values
            .Select(organism => Breakpoint.GetApplicableBreakpoints(
                    organism.WHONET_ORG_CODE,
                    [],
                    prioritizedGuidelines: [universalBreakpoint.GUIDELINES],
                    prioritizedGuidelineYears: [universalBreakpoint.YEAR],
                    prioritizedBreakpointTypes: [universalBreakpoint.BREAKPOINT_TYPE],
                    prioritizedSitesOfInfection: Constants.SitesOfInfection.DefaultOrder.ToList(),
                    prioritizedWhonetAbxFullDrugCodes: [testCode],
                    returnFirstBreakpointOnly: true)
                .FirstOrDefault())
            .FirstOrDefault(breakpoint => breakpoint?.ORGANISM_CODE_TYPE == "ALL");
        Assert.NotNull(fallbackBreakpoint);
        Assert.Same(universalBreakpoint, fallbackBreakpoint);
    }

    [Fact]
    public void BreakpointTypeAndSitePrioritiesAreRespectedAcrossCacheHits()
    {
        AntibioticSpecificInterpretationRules.ClearBreakpoints();

        InterpretationConfiguration humanFirst = ConfigurationFor(2020);
        humanFirst.PrioritizedBreakpointTypes = [Breakpoint.BreakpointTypes.Human, Breakpoint.BreakpointTypes.ECOFF];
        InterpretationConfiguration ecoffFirst = ConfigurationFor(2020);
        ecoffFirst.PrioritizedBreakpointTypes = [Breakpoint.BreakpointTypes.ECOFF, Breakpoint.BreakpointTypes.Human];

        Assert.Equal(Constants.InterpretationCodes.Resistant,
            IsolateInterpretation.GetSingleInterpretation(humanFirst, "cal", "AMB_EM", "2"));
        Assert.Equal(Constants.InterpretationCodes.NonWildType,
            IsolateInterpretation.GetSingleInterpretation(ecoffFirst, "cal", "AMB_EM", "2"));

        List<Breakpoint> userBreakpoints =
        [
            UserBreakpoint(Constants.SitesOfInfection.Meningitis, resistant: 8, susceptible: 2),
            UserBreakpoint(Constants.SitesOfInfection.NonMeningitis, resistant: 8, susceptible: 4)
        ];
        InterpretationConfiguration nonMeningitisFirst = ConfigurationFor(2026);
        nonMeningitisFirst.PrioritizedSitesOfInfection =
            [Constants.SitesOfInfection.NonMeningitis, Constants.SitesOfInfection.Meningitis];
        nonMeningitisFirst.UserDefinedBreakpoints = userBreakpoints;
        InterpretationConfiguration meningitisFirst = ConfigurationFor(2026);
        meningitisFirst.PrioritizedSitesOfInfection =
            [Constants.SitesOfInfection.Meningitis, Constants.SitesOfInfection.NonMeningitis];
        meningitisFirst.UserDefinedBreakpoints = userBreakpoints;

        Assert.Equal(Constants.InterpretationCodes.Susceptible,
            IsolateInterpretation.GetSingleInterpretation(nonMeningitisFirst, "spn", "AMC_NM", "4"));
        Assert.Equal(Constants.InterpretationCodes.Intermediate,
            IsolateInterpretation.GetSingleInterpretation(meningitisFirst, "spn", "AMC_NM", "4"));
    }

    private static Dictionary<(string Type, string Code), List<Organism>> CreateCandidateOrganisms()
    {
        Dictionary<(string Type, string Code), List<Organism>> candidates = new();

        void Add(string type, string code, Organism organism)
        {
            if (string.IsNullOrWhiteSpace(code))
                return;

            if (!candidates.TryGetValue((type, code), out List<Organism>? matchingOrganisms))
            {
                matchingOrganisms = new List<Organism>();
                candidates.Add((type, code), matchingOrganisms);
            }

            if (!matchingOrganisms.Contains(organism))
                matchingOrganisms.Add(organism);
        }

        foreach (Organism organism in Organism.CurrentOrganisms.Values)
        {
            Add("ALL", "ALL", organism);
            Add(nameof(Organism.WHONET_ORG_CODE), organism.WHONET_ORG_CODE, organism);
            Add(nameof(Organism.SEROVAR_GROUP), organism.SEROVAR_GROUP, organism);
            Add(nameof(Organism.SPECIES_GROUP), organism.SPECIES_GROUP, organism);
            Add(nameof(Organism.GENUS_CODE), organism.GENUS_CODE, organism);
            Add(nameof(Organism.GENUS_GROUP), organism.GENUS_GROUP, organism);
            Add(nameof(Organism.FAMILY_CODE), organism.FAMILY_CODE, organism);
            Add(nameof(Organism.SUBKINGDOM_CODE), organism.SUBKINGDOM_CODE, organism);

            if (organism.ANAEROBE)
            {
                Add(nameof(Organism.ANAEROBE), Constants.OrganismGroups.Anaerobes, organism);
                Add(Constants.OrganismGroups.AnaerobePlusSubkingdomCode,
                    organism.SUBKINGDOM_CODE == Constants.TestResultCodes.Positive
                        ? Constants.OrganismGroups.GramPositiveAnaerobes
                        : Constants.OrganismGroups.GramNegativeAnaerobes,
                    organism);
            }
        }

        foreach (KeyValuePair<string, string> merged in Organism.MergedOrganisms)
            if (Organism.CurrentOrganisms.TryGetValue(merged.Value, out Organism? currentOrganism)
                && currentOrganism != null)
                Add(nameof(Organism.WHONET_ORG_CODE), merged.Key, currentOrganism);

        return candidates;
    }

    private static IEnumerable<decimal> GetBoundaryMeasurements(Breakpoint breakpoint, string testMethod)
    {
        HashSet<decimal> measurements = new();

        void AddAround(decimal threshold)
        {
            if (threshold <= 0)
                return;

            if (testMethod == Antibiotic.TestMethods.Disk)
            {
                measurements.Add(decimal.Floor(threshold) - 1);
                measurements.Add(decimal.Floor(threshold));
                measurements.Add(decimal.Ceiling(threshold));
                measurements.Add(decimal.Ceiling(threshold) + 1);
            }
            else
            {
                decimal epsilon = Math.Max(0.0000001M, threshold / 10000M);
                measurements.Add(threshold - epsilon);
                measurements.Add(threshold);
                measurements.Add(threshold + epsilon);
            }
        }

        AddAround(breakpoint.R);
        AddAround(breakpoint.S);
        AddAround(breakpoint.ECV_ECOFF);

        if (testMethod == Antibiotic.TestMethods.Disk)
        {
            measurements.Add(Constants.Disk.MinimumDiskMeasurement);
            measurements.Add(Constants.Disk.MaximumDiskMeasurement);
        }
        else if (measurements.Count == 0)
            measurements.Add(1M);

        return measurements.Where(measurement => measurement > 0)
            .Where(measurement => testMethod != Antibiotic.TestMethods.Disk
                || measurement >= Constants.Disk.MinimumDiskMeasurement
                && measurement <= Constants.Disk.MaximumDiskMeasurement);
    }

    private static string ExpectedInterpretation(Breakpoint breakpoint, string testMethod, decimal measurement)
    {
        if (breakpoint.BREAKPOINT_TYPE == Breakpoint.BreakpointTypes.ECOFF)
        {
            if (testMethod == Antibiotic.TestMethods.Disk)
                return measurement >= breakpoint.ECV_ECOFF
                    ? Constants.InterpretationCodes.WildType
                    : Constants.InterpretationCodes.NonWildType;

            return measurement <= breakpoint.ECV_ECOFF
                ? Constants.InterpretationCodes.WildType
                : Constants.InterpretationCodes.NonWildType;
        }

        if (testMethod == Antibiotic.TestMethods.Disk)
        {
            if (measurement < Constants.Disk.MinimumDiskMeasurement || measurement > Constants.Disk.MaximumDiskMeasurement
                || breakpoint.S <= 0)
                return Constants.InterpretationCodes.Uninterpretable;

            if (measurement <= breakpoint.R)
                return Constants.InterpretationCodes.Resistant;
            if (measurement >= breakpoint.S)
                return Constants.InterpretationCodes.Susceptible;
            if (breakpoint.R > 0)
                return string.IsNullOrEmpty(breakpoint.I)
                    ? Constants.InterpretationCodes.SusceptibleDoseDependent
                    : Constants.InterpretationCodes.Intermediate;
            return Constants.InterpretationCodes.NonSusceptible;
        }

        if (breakpoint.S <= 0)
            return Constants.InterpretationCodes.Uninterpretable;
        if (measurement <= breakpoint.S)
            return Constants.InterpretationCodes.Susceptible;
        if (measurement >= breakpoint.R)
            return breakpoint.R > 0
                ? Constants.InterpretationCodes.Resistant
                : Constants.InterpretationCodes.NonSusceptible;
        if (breakpoint.R > 0)
            return string.IsNullOrEmpty(breakpoint.I)
                ? Constants.InterpretationCodes.SusceptibleDoseDependent
                : Constants.InterpretationCodes.Intermediate;
        return Constants.InterpretationCodes.NonSusceptible;
    }

    private static string TestMethodFromCode(string fullAntibioticCode)
    {
        string[] components = IO_Library.SplitLine(fullAntibioticCode, Constants.Delimiters.Underscore);
        string guidelineAndMethod = components[0] == "X" ? components[2] : components[1];
        return Antibiotic.TestMethods.GetTestMethodFromCode(guidelineAndMethod[1]);
    }

    private static bool ResourceCodeMatchesBreakpointMetadata(string fullAntibioticCode, string guideline, string testMethod)
    {
        string[] components = IO_Library.SplitLine(fullAntibioticCode, Constants.Delimiters.Underscore);
        if (components.Length < 2)
            return false;

        string guidelineAndMethod = components[0] == "X"
            ? components.Length > 2 ? components[2] : string.Empty
            : components[1];
        if (guidelineAndMethod.Length < 2)
            return false;

        try
        {
            return Antibiotic.GuidelineNames.GetGuidelineFromCode(guidelineAndMethod[0]) == guideline
                && Antibiotic.TestMethods.GetTestMethodFromCode(guidelineAndMethod[1])
                    == testMethod;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static Breakpoint CreateUserBreakpointFromResource(Organism organism, Breakpoint resourceBreakpoint) => new(
        Antibiotic.GuidelineNames.UserDefined,
        resourceBreakpoint.YEAR,
        resourceBreakpoint.TEST_METHOD,
        resourceBreakpoint.POTENCY,
        organism.WHONET_ORG_CODE,
        nameof(Organism.WHONET_ORG_CODE),
        resourceBreakpoint.BREAKPOINT_TYPE,
        resourceBreakpoint.HOST,
        resourceBreakpoint.SITE_OF_INFECTION,
        resourceBreakpoint.REFERENCE_TABLE,
        resourceBreakpoint.REFERENCE_SEQUENCE,
        resourceBreakpoint.WHONET_ABX_CODE,
        resourceBreakpoint.WHONET_TEST,
        resourceBreakpoint.R,
        resourceBreakpoint.I,
        resourceBreakpoint.SDD,
        resourceBreakpoint.S,
        resourceBreakpoint.ECV_ECOFF,
        resourceBreakpoint.ECV_ECOFF_TENTATIVE,
        resourceBreakpoint.DATE_ENTERED,
        resourceBreakpoint.DATE_MODIFIED,
        resourceBreakpoint.COMMENTS);

    private static Breakpoint UserBreakpoint(string site, decimal resistant, decimal susceptible) => new(
        Antibiotic.GuidelineNames.UserDefined,
        2026,
        Antibiotic.TestMethods.MIC,
        string.Empty,
        "spn",
        nameof(Organism.WHONET_ORG_CODE),
        Breakpoint.BreakpointTypes.Human,
        "Human",
        site,
        string.Empty,
        string.Empty,
        "AMC",
        "AMC_NM",
        resistant,
        "I",
        string.Empty,
        susceptible,
        0,
        false,
        DateTime.MinValue,
        DateTime.MinValue,
        string.Empty);

    private static InterpretationConfiguration ConfigurationFor(int year) => new()
    {
        GuidelineYear = year,
        EnabledExpertInterpretationRules = [],
        UseIntrinsicResistanceRules = false,
        PrioritizedBreakpointTypes = [Breakpoint.BreakpointTypes.Human],
        PrioritizedSitesOfInfection = Constants.SitesOfInfection.DefaultOrder.ToList()
    };
}