using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;

namespace AMR_Engine
{
	public class ExpectedResistancePhenotypeRule
	{

		#region Constants

		public const string AllOrganisms = "ALL";

		public static readonly List<ExpectedResistancePhenotypeRule> ExpectedResistancePhenotypeRules = LoadExpectedResistancePhenotypeRules();

		#endregion

		#region Properties

		public readonly string GUIDELINE;
		public readonly string REFERENCE_TABLE;
		public readonly string ORGANISM_CODE;
		public readonly string ORGANISM_CODE_TYPE;
		public readonly string EXCEPTION_ORGANISM_CODE;
		public readonly string EXCEPTION_ORGANISM_CODE_TYPE;
		public readonly string ABX_CODE;
		public readonly string ABX_CODE_TYPE;
		public List<string> ANTIBIOTIC_EXCEPTIONS { get; set; }
		public readonly DateTime DATE_ENTERED;
		public readonly DateTime DATE_MODIFIED;
		public readonly string COMMENTS;

		#endregion

		#region Init

		/// <summary>
		/// Holds one "expected resistance" or "intrinsic resistance" rule.
		/// </summary>
		/// <param name="GUIDELINE_"></param>
		/// <param name="REFERENCE_TABLE_"></param>
		/// <param name="ORGANISM_CODE_"></param>
		/// <param name="ORGANISM_CODE_TYPE_"></param>
		/// <param name="EXCEPTION_ORGANISM_CODE_"></param>
		/// <param name="EXCEPTION_ORGANISM_CODE_TYPE_"></param>
		/// <param name="ABX_CODE_"></param>
		/// <param name="ABX_CODE_TYPE_"></param>
		/// <param name="ANTIBIOTIC_EXCEPTIONS_"></param>
		/// <param name="DATE_ENTERED_"></param>
		/// <param name="DATE_MODIFIED_"></param>
		/// <param name="COMMENTS_"></param>
		private ExpectedResistancePhenotypeRule(string GUIDELINE_, string REFERENCE_TABLE_, string ORGANISM_CODE_,
			string ORGANISM_CODE_TYPE_, string EXCEPTION_ORGANISM_CODE_, string EXCEPTION_ORGANISM_CODE_TYPE_,
			string ABX_CODE_, string ABX_CODE_TYPE_, List<string> ANTIBIOTIC_EXCEPTIONS_,
			DateTime DATE_ENTERED_, DateTime DATE_MODIFIED_, string COMMENTS_)
		{
			GUIDELINE = GUIDELINE_;
			REFERENCE_TABLE = REFERENCE_TABLE_;
			ORGANISM_CODE = ORGANISM_CODE_;
			ORGANISM_CODE_TYPE = ORGANISM_CODE_TYPE_;
			EXCEPTION_ORGANISM_CODE = EXCEPTION_ORGANISM_CODE_;
			EXCEPTION_ORGANISM_CODE_TYPE = EXCEPTION_ORGANISM_CODE_TYPE_;
			ABX_CODE = ABX_CODE_;
			ABX_CODE_TYPE = ABX_CODE_TYPE_;
			ANTIBIOTIC_EXCEPTIONS = ANTIBIOTIC_EXCEPTIONS_;
			DATE_ENTERED = DATE_ENTERED_;
			DATE_MODIFIED = DATE_MODIFIED_;
			COMMENTS = COMMENTS_;
		}

		#endregion

		#region Public

		/// <summary>
		/// Return the set of matching expected resistance rules.
		/// </summary>
		/// <param name="whonetOrganismCode"></param>
		/// <param name="prioritizedGuidelines"></param>
		/// <param name="antimicrobialCodes"></param>
		/// <returns></returns>
		public static IEnumerable<ExpectedResistancePhenotypeRule> GetApplicableExpectedResistanceRules(
			string whonetOrganismCode, List<string> prioritizedGuidelines = null, List<string> antimicrobialCodes = null)
		{
			Organism o;
			if (Organism.CurrentOrganisms.ContainsKey(whonetOrganismCode))
				o = Organism.CurrentOrganisms[whonetOrganismCode];

			else if (whonetOrganismCode == AllOrganisms)
				o = new Organism() { WHONET_ORG_CODE = whonetOrganismCode };

			else
			{
				// Cannot interpret an unknown organism.
				ExpectedResistancePhenotypeRule[] empty = new ExpectedResistancePhenotypeRule[0];
				return empty;
			}

			IEnumerable<ExpectedResistancePhenotypeRule> applicableExpectedResistanceRules =
				from ExpectedResistancePhenotypeRule thisRule in ExpectedResistancePhenotypeRules
				where prioritizedGuidelines is null || prioritizedGuidelines.Contains(thisRule.GUIDELINE)
				where string.IsNullOrWhiteSpace(thisRule.EXCEPTION_ORGANISM_CODE)
					|| whonetOrganismCode == AllOrganisms
					|| !(
						 (
							 !string.IsNullOrWhiteSpace(o.SEROVAR_GROUP)
							 && thisRule.EXCEPTION_ORGANISM_CODE_TYPE == nameof(Organism.SEROVAR_GROUP)
							 && IO_Library.SplitLine(thisRule.EXCEPTION_ORGANISM_CODE, Constants.Delimiters.CommaChar).Select(s => s.Trim()).Contains(o.SEROVAR_GROUP)
						 ) || (
							 thisRule.EXCEPTION_ORGANISM_CODE_TYPE == nameof(Organism.WHONET_ORG_CODE)
							 && IO_Library.SplitLine(thisRule.EXCEPTION_ORGANISM_CODE, Constants.Delimiters.CommaChar).Select(s => s.Trim()).Contains(o.WHONET_ORG_CODE)
						 ) || (
							 !string.IsNullOrWhiteSpace(o.SPECIES_GROUP)
							 && thisRule.EXCEPTION_ORGANISM_CODE_TYPE == nameof(Organism.SPECIES_GROUP)
							 && IO_Library.SplitLine(thisRule.EXCEPTION_ORGANISM_CODE, Constants.Delimiters.CommaChar).Select(s => s.Trim()).Contains(o.SPECIES_GROUP)
						 ) || (
							 !string.IsNullOrWhiteSpace(o.GENUS_CODE)
							 && thisRule.EXCEPTION_ORGANISM_CODE_TYPE == nameof(Organism.GENUS_CODE)
							 && IO_Library.SplitLine(thisRule.EXCEPTION_ORGANISM_CODE, Constants.Delimiters.CommaChar).Select(s => s.Trim()).Contains(o.GENUS_CODE)
						 ) || (
							 !string.IsNullOrWhiteSpace(o.GENUS_GROUP)
							 && thisRule.EXCEPTION_ORGANISM_CODE_TYPE == nameof(Organism.GENUS_GROUP)
							 && IO_Library.SplitLine(thisRule.EXCEPTION_ORGANISM_CODE, Constants.Delimiters.CommaChar).Select(s => s.Trim()).Contains(o.GENUS_GROUP)
						 ) || (
							 !string.IsNullOrWhiteSpace(o.FAMILY_CODE)
							 && thisRule.EXCEPTION_ORGANISM_CODE_TYPE == nameof(Organism.FAMILY_CODE)
							 && IO_Library.SplitLine(thisRule.EXCEPTION_ORGANISM_CODE, Constants.Delimiters.CommaChar).Select(s => s.Trim()).Contains(o.FAMILY_CODE)
						 ) || (
							 !string.IsNullOrWhiteSpace(o.SUBKINGDOM_CODE)
							 && thisRule.EXCEPTION_ORGANISM_CODE_TYPE == nameof(Organism.SUBKINGDOM_CODE)
							 && IO_Library.SplitLine(thisRule.EXCEPTION_ORGANISM_CODE, Constants.Delimiters.CommaChar).Select(s => s.Trim()).Contains(o.SUBKINGDOM_CODE)
						 )|| (
							 o.ANAEROBE
							 && thisRule.EXCEPTION_ORGANISM_CODE_TYPE == Constants.OrganismGroups.AnaerobePlusSubkingdomCode
							 && (
								 (
									 o.SUBKINGDOM_CODE == Constants.TestResultCodes.Positive
									 && IO_Library.SplitLine(thisRule.EXCEPTION_ORGANISM_CODE, Constants.Delimiters.CommaChar).Select(s => s.Trim()).Contains(Constants.OrganismGroups.GramPositiveAnaerobes)
								 ) || (
									 o.SUBKINGDOM_CODE == Constants.TestResultCodes.Negative
									 && IO_Library.SplitLine(thisRule.EXCEPTION_ORGANISM_CODE, Constants.Delimiters.CommaChar).Select(s => s.Trim()).Contains(Constants.OrganismGroups.GramNegativeAnaerobes)
								 )
							 )
						 ) || (
							 o.ANAEROBE
							 && thisRule.EXCEPTION_ORGANISM_CODE_TYPE == nameof(Organism.ANAEROBE)
							 && IO_Library.SplitLine(thisRule.EXCEPTION_ORGANISM_CODE, Constants.Delimiters.CommaChar).Select(s => s.Trim()).Contains(Constants.OrganismGroups.Anaerobes)
						 )
					 )
				where (
						// This section restricts to only those breakpoints which match our organism at some level.
						whonetOrganismCode == AllOrganisms
						|| (!string.IsNullOrWhiteSpace(o.SEROVAR_GROUP) && thisRule.ORGANISM_CODE_TYPE == nameof(o.SEROVAR_GROUP) && o.SEROVAR_GROUP == thisRule.ORGANISM_CODE)
						|| (thisRule.ORGANISM_CODE_TYPE == nameof(o.WHONET_ORG_CODE) && o.WHONET_ORG_CODE == thisRule.ORGANISM_CODE)
						|| (!string.IsNullOrWhiteSpace(o.SPECIES_GROUP) && thisRule.ORGANISM_CODE_TYPE == nameof(o.SPECIES_GROUP) && o.SPECIES_GROUP == thisRule.ORGANISM_CODE)
						|| (!string.IsNullOrWhiteSpace(o.GENUS_CODE) && thisRule.ORGANISM_CODE_TYPE == nameof(o.GENUS_CODE) && o.GENUS_CODE == thisRule.ORGANISM_CODE)
						|| (!string.IsNullOrWhiteSpace(o.GENUS_GROUP) && thisRule.ORGANISM_CODE_TYPE == nameof(o.GENUS_GROUP) && o.GENUS_GROUP == thisRule.ORGANISM_CODE)
						|| (!string.IsNullOrWhiteSpace(o.FAMILY_CODE) && thisRule.ORGANISM_CODE_TYPE == nameof(o.FAMILY_CODE) && o.FAMILY_CODE == thisRule.ORGANISM_CODE)
						|| (!string.IsNullOrWhiteSpace(o.SUBKINGDOM_CODE) && thisRule.ORGANISM_CODE_TYPE == nameof(o.SUBKINGDOM_CODE) && o.SUBKINGDOM_CODE == thisRule.ORGANISM_CODE)
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
				join Antibiotic thisAntibiotic in Antibiotic.AllAntibiotics
				on 1 equals 1
				where antimicrobialCodes is null || antimicrobialCodes.Contains(thisAntibiotic.WHONET_ABX_CODE)
				where 
					// We don't care about the method or potency in the context of the intrinsic resistance rule.
					(thisRule.ABX_CODE_TYPE == nameof(Antibiotic.WHONET_ABX_CODE) && thisRule.ABX_CODE == thisAntibiotic.WHONET_ABX_CODE)
					|| (thisRule.ABX_CODE_TYPE == nameof(Antibiotic.ATC_CODE) && thisAntibiotic.ATC_CODE.StartsWith(thisRule.ABX_CODE))
				where !thisRule.ANTIBIOTIC_EXCEPTIONS.Contains(thisAntibiotic.WHONET_ABX_CODE)
				orderby thisRule.GUIDELINE,
					(
						thisRule.ORGANISM_CODE_TYPE == nameof(o.SEROVAR_GROUP) ? 1 :
						thisRule.ORGANISM_CODE_TYPE == nameof(o.WHONET_ORG_CODE) ? 2 :
						thisRule.ORGANISM_CODE_TYPE == nameof(o.SPECIES_GROUP) ? 3 :
						thisRule.ORGANISM_CODE_TYPE == nameof(o.GENUS_CODE) ? 4 :
						thisRule.ORGANISM_CODE_TYPE == nameof(o.GENUS_GROUP) ? 5 :
						thisRule.ORGANISM_CODE_TYPE == nameof(o.FAMILY_CODE) ? 6 :
						thisRule.ORGANISM_CODE_TYPE == nameof(o.SUBKINGDOM_CODE) ? 7 :
						thisRule.ORGANISM_CODE_TYPE == Constants.OrganismGroups.AnaerobePlusSubkingdomCode ? 8 :
						thisRule.ORGANISM_CODE_TYPE == nameof(o.ANAEROBE) ? 9 : 10
					), (
						thisRule.ABX_CODE_TYPE == nameof(Antibiotic.WHONET_ABX_CODE) ? 1 :
						thisRule.ABX_CODE_TYPE == nameof(Antibiotic.ATC_CODE) ? 2 : 3
					),
					// Note that we use the real abx code here if the row has an ATC code.
					thisAntibiotic.WHONET_ABX_CODE
				// Take the rule, but substitute the joined antibiotic code instead of the ATC code when applicable.
				select new ExpectedResistancePhenotypeRule(thisRule.GUIDELINE, thisRule.REFERENCE_TABLE, thisRule.ORGANISM_CODE, thisRule.ORGANISM_CODE_TYPE,
					thisRule.EXCEPTION_ORGANISM_CODE, thisRule.EXCEPTION_ORGANISM_CODE_TYPE, thisAntibiotic.WHONET_ABX_CODE, thisRule.ABX_CODE_TYPE,
					thisRule.ANTIBIOTIC_EXCEPTIONS, thisRule.DATE_ENTERED, thisRule.DATE_MODIFIED, thisRule.COMMENTS);

			// Make sure at most one row is returned per GUIDELINE and ABX_CODE. This happens because we don't restrict on the
			// GUIDELINE above to prevent problems when an antibiotic doesn't have a check in the column, but it is mentioned by an expected phenotype rule.
			return applicableExpectedResistanceRules.GroupBy(x => new { guideline = x.GUIDELINE, code = x.ABX_CODE }).Select(g => g.FirstOrDefault());
		}

		/// <summary>
		/// Convert a list of expected resistance rules into a DataTable.
		/// </summary>
		/// <param name="expectedResistanceRules"></param>
		/// <returns></returns>
		public static DataTable CreateTableFromArray(IEnumerable<ExpectedResistancePhenotypeRule> expectedResistanceRules)
		{
			DataTable expectedResistanceRuleData = new DataTable();

			expectedResistanceRuleData.Columns.AddRange(columns: new[]
			{
				new DataColumn(nameof(GUIDELINE), typeof(string)),
				new DataColumn(nameof(REFERENCE_TABLE), typeof(string)),
				new DataColumn(nameof(ORGANISM_CODE), typeof(string)),
				new DataColumn(nameof(ORGANISM_CODE_TYPE), typeof(string)),
				new DataColumn(nameof(EXCEPTION_ORGANISM_CODE), typeof(string)),
				new DataColumn(nameof(EXCEPTION_ORGANISM_CODE_TYPE), typeof(string)),
				new DataColumn(nameof(ABX_CODE), typeof(string)),
				new DataColumn(nameof(ABX_CODE_TYPE), typeof(string)),
				new DataColumn(nameof(ANTIBIOTIC_EXCEPTIONS), typeof(string)),
				new DataColumn(nameof(DATE_ENTERED), typeof(DateTime)),
				new DataColumn(nameof(DATE_MODIFIED), typeof(DateTime)),
				new DataColumn(nameof(COMMENTS), typeof(string)),
			});

			foreach (ExpectedResistancePhenotypeRule rule in expectedResistanceRules)
			{
				DataRow newRow = expectedResistanceRuleData.NewRow();

				newRow[nameof(GUIDELINE)] = rule.GUIDELINE;
				newRow[nameof(REFERENCE_TABLE)] = rule.REFERENCE_TABLE;
				newRow[nameof(ORGANISM_CODE)] = rule.ORGANISM_CODE;
				newRow[nameof(ORGANISM_CODE_TYPE)] = rule.ORGANISM_CODE_TYPE;
				newRow[nameof(EXCEPTION_ORGANISM_CODE)] = rule.EXCEPTION_ORGANISM_CODE;
				newRow[nameof(EXCEPTION_ORGANISM_CODE_TYPE)] = rule.EXCEPTION_ORGANISM_CODE_TYPE;
				newRow[nameof(ABX_CODE)] = rule.ABX_CODE;
				newRow[nameof(ABX_CODE_TYPE)] = rule.ABX_CODE_TYPE;
				newRow[nameof(ANTIBIOTIC_EXCEPTIONS)] = string.Join(",", rule.ANTIBIOTIC_EXCEPTIONS);
				newRow[nameof(DATE_ENTERED)] = rule.DATE_ENTERED;
				newRow[nameof(DATE_MODIFIED)] = rule.DATE_MODIFIED;
				newRow[nameof(COMMENTS)] = rule.COMMENTS;

				expectedResistanceRuleData.Rows.Add(newRow);
			}
			expectedResistanceRuleData.AcceptChanges();

			return expectedResistanceRuleData;
		}

		#endregion

		#region Private

		/// <summary>
		/// Load the expected resistance (intrinsic resistance) rules from the text file.
		/// </summary>
		/// <returns></returns>
		/// <exception cref="FileNotFoundException"></exception>
		private static List<ExpectedResistancePhenotypeRule> LoadExpectedResistancePhenotypeRules()
		{
			string expectedResistancePhenotypesTableFile;
			string relativePath = string.Join(Path.DirectorySeparatorChar.ToString(), "Resources", "ExpectedResistancePhenotypes.txt");
			if (string.IsNullOrWhiteSpace(Constants.SystemRootPath))
				expectedResistancePhenotypesTableFile = relativePath;
			else
				expectedResistancePhenotypesTableFile = string.Join(Path.DirectorySeparatorChar.ToString(), Constants.SystemRootPath, relativePath);

			if (File.Exists(expectedResistancePhenotypesTableFile))
			{
				List<ExpectedResistancePhenotypeRule> expectedResistanceRules = 
					new List<ExpectedResistancePhenotypeRule>();


				using (TextFieldParser parser = new TextFieldParser(expectedResistancePhenotypesTableFile, System.Text.Encoding.UTF8))
				{
					parser.SetDelimiters(Constants.Delimiters.TabChar.ToString());
					parser.HasFieldsEnclosedInQuotes = true;

					string[] headers = parser.ReadFields();
					Dictionary<string, int> headerMap = IO_Library.GetResourceHeaders(headers);

					while (!parser.EndOfData)
					{
						string[] values = parser.ReadFields();

						// Exeptions to the affected antibiotics, if any.
						List<string> antibioticExceptions =
							IO_Library.SplitLine(values[headerMap[nameof(ANTIBIOTIC_EXCEPTIONS)]], Constants.Delimiters.CommaChar).ToList();

						DateTime tempEntered = DateTime.MinValue;
						if (!string.IsNullOrWhiteSpace(values[headerMap[nameof(DATE_ENTERED)]]))
							tempEntered = DateTime.Parse(values[headerMap[nameof(DATE_ENTERED)]], System.Globalization.CultureInfo.InvariantCulture);

						DateTime tempModified = DateTime.MinValue;
						if (!string.IsNullOrWhiteSpace(values[headerMap[nameof(DATE_MODIFIED)]]))
							tempModified = DateTime.Parse(values[headerMap[nameof(DATE_MODIFIED)]], System.Globalization.CultureInfo.InvariantCulture);

						ExpectedResistancePhenotypeRule newRule = new ExpectedResistancePhenotypeRule(values[headerMap[nameof(GUIDELINE)]], values[headerMap[nameof(REFERENCE_TABLE)]],
							values[headerMap[nameof(ORGANISM_CODE)]], values[headerMap[nameof(ORGANISM_CODE_TYPE)]], values[headerMap[nameof(EXCEPTION_ORGANISM_CODE)]],
							values[headerMap[nameof(EXCEPTION_ORGANISM_CODE_TYPE)]], values[headerMap[nameof(ABX_CODE)]], values[headerMap[nameof(ABX_CODE_TYPE)]],
							antibioticExceptions, tempEntered, tempModified, values[headerMap[nameof(COMMENTS)]]);

						expectedResistanceRules.Add(newRule);
					}
				}

				return expectedResistanceRules;
			}
			else throw new FileNotFoundException(expectedResistancePhenotypesTableFile);
		}

		#endregion

	}
}
