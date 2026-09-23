using Microsoft.VisualBasic.FileIO;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;

namespace AMR_Engine
{
    public class ExpertInterpretationRule
    {
        #region Constants

        public static readonly List<ExpertInterpretationRule> ExpertInterpretationRules = LoadExpertInterpretationRules();

        public class RuleCodes
        {
            public const string ESBL_Confirmed = "ESBL-CONFIRMED";
            public const string ESBL_Probable = "ESBL-AMPC-PROBABLE";
            public const string BLNAR = "BLNAR-HFLU";
            public const string MRStaph = "MRS";
            public const string ICR = "ICR";

            public static readonly List<string> All = new List<string>() { ESBL_Confirmed, ESBL_Probable, BLNAR, MRStaph, ICR };
        }

        public class PROF_CLASS
        {
            public const string CEPH3 = "CEPH3";
        }

        public class RuleOperators
        {
            public const string And = "AND";
            public const string Or = "OR";
        }
        #endregion

        #region Init

        public ExpertInterpretationRule(string ruleCode_, string description_, string organismCode_,
            string organismTypeCode_, List<ExpertRuleCriterion> ruleCriteria_, string criteriaOperator_,
            List<string> affectedAntibiotics_, List<string> antibioticExceptions_)
        {
            RULE_CODE = ruleCode_;
            DESCRIPTION = description_;
            ORGANISM_CODE = organismCode_;
            ORGANISM_CODE_TYPE = organismTypeCode_;
            RULE_CRITERIA = ruleCriteria_;
            CriteriaOperator = criteriaOperator_;
            AFFECTED_ANTIBIOTICS = affectedAntibiotics_;
            ANTIBIOTIC_EXCEPTIONS = antibioticExceptions_;
        }

        #endregion

        #region Properties

        public string RULE_CODE { get; set; }
        public string DESCRIPTION { get; set; }
        public string ORGANISM_CODE { get; set; }
        public string ORGANISM_CODE_TYPE { get; set; }
        public List<ExpertRuleCriterion> RULE_CRITERIA { get; set; }
        public string CriteriaOperator { get; set; }
        public List<string> AFFECTED_ANTIBIOTICS { get; set; }
        public List<string> ANTIBIOTIC_EXCEPTIONS { get; set; }

        #endregion

        #region Public

        public bool EvaluateCriteria(Dictionary<string, string> rowValues, Dictionary<string, string> resultInterpretations)
        {
            List<bool> ruleResults = new List<bool>();

            // An earlier step has already matched the organism.
            // We only need to evaluate the rule criteria.
            foreach (ExpertRuleCriterion criterion in RULE_CRITERIA)
            {
                if (IsolateInterpretation.ValidAntibioticCode.IsMatch(criterion.TestName) || criterion.TestName == PROF_CLASS.CEPH3)
                {
                    // Find all drugs which start with this code, or are one of the CEPH3 drugs if evaluating that rule.
                    IEnumerable<string> matchingAntibiotics =
                        rowValues.Keys.Where(abx =>
                            (
                                IsolateInterpretation.ValidAntibioticFieldNameRegex.IsMatch(abx)
                                && abx.StartsWith(criterion.TestName)
                            ) || (
                                criterion.TestName == PROF_CLASS.CEPH3
                                && Antibiotic.CEPH3_AntibioticCodes.Exists(ceph3Code => ceph3Code == abx.Substring(0, 3))
                            )
                        );

                    // At least one of these needs to evaluate as indicated by the rule.
                    bool criterionSatisfied = false;
                    foreach (string abx in matchingAntibiotics)
                    {
                        if (resultInterpretations.ContainsKey(abx))
                        {
                            if (criterion.TestResult == Constants.InterpretationCodes.NonSusceptible)
                            {
                                if (resultInterpretations[abx] == Constants.InterpretationCodes.Resistant
                                        || resultInterpretations[abx] == Constants.InterpretationCodes.Intermediate)
                                {
                                    criterionSatisfied = true;
                                    break;
                                }
                            }
                            else if (criterion.TestResult == Constants.InterpretationCodes.Resistant)
                            {
                                if (resultInterpretations[abx] == Constants.InterpretationCodes.Resistant)
                                {
                                    criterionSatisfied = true;
                                    break;
                                }
                            }
                        }
                    }

                    ruleResults.Add(criterionSatisfied);
                }
                else
                {
                    // Other test types.
                    if (rowValues.ContainsKey(criterion.TestName))
                        ruleResults.Add(rowValues[criterion.TestName] == criterion.TestResult);
                    else
                        // This test was not performed.
                        ruleResults.Add(false);
                }
            }

            switch (CriteriaOperator)
            {
                case RuleOperators.And:
                    return ruleResults.Count > 0 && ruleResults.All(r => r);

                case RuleOperators.Or:
                    return ruleResults.Count > 0 && ruleResults.Any(r => r);

                default:
                    return false;
            }
        }

        /// <summary>
        /// Get the applicable expert rules for this organism and field set.
        /// Given the nature of expert rules, this routine is less generic than the rest of the system.
        /// </summary>
        /// <param name="whonetOrganismCode"></param>
        /// <param name="antimicrobialCodes"></param>
        /// <param name="otherTests"></param>
        /// <returns></returns>
        public static List<ExpertInterpretationRule> GetApplicableExpertRules(
            string whonetOrganismCode, List<string> antimicrobialCodes, List<string> otherTests, List<string> enabledExpertInterpretationRules)
        {
            if (!Organism.CurrentOrganisms.ContainsKey(whonetOrganismCode))
                // Cannot determine the expert rules for an unknown organism.
                return new List<ExpertInterpretationRule>();

            Organism o = Organism.CurrentOrganisms[whonetOrganismCode];

            IEnumerable<ExpertInterpretationRule> expertRules =
                from ExpertInterpretationRule thisRule in ExpertInterpretationRules
                where enabledExpertInterpretationRules is null || enabledExpertInterpretationRules.Contains(thisRule.RULE_CODE)
                where (
                        // This section restricts to only those expert rules which match our organism at some level.
                        (!string.IsNullOrWhiteSpace(o.SEROVAR_GROUP) && thisRule.ORGANISM_CODE_TYPE == nameof(o.SEROVAR_GROUP) && o.SEROVAR_GROUP == thisRule.ORGANISM_CODE)
                        || (thisRule.ORGANISM_CODE_TYPE == nameof(o.WHONET_ORG_CODE) && o.WHONET_ORG_CODE == thisRule.ORGANISM_CODE)
                        || (!string.IsNullOrWhiteSpace(o.SPECIES_GROUP) && thisRule.ORGANISM_CODE_TYPE == nameof(o.SPECIES_GROUP) && o.SPECIES_GROUP == thisRule.ORGANISM_CODE)
                        || (!string.IsNullOrWhiteSpace(o.GENUS_CODE) && thisRule.ORGANISM_CODE_TYPE == nameof(o.GENUS_CODE) && o.GENUS_CODE == thisRule.ORGANISM_CODE)
                        || (!string.IsNullOrWhiteSpace(o.GENUS_GROUP) && thisRule.ORGANISM_CODE_TYPE == nameof(o.GENUS_GROUP) && o.GENUS_GROUP == thisRule.ORGANISM_CODE)
                        || (!string.IsNullOrWhiteSpace(o.FAMILY_CODE) && thisRule.ORGANISM_CODE_TYPE == nameof(o.FAMILY_CODE) && o.FAMILY_CODE == thisRule.ORGANISM_CODE)
                        || (
                                o.ANAEROBE
                                && thisRule.ORGANISM_CODE_TYPE == Constants.OrganismGroups.AnaerobePlusSubkingdomCode
                                && (
                                    (o.SUBKINGDOM_CODE == Constants.TestResultCodes.Positive && thisRule.ORGANISM_CODE == Constants.OrganismGroups.GramPositiveAnaerobes)
                                    || (o.SUBKINGDOM_CODE == Constants.TestResultCodes.Negative && thisRule.ORGANISM_CODE == Constants.OrganismGroups.GramNegativeAnaerobes)
                                )
                            )
                        || (o.ANAEROBE && thisRule.ORGANISM_CODE_TYPE == nameof(o.ANAEROBE) && thisRule.ORGANISM_CODE == Constants.OrganismGroups.Anaerobes)
                    )
                select thisRule;

            // Make two copies so that we can iterate over one and remove items from the other if needed.
            List<ExpertInterpretationRule> returnList = expertRules.ToList();
            List<ExpertInterpretationRule> iterationList = returnList.ToList();

            foreach (ExpertInterpretationRule thisRule in iterationList)
            {
                // An "AND" rule must have all RULE_CRITERIA fields present,
                // while the OR rule must have at least one.
                if (thisRule.CriteriaOperator == RuleOperators.And)
                {
                    if (thisRule.RULE_CODE == RuleCodes.ESBL_Probable)
                    {
                        // The "ESBL-PROBABLE" rule looks for any of the CEPH3 drugs,
                        // which are found by looking at the PROF_CLASS column of the antibiotic.
                        if (!Antibiotic.CEPH3_AntibioticCodes.Any(ceph3 => antimicrobialCodes.Any(a => ceph3 == Antibiotic.ShortCode(a))))
                            // None of the CEPH3 drugs were provided in the antimicrobial list.
                            returnList.Remove(thisRule);
                    }
                    else
                    {
                        // Other AND rules are evaluated by requiring all listed fields.
                        // For now, the only other AND rule is BLNAR.
                        if (!thisRule.RULE_CRITERIA.All(t =>
                            IsolateInterpretation.ValidAntibioticCode.IsMatch(t.TestName)
                                ? antimicrobialCodes.Any(a => t.TestName == Antibiotic.ShortCode(a))
                                : otherTests.Contains(t.TestName)))
                            // Missing a required field or antibiotic.
                            returnList.Remove(thisRule);
                    }
                }
                else
                {
                    // The OR rules need to have at least one of the fields or drugs.
                    if (!thisRule.RULE_CRITERIA.Any(t =>
                        IsolateInterpretation.ValidAntibioticCode.IsMatch(t.TestName)
                            ? antimicrobialCodes.Any(a => t.TestName == Antibiotic.ShortCode(a))
                            : otherTests.Contains(t.TestName)))
                        // None of the rules fields or antibiotics were found, so this rule is not applicable.
                        returnList.Remove(thisRule);
                }
            }

            return returnList;
        }

        /// <summary>
        /// Convert a list of expert interpretation rules into a DataTable.
        /// </summary>
        /// <param name="expertRules"></param>
        /// <returns></returns>
        public static DataTable CreateTableFromArray(List<ExpertInterpretationRule> expertRules)
        {
            DataTable expertRuleData = new DataTable();

            expertRuleData.Columns.AddRange(columns: new[] {
                new DataColumn(nameof(RULE_CODE), typeof(string)),
                new DataColumn(nameof(DESCRIPTION), typeof(string)),
                new DataColumn(nameof(ORGANISM_CODE), typeof(string)),
                new DataColumn(nameof(ORGANISM_CODE_TYPE), typeof(string)),
                new DataColumn(nameof(RULE_CRITERIA), typeof(string)),
                new DataColumn(nameof(AFFECTED_ANTIBIOTICS), typeof(string)),
                new DataColumn(nameof(ANTIBIOTIC_EXCEPTIONS), typeof(string))
            });

            foreach (ExpertInterpretationRule exp in expertRules)
            {
                DataRow newRow = expertRuleData.NewRow();

                newRow[nameof(RULE_CODE)] = exp.RULE_CODE;
                newRow[nameof(DESCRIPTION)] = exp.DESCRIPTION;
                newRow[nameof(ORGANISM_CODE)] = exp.ORGANISM_CODE;
                newRow[nameof(ORGANISM_CODE_TYPE)] = exp.ORGANISM_CODE_TYPE;
                newRow[nameof(RULE_CRITERIA)] =
                    string.Join(string.Format(" {0} ", exp.CriteriaOperator),
                    exp.RULE_CRITERIA.Select(c => string.Join(Constants.Delimiters.EqualsSign.ToString(), c.TestName, c.TestResult)));
                newRow[nameof(AFFECTED_ANTIBIOTICS)] = string.Join(", ", exp.AFFECTED_ANTIBIOTICS);
                newRow[nameof(ANTIBIOTIC_EXCEPTIONS)] = string.Join(", ", exp.ANTIBIOTIC_EXCEPTIONS);

                expertRuleData.Rows.Add(newRow);
            }
            expertRuleData.AcceptChanges();

            return expertRuleData;
        }

        #endregion

        #region Private

		/// <summary>
		/// Load the expert interpretation rules from the text file.
		/// </summary>
		/// <returns></returns>
		/// <exception cref="InvalidDataException"></exception>
		/// <exception cref="FileNotFoundException"></exception>
		private static List<ExpertInterpretationRule> LoadExpertInterpretationRules()
		{
			string expertRulesTableFile;
			string relativePath = string.Join(Path.DirectorySeparatorChar.ToString(), "Resources", "ExpertInterpretationRules.txt");
			if (string.IsNullOrWhiteSpace(Constants.SystemRootPath))
				expertRulesTableFile = relativePath;
			else
				expertRulesTableFile = string.Join(Path.DirectorySeparatorChar.ToString(), Constants.SystemRootPath, relativePath);

            if (File.Exists(expertRulesTableFile))
            {
                List<ExpertInterpretationRule> allRules =
                    new List<ExpertInterpretationRule>();

                using (TextFieldParser parser = new TextFieldParser(expertRulesTableFile, System.Text.Encoding.UTF8))
                {
                    parser.SetDelimiters(Constants.Delimiters.TabChar.ToString());
                    parser.HasFieldsEnclosedInQuotes = true;

                    string[] headers = parser.ReadFields();
                    Dictionary<string, int> headerMap = IO_Library.GetResourceHeaders(headers);

                    while (!parser.EndOfData)
                    {
                        string[] values = parser.ReadFields();

                        List<ExpertRuleCriterion> ruleCriteria = new List<ExpertRuleCriterion>();

                        // Default operator is AND.
                        string ruleOp = RuleOperators.And;

                        // Rule criteria
                        foreach (string token in IO_Library.SplitLine(values[headerMap[nameof(RULE_CRITERIA)]], Constants.Delimiters.Space))
                        {
                            if (token.Contains(Constants.Delimiters.EqualsSign))
                            {
                                // This token represents a test.
                                string[] testComponents = IO_Library.SplitLine(token, Constants.Delimiters.EqualsSign);
                                ExpertRuleCriterion thisCriterion = new ExpertRuleCriterion(testComponents[0], testComponents[1]);
                                ruleCriteria.Add(thisCriterion);
                            }
                            else if (token == RuleOperators.And || token == RuleOperators.Or)
                            {
                                // The operator used to evaluate criteria when there are more than one.
                                ruleOp = token;
                            }
                            else throw new InvalidDataException();
                        }

                        string ruleCode = values[headerMap[nameof(RULE_CODE)]];
                        List<string> affectedAntibiotics;
                        switch (ruleCode)
                        {
                            case RuleCodes.MRStaph:
                                affectedAntibiotics = Antibiotic.MRS_Antibiotics_Except_CPT_BPR;
                                break;

                            case RuleCodes.ICR:
                                affectedAntibiotics = Antibiotic.ICR_Antibiotics;
                                break;

                            default:
                                affectedAntibiotics = IO_Library.SplitLine(values[headerMap[nameof(AFFECTED_ANTIBIOTICS)]], Constants.Delimiters.CommaChar).ToList();
                                break;
                        }
                        ;

                        // Exeptions to the affected antibiotics, if any.
                        List<string> antibioticExceptions = IO_Library.SplitLine(values[headerMap[nameof(ANTIBIOTIC_EXCEPTIONS)]], Constants.Delimiters.CommaChar).ToList();

                        ExpertInterpretationRule newRule = new ExpertInterpretationRule(ruleCode, values[headerMap[nameof(DESCRIPTION)]],
                            values[headerMap[nameof(ORGANISM_CODE)]], values[headerMap[nameof(ORGANISM_CODE_TYPE)]],
                            ruleCriteria, ruleOp, affectedAntibiotics, antibioticExceptions);

                        allRules.Add(newRule);
                    }
                }

                return allRules;
            }
            else throw new FileNotFoundException(expertRulesTableFile);
        }

        #endregion
    }
}
