using System;

namespace AMRIE.Library
{
    class AntibioticComponents
    {
        #region Constants

        private const string UserDefinedAntibiotic = "X";

        #endregion

        #region Properties

        public readonly string Code;

        public readonly string Guideline;

        public readonly string TestMethod;

        #endregion

        #region Init

        /// <summary>
        /// Break the full antimicrobial code into its components.
        /// </summary>
        /// <param name="whonetAntibioticFullCode"></param>
        public AntibioticComponents(string whonetAntibioticFullCode)
        {
            ArgumentNullException.ThrowIfNull(whonetAntibioticFullCode);

            char guidelineCode;
            string[] abxComponents = IO_Library.SplitLine(whonetAntibioticFullCode, Constants.Delimiters.Underscore);
            if (abxComponents.Length < 2)
                throw new ArgumentException($"Invalid antibiotic test code format '{whonetAntibioticFullCode}'.", nameof(whonetAntibioticFullCode));

            Code = abxComponents[0];

            if (string.Equals(Code, UserDefinedAntibiotic, StringComparison.OrdinalIgnoreCase))
            {
                // Enforce WHONET restriction: User-defined antibiotic must be X_# or X_## (1 or 2 digits, no leading zero)
                if (abxComponents.Length < 3 || abxComponents[1].Length < 1 || abxComponents[1].Length > 2
                    || abxComponents[1][0] < '1' || abxComponents[1][0] > '9'
                    || (abxComponents[1].Length == 2 && (abxComponents[1][1] < '0' || abxComponents[1][1] > '9')))
                {
                    throw new ArgumentException(
                        $"Invalid user-defined antibiotic test code '{whonetAntibioticFullCode}'. User-defined antibiotics must follow the pattern X_# or X_## (e.g., X_1 or X_12).",
                        nameof(whonetAntibioticFullCode));
                }

                Code = IO_Library.ToLine([abxComponents[0], abxComponents[1]], Constants.Delimiters.Underscore);
                if (abxComponents[2].Length < 2)
                    throw new ArgumentException($"Missing guideline and test method in '{whonetAntibioticFullCode}'.", nameof(whonetAntibioticFullCode));

                guidelineCode = abxComponents[2][0];
                TestMethod = Antibiotic.TestMethods.GetTestMethodFromCode(abxComponents[2][1]);
            }
            else
            {
                // Standard WHONET antibiotic formatting.
                if (abxComponents[1].Length < 2)
                    throw new ArgumentException($"Missing guideline and test method in '{whonetAntibioticFullCode}'.", nameof(whonetAntibioticFullCode));

                guidelineCode = abxComponents[1][0];
                TestMethod = Antibiotic.TestMethods.GetTestMethodFromCode(abxComponents[1][1]);
            }

            Guideline = Antibiotic.GuidelineNames.GetGuidelineFromCode(guidelineCode);
        }

        #endregion
    }
}
