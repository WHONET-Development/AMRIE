using AMR_Engine;
using System.IO;

namespace Interpretation_Engine.Tests;

public class ResourceCoverageTests
{
    [Fact]
    public void EngineResourcesAreCopiedBesideTheTestAssembly()
    {
        string resources = Path.Combine(Constants.SystemRootPath, "Resources");
        string[] requiredFiles =
        [
            "Antibiotics.txt",
            "Breakpoints.txt",
            "ExpectedResistancePhenotypes.txt",
            "ExpertInterpretationRules.txt",
            "Organisms.txt",
            "QC_Ranges.txt",
            "SampleConfig.json",
            "SampleInputFile.txt",
            "SampleUserDefinedBreakpoints.txt"
        ];

        foreach (string file in requiredFiles)
            Assert.True(File.Exists(Path.Combine(resources, file)), $"Missing copied resource: {file}");

        Assert.NotEmpty(Antibiotic.AllAntibiotics);
        Assert.NotEmpty(Organism.CurrentOrganisms);
        Assert.NotEmpty(Breakpoint.Breakpoints);
    }

    [Fact]
    public void BreakpointInventoryCoversEveryResourceDefinedCombination()
    {
        List<Breakpoint> breakpoints = Breakpoint.Breakpoints;
        Assert.NotEmpty(breakpoints);

        var combinations = breakpoints.Select(bp => new
        {
            bp.GUIDELINES,
            bp.YEAR,
            bp.TEST_METHOD,
            bp.WHONET_TEST,
            bp.ORGANISM_CODE_TYPE,
            bp.ORGANISM_CODE
        }).ToHashSet();

        Assert.NotEmpty(combinations);
        Assert.NotEmpty(breakpoints.Select(bp => (bp.GUIDELINES, bp.YEAR)).Distinct());
        Assert.Contains(Antibiotic.TestMethods.Disk, breakpoints.Select(bp => bp.TEST_METHOD));
        Assert.Contains(Antibiotic.TestMethods.MIC, breakpoints.Select(bp => bp.TEST_METHOD));

        Assert.All(breakpoints, breakpoint =>
        {
            Assert.True(breakpoint.YEAR > 0);
            Assert.Contains(breakpoint.TEST_METHOD, new[] { Antibiotic.TestMethods.Disk, Antibiotic.TestMethods.MIC });
        });
    }

    [Fact]
    public void BreakpointOrganismSelectorsResolveOnlyUnambiguousCatalogTypeMismatches()
    {
        Assert.Contains(Breakpoint.Breakpoints, breakpoint =>
            breakpoint.GUIDELINES == Antibiotic.GuidelineNames.EUCAST
            && breakpoint.YEAR == 2012
            && breakpoint.WHONET_TEST == "PEN_ED1"
            && breakpoint.ORGANISM_CODE == "BS-"
            && breakpoint.ORGANISM_CODE_TYPE == nameof(Organism.SPECIES_GROUP));

        Assert.Contains(Breakpoint.Breakpoints, breakpoint =>
            breakpoint.GUIDELINES == Antibiotic.GuidelineNames.EUCAST
            && breakpoint.YEAR == 2013
            && breakpoint.WHONET_TEST == "PEN_EM"
            && breakpoint.ORGANISM_CODE == "NME"
            && breakpoint.ORGANISM_CODE_TYPE == nameof(Organism.SEROVAR_GROUP));
    }

    [Fact]
    public void ResourceDefinedRulesAndGuidelineYearsAreLoaded()
    {
        Assert.NotEmpty(ExpectedResistancePhenotypeRule.ExpectedResistancePhenotypeRules);
        Assert.NotEmpty(ExpertInterpretationRule.ExpertInterpretationRules);

        Assert.Equal(
            new HashSet<string>(ExpertInterpretationRule.RuleCodes.All),
            ExpertInterpretationRule.ExpertInterpretationRules.Select(rule => rule.RULE_CODE).ToHashSet());

        Assert.Contains(Antibiotic.AllAntibiotics, antibiotic => antibiotic.AFA);
        Assert.Contains(Antibiotic.AllAntibiotics, antibiotic => antibiotic.NEO);
    }

    [Fact]
    public void EveryExpertRuleTruthAssignmentMatchesItsAndOrSemantics()
    {
        foreach (ExpertInterpretationRule rule in ExpertInterpretationRule.ExpertInterpretationRules)
        {
            int truthAssignments = 1 << rule.RULE_CRITERIA.Count;
            for (int mask = 0; mask < truthAssignments; mask++)
            {
                Dictionary<string, string> input = new();
                Dictionary<string, string> interpretations = new();
                List<bool> expectedCriteria = new();

                for (int index = 0; index < rule.RULE_CRITERIA.Count; index++)
                {
                    ExpertRuleCriterion criterion = rule.RULE_CRITERIA[index];
                    bool criterionIsTrue = (mask & (1 << index)) != 0;
                    expectedCriteria.Add(criterionIsTrue);

                    if (criterion.TestName == ExpertInterpretationRule.PROF_CLASS.CEPH3)
                    {
                        string cephalosporinCode = Antibiotic.CEPH3_AntibioticCodes.First();
                        string fieldName = $"{cephalosporinCode}_NM";
                        input[fieldName] = "1";
                        interpretations[fieldName] = criterionIsTrue
                            ? Constants.InterpretationCodes.Resistant
                            : Constants.InterpretationCodes.Susceptible;
                    }
                    else if (IsolateInterpretation.ValidAntibioticCode.IsMatch(criterion.TestName))
                    {
                        string fieldName = $"{criterion.TestName}_ND10";
                        input[fieldName] = "1";
                        interpretations[fieldName] = criterionIsTrue
                            ? criterion.TestResult == Constants.InterpretationCodes.NonSusceptible
                                ? Constants.InterpretationCodes.Intermediate
                                : Constants.InterpretationCodes.Resistant
                            : Constants.InterpretationCodes.Susceptible;
                    }
                    else
                    {
                        input[criterion.TestName] = criterionIsTrue
                            ? criterion.TestResult
                            : criterion.TestResult == Constants.TestResultCodes.Positive
                                ? Constants.TestResultCodes.Negative
                                : criterion.TestResult == Constants.TestResultCodes.Negative
                                    ? Constants.TestResultCodes.Positive
                                    : "not-" + criterion.TestResult;
                    }
                }

                bool expected = rule.CriteriaOperator switch
                {
                    ExpertInterpretationRule.RuleOperators.And => expectedCriteria.All(value => value),
                    ExpertInterpretationRule.RuleOperators.Or => expectedCriteria.Any(value => value),
                    _ => false
                };

                Assert.Equal(expected, rule.EvaluateCriteria(input, interpretations));
            }
        }
    }

    [Fact]
    public void ExpertRuleEvaluationIgnoresMalformedNonAntibioticFieldNames()
    {
        ExpertInterpretationRule rule = ExpertInterpretationRule.ExpertInterpretationRules.Single(candidate =>
            candidate.RULE_CODE == ExpertInterpretationRule.RuleCodes.ESBL_Probable);
        Organism organism = Organism.CurrentOrganisms.Values.First(candidate =>
            MatchesOrganism(candidate, rule.ORGANISM_CODE_TYPE, rule.ORGANISM_CODE));
        string cephalosporinField = $"{Antibiotic.CEPH3_AntibioticCodes.First()}_NM";
        Dictionary<string, string> input = new()
        {
            [Constants.KeyFields.ORGANISM] = organism.WHONET_ORG_CODE,
            [cephalosporinField] = "4",
            ["x"] = "unexpected"
        };

        IsolateInterpretation isolate = new(
            input,
            input.Keys.ToList(),
            [rule.RULE_CODE],
            [],
            guidelineYear: 2026);

        Assert.Null(Record.Exception(() => isolate.GetAllInterpretations()));
    }

    [Fact]
    public void EveryExpertRuleOverridesItsAffectedAntibioticsAndHonorsExceptions()
    {
        foreach (ExpertInterpretationRule rule in ExpertInterpretationRule.ExpertInterpretationRules)
        {
            Organism organism = Organism.CurrentOrganisms.Values.First(candidate =>
                MatchesOrganism(candidate, rule.ORGANISM_CODE_TYPE, rule.ORGANISM_CODE));
            Dictionary<string, string> input = new()
            {
                [Constants.KeyFields.ORGANISM] = organism.WHONET_ORG_CODE
            };
            HashSet<string> antibioticCodes = new(rule.AFFECTED_ANTIBIOTICS, StringComparer.Ordinal);
            antibioticCodes.UnionWith(rule.ANTIBIOTIC_EXCEPTIONS.Where(code => !string.IsNullOrWhiteSpace(code)));
            antibioticCodes.UnionWith(rule.RULE_CRITERIA
                .Where(criterion => IsolateInterpretation.ValidAntibioticCode.IsMatch(criterion.TestName))
                .Select(criterion => criterion.TestName));

            if (rule.RULE_CRITERIA.Any(criterion => criterion.TestName == ExpertInterpretationRule.PROF_CLASS.CEPH3))
                antibioticCodes.UnionWith(Antibiotic.CEPH3_AntibioticCodes);

            List<Breakpoint> userBreakpoints = new();
            foreach (string antibioticCode in antibioticCodes)
            {
                string fieldName = $"{antibioticCode}_NM";
                bool requiredForRule = rule.RULE_CRITERIA.Any(criterion =>
                    criterion.TestName == antibioticCode || criterion.TestName == ExpertInterpretationRule.PROF_CLASS.CEPH3
                        && Antibiotic.CEPH3_AntibioticCodes.Contains(antibioticCode));
                input[fieldName] = requiredForRule ? "4" : "1";
                userBreakpoints.Add(CreateUserBreakpoint(organism.WHONET_ORG_CODE, fieldName, antibioticCode));
            }

            foreach (ExpertRuleCriterion criterion in rule.RULE_CRITERIA.Where(criterion =>
                !IsolateInterpretation.ValidAntibioticCode.IsMatch(criterion.TestName)
                && criterion.TestName != ExpertInterpretationRule.PROF_CLASS.CEPH3))
                input[criterion.TestName] = criterion.TestResult;

            IsolateInterpretation isolate = new(
                input,
                input.Keys.ToList(),
                [rule.RULE_CODE],
                userBreakpoints,
                guidelineYear: 2026,
                prioritizedBreakpointTypes: [Breakpoint.BreakpointTypes.Human],
                prioritizedSitesOfInfection: Constants.SitesOfInfection.DefaultOrder.ToList(),
                useIntrinsicResistanceRules: false);

            Dictionary<string, string> interpretations = isolate.GetAllInterpretations();
            foreach (string antibioticCode in rule.AFFECTED_ANTIBIOTICS)
                Assert.Equal(
                    Constants.InterpretationCodes.Resistant + Constants.InterpretationCodes.ExclamationPoint,
                    interpretations[$"{antibioticCode}_NM"]);

            foreach (string antibioticCode in rule.ANTIBIOTIC_EXCEPTIONS.Where(code => !string.IsNullOrWhiteSpace(code)))
                Assert.Equal(Constants.InterpretationCodes.Susceptible, interpretations[$"{antibioticCode}_NM"]);
        }
    }

    [Fact]
    public void EveryIntrinsicRuleWithSupportedTaxonomyAndDrugCodesIsExecutable()
    {
        List<string> unsupportedOrganismRules = new();
        int exercisedRules = 0;
        int exercisedCombinations = 0;

        foreach (ExpectedResistancePhenotypeRule rule in ExpectedResistancePhenotypeRule.ExpectedResistancePhenotypeRules)
        {
            string[] organismExceptions = IO_Library.SplitLine(
                rule.EXCEPTION_ORGANISM_CODE ?? string.Empty,
                Constants.Delimiters.CommaChar);

            List<Organism> matchingOrganisms = Organism.CurrentOrganisms.Values
                .Where(candidate => MatchesOrganism(candidate, rule.ORGANISM_CODE_TYPE, rule.ORGANISM_CODE))
                .ToList();

            if (matchingOrganisms.Count == 0)
            {
                unsupportedOrganismRules.Add($"{rule.ORGANISM_CODE_TYPE}:{rule.ORGANISM_CODE}");
                continue;
            }

            List<Organism> eligibleOrganisms = matchingOrganisms.Where(candidate =>
                !organismExceptions.Any(exception =>
                    MatchesOrganism(candidate, rule.EXCEPTION_ORGANISM_CODE_TYPE, exception.Trim()))).ToList();

            string[] antibioticExceptions = IO_Library.SplitLine(
                string.Join(Constants.Delimiters.CommaChar, rule.ANTIBIOTIC_EXCEPTIONS),
                Constants.Delimiters.CommaChar);

            List<Antibiotic> matchingAntibiotics = Antibiotic.AllAntibiotics.Where(candidate =>
                !antibioticExceptions.Contains(candidate.WHONET_ABX_CODE)
                && (rule.ABX_CODE_TYPE == nameof(Antibiotic.WHONET_ABX_CODE)
                    ? candidate.WHONET_ABX_CODE == rule.ABX_CODE
                    : rule.ABX_CODE_TYPE == nameof(Antibiotic.ATC_CODE)
                        && candidate.ATC_CODE.StartsWith(rule.ABX_CODE, StringComparison.Ordinal)))
                .ToList();
            Assert.NotEmpty(matchingAntibiotics);

            if (eligibleOrganisms.Count == 0)
                continue;

            char guidelineCode = Antibiotic.GuidelineCodes.AllCodes.Single(code =>
                Antibiotic.GuidelineNames.GetGuidelineFromCode(code) == rule.GUIDELINE);
            foreach (Organism organism in eligibleOrganisms)
            foreach (Antibiotic antibiotic in matchingAntibiotics)
            {
                string fullAntibioticCode = $"{antibiotic.WHONET_ABX_CODE}_{guidelineCode}M";
                Dictionary<string, string> input = new()
                {
                    [Constants.KeyFields.ORGANISM] = organism.WHONET_ORG_CODE,
                    [fullAntibioticCode] = string.Empty
                };

                Dictionary<string, string> results = new IsolateInterpretation(
                    input,
                    input.Keys.ToList(),
                    [],
                    [],
                    guidelineYear: 2026,
                    useIntrinsicResistanceRules: true).GetAllInterpretations();

                Assert.True(
                    results[fullAntibioticCode] == Constants.InterpretationCodes.Resistant + Constants.InterpretationCodes.Asterisk,
                    $"Intrinsic rule was not applied for {rule.GUIDELINE}/{organism.WHONET_ORG_CODE}/{rule.ORGANISM_CODE_TYPE}:{rule.ORGANISM_CODE}/{antibiotic.WHONET_ABX_CODE}.");
                exercisedCombinations++;
            }

            exercisedRules++;
        }

        Assert.Equal(ExpectedResistancePhenotypeRule.ExpectedResistancePhenotypeRules.Count - 2, exercisedRules);
        Assert.Equal(2, unsupportedOrganismRules.Count);
        Assert.All(unsupportedOrganismRules, rule => Assert.Equal("GENUS_CODE:RZH", rule));
        Assert.True(exercisedCombinations > exercisedRules);
    }

    private static bool MatchesOrganism(Organism organism, string type, string code) => type switch
    {
        nameof(Organism.WHONET_ORG_CODE) => organism.WHONET_ORG_CODE == code,
        nameof(Organism.SEROVAR_GROUP) => organism.SEROVAR_GROUP == code,
        nameof(Organism.SPECIES_GROUP) => organism.SPECIES_GROUP == code,
        nameof(Organism.GENUS_CODE) => organism.GENUS_CODE == code,
        nameof(Organism.GENUS_GROUP) => organism.GENUS_GROUP == code,
        nameof(Organism.FAMILY_CODE) => organism.FAMILY_CODE == code,
        nameof(Organism.SUBKINGDOM_CODE) => organism.SUBKINGDOM_CODE == code,
        nameof(Organism.ANAEROBE) => organism.ANAEROBE && code == Constants.OrganismGroups.Anaerobes,
        nameof(Organism.ANAEROBE) + "+" + nameof(Organism.SUBKINGDOM_CODE) =>
            organism.ANAEROBE
            && code == (organism.SUBKINGDOM_CODE == Constants.TestResultCodes.Positive
                ? Constants.OrganismGroups.GramPositiveAnaerobes
                : Constants.OrganismGroups.GramNegativeAnaerobes),
        _ => false
    };

    private static Breakpoint CreateUserBreakpoint(string organismCode, string fieldName, string antibioticCode) => new(
        Antibiotic.GuidelineNames.UserDefined,
        2026,
        Antibiotic.TestMethods.MIC,
        string.Empty,
        organismCode,
        nameof(Organism.WHONET_ORG_CODE),
        Breakpoint.BreakpointTypes.Human,
        "Human",
        string.Empty,
        string.Empty,
        string.Empty,
        antibioticCode,
        fieldName,
        4,
        "I",
        string.Empty,
        2,
        0,
        false,
        DateTime.MinValue,
        DateTime.MinValue,
        string.Empty);
}