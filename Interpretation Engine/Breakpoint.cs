using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;

namespace AMR_Engine
{
    public class Breakpoint
    {
        #region Constants

        public class BreakpointTypes
        {
            public const string Human = "Human";
            public const string Animal = "Animal";
            public const string ECOFF = "ECOFF";
            public const string IntrinsicResistance = "Intrinsic";
        }

        public class SiteOfInfection
        {
            public static readonly string None = string.Empty;
            public static readonly string Abscesses = "Abscesses";
            public static readonly string Extraintestinal = "Extraintestinal";
            public static readonly string Genital = "Genital";
            public static readonly string Inhaled = "Inhaled";
            public static readonly string Intestinal = "Intestinal";
            public static readonly string Intravenous = "Intravenous";
            public static readonly string InvestigationalAgent = "Investigational agent";
            public static readonly string Liposomal = "Liposomal";
            public static readonly string MammaryGland = "Mammary gland";
            public static readonly string Meningitis = "Meningitis";
            public static readonly string Metritis = "Metritis";
            public static readonly string NonMeningitis = "Non-meningitis";
            public static readonly string NonPneumonia = "Non-pneumonia";
            public static readonly string Oral = "Oral";
            public static readonly string OtherInfections = "Other infections";
            public static readonly string Parenteral = "Parenteral";
            public static readonly string Pneumonia = "Pneumonia";
            public static readonly string Prophylaxis = "Prophylaxis";
            public static readonly string Respiratory = "Respiratory";
            public static readonly string Screen = "Screen";
            public static readonly string Skin = "Skin";
            public static readonly string SoftTissue = "Soft tissue";
            public static readonly string UTI = "Uncomplicated urinary tract infection";
            public static readonly string Wounds = "Wounds";


        }

        #endregion

        #region Static properties

		/// <summary>
		/// List of breakpoints loaded from the text file resource.
		/// </summary>
		public static readonly List<Breakpoint> Breakpoints = 
			LoadBreakpoints(Path.Join(Constants.SystemRootPath, "Resources", "Breakpoints.txt"));

        #endregion

        #region Properties

        public readonly string GUIDELINES;
        public readonly int YEAR;
        public readonly string TEST_METHOD;
        public readonly string POTENCY;
        public readonly string ORGANISM_CODE;
        public readonly string ORGANISM_CODE_TYPE;
        public readonly string BREAKPOINT_TYPE;
        public readonly string HOST;
        public readonly string SITE_OF_INFECTION;
        public readonly string[] SITES_OF_INFECTION;
        public readonly string REFERENCE_TABLE;
        public readonly string REFERENCE_SEQUENCE;
        public readonly string WHONET_ABX_CODE;
        public readonly string WHONET_TEST;
        public readonly decimal R;
        public readonly string I;
        public readonly string SDD;
        public readonly decimal S;
        public readonly decimal ECV_ECOFF;
        public readonly bool ECV_ECOFF_TENTATIVE;
        public readonly DateTime DATE_ENTERED;
        public readonly DateTime DATE_MODIFIED;
        public readonly string COMMENTS;

        #endregion

        #region Init

        /// <summary>
        /// Used only to support static nameof()
        /// </summary>
        private Breakpoint() { }

        /// <summary>
        /// Container for a single breakpoint.
        /// </summary>
        /// <param name="GUIDELINES_"></param>
        /// <param name="YEAR_"></param>
        /// <param name="TEST_METHOD_"></param>
        /// <param name="POTENCY_"></param>
        /// <param name="ORGANISM_CODE_"></param>
        /// <param name="ORGANISM_CODE_TYPE_"></param>
        /// <param name="BREAKPOINT_TYPE_"></param>
        /// <param name="HOST_"></param>
        /// <param name="SITE_OF_INFECTION_"></param>
        /// <param name="REFERENCE_TABLE_"></param>
        /// <param name="REFERENCE_SEQUENCE_"></param>
        /// <param name="WHONET_ABX_CODE_"></param>
        /// <param name="WHONET_TEST_"></param>
        /// <param name="R_"></param>
        /// <param name="I_"></param>
        /// <param name="SDD_"></param>
        /// <param name="S_"></param>
        /// <param name="ECV_ECOFF_"></param>
        /// /// <param name="ECV_ECOFF_TENTATIVE_"></param>
        /// <param name="DATE_ENTERED_"></param>
        /// <param name="DATE_MODIFIED_"></param>
        /// <param name="COMMENTS_"></param>
        public Breakpoint(string GUIDELINES_, int YEAR_, string TEST_METHOD_, string POTENCY_, string ORGANISM_CODE_,
            string ORGANISM_CODE_TYPE_, string BREAKPOINT_TYPE_, string HOST_, string SITE_OF_INFECTION_,
            string REFERENCE_TABLE_, string REFERENCE_SEQUENCE_, string WHONET_ABX_CODE_, string WHONET_TEST_, decimal R_, string I_,
            string SDD_, decimal S_, decimal ECV_ECOFF_, bool ECV_ECOFF_TENTATIVE_, DateTime DATE_ENTERED_, DateTime DATE_MODIFIED_, string COMMENTS_)
        {
            GUIDELINES = GUIDELINES_;
            YEAR = YEAR_;
            TEST_METHOD = TEST_METHOD_;
            POTENCY = POTENCY_;
            ORGANISM_CODE = ORGANISM_CODE_;
            ORGANISM_CODE_TYPE = ORGANISM_CODE_TYPE_;
            BREAKPOINT_TYPE = BREAKPOINT_TYPE_;
            HOST = HOST_;
            SITE_OF_INFECTION = SITE_OF_INFECTION_;

            SITES_OF_INFECTION = IO_Library.SplitLine(
                SITE_OF_INFECTION_,
                Constants.Delimiters.CommaChar).
                Select(s =>
                {
                    string nonBlankSite = s.Trim();

                    // Process (Blank) as the empty string.
                    if (nonBlankSite == Constants.SitesOfInfection.Blank)
                        nonBlankSite = string.Empty;

                    return nonBlankSite;
                }).ToArray();

            REFERENCE_TABLE = REFERENCE_TABLE_;
            REFERENCE_SEQUENCE = REFERENCE_SEQUENCE_;
            WHONET_ABX_CODE = WHONET_ABX_CODE_;
            WHONET_TEST = WHONET_TEST_;
            R = R_;
            I = I_;
            SDD = SDD_;
            S = S_;
            ECV_ECOFF = ECV_ECOFF_;
            ECV_ECOFF_TENTATIVE = ECV_ECOFF_TENTATIVE_;
            DATE_ENTERED = DATE_ENTERED_;
            DATE_MODIFIED = DATE_MODIFIED_;
            COMMENTS = COMMENTS_;
        }

        #endregion

        #region Public

        /// <summary>
        /// Generate a sorted list of applicable breakpoints given the arguments.
        /// This is the LINQ implementation of the Resources\Breakpoints.sql query.
        /// </summary>
        /// <param name="whonetOrganismCode"></param>
        /// <param name="prioritizedGuidelines"></param>
        /// <param name="prioritizedGuidelineYears"></param>
        /// <param name="prioritizedBreakpointTypes"></param>
        /// <param name="prioritizedSitesOfInfection"></param>
        /// <param name="prioritizedWhonetAbxFullDrugCodes"></param>
        /// <param name="returnFirstBreakpointOnly"></param>
        /// <returns></returns>
        public static List<Breakpoint> GetApplicableBreakpoints(
            string whonetOrganismCode, List<Breakpoint> userDefinedBreakpoints, List<string> prioritizedGuidelines = null,
            List<int> prioritizedGuidelineYears = null, List<string> prioritizedBreakpointTypes = null,
            List<string> prioritizedSitesOfInfection = null, List<string> prioritizedWhonetAbxFullDrugCodes = null,
            bool returnFirstBreakpointOnly = false)
        {
            if (prioritizedWhonetAbxFullDrugCodes != null && prioritizedWhonetAbxFullDrugCodes.Count() > 1 && returnFirstBreakpointOnly)
                throw new ArgumentException("There must be exactly one drug specified when only the first breakpoint is requested.");

            if (!Organism.CurrentOrganisms.ContainsKey(whonetOrganismCode))
            {
                if (Organism.MergedOrganisms.ContainsKey(whonetOrganismCode) && Organism.CurrentOrganisms.ContainsKey(Organism.MergedOrganisms[whonetOrganismCode]))
                    // This "organism" has been merged into another, usually because genetic sequencing has revealed them to be the same regardless of phenotypic differences.
                    whonetOrganismCode = Organism.MergedOrganisms[whonetOrganismCode];
                else
                    // Cannot determine breakpoints for an unknown organism.
                    return new List<Breakpoint>();
            }

            if (prioritizedSitesOfInfection is null)
                prioritizedSitesOfInfection = Constants.SitesOfInfection.DefaultOrder.ToList();

            Organism o = Organism.CurrentOrganisms[whonetOrganismCode];

            // ETest and MIC breakpoints share the same row in the Breakpoints table.
            // If ETest breakpoints were requested, we need to recode these to match the MIC rows instead.
            List<string> recodedDrugCodes = null;
            if (prioritizedWhonetAbxFullDrugCodes != null)
                // Don't change the original list.
                recodedDrugCodes = prioritizedWhonetAbxFullDrugCodes.ToList().
                    Select(abx => IsolateInterpretation.ValidAntibioticFieldNameRegex.IsMatch(abx) && abx.EndsWith("E") ? abx.Remove(abx.Length - 1) + "M" : abx).
                    Distinct().ToList();

            // Returns an ordered list of breakpoints according to a default order (most specific first),
            // or one that the caller specified through prioritized parameters.
            // Find all matches on ORGANISM_CODE_TYPE, but sort them by specificity.
            IEnumerable<Breakpoint> relevantBreakpoints =
                from Breakpoint thisBreakpoint in Breakpoints.Concat(userDefinedBreakpoints)
                where prioritizedGuidelineYears is null || prioritizedGuidelineYears.Contains(thisBreakpoint.YEAR) || thisBreakpoint.GUIDELINES == Antibiotic.GuidelineNames.UserDefined
                where prioritizedGuidelines is null || prioritizedGuidelines.Contains(thisBreakpoint.GUIDELINES) || thisBreakpoint.GUIDELINES == Antibiotic.GuidelineNames.UserDefined
                where prioritizedBreakpointTypes is null || prioritizedBreakpointTypes.Contains(thisBreakpoint.BREAKPOINT_TYPE)
                where prioritizedSitesOfInfection.Any((requestedSite) =>
                             // Handle blank sites of infection from the breakpoint with the Blank constant (which is not an empty string).
                             thisBreakpoint.SITES_OF_INFECTION.
                             Any(sitesFromBP => (requestedSite == Constants.SitesOfInfection.Blank && string.IsNullOrWhiteSpace(sitesFromBP)) || requestedSite.Equals(sitesFromBP, StringComparison.InvariantCultureIgnoreCase)))
                where recodedDrugCodes is null || recodedDrugCodes.Contains(thisBreakpoint.WHONET_TEST)
                where (
                        // This section restricts to only those breakpoints which match our organism at some level.
                        (!string.IsNullOrWhiteSpace(o.SEROVAR_GROUP) && thisBreakpoint.ORGANISM_CODE_TYPE == nameof(o.SEROVAR_GROUP) && o.SEROVAR_GROUP == thisBreakpoint.ORGANISM_CODE)
                        || (thisBreakpoint.ORGANISM_CODE_TYPE == nameof(o.WHONET_ORG_CODE) && o.WHONET_ORG_CODE == thisBreakpoint.ORGANISM_CODE)
                        || (!string.IsNullOrWhiteSpace(o.SPECIES_GROUP) && thisBreakpoint.ORGANISM_CODE_TYPE == nameof(o.SPECIES_GROUP) && o.SPECIES_GROUP == thisBreakpoint.ORGANISM_CODE)
                        || (!string.IsNullOrWhiteSpace(o.GENUS_CODE) && thisBreakpoint.ORGANISM_CODE_TYPE == nameof(o.GENUS_CODE) && o.GENUS_CODE == thisBreakpoint.ORGANISM_CODE)
                        || (!string.IsNullOrWhiteSpace(o.GENUS_GROUP) && thisBreakpoint.ORGANISM_CODE_TYPE == nameof(o.GENUS_GROUP) && o.GENUS_GROUP == thisBreakpoint.ORGANISM_CODE)
                        || (!string.IsNullOrWhiteSpace(o.FAMILY_CODE) && thisBreakpoint.ORGANISM_CODE_TYPE == nameof(o.FAMILY_CODE) && o.FAMILY_CODE == thisBreakpoint.ORGANISM_CODE)
                        || (
                                o.ANAEROBE
                                && thisBreakpoint.ORGANISM_CODE_TYPE == Constants.OrganismGroups.AnaerobePlusSubkingdomCode
                                && (
                                    (o.SUBKINGDOM_CODE == Constants.TestResultCodes.Positive && thisBreakpoint.ORGANISM_CODE == Constants.OrganismGroups.GramPositiveAnaerobes)
                                    || (o.SUBKINGDOM_CODE == Constants.TestResultCodes.Negative && thisBreakpoint.ORGANISM_CODE == Constants.OrganismGroups.GramNegativeAnaerobes)
                                )
                            )
                        || (o.ANAEROBE && thisBreakpoint.ORGANISM_CODE_TYPE == nameof(o.ANAEROBE) && thisBreakpoint.ORGANISM_CODE == Constants.OrganismGroups.Anaerobes)
                    )
                orderby (
                        // The sorting order implemented here ensures that the most specific breakpoint will appear higher on the list per set of (Drug, Method, Guideline, Year, Type, Host).
                        recodedDrugCodes is null ? thisBreakpoint.WHONET_TEST : recodedDrugCodes.IndexOf(thisBreakpoint.WHONET_TEST).ToString()
                    ), (
                        // Sort user-defined breakpoints ahead of any others.
                        thisBreakpoint.GUIDELINES == Antibiotic.GuidelineNames.UserDefined ? 0 : 1
                    ), (
                        prioritizedGuidelines is null ? thisBreakpoint.GUIDELINES : prioritizedGuidelines.IndexOf(thisBreakpoint.GUIDELINES).ToString()
                    ), (
                        prioritizedGuidelineYears is null ? -thisBreakpoint.YEAR : prioritizedGuidelineYears.IndexOf(thisBreakpoint.YEAR)
                    ), thisBreakpoint.TEST_METHOD, (
                        prioritizedBreakpointTypes is null ? (
                            thisBreakpoint.BREAKPOINT_TYPE == BreakpointTypes.Human ? 1 :
                            thisBreakpoint.BREAKPOINT_TYPE == BreakpointTypes.Animal ? 2 :
                            thisBreakpoint.BREAKPOINT_TYPE == BreakpointTypes.ECOFF ? 3 : 4
                        ) : prioritizedBreakpointTypes.IndexOf(thisBreakpoint.BREAKPOINT_TYPE)
                    ), thisBreakpoint.HOST, (
                        thisBreakpoint.ORGANISM_CODE_TYPE == nameof(o.SEROVAR_GROUP) ? 1 :
                        thisBreakpoint.ORGANISM_CODE_TYPE == nameof(o.WHONET_ORG_CODE) ? 2 :
                        thisBreakpoint.ORGANISM_CODE_TYPE == nameof(o.SPECIES_GROUP) ? 3 :
                        thisBreakpoint.ORGANISM_CODE_TYPE == nameof(o.GENUS_CODE) ? 4 :
                        thisBreakpoint.ORGANISM_CODE_TYPE == nameof(o.GENUS_GROUP) ? 5 :
                        thisBreakpoint.ORGANISM_CODE_TYPE == nameof(o.FAMILY_CODE) ? 6 :
                        thisBreakpoint.ORGANISM_CODE_TYPE == Constants.OrganismGroups.AnaerobePlusSubkingdomCode ? 7 :
                        thisBreakpoint.ORGANISM_CODE_TYPE == nameof(o.ANAEROBE) ? 8 : 9
                    ),
                    // Some breakpoints have multiple sites of infection that they apply to.
                    // We will sort this breakpoint according to the lowest index for any term in the breakpoint's site of infections.
                    // For example, if the priority is [UTI, Skin, Meningitis], then a breakpoint for meningitis alone should sort below a breakpoint for both skin and meningitis.
                    GetIndex(prioritizedSitesOfInfection, thisBreakpoint.SITE_OF_INFECTION)
                select thisBreakpoint;

            // We have found the relevant breakpoints. We must now filter them down to only the applicable breakpoints.
            List<Breakpoint> applicableBreakpoints = new List<Breakpoint>();

            if (returnFirstBreakpointOnly)
            {
                Breakpoint top = relevantBreakpoints.FirstOrDefault();

                // No breakpoints available.
                if (top == null)
                    return applicableBreakpoints;

                // No need to put the breakpoints into groups prior to selecting the top breakpoint.
                applicableBreakpoints.Add(top);
                return applicableBreakpoints;
            }

            // Group the relevant breakpoints by the following fields.
            // We want to take the most specific breakpoints for each set.
            var groups = relevantBreakpoints.GroupBy(
                bp => new
                {
                    bp.GUIDELINES,
                    bp.YEAR,
                    bp.BREAKPOINT_TYPE,
                    bp.HOST,
                    bp.WHONET_TEST
                });

            foreach (var g in groups)
            {
                // Top breakpoint is always applicable.
                Breakpoint top = g.First();
                applicableBreakpoints.Add(top);

                if (g.Count() > 1)
                {
                    foreach (Breakpoint remainingBP in g.Skip(1))
                    {
                        if (top.ORGANISM_CODE == remainingBP.ORGANISM_CODE && top.ORGANISM_CODE_TYPE == remainingBP.ORGANISM_CODE_TYPE)
                        {
                            // We want to keep all breakpoints that only differ only on the site of infection from the "top" breakpoint.
                            // We want to exclude breakpoints which are less specific than the top breakpoint, i.e. those breakpoints which apply further up the organism hierarchy.
                            // For example, we would keep all spn breakpoints for PEN_NM that differ on the site of infection,
                            // but discard any STR genus breakpoints because we have a specific match that overrides the genus breakpoint.
                            applicableBreakpoints.Add(remainingBP);
                        }
                    }
                }
            }

            return applicableBreakpoints;
        }

        /// <summary>
        /// Create a memory DataTable given a list of breakpoints.
        /// </summary>
        /// <param name="breakpoints"></param>
        /// <returns></returns>
        public static DataTable CreateTableFromArray(IEnumerable<Breakpoint> breakpoints)
        {
            DataTable breakpointData = new DataTable();

            breakpointData.Columns.AddRange(columns: new[] {
                new DataColumn(nameof(GUIDELINES), typeof(string)),
                new DataColumn(nameof(YEAR), typeof(int)),
                new DataColumn(nameof(TEST_METHOD), typeof(string)),
                new DataColumn(nameof(POTENCY), typeof(string)),
                new DataColumn(nameof(ORGANISM_CODE), typeof(string)),
                new DataColumn(nameof(ORGANISM_CODE_TYPE), typeof(string)),
                new DataColumn(nameof(BREAKPOINT_TYPE), typeof(string)),
                new DataColumn(nameof(HOST), typeof(string)),
                new DataColumn(nameof(SITE_OF_INFECTION), typeof(string)),
                new DataColumn(nameof(REFERENCE_TABLE), typeof(string)),
                new DataColumn(nameof(WHONET_ABX_CODE), typeof(string)),
                new DataColumn(nameof(WHONET_TEST), typeof(string)),
                new DataColumn(nameof(R), typeof(decimal)),
                new DataColumn(nameof(I), typeof(string)),
                new DataColumn(nameof(SDD), typeof(string)),
                new DataColumn(nameof(S), typeof(decimal)),
                new DataColumn(nameof(ECV_ECOFF), typeof(decimal)),
                new DataColumn(nameof(ECV_ECOFF_TENTATIVE), typeof(bool)),
                new DataColumn(nameof(DATE_ENTERED), typeof(DateTime)),
                new DataColumn(nameof(DATE_MODIFIED), typeof(DateTime)),
                new DataColumn(nameof(COMMENTS), typeof(string))
            });

            foreach (Breakpoint bp in breakpoints)
            {
                DataRow newRow = breakpointData.NewRow();

                newRow[nameof(GUIDELINES)] = bp.GUIDELINES;
                newRow[nameof(YEAR)] = bp.YEAR;
                newRow[nameof(TEST_METHOD)] = bp.TEST_METHOD;
                newRow[nameof(POTENCY)] = bp.POTENCY;
                newRow[nameof(ORGANISM_CODE)] = bp.ORGANISM_CODE;
                newRow[nameof(ORGANISM_CODE_TYPE)] = bp.ORGANISM_CODE_TYPE;
                newRow[nameof(BREAKPOINT_TYPE)] = bp.BREAKPOINT_TYPE;
                newRow[nameof(HOST)] = bp.HOST;
                newRow[nameof(SITE_OF_INFECTION)] = bp.SITE_OF_INFECTION;
                newRow[nameof(REFERENCE_TABLE)] = bp.REFERENCE_TABLE;
                newRow[nameof(WHONET_ABX_CODE)] = bp.WHONET_ABX_CODE;
                newRow[nameof(WHONET_TEST)] = bp.WHONET_TEST;

                if (bp.R > 0)
                    newRow[nameof(R)] = bp.R;
                else
                    newRow[nameof(R)] = DBNull.Value;

                newRow[nameof(I)] = bp.I;
                newRow[nameof(SDD)] = bp.SDD;

                if (bp.S > 0)
                    newRow[nameof(S)] = bp.S;
                else
                    newRow[nameof(S)] = DBNull.Value;

                if (bp.ECV_ECOFF > 0)
                    newRow[nameof(ECV_ECOFF)] = bp.ECV_ECOFF;
                else
                    newRow[nameof(ECV_ECOFF)] = DBNull.Value;

                newRow[nameof(ECV_ECOFF_TENTATIVE)] = bp.ECV_ECOFF_TENTATIVE;
                newRow[nameof(DATE_ENTERED)] = bp.DATE_ENTERED;
                newRow[nameof(DATE_MODIFIED)] = bp.DATE_MODIFIED;
                newRow[nameof(COMMENTS)] = bp.COMMENTS;

                breakpointData.Rows.Add(newRow);
            }
            breakpointData.AcceptChanges();

            return breakpointData;
        }

        /// <summary>
        /// Load breakpoints from the text resource file.
        /// </summary>
        /// <param name="breakpointsTableFile"></param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        public static List<Breakpoint> LoadBreakpoints(string breakpointsTableFile, bool userDefined = false)
        {
            if (!string.IsNullOrWhiteSpace(breakpointsTableFile) && File.Exists(breakpointsTableFile))
            {
                List<Breakpoint> breakpoints = new List<Breakpoint>();

                using (TextFieldParser parser = new TextFieldParser(breakpointsTableFile, System.Text.Encoding.UTF8))
                {
                    parser.SetDelimiters(Constants.Delimiters.TabChar.ToString());
                    parser.HasFieldsEnclosedInQuotes = true;

                    string[] headers = parser.ReadFields();
                    Dictionary<string, int> headerMap = IO_Library.GetResourceHeaders(headers);

                    while (!parser.EndOfData)
                    {
                        string[] values = parser.ReadFields();

                        decimal tempR = decimal.Zero;
                        if (!string.IsNullOrWhiteSpace(values[headerMap[nameof(R)]]))
                            tempR = decimal.Parse(values[headerMap[nameof(R)]], System.Globalization.CultureInfo.InvariantCulture);

                        decimal tempS = decimal.Zero;
                        if (!string.IsNullOrWhiteSpace(values[headerMap[nameof(S)]]))
                            tempS = decimal.Parse(values[headerMap[nameof(S)]], System.Globalization.CultureInfo.InvariantCulture);

                        decimal tempECV = decimal.Zero;
                        if (!string.IsNullOrWhiteSpace(values[headerMap[nameof(ECV_ECOFF)]]))
                            tempECV = decimal.Parse(values[headerMap[nameof(ECV_ECOFF)]], System.Globalization.CultureInfo.InvariantCulture);

                        DateTime tempEntered = DateTime.MinValue;
                        if (!string.IsNullOrWhiteSpace(values[headerMap[nameof(DATE_ENTERED)]]))
                            tempEntered = DateTime.Parse(values[headerMap[nameof(DATE_ENTERED)]], System.Globalization.CultureInfo.InvariantCulture);

                        DateTime tempModified = DateTime.MinValue;
                        if (!string.IsNullOrWhiteSpace(values[headerMap[nameof(DATE_MODIFIED)]]))
                            tempModified = DateTime.Parse(values[headerMap[nameof(DATE_MODIFIED)]], System.Globalization.CultureInfo.InvariantCulture);

                        // Override any text for the guidelines in user-defined breakpoint files.
                        if (userDefined)
                            values[headerMap[nameof(GUIDELINES)]] = Antibiotic.GuidelineNames.UserDefined;

                        Breakpoint newBreakpoint = new Breakpoint(values[headerMap[nameof(GUIDELINES)]], int.Parse(values[headerMap[nameof(YEAR)]], System.Globalization.CultureInfo.InvariantCulture),
                            values[headerMap[nameof(TEST_METHOD)]], values[headerMap[nameof(POTENCY)]], values[headerMap[nameof(ORGANISM_CODE)]],
                            values[headerMap[nameof(ORGANISM_CODE_TYPE)]], values[headerMap[nameof(BREAKPOINT_TYPE)]], values[headerMap[nameof(HOST)]],
                            values[headerMap[nameof(SITE_OF_INFECTION)]], values[headerMap[nameof(REFERENCE_TABLE)]], values[headerMap[nameof(REFERENCE_SEQUENCE)]],
                            values[headerMap[nameof(WHONET_ABX_CODE)]],
                            values[headerMap[nameof(WHONET_TEST)]], tempR, values[headerMap[nameof(I)]],
                            values[headerMap[nameof(SDD)]], tempS, tempECV, values[headerMap[nameof(ECV_ECOFF_TENTATIVE)]] == "X",
                            tempEntered, tempModified, values[headerMap[nameof(COMMENTS)]]);

                        breakpoints.Add(newBreakpoint);
                    }
                }

                return breakpoints;
            }
            else throw new FileNotFoundException(breakpointsTableFile);
        }

        #endregion

        #region Private

        /// <summary>
        /// We need to scan the list of breakpoint sites of infection to find the lowest sorting term on the prioritized list.
        /// </summary>
        /// <param name="prioritizedSitesOfInfection"></param>
        /// <param name="breakpointSitesOfInfection"></param>
        /// <returns></returns>
        private static int GetIndex(List<string> prioritizedSitesOfInfection, string breakpointSitesOfInfection)
        {
            List<string> breakpointSitesList =
                IO_Library.SplitLine(breakpointSitesOfInfection, Constants.Delimiters.CommaChar).
                Select((sitesFromBP) => sitesFromBP.Trim()).ToList();

            return breakpointSitesList.Min(thisBreakpointSite =>
            {
                for (int x = 0; x < prioritizedSitesOfInfection.Count; x++)
                {
                    // Process (Blank) as the empty string.
                    string thisPrioritizedSite = prioritizedSitesOfInfection[x];
                    if (thisPrioritizedSite.Equals(Constants.SitesOfInfection.Blank, StringComparison.InvariantCultureIgnoreCase))
                        thisPrioritizedSite = string.Empty;

                    if (thisPrioritizedSite.Equals(thisBreakpointSite, StringComparison.InvariantCultureIgnoreCase))
                        return x;
                }

                // If the item isn't on the list, sort it last.
                return prioritizedSitesOfInfection.Count;
            });
        }

        #endregion
    }
}
