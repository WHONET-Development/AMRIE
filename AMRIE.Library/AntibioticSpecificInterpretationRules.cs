using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AMRIE.Library
{
    public class AntibioticSpecificInterpretationRules
    {
        #region Init

        /// <summary>
        /// Populates the intrinsic resistance rules and breakpoints associated with the drug/bug combination.
        /// Use the lookup tables to collect this information so that we only need to make the determinations once.
        /// </summary>
        /// <param name="whonetOrganismCode"></param>
        /// <param name="whonetAntimicrobialFullCode"></param>
        /// <param name="antimicrobialResult_"></param>
        /// <param name="userDefinedBreakpoints"></param>
        /// <param name="guidelineYear"></param>
        /// <param name="roundHalfDilutions"></param>
        /// <param name="prioritizedBreakpointTypes"></param>
        /// <param name="prioritizedSitesOfInfection"></param>
        public AntibioticSpecificInterpretationRules(
            string whonetOrganismCode,
            string whonetAntimicrobialFullCode,
            string antimicrobialResult_,
            List<Breakpoint> userDefinedBreakpoints,
            int guidelineYear = -1,
            bool roundHalfDilutions = true,
            List<string> prioritizedBreakpointTypes = null,
            List<string> prioritizedSitesOfInfection = null,
            bool useIntrinsicResistanceRules = true)
        {
            RoundHalfDilutions = roundHalfDilutions;
            UseIntrinsicResistanceRules = useIntrinsicResistanceRules;

            AntibioticComponents thisAntibiotic = new AntibioticComponents(whonetAntimicrobialFullCode);
            AntimicrobialTestMethod = thisAntibiotic.TestMethod;

            AntimicrobialResult = antimicrobialResult_;

            InterpretationLibrary.ParseResult(AntimicrobialTestMethod, AntimicrobialResult, ref NumericResult, ref ResultModifier);

            if (useIntrinsicResistanceRules)
            {
                ArgumentNullException.ThrowIfNull(whonetOrganismCode);
                MostApplicableIntrinsicResistanceRule = IntrinsicResistanceRuleLookup.GetOrAdd(
                    (whonetOrganismCode, thisAntibiotic.Guideline, thisAntibiotic.Code),
                    (long)whonetOrganismCode.Length + thisAntibiotic.Guideline.Length + thisAntibiotic.Code.Length,
                    () => ExpectedResistancePhenotypeRule.GetApplicableExpectedResistanceRules(
                        whonetOrganismCode,
                        [thisAntibiotic.Guideline],
                        [thisAntibiotic.Code]).FirstOrDefault());
            }
            else
                MostApplicableIntrinsicResistanceRule = null;

            if (NumericResult > 0M)
            {
                MostApplicableBreakpoint =
                    DetermineMostApplicableBreakpoint(
                        userDefinedBreakpoints, guidelineYear, prioritizedBreakpointTypes, prioritizedSitesOfInfection,
                        whonetOrganismCode, thisAntibiotic.Guideline, whonetAntimicrobialFullCode);
            }
        }

        #endregion

        #region Properties

        private readonly string AntimicrobialTestMethod;

        private readonly string AntimicrobialResult;

        private readonly string ResultModifier;

        private readonly decimal NumericResult;

        private readonly bool RoundHalfDilutions;

        private readonly bool UseIntrinsicResistanceRules;

        private readonly ExpectedResistancePhenotypeRule MostApplicableIntrinsicResistanceRule;

        internal const int MaxIntrinsicCacheEntries = 131072;
        internal const long MaxIntrinsicCacheCharacters = 4194304;
        private static readonly BoundedCache<(string Organism, string Guideline, string Antibiotic), ExpectedResistancePhenotypeRule> IntrinsicResistanceRuleLookup =
            new(MaxIntrinsicCacheEntries, MaxIntrinsicCacheCharacters);

        private readonly Breakpoint MostApplicableBreakpoint;

        internal const int MaxBreakpointCacheEntries = 262144;
        internal const long MaxBreakpointCacheCharacters = 16777216;
        private static readonly BoundedCache<(string Organism, string Guideline, int Year, string SelectionKey), Breakpoint> BreakpointLookup =
            new(MaxBreakpointCacheEntries, MaxBreakpointCacheCharacters);

        internal static (BoundedCacheStatistics Breakpoints, BoundedCacheStatistics Intrinsic) CacheStatistics =>
            (BreakpointLookup.Statistics, IntrinsicResistanceRuleLookup.Statistics);

        #endregion

        #region Public

        public static void ClearBreakpoints()
        {
            BreakpointLookup.Clear();
        }

        /// <summary>
        /// Prepopulate the breakpoint cache with the "MostApplicableBreakpoint" for each combination provided.
        /// This will greatly improve performance rather than looking them up ad hoc during processing because
        /// the BreakpointLookup has to be locked (blocking the other threads) during lookup.
        /// </summary>
        /// <param name="distinctInterpretationKeys"></param>
        public static void PreheatBreakpointCache(
            List<Breakpoint> userDefinedBreakpoints,
            int guidelineYear,
            List<string> prioritizedBreakpointTypes,
            List<string> prioritizedSitesOfInfection,
            List<Tuple<string, string, string>> distinctInterpretationKeys,
            BackgroundWorker worker = null)
        {
            PreheatBreakpointCache(userDefinedBreakpoints, guidelineYear, prioritizedBreakpointTypes,
                prioritizedSitesOfInfection, distinctInterpretationKeys, worker, reportProgress: true);
        }

        internal static void PreheatBreakpointCache(
            List<Breakpoint> userDefinedBreakpoints,
            int guidelineYear,
            List<string> prioritizedBreakpointTypes,
            List<string> prioritizedSitesOfInfection,
            List<Tuple<string, string, string>> distinctInterpretationKeys,
            BackgroundWorker worker,
            bool reportProgress)
        {
            distinctInterpretationKeys = [.. distinctInterpretationKeys.Distinct()];

			object syncObject = new();
			int totalKeys = distinctInterpretationKeys.Count();
			int lastReportedProgress = 0;
			int completedKeys = 0;

            Parallel.ForEach(
                distinctInterpretationKeys,
                (key, state) =>
                {
                    if (worker != null && worker.CancellationPending)
                    {
                        state.Stop();
                        return;
                    }

                    // Evaluating the breakpoint for the side effect that the cache will be populated.
                    // We don't need a reference to the breakpoint itself here.
                    DetermineMostApplicableBreakpoint(
                        userDefinedBreakpoints,
                        guidelineYear,
                        prioritizedBreakpointTypes,
                        prioritizedSitesOfInfection,
                        key.Item1, key.Item2, key.Item3);

                    // Update the UI's progress
                    if (worker != null && reportProgress)
                    {
                        if (worker.CancellationPending)
                        {
                            state.Stop();
                            return;
                        }

                        lock (syncObject)
                        {
                            completedKeys++;
                            int currentProgress = (completedKeys * 100) / totalKeys;

                            if (currentProgress > lastReportedProgress)
                            {
                                lastReportedProgress = currentProgress;
                                worker.ReportProgress(lastReportedProgress);
                            }
                        }
                    }
                });
        }

        /// <summary>
        /// Apply any matching intrinsic resistance rules and breakpoints for this result.
        /// </summary>
        /// <returns></returns>
        public string GetInterpretation()
        {
            string intrinsicResistanceInterp = ApplyIntrinsicResistanceRules();

            if (intrinsicResistanceInterp == Constants.InterpretationCodes.Uninterpretable)
                // The drug/bug pair not intrinsically resistant. Apply the breakpoints.
                return ApplyBreakpoints();
            else
                return intrinsicResistanceInterp;
        }

        #endregion

        #region Private

        /// <summary>
        /// If this drug/bug matches an intrinsic resistance rule, then we don't need to evaluate breakpoints.
        /// </summary>
        /// <returns></returns>
        private string ApplyIntrinsicResistanceRules()
        {
            if (UseIntrinsicResistanceRules && MostApplicableIntrinsicResistanceRule != null)
                // This isolate intrinsically resistant. We don't need to apply the breakpoints.
                return Constants.InterpretationCodes.Resistant + Constants.InterpretationCodes.Asterisk;

            else
                return Constants.InterpretationCodes.Uninterpretable;
        }

        /// <summary>
        /// Evaluate the numeric measurement against the most-applicable breakpoint as determined by the users configuration and breakpoint hierarchy.
        /// </summary>
        /// <returns></returns>
        private string ApplyBreakpoints()
        {
            if (NumericResult > 0M)
            {
                if (MostApplicableBreakpoint != null)
                {
                    if (MostApplicableBreakpoint.BREAKPOINT_TYPE == Breakpoint.BreakpointTypes.ECOFF)
                    {
                        // Use the ECOFF values instead of the R, I, S columns.
                        switch (AntimicrobialTestMethod)
                        {
                            case Antibiotic.TestMethods.Disk:
                                if (NumericResult >= MostApplicableBreakpoint.ECV_ECOFF)
                                    return Constants.InterpretationCodes.WildType;
                                else
                                    return Constants.InterpretationCodes.NonWildType;

                            case Antibiotic.TestMethods.MIC:
                                // ETest also uses the MIC breakpoints.
                                decimal tempNumericResult;
                                if (RoundHalfDilutions)
                                    tempNumericResult = InterpretationLibrary.Round_ETestHalfDilutionsUp(NumericResult);
                                else
                                    tempNumericResult = NumericResult;

                                if (string.IsNullOrWhiteSpace(ResultModifier))
                                {
                                    // No > or < symbol involved.
                                    if (tempNumericResult <= MostApplicableBreakpoint.ECV_ECOFF)
                                        return Constants.InterpretationCodes.WildType;

                                    else
                                        return Constants.InterpretationCodes.NonWildType;
                                }
                                else
                                {
                                    // Must take the > or < into account.
                                    if (ResultModifier.StartsWith(Constants.MeasurementModifiers.GreaterThan))
                                    {
                                        if (tempNumericResult > MostApplicableBreakpoint.ECV_ECOFF)
                                            // Must include the question mark because of the > symbol.
                                            return Constants.InterpretationCodes.NonWildType;
                                        else
                                            return Constants.InterpretationCodes.NonWildType + Constants.InterpretationCodes.QuestionMark;
                                    }
                                    else
                                    {
                                        // Modifier begins with less than symbol.
                                        if (tempNumericResult <= MostApplicableBreakpoint.ECV_ECOFF)
                                            return Constants.InterpretationCodes.WildType;
                                        else
                                            // Must include the question mark because of the < symbol.
                                            return Constants.InterpretationCodes.WildType + Constants.InterpretationCodes.QuestionMark;
                                    }
                                }
                        }
                    }
                    else
                    {
                        // Normal human or animal breakpoint.
                        switch (AntimicrobialTestMethod)
                        {
                            case Antibiotic.TestMethods.Disk:
                                if (MostApplicableBreakpoint.S > 0M)
                                {
                                    if (NumericResult <= MostApplicableBreakpoint.R)
                                        return Constants.InterpretationCodes.Resistant;

                                    else if (NumericResult >= MostApplicableBreakpoint.S)
                                        return Constants.InterpretationCodes.Susceptible;

                                    else if (MostApplicableBreakpoint.R > 0M)
                                        if (!string.IsNullOrEmpty(MostApplicableBreakpoint.I))
                                            return Constants.InterpretationCodes.Intermediate;
                                        else
                                            return Constants.InterpretationCodes.SusceptibleDoseDependent;
                                    else
                                        return Constants.InterpretationCodes.NonSusceptible;
                                }
                                break;

                            case Antibiotic.TestMethods.MIC:
                                // ETest also uses the MIC breakpoints.
                                if (MostApplicableBreakpoint.S > 0M)
                                {
                                    decimal tempNumericResult;
                                    if (RoundHalfDilutions)
                                        tempNumericResult = InterpretationLibrary.Round_ETestHalfDilutionsUp(NumericResult);
                                    else
                                        tempNumericResult = NumericResult;

                                    if (string.IsNullOrWhiteSpace(ResultModifier) || ResultModifier == Constants.MeasurementModifiers.EqualsSign)
                                    {
                                        // No > or < symbol involved.
                                        if (tempNumericResult <= MostApplicableBreakpoint.S)
                                            return Constants.InterpretationCodes.Susceptible;

                                        else if (tempNumericResult >= MostApplicableBreakpoint.R)
                                        {
                                            if (MostApplicableBreakpoint.R > 0M)
                                                return Constants.InterpretationCodes.Resistant;
                                            else
                                                return Constants.InterpretationCodes.NonSusceptible;
                                        }
                                        else
                                        {
                                            if (!string.IsNullOrEmpty(MostApplicableBreakpoint.I))
                                                return Constants.InterpretationCodes.Intermediate;
                                            else
                                                return Constants.InterpretationCodes.SusceptibleDoseDependent;
                                        }
                                    }
                                    else
                                    {
                                        // Must take the > or < into account.
                                        if (ResultModifier.StartsWith(Constants.MeasurementModifiers.GreaterThan))
                                        {
                                            tempNumericResult *= 2M;
                                            if (tempNumericResult >= MostApplicableBreakpoint.R)
                                            {
                                                if (MostApplicableBreakpoint.R > 0M)
                                                    return Constants.InterpretationCodes.Resistant;
                                                else
                                                {
                                                    // No "R" breakpoint.
                                                    if (tempNumericResult > MostApplicableBreakpoint.S)
                                                        return Constants.InterpretationCodes.NonSusceptible;

                                                    else
                                                        // Must include the question mark because of the > symbol.
                                                        return Constants.InterpretationCodes.NonSusceptible + Constants.InterpretationCodes.QuestionMark;
                                                }
                                            }
                                            else
                                            {
                                                if (tempNumericResult > MostApplicableBreakpoint.S)
                                                    return Constants.InterpretationCodes.NonSusceptible;

                                                else
                                                    // Must include the question mark because of the > symbol.
                                                    return Constants.InterpretationCodes.Resistant + Constants.InterpretationCodes.QuestionMark;
                                            }
                                        }
                                        else
                                        {
                                            // Modifier begins with less than symbol.
                                            if (tempNumericResult <= MostApplicableBreakpoint.S)
                                                return Constants.InterpretationCodes.Susceptible;

                                            else if (MostApplicableBreakpoint.S == Constants.MIC.MinimumMIC_Measurement)
                                                if (tempNumericResult >= MostApplicableBreakpoint.R)
                                                {
                                                    if (!string.IsNullOrEmpty(MostApplicableBreakpoint.I))
                                                        return Constants.InterpretationCodes.Intermediate + Constants.InterpretationCodes.QuestionMark;
                                                    else
                                                        return Constants.InterpretationCodes.SusceptibleDoseDependent + Constants.InterpretationCodes.QuestionMark;
                                                }
                                                else
                                                {
                                                    if (!string.IsNullOrEmpty(MostApplicableBreakpoint.I))
                                                        return Constants.InterpretationCodes.Intermediate;
                                                    else
                                                        return Constants.InterpretationCodes.SusceptibleDoseDependent;
                                                }
                                            else
                                                // Must include the question mark because of the < symbol.
                                                return Constants.InterpretationCodes.Susceptible + Constants.InterpretationCodes.QuestionMark;
                                        }
                                    }
                                }
                                break;
                        }
                    }
                }
            }

            return Constants.InterpretationCodes.Uninterpretable;
        }

        private static Breakpoint DetermineMostApplicableBreakpoint(
            List<Breakpoint> userDefinedBreakpoints,
            int guidelineYear,
            List<string> prioritizedBreakpointTypes,
            List<string> prioritizedSitesOfInfection,
            string whonetOrganismCode,
            string guideline,
            string whonetAntimicrobialFullCode)
        {
            var cacheKey = (
                whonetOrganismCode,
                guideline,
                guidelineYear,
                CreateBreakpointCacheKey(
                    whonetAntimicrobialFullCode,
                    userDefinedBreakpoints,
                    prioritizedBreakpointTypes,
                    prioritizedSitesOfInfection));

            return BreakpointLookup.GetOrAdd(cacheKey,
                (long)(whonetOrganismCode?.Length ?? 0) + (guideline?.Length ?? 0) + cacheKey.Item4.Length,
                () => Breakpoint.GetApplicableBreakpoints(
                        whonetOrganismCode,
                        userDefinedBreakpoints ?? [],
                        prioritizedGuidelines: [guideline],
                        prioritizedGuidelineYears: [guidelineYear],
                        prioritizedBreakpointTypes: prioritizedBreakpointTypes,
                        prioritizedSitesOfInfection: prioritizedSitesOfInfection,
                        prioritizedWhonetAbxFullDrugCodes: [whonetAntimicrobialFullCode],
                        returnFirstBreakpointOnly: true).FirstOrDefault());
        }

        private static string CreateBreakpointCacheKey(
            string whonetAntimicrobialFullCode,
            List<Breakpoint> userDefinedBreakpoints,
            List<string> prioritizedBreakpointTypes,
            List<string> prioritizedSitesOfInfection)
        {
            StringBuilder key = new();
            AppendCacheKeyValue(key, whonetAntimicrobialFullCode);
            AppendCacheKeyList(key, prioritizedBreakpointTypes);
            AppendCacheKeyList(key, prioritizedSitesOfInfection);

            if (userDefinedBreakpoints == null)
                key.Append("-1;");
            else
            {
                key.Append(userDefinedBreakpoints.Count).Append(';');
                foreach (Breakpoint breakpoint in userDefinedBreakpoints)
                {
                    AppendCacheKeyValue(key, breakpoint.GUIDELINES);
                    key.Append(breakpoint.YEAR.ToString(CultureInfo.InvariantCulture)).Append(';');
                    AppendCacheKeyValue(key, breakpoint.TEST_METHOD);
                    AppendCacheKeyValue(key, breakpoint.ORGANISM_CODE);
                    AppendCacheKeyValue(key, breakpoint.ORGANISM_CODE_TYPE);
                    AppendCacheKeyValue(key, breakpoint.BREAKPOINT_TYPE);
                    AppendCacheKeyValue(key, breakpoint.HOST);
                    AppendCacheKeyValue(key, breakpoint.SITE_OF_INFECTION);
                    AppendCacheKeyValue(key, breakpoint.WHONET_TEST);
                    AppendCacheKeyValue(key, breakpoint.R.ToString(CultureInfo.InvariantCulture));
                    AppendCacheKeyValue(key, breakpoint.I);
                    AppendCacheKeyValue(key, breakpoint.SDD);
                    AppendCacheKeyValue(key, breakpoint.S.ToString(CultureInfo.InvariantCulture));
                    AppendCacheKeyValue(key, breakpoint.ECV_ECOFF.ToString(CultureInfo.InvariantCulture));
                }
            }

            return key.ToString();
        }

        private static void AppendCacheKeyList(StringBuilder key, List<string> values)
        {
            if (values == null)
            {
                key.Append("-1;");
                return;
            }

            key.Append(values.Count).Append(';');
            foreach (string value in values)
                AppendCacheKeyValue(key, value);
        }

        private static void AppendCacheKeyValue(StringBuilder key, string value)
        {
            if (value == null)
                key.Append("-1:");
            else
                key.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
        }
        #endregion

    }
}
