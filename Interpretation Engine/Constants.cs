using System.Collections.ObjectModel;
using System.IO;

namespace AMR_Engine
{
	public class Constants
	{
		// This should be set to the current year when annual
		// breakpoint changes are made to the Breakpoints.txt file.
		// The minor change number below should be reset to 0 each year.
		public static readonly int BreakpointTableRevisionYear = 2026;

		// This should increase whenever an issue is resolved
		// within a year to indicate that the table itself has changed.
		// For example, if there is a document correction or error on our part.
		public static readonly int BreakpointTableRevisionMinorChangeNumber = 1;

		// We use this to locate resources.
		public static string SystemRootPath =
			Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location) + Path.DirectorySeparatorChar;

		public static readonly char Quote = '"';

		public static readonly string TwoQuotes = "\"\"";

		public class Disk
		{
			public static readonly decimal MinimumDiskMeasurement = 6M;
			public static readonly decimal MaximumDiskMeasurement = 80M;
		}

		public class MIC
		{
			public static readonly decimal MinimumMIC_Measurement = 0.0001M;
			public static readonly decimal MaximumMIC_Measurement = 2048;
		}

		public class MeasurementModifiers
		{
			public static readonly string LessThan = "<";
			public static readonly string GreaterThan = ">";
			public static readonly string EqualsSign = "=";
			public class Invalid
			{
				public static readonly string LessThanOrEqualTo = "≤";
				public static readonly string GreaterThanOrEqualTo = "≥";
			}
		}

		public class Delimiters
		{
			public static readonly string TabPlaceholder = "TAB";
			public static readonly char TabChar = '\t';
			public static readonly char CommaChar = ',';
			public static readonly char Underscore = '_';
			public static readonly char Space = ' ';
			public static readonly char EqualsSign = '=';
		}

		public class CommandLineModes
		{
			public static readonly string File = "FILE";
			public static readonly string SingleInterpretation = "SINGLE_INTERPRETATION";
		}

		public class KeyFields
		{
			public static readonly string ORGANISM = "ORGANISM";
		}

		public class InterpretationCodes
		{
			public static readonly string Uninterpretable = string.Empty;
			public static readonly string Susceptible = "S";
			public static readonly string SusceptibleDoseDependent = "SDD";
			public static readonly string NonSusceptible = "NS";
			public static readonly string Intermediate = "I";
			public static readonly string Resistant = "R";
			public static readonly string Asterisk = "*";
			public static readonly string ExclamationPoint = "!";
			public static readonly string QuestionMark = "?";
			public static readonly string WildType = "WT";
			public static readonly string NonWildType = "NWT";
			public static readonly string InRange = "IN";
			public static readonly string OutOfRange = "OUT";
		}

		public class TestResultCodes
		{
			public static readonly string Positive = "+";
			public static readonly string Negative = "-";
		}

		public class OrganismGroups
		{
			public static readonly string GramPositiveAnaerobes = "AN" + TestResultCodes.Positive;
			public static readonly string GramNegativeAnaerobes = "AN" + TestResultCodes.Negative;
			public static readonly string Anaerobes = "ANA";
			public static readonly string AnaerobePlusSubkingdomCode = nameof(Organism.ANAEROBE) + "+" + nameof(Organism.SUBKINGDOM_CODE);
		}

		public class SitesOfInfection
		{
			public const string Blank = "(Blank)";
			public const string Abscesses = "Abscesses";
			public const string Extraintestinal = "Extraintestinal";
			public const string Endocarditis = "Endocarditis";
			public const string EndocarditisWithCombinationTreatment = "Endocarditis with combination treatment";
			public const string Genital = "Genital";
			public const string InfectionsOriginatingFromTheUrinaryTract = "Infections originating from the urinary tract";
			public const string Inhaled = "Inhaled";
			public const string Intestinal = "Intestinal";
			public const string Intravenous = "Intravenous";
			public const string InvestigationalAgent = "Investigational agent";
			public const string Liposomal = "Liposomal";
			public const string MammaryGland = "Mammary gland";
			public const string Mastitis = "Mastitis";
			public const string Meningitis = "Meningitis";
			public const string Metritis = "Metritis";
			public const string NonEndocarditis = "Non-endocarditis";
			public const string NonMeningitis = "Non-meningitis";
			public const string NonPneumonia = "Non-pneumonia";
			public const string Oral = "Oral";
			public const string OtherIndications = "Other indications";
			public const string OtherInfections = "Other infections";
			public const string Parenteral = "Parenteral";
			public const string Pneumonia = "Pneumonia";
			public const string Prophylaxis = "Prophylaxis";
			public const string Respiratory = "Respiratory";
			public const string Screen = "Screen";
			public const string Skin = "Skin";
			public const string SoftTissue = "Soft tissue";
			public const string UncomplicatedUrinaryTractInfection = "Uncomplicated urinary tract infection";
			public const string Wounds = "Wounds";

			public static readonly ReadOnlyCollection<string> DefaultOrder =
				new([
					NonMeningitis,
					NonEndocarditis,
					Parenteral,
					Blank,
					UncomplicatedUrinaryTractInfection,
					InfectionsOriginatingFromTheUrinaryTract,
					Meningitis,
					Endocarditis,
					EndocarditisWithCombinationTreatment,
					Intravenous,
					Oral,
					Inhaled,
					InvestigationalAgent,
					Extraintestinal,
					Abscesses,
					Genital,
					Intestinal,
					Liposomal,
					MammaryGland,
					Mastitis,
					Metritis,
					NonPneumonia,
					OtherInfections,
					OtherIndications,
					Pneumonia,
					Prophylaxis,
					Respiratory,
					Screen,
					Skin,
					SoftTissue,
					Wounds
				]);
		}
	}
}
