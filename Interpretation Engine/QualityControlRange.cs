using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AMR_Engine
{
    public class QualityControlRange
    {
        #region Constants

        public static readonly List<QualityControlRange> QualityControlRanges = LoadQualityControlRanges();

        #endregion

        #region Properties

        public readonly string GUIDELINE;
        public readonly int YEAR;
        public readonly string STRAIN;
        public readonly string REFERENCE_TABLE;
        public readonly string WHONET_ORG_CODE;
        public readonly string ANTIBIOTIC;
        public readonly string ABX_TEST;
        public readonly string WHONET_ABX_CODE;
        public readonly string METHOD;
        public readonly string MEDIUM;
        public readonly decimal MINIMUM;
        public readonly decimal MAXIMUM;
        public readonly DateTime DATE_ENTERED;
        public readonly DateTime DATE_MODIFIED;
        public readonly string COMMENTS;

        #endregion

        #region Init

        /// <summary>
        /// Used only to support static nameof()
        /// </summary>
        private QualityControlRange() { }

        public QualityControlRange(string GUIDELINE_, int YEAR_, string STRAIN_, string REFERENCE_TABLE_, string WHONET_ORG_CODE_,
            string ANTIBIOTIC_, string ABX_TEST_, string WHONET_ABX_CODE_, string METHOD_, string MEDIUM_,
            decimal MINIMUM_, decimal MAXIMUM_, DateTime DATE_ENTERED_, DateTime DATE_MODIFIED_, string COMMENTS_)
        {
            GUIDELINE = GUIDELINE_;
            YEAR = YEAR_;
            STRAIN = STRAIN_;
            REFERENCE_TABLE = REFERENCE_TABLE_;
            WHONET_ORG_CODE = WHONET_ORG_CODE_;
            ANTIBIOTIC = ANTIBIOTIC_;
            ABX_TEST = ABX_TEST_;
            WHONET_ABX_CODE = WHONET_ABX_CODE_;
            METHOD = METHOD_;
            MEDIUM = MEDIUM_;
            MINIMUM = MINIMUM_;
            MAXIMUM = MAXIMUM_;
            DATE_ENTERED = DATE_ENTERED_;
            DATE_MODIFIED = DATE_MODIFIED_;
            COMMENTS = COMMENTS_;
        }

        #endregion

        #region Public

        /// <summary>
        /// Determine the best quality control range match for the given strain and antibiotic.
        /// </summary>
        /// <param name="referenceStrain"></param>
        /// <param name="whonetAbxFullDrugCode"></param>
        /// <returns></returns>
        public static QualityControlRange GetApplicableQualityControlRange(string referenceStrain, string whonetAbxFullDrugCode)
        {
            AntibioticComponents thisAntibiotic = new AntibioticComponents(whonetAbxFullDrugCode);

            IEnumerable<QualityControlRange> matchingRanges =
                QualityControlRanges.Where(qc =>
                qc.STRAIN.ToUpperInvariant() == referenceStrain.ToUpperInvariant()
                && qc.WHONET_ABX_CODE.ToUpperInvariant() == thisAntibiotic.Code.ToUpperInvariant()
                && qc.GUIDELINE.ToUpperInvariant() == thisAntibiotic.Guideline.ToUpperInvariant()
                && qc.METHOD.ToUpperInvariant() ==
                    (thisAntibiotic.TestMethod.ToUpperInvariant() == Antibiotic.TestMethods.Disk.ToUpperInvariant() ?
                    Antibiotic.TestMethods.Disk.ToUpperInvariant() : Antibiotic.TestMethods.MIC.ToUpperInvariant()))
                .OrderByDescending(qc => qc.YEAR);

            if (matchingRanges.Any())
                return matchingRanges.First();
            else
                return null;
        }

        /// <summary>
        /// Evaluate the quality control range against this set of inputs.
        /// </summary>
        /// <param name="referenceStrain"></param>
        /// <param name="whonetAbxFullDrugCode"></param>
        /// <param name="measurement"></param>
        /// <param name="roundHalfDilutions"></param>
        /// <returns></returns>
        public static string GetQualityControlInterpretation(string referenceStrain, string whonetAbxFullDrugCode, string measurement, bool roundHalfDilutions)
        {
            if (string.IsNullOrWhiteSpace(measurement))
                return Constants.InterpretationCodes.Uninterpretable;

            QualityControlRange matchingRange = GetApplicableQualityControlRange(referenceStrain, whonetAbxFullDrugCode);

            if (matchingRange == null)
                return Constants.InterpretationCodes.Uninterpretable;

            AntibioticComponents thisAntibiotic = new AntibioticComponents(whonetAbxFullDrugCode);

            decimal numericResult = decimal.Zero;
            string modifier = string.Empty;
            InterpretationLibrary.ParseResult(thisAntibiotic.TestMethod, measurement, ref numericResult, ref modifier);

            // Make sure the value is within range.
            switch (thisAntibiotic.TestMethod)
            {
                case Antibiotic.TestMethods.Disk:
                    if (numericResult < matchingRange.MINIMUM || numericResult > matchingRange.MAXIMUM)
                        return Constants.InterpretationCodes.OutOfRange;
                    else
                        return Constants.InterpretationCodes.InRange;

                case Antibiotic.TestMethods.MIC:
                    decimal tempNumericResult;
                    if (roundHalfDilutions)
                        tempNumericResult = InterpretationLibrary.Round_ETestHalfDilutionsUp(numericResult);
                    else
                        tempNumericResult = numericResult;

                    if (string.IsNullOrWhiteSpace(modifier))
                    {
                        // No > or < symbol involved.
                        if (tempNumericResult < matchingRange.MINIMUM || tempNumericResult > matchingRange.MAXIMUM)
                            return Constants.InterpretationCodes.OutOfRange;
                        else
                            return Constants.InterpretationCodes.InRange;
                    }
                    else
                    {
                        // Must take the > or < into account.
                        if (modifier.StartsWith(Constants.MeasurementModifiers.GreaterThan))
                        {
                            tempNumericResult *= 2M;
                            if (tempNumericResult < matchingRange.MINIMUM || tempNumericResult > matchingRange.MAXIMUM)
                                return Constants.InterpretationCodes.OutOfRange;
                            else
                                return Constants.InterpretationCodes.InRange;
                        }
                        else
                        {
                            if (tempNumericResult < matchingRange.MINIMUM || tempNumericResult > matchingRange.MAXIMUM)
                                return Constants.InterpretationCodes.OutOfRange;
                            else
                                return Constants.InterpretationCodes.InRange;
                        }
                    }
            }

            return Constants.InterpretationCodes.Uninterpretable;
        }

        #endregion

        #region Private

		/// <summary>
		/// Load the quality control ranges from the text file resource.
		/// </summary>
		/// <returns></returns>
		/// <exception cref="FileNotFoundException"></exception>
		private static List<QualityControlRange> LoadQualityControlRanges()
		{
			string qualityControlTableFile;
			string relativePath = Path.Join("Resources", "QC_Ranges.txt");
			if (string.IsNullOrWhiteSpace(Constants.SystemRootPath))
				qualityControlTableFile = relativePath;
			else
				qualityControlTableFile = Path.Join(Constants.SystemRootPath, relativePath);

            if (!string.IsNullOrWhiteSpace(qualityControlTableFile) && File.Exists(qualityControlTableFile))
            {
                List<QualityControlRange> qcRanges = new List<QualityControlRange>();

                using (TextFieldParser parser = new TextFieldParser(qualityControlTableFile, System.Text.Encoding.UTF8))
                {
                    parser.SetDelimiters(Constants.Delimiters.TabChar.ToString());
                    parser.HasFieldsEnclosedInQuotes = true;

                    string[] headers = parser.ReadFields();
                    Dictionary<string, int> headerMap = IO_Library.GetResourceHeaders(headers);

                    while (!parser.EndOfData)
                    {
                        string[] values = parser.ReadFields();

                        int tempYear = 0;
                        if (!string.IsNullOrWhiteSpace(values[headerMap[nameof(YEAR)]]))
                            tempYear = int.Parse(values[headerMap[nameof(YEAR)]], System.Globalization.CultureInfo.InvariantCulture);

                        decimal tempMin = decimal.Zero;
                        if (!string.IsNullOrWhiteSpace(values[headerMap[nameof(MINIMUM)]]))
                            tempMin = decimal.Parse(values[headerMap[nameof(MINIMUM)]], System.Globalization.CultureInfo.InvariantCulture);

                        decimal tempMax = decimal.Zero;
                        if (!string.IsNullOrWhiteSpace(values[headerMap[nameof(MAXIMUM)]]))
                            tempMax = decimal.Parse(values[headerMap[nameof(MAXIMUM)]], System.Globalization.CultureInfo.InvariantCulture);

                        DateTime tempEntered = DateTime.MinValue;
                        if (!string.IsNullOrWhiteSpace(values[headerMap[nameof(DATE_ENTERED)]]))
                            tempEntered = DateTime.Parse(values[headerMap[nameof(DATE_ENTERED)]], System.Globalization.CultureInfo.InvariantCulture);

                        DateTime tempModified = DateTime.MinValue;
                        if (!string.IsNullOrWhiteSpace(values[headerMap[nameof(DATE_MODIFIED)]]))
                            tempModified = DateTime.Parse(values[headerMap[nameof(DATE_MODIFIED)]], System.Globalization.CultureInfo.InvariantCulture);

                        QualityControlRange newRange = new QualityControlRange(values[headerMap[nameof(GUIDELINE)]],
                            tempYear, values[headerMap[nameof(STRAIN)]], values[headerMap[nameof(REFERENCE_TABLE)]],
                            values[headerMap[nameof(WHONET_ORG_CODE)]], values[headerMap[nameof(ANTIBIOTIC)]],
                            values[headerMap[nameof(ABX_TEST)]], values[headerMap[nameof(WHONET_ABX_CODE)]],
                            values[headerMap[nameof(METHOD)]], values[headerMap[nameof(MEDIUM)]], tempMin,
                            tempMax, tempEntered, tempModified, values[headerMap[nameof(COMMENTS)]]);

                        qcRanges.Add(newRange);
                    }
                }

                return qcRanges;
            }
            else throw new FileNotFoundException(qualityControlTableFile);
        }

        #endregion
    }
}
