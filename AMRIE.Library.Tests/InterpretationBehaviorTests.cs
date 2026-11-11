using AMRIE.Library;

namespace AMRIE.Library.Tests;

public class InterpretationBehaviorTests
{
    [Theory]
    [InlineData("DISK", "6", true, 6, null)]
    [InlineData("DISK", "80", true, 80, null)]
    [InlineData("DISK", "5", false, 0, null)]
    [InlineData("DISK", "81", false, 0, null)]
    [InlineData("DISK", "6.5", false, 0, null)]
    [InlineData("MIC", "<=8", true, 8, "<=")]
    [InlineData("MIC", "<8", true, 4, "<=")]
    [InlineData("MIC", ">=8", true, 4, ">")]
    [InlineData("MIC", ">8", true, 8, ">")]
    [InlineData("MIC", "=8", true, 8, "=")]
    [InlineData("MIC", "0.25", true, 0.25, null)]
    [InlineData("MIC", "0.119988", true, 0.119988, null)]
    [InlineData("MIC", "0", false, 0, null)]
    [InlineData("MIC", "8mg", false, 0, "")]
    [InlineData("UNKNOWN", "8", false, 0, null)]
    public void ParseResultNormalizesSupportedMeasurements(
        string method, string raw, bool expectedSuccess, decimal expectedValue, string expectedModifier)
    {
        decimal value = 0;
        string modifier = string.Empty;

        bool success = InterpretationLibrary.ParseResult(method, raw, ref value, ref modifier);

        Assert.Equal(expectedSuccess, success);
        Assert.Equal(expectedValue, value);
        Assert.Equal(expectedModifier, modifier);
    }

    [Theory]
    [InlineData(0.001, 0.001)]
    [InlineData(0.0011, 0.002)]
    [InlineData(0.002, 0.002)]
    [InlineData(0.0021, 0.004)]
    [InlineData(0.0041, 0.008)]
    [InlineData(0.0081, 0.015)]
    [InlineData(0.0151, 0.016)]
    [InlineData(0.0161, 0.03)]
    [InlineData(0.0301, 0.032)]
    [InlineData(0.0321, 0.06)]
    [InlineData(0.0601, 0.064)]
    [InlineData(0.0641, 0.12)]
    [InlineData(0.1201, 0.125)]
    [InlineData(0.1251, 0.25)]
    [InlineData(1.1, 2)]
    [InlineData(2048.1, 2048.1)]
    [InlineData(500, 500)]
    [InlineData(1000, 1000)]
    public void EtestRoundingUsesTheNextSupportedDilution(decimal input, decimal expected)
    {
        Assert.Equal(expected, InterpretationLibrary.Round_ETestHalfDilutionsUp(input));
    }

    [Fact]
    public void DiskBreakpointBoundaryIsInclusiveAndAdjacentValuesChangeCategory()
    {
        InterpretationConfiguration configuration = ConfigurationFor(2026);

        Assert.Equal(Constants.InterpretationCodes.Resistant,
            IsolateInterpretation.GetSingleInterpretation(configuration, "psd", "OXA_ND1", "17"));
        Assert.Equal(Constants.InterpretationCodes.Susceptible,
            IsolateInterpretation.GetSingleInterpretation(configuration, "psd", "OXA_ND1", "18"));
    }

    [Fact]
    public void MicBreakpointBoundariesUseTheResourceThresholds()
    {
        InterpretationConfiguration configuration = ConfigurationFor(2026);

        Assert.Equal(Constants.InterpretationCodes.Susceptible,
            IsolateInterpretation.GetSingleInterpretation(configuration, "spn", "AMC_NM", "2"));
        Assert.Equal(Constants.InterpretationCodes.Intermediate,
            IsolateInterpretation.GetSingleInterpretation(configuration, "spn", "AMC_NM", "4"));
        Assert.Equal(Constants.InterpretationCodes.Resistant,
            IsolateInterpretation.GetSingleInterpretation(configuration, "spn", "AMC_NM", "8"));
        Assert.Equal(Constants.InterpretationCodes.Resistant,
            IsolateInterpretation.GetSingleInterpretation(configuration, "spn", "AMC_NM", ">=8"));
    }

    [Fact]
    public void MicMeasurementModifiersPreserveBoundaryUncertainty()
    {
        InterpretationConfiguration configuration = ConfigurationFor(2026);

        Assert.Equal(Constants.InterpretationCodes.Susceptible,
            IsolateInterpretation.GetSingleInterpretation(configuration, "spn", "AMC_NM", "<=2"));
        Assert.Equal(Constants.InterpretationCodes.Susceptible + Constants.InterpretationCodes.QuestionMark,
            IsolateInterpretation.GetSingleInterpretation(configuration, "spn", "AMC_NM", "<=4"));
        Assert.Equal(Constants.InterpretationCodes.Susceptible + Constants.InterpretationCodes.QuestionMark,
            IsolateInterpretation.GetSingleInterpretation(configuration, "spn", "AMC_NM", "<8"));
        Assert.Equal(Constants.InterpretationCodes.Resistant,
            IsolateInterpretation.GetSingleInterpretation(configuration, "spn", "AMC_NM", ">=8"));
    }

    [Fact]
    public void MissingAndUnknownInputsRemainUninterpretable()
    {
        InterpretationConfiguration configuration = ConfigurationFor(2026);

        Assert.Empty(IsolateInterpretation.GetSingleInterpretation(configuration, "", "AMP_NM", "4"));
        Assert.Empty(IsolateInterpretation.GetSingleInterpretation(configuration, "unknown", "AMP_NM", "4"));
        Assert.Empty(IsolateInterpretation.GetSingleInterpretation(configuration, "spn", "ZZZ_NM", "4"));
        Assert.Empty(IsolateInterpretation.GetSingleInterpretation(configuration, "spn", "AMP_NM", ""));
        Assert.Empty(IsolateInterpretation.GetSingleInterpretation(configuration, "spn", "AMP_NM", "not numeric"));
    }

    [Fact]
    public void RoundHalfDilutionsConfigurationChangesEtestInterpretation()
    {
        InterpretationConfiguration rounded = ConfigurationFor(2014);
        InterpretationConfiguration unrounded = ConfigurationFor(2014);
        rounded.RoundHalfDilutions = true;
        unrounded.RoundHalfDilutions = false;

        string roundedResult = IsolateInterpretation.GetSingleInterpretation(rounded, "cal", "MIF_EM", "0.02");
        string unroundedResult = IsolateInterpretation.GetSingleInterpretation(unrounded, "cal", "MIF_EM", "0.02");

        Assert.Equal(Constants.InterpretationCodes.Resistant, roundedResult);
        Assert.Equal(Constants.InterpretationCodes.SusceptibleDoseDependent, unroundedResult);
    }

    [Fact]
    public void IntrinsicResistanceConfigurationCanDisableIntrinsicRules()
    {
        InterpretationConfiguration enabled = ConfigurationFor(2026);
        InterpretationConfiguration disabled = ConfigurationFor(2026);
        enabled.UseIntrinsicResistanceRules = true;
        disabled.UseIntrinsicResistanceRules = false;

        string enabledResult = IsolateInterpretation.GetSingleInterpretation(enabled, "cfr", "AMP_ND10", "30");
        string disabledResult = IsolateInterpretation.GetSingleInterpretation(disabled, "cfr", "AMP_ND10", "30");

        Assert.Equal(Constants.InterpretationCodes.Resistant + Constants.InterpretationCodes.Asterisk, enabledResult);
        Assert.NotEqual(enabledResult, disabledResult);
    }

    [Fact]
    public void ConfigurationCloneRetainsAllInterpretationOptions()
    {
        InterpretationConfiguration original = ConfigurationFor(2026);
        original.RoundHalfDilutions = false;
        original.UseIntrinsicResistanceRules = false;
        original.IncludeInterpretationComments = true;
        original.EnabledExpertInterpretationRules = [ExpertInterpretationRule.RuleCodes.ICR];
        original.PrioritizedBreakpointTypes = [Breakpoint.BreakpointTypes.ECOFF, Breakpoint.BreakpointTypes.Human];

        InterpretationConfiguration clone = original.Clone();

        Assert.False(clone.RoundHalfDilutions);
        Assert.False(clone.UseIntrinsicResistanceRules);
        Assert.True(clone.IncludeInterpretationComments);
        Assert.Equal(original.EnabledExpertInterpretationRules, clone.EnabledExpertInterpretationRules);
        Assert.Equal(original.PrioritizedBreakpointTypes, clone.PrioritizedBreakpointTypes);
        Assert.NotSame(original.EnabledExpertInterpretationRules, clone.EnabledExpertInterpretationRules);
    }

    private static InterpretationConfiguration ConfigurationFor(int year) => new()
    {
        GuidelineYear = year,
        EnabledExpertInterpretationRules = [],
        PrioritizedBreakpointTypes = [Breakpoint.BreakpointTypes.Human],
        PrioritizedSitesOfInfection = Constants.SitesOfInfection.DefaultOrder.ToList()
    };
}