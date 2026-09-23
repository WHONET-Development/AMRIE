using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AMR_Engine
{
    public class IsolateInterpretation
    {
        #region Constants

        /// <summary>
        /// Determine if the input matches the WHONET antibiotic column naming conventions.
        /// Example: PEN_ND10, AMP_EM, FOX_FE
        /// </summary>
        public static readonly Regex ValidAntibioticFieldNameRegex = new Regex(@"^([A-Z]{3}|X_\d+)_(" + string.Join("|", Antibiotic.GuidelineCodes.AllCodes) + @")(D.*|M|E)$",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Matches the antibiotic code portion of the WHONET antibiotic column name.
        /// The field should have already been determined to match the full ValidAntibioticFieldNameRegex
        /// before using this to extract the code portion.
        /// </summary>
        public static readonly Regex ValidAntibioticCode =
            new Regex(@"^([A-Z]{3}|X_\d+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        #endregion

        #region Properties

        private string OrganismCode { get; set; }

        private readonly Dictionary<string, AntibioticSpecificInterpretationRules> ApplicableAntibioticSpecificRules = new Dictionary<string, AntibioticSpecificInterpretationRules>();

        private readonly Dictionary<string, string> ResultInterpretations = new Dictionary<string, string>();

        private readonly Dictionary<string, string> DataRowValues;

        private readonly IEnumerable<string> AntibioticFields;

        private readonly List<string> ColumnNames;

        private readonly List<string> EnabledExpertInterpretationRules;

        #endregion

        #region Init

        /// <summary>
        /// Prepares 1 data row for interpretations.
        /// </summary>
        /// <param name="dataRowValues_"></param>
        /// <param name="columnNames_"></param>
        /// <param name="enabledExpertInterpretationRules_"></param>
        /// <param name="userDefinedBreakpoints"></param>
        /// <param name="guidelineYear"></param>
        /// <param name="prioritizedBreakpointTypes"></param>
        /// <param name="prioritizedSitesOfInfection"></param>
        public IsolateInterpretation(Dictionary<string, string> dataRowValues_, List<string> columnNames_, List<string> enabledExpertInterpretationRules_,
            List<Breakpoint> userDefinedBreakpoints,
            int guidelineYear = -1, List<string> prioritizedBreakpointTypes = null, List<string> prioritizedSitesOfInfection = null)
        {
            DataRowValues = dataRowValues_;
            ColumnNames = columnNames_;
            EnabledExpertInterpretationRules = enabledExpertInterpretationRules_;

            if (DataRowValues.ContainsKey(Constants.KeyFields.ORGANISM) && !string.IsNullOrWhiteSpace(DataRowValues[Constants.KeyFields.ORGANISM]))
                OrganismCode = DataRowValues[Constants.KeyFields.ORGANISM].Trim();
            else
                OrganismCode = string.Empty;

            AntibioticFields = DataRowValues.Keys.Where(k => ValidAntibioticFieldNameRegex.IsMatch(k));
            foreach (string fieldName in AntibioticFields)
            {
                string antibioticResult = DataRowValues[fieldName];

                AntibioticSpecificInterpretationRules thisAbxSpecificRuleSet =
                    new AntibioticSpecificInterpretationRules(OrganismCode, fieldName, antibioticResult,
                    userDefinedBreakpoints,
                    guidelineYear: guidelineYear,
                    prioritizedBreakpointTypes: prioritizedBreakpointTypes,
                    prioritizedSitesOfInfection: prioritizedSitesOfInfection);

                ApplicableAntibioticSpecificRules.Add(fieldName, thisAbxSpecificRuleSet);
            }

            // The application of these rules will evaluate the precondition antibiotics,
            // as well as overriding the interpretation of any affected antibiotics.
            GetExpertInterpretations();
        }

        #endregion

        #region Public

        /// <summary>
        /// Returns the set of interpretations for this isolate as a dictionary with the antibiotic name as the key.
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, string> GetAllInterpretations()
        {
            // Interpret the remaining antibiotics. Do not overwrite results from the expert rules.

            // All antibiotics, except those which already have an interpretation (expert rule evaluation),
            // and also excepting blanks, which don't need to be evaluated at all.
            IEnumerable<string> pendingAntibiotics =
                AntibioticFields.Except(ResultInterpretations.Keys).Except(ColumnNames.Except(DataRowValues.Keys));

            // Interpret each of these drugs using the normal intrinsic resistance and breakpoint methods.
            foreach (string fieldName in pendingAntibiotics)
                ResultInterpretations.Add(fieldName, ApplicableAntibioticSpecificRules[fieldName].GetInterpretation());

            return ResultInterpretations;
        }

        /// <summary>
        /// Returns a single interpretation
        /// </summary>
        /// <param name="interpretationConfig"></param>
        /// <param name="organismCode"></param>
        /// <param name="antibioticCode"></param>
        /// <param name="measurement"></param>
        /// <returns></returns>
        public static string GetSingleInterpretation(InterpretationConfiguration interpretationConfig,
            string organismCode, string antibioticCode, string measurement)
        {
            Dictionary<string, string> sampleRow = new Dictionary<string, string>
            {
                { Constants.KeyFields.ORGANISM, organismCode },
                { antibioticCode, measurement }
            };

            IsolateInterpretation thisInterp = new IsolateInterpretation(sampleRow,
                sampleRow.Keys.ToList(),
                interpretationConfig.EnabledExpertInterpretationRules,
                interpretationConfig.UserDefinedBreakpoints,
                Convert.ToInt32(interpretationConfig.GuidelineYear),
                 interpretationConfig.PrioritizedBreakpointTypes,
                interpretationConfig.PrioritizedSitesOfInfection);

            return thisInterp.GetSingleInterpretation(antibioticCode);
        }

        /// <summary>
        /// Strips the interpretation modifiers (!, *) leaving the text-based part only.
        /// </summary>
        /// <param name="interpretation"></param>
        /// <returns></returns>
        public static string RemoveComments(string interpretation)
        {
            return interpretation.Replace(Constants.InterpretationCodes.ExclamationPoint, string.Empty).Replace(Constants.InterpretationCodes.Asterisk, string.Empty);
        }

        #endregion

        #region Private

        /// <summary>
        /// Get the "default" interpretation for this result after applying any applicable expert rules,
        /// intrinsic resistance rules, and finally breakpoints.
        /// </summary>
        /// <param name="antibioticFullCode"></param>
        /// <returns></returns>
        private string GetSingleInterpretation(string antibioticFullCode)
        {
            if (!ResultInterpretations.ContainsKey(antibioticFullCode))
                ResultInterpretations.Add(antibioticFullCode,
                    ApplicableAntibioticSpecificRules[antibioticFullCode].GetInterpretation());

            return ResultInterpretations[antibioticFullCode];
        }

        /// <summary>
        /// If any expert rules are applicable, we need to first interpret the input antibiotics,
        /// then store the output results if applicable.
        /// </summary>
        private void GetExpertInterpretations()
        {
            List<ExpertInterpretationRule> applicableExpertRules = ExpertInterpretationRule.GetApplicableExpertRules(OrganismCode,
                DataRowValues.Keys.Where(k => ValidAntibioticFieldNameRegex.IsMatch(k)).ToList(),
                DataRowValues.Keys.Where(k => !ValidAntibioticFieldNameRegex.IsMatch(k)).ToList(),
                EnabledExpertInterpretationRules);

            if (applicableExpertRules.Count == 0)
                // No rules to evaluate.
                return;

            foreach (ExpertInterpretationRule rule in applicableExpertRules)
            {
                // If this rule contains an antibiotic precondition, we need to evaluate that result using the normal procedure.
                IEnumerable<string> unevaluatedExpertRuleDrugs =
                    rule.RULE_CRITERIA.Where(c => ValidAntibioticCode.IsMatch(c.TestName)
                    && DataRowValues.Keys.Any(k => k.StartsWith(c.TestName))).Select(c => c.TestName);

                if (rule.RULE_CRITERIA.Exists(c => c.TestName == ExpertInterpretationRule.PROF_CLASS.CEPH3))
                {
                    // Append the CEPH3 drugs which were tested.
                    unevaluatedExpertRuleDrugs =
                        unevaluatedExpertRuleDrugs.Concat(Antibiotic.CEPH3_AntibioticCodes.Where(ceph3Code =>
                        DataRowValues.Keys.Any(k => ValidAntibioticFieldNameRegex.IsMatch(k) && Antibiotic.ShortCode(k) == ceph3Code)));
                }

                // Exclude the drugs which have already been evaluated.
                unevaluatedExpertRuleDrugs = unevaluatedExpertRuleDrugs.Except(ResultInterpretations.Keys.Select(k => k.Substring(0, 3))).Distinct();

                foreach (string abxCode in unevaluatedExpertRuleDrugs)
                    // This 3-letter abx code might correspond to multiple tests which differ by method (AMP_NM, AMP_ND10, AMP_EM, AMP_ED10, etc.)
                    foreach (string fullAntibioticCode in DataRowValues.Keys.Where(k => k.StartsWith(abxCode)))
                        // The result is stored in the dictionary by this routine.
                        GetSingleInterpretation(fullAntibioticCode);

                // Evaluate the rule.
                if (rule.EvaluateCriteria(DataRowValues, ResultInterpretations))
                {
                    // Set the affected antibiotics to Resistant.
                    // Use the column names list here because DataRowValues only contains entries for fields with values for a given row,
                    // but we need to set the drugs which are listed in the file even if they aren't tested for this antibiotic.
                    IEnumerable<string> affectedAntibiotics =
                        ColumnNames.Where(k => ValidAntibioticFieldNameRegex.IsMatch(k) && rule.AFFECTED_ANTIBIOTICS.Contains(Antibiotic.ShortCode(k)));

                    foreach (string affectedAntibioticCode in affectedAntibiotics)
                    {
                        if (ResultInterpretations.ContainsKey(affectedAntibioticCode))
                        {
                            if (ResultInterpretations[affectedAntibioticCode] != Constants.InterpretationCodes.Resistant + Constants.InterpretationCodes.ExclamationPoint)
                                ResultInterpretations[affectedAntibioticCode] = Constants.InterpretationCodes.Resistant + Constants.InterpretationCodes.ExclamationPoint;
                        }
                        else if (DataRowValues.ContainsKey(affectedAntibioticCode))
                            // Don't apply the expert interpretation to this drug unless a measurement or interpretation was provided in the input data.
                            ResultInterpretations.Add(affectedAntibioticCode, Constants.InterpretationCodes.Resistant + Constants.InterpretationCodes.ExclamationPoint);
                    }
                }
            }
        }

        #endregion
    }
}
