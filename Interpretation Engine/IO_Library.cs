using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AMR_Engine
{
    public class IO_Library
    {
        #region Constants

        private const int MaxCanonicalizedAntibioticValueLength = 8;
        private const int MaxCanonicalizedAntibioticValues = 4096;
        private const int MaxCanonicalizedAntibioticCharacters = 65536;

        private class OutputAntibioticColumns
        {
            public const string AntibioticCode = "ANTIBIOTIC_CODE";
            public const string AntibioticMeasurement = "ANTIBIOTIC_MEASUREMENT";
            public const string AntibioticInterpretation = "ANTIBIOTIC_INTERPRETATION";

            public static readonly string[] VerticalAntibioticFields = [
                AntibioticCode,
                AntibioticMeasurement,
                AntibioticInterpretation
            ];
        }

        #endregion

        #region Public

        /// <summary>
        /// Process an entire data file, generating an output file with the interpretations.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="e"></param>
        public static void InterpretDataFile(object s, DoWorkEventArgs e)
        {
            FileInterpretationParameters arguments = (FileInterpretationParameters)e.Argument;
            arguments.Worker?.ReportProgress(0);

            InterpretationConfiguration interpretationConfig =
                InterpretationConfiguration.ReadConfiguration(arguments.ConfigFile);

            int headerLineNumber = 0;
            List<string> inputColumnNames = new List<string>();
            List<Dictionary<string, string>> rowValueSets = new List<Dictionary<string, string>>();
            LoadInputFile(e, arguments, ref headerLineNumber, ref inputColumnNames, rowValueSets);

            Tuple<Dictionary<string, string>, Dictionary<string, string>>[] interpretationResults =
                InterpretIsolates(e, arguments, interpretationConfig, inputColumnNames, rowValueSets);

            GenerateOutputFile(e, arguments, interpretationConfig, inputColumnNames, interpretationResults);  
        }

        /// <summary>
        /// Get the headers for the a resource file header line provided.
        /// </summary>
        /// <param name="headerLine"></param>
        /// <returns></returns>
        public static Dictionary<string, int> GetResourceHeaders(string headerLine)
        {
            Dictionary<string, int> headerMap = new Dictionary<string, int>();
            string[] headerValues = SplitLine(headerLine, Constants.Delimiters.TabChar);
            for (int i = 0; i < headerValues.Length; i++)
                headerMap.Add(headerValues[i], i);

            return headerMap;
        }

        /// <summary>
        /// Get the headers for the a resource file header line provided.
        /// </summary>
        /// <param name="headerLine"></param>
        /// <returns></returns>
        public static Dictionary<string, int> GetResourceHeaders(string[] headers)
        {
            Dictionary<string, int> headerMap = new Dictionary<string, int>();
            for (int i = 0; i < headers.Length; i++)
                headerMap.Add(headers[i], i);

            return headerMap;
        }


		public static string[] SplitLine(string record, char delimiter)
		{
            if (string.IsNullOrEmpty(record) || (!record.Contains(delimiter) && !record.Contains(Constants.Quote)))
				return [record];

            List<string> results = new List<string>();
            StringBuilder field = new StringBuilder();
            bool inQualifier = false;
            bool fieldWasQuoted = false;
            bool afterClosingQuote = false;

            void AddField()
            {
                string value = field.ToString();
                results.Add(fieldWasQuoted ? value : value.Trim());
                field.Clear();
                fieldWasQuoted = false;
                afterClosingQuote = false;
            }

            for (int idx = 0; idx < record.Length; idx++)
            {
                char current = record[idx];

                if (inQualifier)
                {
                    if (current == Constants.Quote)
                    {
                        if (idx + 1 < record.Length && record[idx + 1] == Constants.Quote)
                        {
                            field.Append(Constants.Quote);
                            idx++;
                        }
                        else
                        {
                            inQualifier = false;
                            afterClosingQuote = true;
                        }
                    }
                    else
                        field.Append(current);
                }
                else if (current == delimiter)
                    AddField();
                else if (afterClosingQuote && char.IsWhiteSpace(current))
                    continue;
                else if (current == Constants.Quote && field.ToString().Trim().Length == 0)
                {
                    field.Clear();
                    fieldWasQuoted = true;
                    inQualifier = true;
                }
                else
                {
                    afterClosingQuote = false;
                    field.Append(current);
                }
            }

            AddField();
            return results.ToArray();
        }

		public static string ToLine(IEnumerable<string> values, char delimiter)
		{
			return string.Join(delimiter, values.Select(v =>
			{
                if (string.IsNullOrEmpty(v) ||
                    (!v.Contains(delimiter) && !v.Contains(Constants.Quote) && !v.Contains('\r') && !v.Contains('\n')
                    && !char.IsWhiteSpace(v[0]) && !char.IsWhiteSpace(v[^1])))
					// This string doesn't need to be quoted.
					return v;

                else
                {
                    // Escape any existing quotes in the string.
                    if (v.Contains(Constants.Quote))
                        v = v.Replace(Constants.Quote.ToString(), Constants.TwoQuotes);

                    return Constants.Quote + v + Constants.Quote;
                }
            }));
        }

        #endregion

        #region Private

        /// <summary>
        /// Convert the input file into a list of dictionaries for each row to facilitate processing.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="arguments"></param>
        /// <param name="headerLineNumber"></param>
        /// <param name="inputColumnNames"></param>
        /// <param name="rowValueSets"></param>
        private static void LoadInputFile(
            DoWorkEventArgs e,
            FileInterpretationParameters arguments,
            ref int headerLineNumber,
            ref List<string> inputColumnNames,
            List<Dictionary<string, string>> rowValueSets)
        {
            using (TextFieldParser parser =
                new TextFieldParser(arguments.InputFile))
            {
                parser.SetDelimiters(arguments.Delimiter.ToString());
                parser.HasFieldsEnclosedInQuotes = true;

                // In this context the dictionary is in the reverse order (index -> name).
                Dictionary<int, string> headers = new Dictionary<int, string>();

                // Locate the first non-blank row. Use this as the header.
                do
                {
                    string[] headerNames = parser.ReadFields();

                    if (headerNames.Where(h => !string.IsNullOrWhiteSpace(h)).Any())
                    {
                        // We found a non-blank row. Use this as the header.
                        for (int x = 0; x < headerNames.Length; x++)
                            headers.Add(x, headerNames[x]);

                        break;
                    }
                    else
                        headerLineNumber++;

                } while (!parser.EndOfData);

                inputColumnNames = headers.Values.ToList();

                int NumberOfColumns = inputColumnNames.Count;
                bool[] antibioticColumns = new bool[NumberOfColumns];
                for (int x = 0; x < NumberOfColumns; x++)
                {
                    string columnName = inputColumnNames[x];
                    antibioticColumns[x] = !string.IsNullOrWhiteSpace(columnName)
                        && IsolateInterpretation.ValidAntibioticFieldNameRegex.IsMatch(columnName);
                }

                Dictionary<string, string> canonicalizedAntibioticValues = new Dictionary<string, string>(StringComparer.Ordinal);
                int canonicalizedAntibioticCharacters = 0;

                // Process the rest of the data file.
                while (!parser.EndOfData)
                {
                    if (arguments.Worker != null && arguments.Worker.CancellationPending)
                    {
                        e.Cancel = true;
                        return;
                    }

                    string[] values = parser.ReadFields();

                    if (values.Length >= NumberOfColumns && values.Any(v => !string.IsNullOrWhiteSpace(v)))
                    {
                        // Process this data row.
                        // Transfer the lines into value dictionaries.
                        // Don't add empty fields to the row's set.
                        // Don't add anything for a blank line.

                        // Transfer the value array into a row dictionary.
                        Dictionary<string, string> rowValues = new Dictionary<string, string>(values.Count(v => !string.IsNullOrWhiteSpace(v)));

                        for (int x = 0; x < values.Length; x++)
                            if (!string.IsNullOrWhiteSpace(values[x]))
                            {
                                string value = values[x];
                                if (antibioticColumns[x] && value.Length <= MaxCanonicalizedAntibioticValueLength
                                    && canonicalizedAntibioticValues.TryGetValue(value, out string canonicalizedValue))
                                {
                                    value = canonicalizedValue;
                                }
                                else if (antibioticColumns[x] && value.Length <= MaxCanonicalizedAntibioticValueLength
                                    && canonicalizedAntibioticValues.Count < MaxCanonicalizedAntibioticValues
                                    && canonicalizedAntibioticCharacters <= MaxCanonicalizedAntibioticCharacters - value.Length)
                                {
                                    canonicalizedAntibioticValues.Add(value, value);
                                    canonicalizedAntibioticCharacters += value.Length;
                                }

                                rowValues.Add(headers[x], value);
                            }

                        rowValueSets.Add(rowValues);
                    }
                }
            }
        }

        /// <summary>
        /// Parallel interpretation for all rows in the provided data set.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="arguments"></param>
        /// <param name="interpretationConfig"></param>
        /// <param name="inputColumnNames"></param>
        /// <param name="rowValueSets"></param>
        /// <returns></returns>
        private static Tuple<Dictionary<string, string>, Dictionary<string, string>>[] InterpretIsolates(
            DoWorkEventArgs e,
            FileInterpretationParameters arguments,
            InterpretationConfiguration interpretationConfig,
            List<string> inputColumnNames,
            List<Dictionary<string, string>> rowValueSets)
        {
            int remainingLines = rowValueSets.Count;
            int blockSize = Math.Max(1, remainingLines / Environment.ProcessorCount);
            int totalBlocks = (remainingLines / blockSize) + (remainingLines % blockSize == 0 ? 0 : 1);
            int effectiveGuidelineYear = arguments.GuidelineYear == -1
                ? Convert.ToInt32(interpretationConfig.GuidelineYear)
                : arguments.GuidelineYear;
            Tuple<Dictionary<string, string>, Dictionary<string, string>>[] interpretationResults =
                new Tuple<Dictionary<string, string>, Dictionary<string, string>>[remainingLines];

            object countSyncObject = new object();
            int rowCount = 0;
            int previousProgressReport = 0;

			// Determine the set of breakpoints needed for the data.
			List<Tuple<string, string, string>> distinctInterpretationKeys =
				rowValueSets.SelectMany(row =>
				{
					List<Tuple<string, string, string>> combinationsForRow = new();

                    IEnumerable<string> antibioticFields = row.Keys.Where(k => IsolateInterpretation.ValidAntibioticFieldNameRegex.IsMatch(k));

                    if (row.ContainsKey(Constants.KeyFields.ORGANISM) && antibioticFields.Count() > 0)
                    {
                        foreach (string drug in antibioticFields)
                        {
                            // Determine the set of drug-bug combinations for this data row.
                            AntibioticComponents thisAntibiotic = new AntibioticComponents(drug);
                            combinationsForRow.Add(new Tuple<string, string, string>(row[Constants.KeyFields.ORGANISM].Trim(), thisAntibiotic.Guideline, drug));
                        }
                    }

                    return combinationsForRow;
                }).Distinct().ToList();

            // Preheat breakpoint cache.
            AntibioticSpecificInterpretationRules.PreheatBreakpointCache(
                interpretationConfig.UserDefinedBreakpoints,
                effectiveGuidelineYear,
                interpretationConfig.PrioritizedBreakpointTypes,
                interpretationConfig.PrioritizedSitesOfInfection,
                distinctInterpretationKeys,
                arguments.Worker);

            arguments.Worker?.ReportProgress(0);

            if (arguments.Worker != null && arguments.Worker.CancellationPending)
            {
                e.Cancel = true;
                Tuple<Dictionary<string, string>, Dictionary<string, string>>[] _ = [];
                return _;
            }

            int isCancelled = 0;

            Parallel.For(0, totalBlocks, (blockNumber, state) =>
            {
                int blockRowStart = (blockSize * blockNumber);
                int blockRowCount = Math.Min(blockSize, rowValueSets.Count - blockRowStart);

                for (int lineNumber = blockRowStart; lineNumber < blockRowStart + blockRowCount; lineNumber++)
                {
                    if (arguments.Worker != null && arguments.Worker.CancellationPending)
                    {
                        System.Threading.Volatile.Write(ref isCancelled, 1);
                        state.Break();
                    }

                    if (System.Threading.Volatile.Read(ref isCancelled) == 1)
                    {
                        break;
                    }

                    Dictionary<string, string> results =
                    new IsolateInterpretation(rowValueSets[lineNumber],
                    inputColumnNames,
                    interpretationConfig.EnabledExpertInterpretationRules,
                    interpretationConfig.UserDefinedBreakpoints,
                    guidelineYear: effectiveGuidelineYear,
                    prioritizedBreakpointTypes: interpretationConfig.PrioritizedBreakpointTypes,
                    prioritizedSitesOfInfection: interpretationConfig.PrioritizedSitesOfInfection,
                    roundHalfDilutions: interpretationConfig.RoundHalfDilutions,
                    useIntrinsicResistanceRules: interpretationConfig.UseIntrinsicResistanceRules).
                    GetAllInterpretations();

                    // Put each original value and result set into the corresponding array position.
                    // No 2 threads will have the same line number, so there is no need for a lock while saving to the results array.
                    interpretationResults[lineNumber] = new Tuple<Dictionary<string, string>, Dictionary<string, string>>(rowValueSets[lineNumber], results);

                    lock (countSyncObject)
                    {
                        rowCount++;

                        int currentProgress = rowCount * 100 / remainingLines;
                        if (currentProgress > previousProgressReport)
                        {
                            // Only report whole percentage changes.
                            previousProgressReport = currentProgress;
                            arguments.Worker?.ReportProgress(currentProgress);
                        }
                    }
                }
            });

            if (System.Threading.Volatile.Read(ref isCancelled) == 1)
            {
                e.Cancel = true;
                Tuple<Dictionary<string, string>, Dictionary<string, string>>[] _ = [];
                return _;
            }                
            else
                return interpretationResults;
        }

        /// <summary>
        /// Saves the output in the format specified by the interpretation configuration.
        /// </summary>
        /// <param name="arguments"></param>
        /// <param name="interpretationConfig"></param>
        /// <param name="interpretationResults"></param>
        private static void GenerateOutputFile(DoWorkEventArgs e,
            FileInterpretationParameters arguments,
            InterpretationConfiguration interpretationConfig,
            List<string> inputColumnNames,
            Tuple<Dictionary<string, string>, Dictionary<string, string>>[] interpretationResults)
        {
            if (arguments.Worker != null && arguments.Worker.CancellationPending)
            {
                e.Cancel = true;
                return;
            }

            const string InterpSuffix = "_INTERP";

            IEnumerable<string> interpretationHeaders =
                interpretationResults.SelectMany(v => v.Item2.Keys).Distinct();

            List<string> outputHeaders = inputColumnNames.ToList();
            List<string> antibioticFields = null;

            if (interpretationConfig.HorizontalAntibioticResults)
            {
                // The order should be the same as the input files with the addition of the interpretation columns following each antibiotic.
                foreach (string interpHeader in interpretationHeaders)
                    for (int x = 0; x < outputHeaders.Count; x++)
                        if (outputHeaders[x] == interpHeader)
                        {
                            // Insert the interpretation column immediately following the associated measurement column.
                            outputHeaders.Insert(x + 1, interpHeader + InterpSuffix);
                            break;
                        }
            }
            else
            {
                antibioticFields = outputHeaders.Where(h => IsolateInterpretation.ValidAntibioticFieldNameRegex.IsMatch(h)).ToList();

                // The antibiotics are transposed vertically. Remove the horizontal columns, and append the 3 vertical columns.
                outputHeaders = outputHeaders.Except(antibioticFields).Concat(OutputAntibioticColumns.VerticalAntibioticFields).ToList();
            }

            using (StreamWriter writer = new StreamWriter(arguments.OutputFile))
            {
                writer.WriteLine(ToLine(outputHeaders, Constants.Delimiters.TabChar));

				foreach (Tuple<Dictionary<string, string>, Dictionary<string, string>> row in interpretationResults)
				{
					if (arguments.Worker != null && arguments.Worker.CancellationPending)
					{
						e.Cancel = true;
						return;
					}

                    if (interpretationConfig.HorizontalAntibioticResults)
                    {
                        List<string> thisRow = new List<string>();

                        foreach (string h in outputHeaders)
                        {
                            if (row.Item1.ContainsKey(h))
                                thisRow.Add(row.Item1[h]);

                            else if (h.EndsWith(InterpSuffix))
                            {
                                string drugCode = h.Substring(0, h.Length - InterpSuffix.Length);

                                if (row.Item2.ContainsKey(drugCode))
                                {
                                    string thisInterp = row.Item2[drugCode];
                                    if (!interpretationConfig.IncludeInterpretationComments)
                                        thisInterp = IsolateInterpretation.RemoveComments(thisInterp);

                                    thisRow.Add(thisInterp);
                                }
                                else
                                    thisRow.Add(string.Empty);
                            }

                            else thisRow.Add(string.Empty);
                        }

                        writer.WriteLine(ToLine(thisRow, Constants.Delimiters.TabChar));
                    }
                    else
                    {
                        List<string> repeatedValueList = new List<string>();

                        // Repeat all but the final 3 columns, which are the variable antibiotics.
                        foreach (string h in outputHeaders.Take(outputHeaders.Count - 3))
                        {
                            if (row.Item1.ContainsKey(h))
                                repeatedValueList.Add(row.Item1[h]);

                            else
                                repeatedValueList.Add(string.Empty);
                        }

                        string repeatedOutputString = ToLine(repeatedValueList, Constants.Delimiters.TabChar);

						if (!antibioticFields.Any(a => row.Item1.ContainsKey(a)))
						{
							// This row didn't have any measurements.
							// We will still write 1 row to the output with blanks for the final three fields.
							writer.WriteLine(string.Join(Constants.Delimiters.TabChar, repeatedOutputString, ToLine([string.Empty, string.Empty, string.Empty], Constants.Delimiters.TabChar)));
						}
						else
						{
							// We need to write one row per antibiotic measurement,
							// whether it has an associated interpretation or not.
							foreach (string antibiotic in antibioticFields)
							{
								if (row.Item1.ContainsKey(antibiotic))
								{
									List<string> antibioticRowValues = new List<string>() { antibiotic, row.Item1[antibiotic] };

                                    if (row.Item2.ContainsKey(antibiotic))
                                    {
                                        string thisInterp = row.Item2[antibiotic];
                                        if (!interpretationConfig.IncludeInterpretationComments)
                                            thisInterp = IsolateInterpretation.RemoveComments(thisInterp);

                                        antibioticRowValues.Add(thisInterp);
                                    }
                                    else
                                        antibioticRowValues.Add(string.Empty);

									string outputLine = string.Join(Constants.Delimiters.TabChar, repeatedOutputString, ToLine(antibioticRowValues, Constants.Delimiters.TabChar));

                                    writer.WriteLine(outputLine);
                                }
                            }
                        }
                    }
                }
            }
        }

        #endregion

    }
}
