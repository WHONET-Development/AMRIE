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

		private class OutputAntibioticColumns
		{
			public const string AntibioticCode = "ANTIBIOTIC_CODE";
			public const string AntibioticMeasurement = "ANTIBIOTIC_MEASUREMENT";
			public const string AntibioticInterpretation = "ANTIBIOTIC_INTERPRETATION";

			public static readonly string[] VerticalAntibioticFields = {
				AntibioticCode,
				AntibioticMeasurement,
				AntibioticInterpretation
			};
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
			if (string.IsNullOrEmpty(record) || !record.Contains(delimiter))
			{
				string[] value = { record };
				return value;
			}

            List<string> results = new List<string>();
            StringBuilder result = new StringBuilder();
            bool inQualifier = false;
            bool inField = false;

            string row = $"{record}{delimiter}";

			for (int idx = 0; idx < row.Length; idx++)
			{
				if (row[idx] == delimiter)
				{
					if (!inQualifier)
					{
						results.Add(result.ToString().Trim());
						result.Clear();
						inField = false;
					}
					else
					{
						result.Append(row[idx]);
					}
				}
				else
				{
					if (row[idx] != ' ')
					{
						if (row[idx] == Constants.Quote)
						{
							if (inQualifier && row[IndexOfNextNonWhiteSpaceChar(row, idx + 1)] == delimiter)
							{
								inQualifier = false;
								continue;
							}

							else
							{
								if (!inQualifier)
								{
									inQualifier = true;
								}
								else
								{
									inField = true;
									result.Append(row[idx]);
								}
							}
						}
						else
						{
							result.Append(row[idx]);
							inField = true;
						}
					}
					else
					{
						if (inQualifier || inField)
						{
							result.Append(row[idx]);
						}
					}
				}
			}

			return results.ToArray<string>();
		}

		public static string ToLine(IEnumerable<string> values, char delimiter)
		{
			return string.Join(delimiter.ToString(), values.Select(v => 
			{
				if (string.IsNullOrEmpty(v) || (!v.Contains(delimiter) && !v.Contains(Constants.Quote)))
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
						Dictionary<string, string> rowValues = new Dictionary<string, string>();

						for (int x = 0; x < values.Length; x++)
							if (!string.IsNullOrWhiteSpace(values[x]))
								rowValues.Add(headers[x], values[x]);

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
			Tuple<Dictionary<string, string>, Dictionary<string, string>>[] interpretationResults =
				new Tuple<Dictionary<string, string>, Dictionary<string, string>>[remainingLines];

			object countSyncObject = new object();
			int rowCount = 0;
			int previousProgressReport = 0;

			// Determine the set of breakpoints needed for the data.
			List<Tuple<string, string, string>> distinctInterpretationKeys =
				rowValueSets.SelectMany(row =>
				{
					List<Tuple<string, string, string>> combinationsForRow = new List<Tuple<string, string, string>>();

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
				arguments.GuidelineYear,
				interpretationConfig.PrioritizedBreakpointTypes,
				interpretationConfig.PrioritizedSitesOfInfection,
				distinctInterpretationKeys,
				arguments.Worker);

			arguments.Worker?.ReportProgress(0);

			Parallel.For(0, totalBlocks, (blockNumber, state) =>
			{
				if (arguments.Worker != null && arguments.Worker.CancellationPending)
				{
					e.Cancel = true;
					state.Break();
				}

				int blockRowStart = (blockSize * blockNumber);
				int blockRowCount = Math.Min(blockSize, rowValueSets.Count - blockRowStart);

				for (int lineNumber = blockRowStart; lineNumber < blockRowStart + blockRowCount; lineNumber++)
				{
					Dictionary<string, string> results =
					new IsolateInterpretation(rowValueSets[lineNumber],
					inputColumnNames,
					interpretationConfig.EnabledExpertInterpretationRules,
					interpretationConfig.UserDefinedBreakpoints,
					guidelineYear: arguments.GuidelineYear == -1 ? Convert.ToInt32(interpretationConfig.GuidelineYear) : arguments.GuidelineYear,
					prioritizedBreakpointTypes: interpretationConfig.PrioritizedBreakpointTypes,
					prioritizedSitesOfInfection: interpretationConfig.PrioritizedSitesOfInfection).
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

				string[] emptyAbxColumns = { string.Empty, string.Empty, string.Empty };
				string emptyAbxText = ToLine(emptyAbxColumns, Constants.Delimiters.TabChar);

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
							writer.WriteLine(
								string.Join(Constants.Delimiters.TabChar.ToString(), 
								repeatedOutputString, emptyAbxText));
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

									string outputLine = 
										string.Join(Constants.Delimiters.TabChar.ToString(), 
										repeatedOutputString, ToLine(antibioticRowValues, Constants.Delimiters.TabChar));

									writer.WriteLine(outputLine);
								}
							}
						}
					}
				}
			}
		}

		static readonly Func<string, int, int> IndexOfNextNonWhiteSpaceChar = delegate (string source, int startIndex)
		{
			if (startIndex >= 0)
			{
				if (source != null)
				{
					for (int i = startIndex; i < source.Length; i++)
					{
						if (!char.IsWhiteSpace(source[i]))
						{
							return i;
						}
					}
				}
			}

			return -1;
		};

		#endregion

	}
}
