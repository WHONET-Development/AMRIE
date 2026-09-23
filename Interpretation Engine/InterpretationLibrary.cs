using System;
using System.Text.RegularExpressions;

namespace AMR_Engine
{
    public class InterpretationLibrary
    {
        public static readonly Regex NumericAntibioticResultRegex = new Regex(@"^(|<|>)(|=)(\d+|(|\d+)\.\d+)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Try to extract the numeric value from a string result.
        /// </summary>
        /// <param name="resultString"></param>
        /// <param name="numericResultOutput"></param>
        /// <returns>True when the result could be parsed as numeric, False otherwise.</returns>
        public static bool ParseResult(string testMethod, string resultString, ref decimal numericResultOutput, ref string modifier)
        {
            // Cannot interpret an empty result.
            if (!string.IsNullOrWhiteSpace(resultString))
            {
                resultString = resultString.Trim();

                // Fix incorrect modifier orderings.
                resultString = resultString.Replace(Constants.MeasurementModifiers.EqualsSign + Constants.MeasurementModifiers.LessThan,
                    Constants.MeasurementModifiers.LessThan + Constants.MeasurementModifiers.EqualsSign);

                resultString = resultString.Replace(Constants.MeasurementModifiers.EqualsSign + Constants.MeasurementModifiers.GreaterThan,
                    Constants.MeasurementModifiers.GreaterThan + Constants.MeasurementModifiers.EqualsSign);

                // Replace invalid symbols with equivalents.
                resultString = resultString.Replace(Constants.MeasurementModifiers.Invalid.LessThanOrEqualTo,
                    Constants.MeasurementModifiers.LessThan + Constants.MeasurementModifiers.EqualsSign);

                resultString = resultString.Replace(Constants.MeasurementModifiers.Invalid.GreaterThanOrEqualTo,
                    Constants.MeasurementModifiers.GreaterThan + Constants.MeasurementModifiers.EqualsSign);

                if (NumericAntibioticResultRegex.IsMatch(resultString))
                {
                    // Parse the result depending on different requirements for disk vs MIC/ETest.
                    modifier = null;
                    decimal tempNumericResult;

                    switch (testMethod)
                    {
                        case Antibiotic.TestMethods.Disk:
                            if (!string.IsNullOrWhiteSpace(resultString)
                                && Decimal.TryParse(resultString,
                                    System.Globalization.NumberStyles.Integer,
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    out tempNumericResult)
                                && tempNumericResult >= Constants.Disk.MinimumDiskMeasurement
                                && tempNumericResult <= Constants.Disk.MaximumDiskMeasurement)
                            {
                                numericResultOutput = tempNumericResult;
                                return true;
                            }
                            break;

                        case Antibiotic.TestMethods.MIC:
                            int numericStartIndex = 0;
                            if (resultString.StartsWith(Constants.MeasurementModifiers.LessThan) || resultString.StartsWith(Constants.MeasurementModifiers.GreaterThan))
                            {
                                if (resultString.Length > 2 && resultString[1].ToString() == Constants.MeasurementModifiers.EqualsSign)
                                    // <=, >=
                                    numericStartIndex = 2;
                                else
                                    // <, >
                                    numericStartIndex = 1;

                                modifier = resultString.Substring(0, numericStartIndex);

                            }
                            else if (resultString.StartsWith(Constants.MeasurementModifiers.EqualsSign))
                            {
                                // = alone
                                modifier = resultString.Substring(0, 1);
                                numericStartIndex = 1;
                            }

                            string numericPart = resultString.Substring(numericStartIndex);
                            if (!string.IsNullOrWhiteSpace(numericPart)
                                && Decimal.TryParse(numericPart,
                                    System.Globalization.NumberStyles.Number,
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    out tempNumericResult)
                                && tempNumericResult > 0M)
                            {
                                if (string.IsNullOrWhiteSpace(modifier)
                                    || modifier == Constants.MeasurementModifiers.EqualsSign
                                    || modifier == Constants.MeasurementModifiers.LessThan + Constants.MeasurementModifiers.EqualsSign
                                    || modifier == Constants.MeasurementModifiers.GreaterThan
                                    )
                                {
                                    // No modification needed.
                                }
                                else if (modifier == Constants.MeasurementModifiers.LessThan)
                                {
                                    // <8 must be converted to <=4
                                    tempNumericResult /= 2M;
                                    modifier = Constants.MeasurementModifiers.LessThan + Constants.MeasurementModifiers.EqualsSign;
                                }
                                else if (modifier == Constants.MeasurementModifiers.GreaterThan + Constants.MeasurementModifiers.EqualsSign)
                                {
                                    // >=8 must be converted to >4
                                    tempNumericResult /= 2M;
                                    modifier = Constants.MeasurementModifiers.GreaterThan;
                                }
                                else
                                {
                                    // Unable to parse.
                                    return false;
                                }

                                numericResultOutput = tempNumericResult;
                                return true;
                            }
                            break;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Handle half-dilution rounding, which is optional. The standard behavior is to round up to the next full dilution with some exceptions.
        /// </summary>
        /// <param name="numericMeasurement"></param>
        /// <returns></returns>
        public static decimal Round_ETestHalfDilutionsUp(decimal numericMeasurement)
        {
            if (numericMeasurement == 500M || numericMeasurement == 1000M)
                // Don't round up for these fixed concentrations used for GEH, STH, and KAH
                return numericMeasurement;

            else if (numericMeasurement <= 0.001M)
                return numericMeasurement;

            else if (numericMeasurement <= 0.002M)
                return 0.002M;

            else if (numericMeasurement <= 0.004M)
                return 0.004M;

            else if (numericMeasurement <= 0.008M)
                return 0.008M;

            else if (numericMeasurement <= 0.015M)
                // Oxoid, Fisher MICE
                return 0.015M;

            else if (numericMeasurement <= 0.016M)
                return 0.016M;

            else if (numericMeasurement <= 0.03M)
                return 0.03M;

            else if (numericMeasurement <= 0.032M)
                return 0.032M;

            else if (numericMeasurement <= 0.06M)
                // Oxoid
                return 0.06M;

            else if (numericMeasurement <= 0.064M)
                return 0.064M;

            else if (numericMeasurement <= 0.12M)
                // Oxoid
                return 0.12M;

            else if (numericMeasurement <= 0.125M)
                return 0.125M;

            else if (numericMeasurement <= 0.25M)
                return 0.25M;

            else if (numericMeasurement <= 0.5M)
                return 0.5M;

            else if (numericMeasurement <= 1M)
                return 1M;

            else if (numericMeasurement <= 2M)
                return 2M;

            else if (numericMeasurement <= 4M)
                return 4M;

            else if (numericMeasurement <= 8M)
                return 8M;

            else if (numericMeasurement <= 16M)
                return 16M;

            else if (numericMeasurement <= 32M)
                return 32M;

            else if (numericMeasurement <= 64M)
                return 64M;

            else if (numericMeasurement <= 128M)
                return 128M;

            else if (numericMeasurement <= 256M)
                return 256M;

            else if (numericMeasurement <= 512M)
                return 512M;

            else if (numericMeasurement <= 1024M)
                return 1024;

            else if (numericMeasurement <= 2048M)
                return 2048;

            else if (numericMeasurement > 2048M)
                return numericMeasurement;

            else
                return numericMeasurement;
        }
    }
}
