using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;

namespace AMRIE.Library
{
    public class IO_Library
    {
        #region Constants

        private const int MaxCanonicalizedAntibioticValueLength = 8;
        private const int MaxCanonicalizedAntibioticValues = 4096;
        private const int MaxCanonicalizedAntibioticCharacters = 65536;
        private const int MaxBatchPreheatKeys = 32768;

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
        /// Compatibility entry point for bounded file interpretation.
        /// </summary>
        /// <param name="s"></param>
        /// <param name="e"></param>
        public static void InterpretDataFile(object s, DoWorkEventArgs e)
            => InterpretDataFileBounded(s, e);

        /// <summary>
        /// Interpret bounded owning batches and publish the staged output only after successful completion.
        /// </summary>
        /// <param name="s">The BackgroundWorker event sender, if any.</param>
        /// <param name="e">Work arguments containing FileInterpretationParameters and cancellation state.</param>
        public static void InterpretDataFileBounded(object s, DoWorkEventArgs e)
        {
            FileInterpretationParameters arguments = (FileInterpretationParameters)e.Argument;
            arguments.Worker?.ReportProgress(0);

            InterpretationConfiguration interpretationConfig =
                InterpretationConfiguration.ReadConfiguration(arguments.ConfigFile);

            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(arguments.BatchRowLimit);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(arguments.BatchCharacterLimit);
            string outputPath = Path.GetFullPath(arguments.OutputFile);
            string directory = Path.GetDirectoryName(outputPath);
            string rowsPath = Path.Combine(directory, $".amrie-{Guid.NewGuid():N}.rows.tmp");
            string outputStagePath = Path.Combine(directory, $".amrie-{Guid.NewGuid():N}.output.tmp");
            bool rowsCreated = false;
            bool outputCreated = false;
            List<string> inputColumnNames = [];
            List<string> interpretationHeaders = [];
            HashSet<string> seenInterpretationHeaders = new(StringComparer.Ordinal);
            List<Dictionary<string, string>> batch = new(arguments.BatchRowLimit);
            long batchCharacters = 0;
            long batchMeasurements = 0;
            long rowsProcessed = 0;
            long measurementsProcessed = 0;
            int lastProgress = 0;

            void Observe(string stage) => arguments.ProgressObserver?.Invoke(
                new(stage, rowsProcessed, measurementsProcessed, batch.Count, batchCharacters));

            void ReportProgress(int percentage)
            {
                if (percentage > lastProgress)
                {
                    lastProgress = percentage;
                    arguments.Worker?.ReportProgress(percentage);
                }
            }

            try
            {
                // Stage owning values/results, not formatted rows: the exact global header order is only known at EOF.
                using FileStream rowsStream = new(rowsPath, FileMode.CreateNew, FileAccess.ReadWrite,
                    FileShare.None, 65536, FileOptions.SequentialScan);
                rowsCreated = true;
                using (BinaryWriter rowsWriter = new(rowsStream, Encoding.UTF8, leaveOpen: true))
                using (FileStream input = new(arguments.InputFile, FileMode.Open, FileAccess.Read,
                    FileShare.Read, 4096, FileOptions.SequentialScan))
                using (CsvReader parser = CsvReader.Create(new StreamReader(input, Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true), CreateCsvReaderOptions(arguments.Delimiter)))
                {
                    void FlushBatch()
                    {
                        Observe("interpreting");
                        InterpretAndStageBatch(e, arguments, interpretationConfig, inputColumnNames, batch,
                            rowsWriter, interpretationHeaders, seenInterpretationHeaders);
                        if (e.Cancel)
                            return;
                        rowsProcessed += batch.Count;
                        measurementsProcessed += batchMeasurements;
                        batch.Clear();
                        batchCharacters = 0;
                        batchMeasurements = 0;
                        ReportProgress((int)(input.Position * 80.0 / input.Length));
                        Observe("interpreting");
                    }

                    foreach (InputRow row in ReadInputRows(e, arguments, parser, inputColumnNames))
                    {
                        if (batch.Count > 0 && batchCharacters > arguments.BatchCharacterLimit - row.Characters)
                        {
                            FlushBatch();
                            if (e.Cancel)
                                return;
                        }
                        batch.Add(row.Values);
                        batchCharacters += row.Characters;
                        batchMeasurements += row.Measurements;
                        if (batch.Count >= arguments.BatchRowLimit || batchCharacters >= arguments.BatchCharacterLimit)
                        {
                            FlushBatch();
                            if (e.Cancel)
                                return;
                        }
                    }
                    if (e.Cancel)
                        return;
                    if (batch.Count > 0)
                        FlushBatch();
                    if (IsCancelled(e, arguments))
                        return;
                }

                ReportProgress(80);
                rowsStream.Position = 0;
                using (FileStream output = new(outputStagePath, FileMode.CreateNew, FileAccess.Write,
                    FileShare.None, 65536, FileOptions.SequentialScan))
                {
                    outputCreated = true;
                    using (StreamWriter writer = new(output, new UTF8Encoding(false), bufferSize: 1024, leaveOpen: true))
                    using (BinaryReader rowsReader = new(rowsStream, Encoding.UTF8, leaveOpen: true))
                    {
                        Observe("writing");
                        WriteOutputRows(e, arguments, interpretationConfig, inputColumnNames,
                            ReadStagedRows(rowsReader, rowsProcessed), interpretationHeaders, writer,
                            completed =>
                            {
                                ReportProgress(80 + (int)(completed * 19.0 / rowsProcessed));
                            });
                        if (IsCancelled(e, arguments))
                            return;
                    }
                    output.Flush(flushToDisk: true);
                }
                rowsStream.Dispose();
                File.Delete(rowsPath);
                rowsCreated = false;
                Observe("publishing");
                if (IsCancelled(e, arguments))
                    return;
                File.Move(outputStagePath, outputPath, overwrite: true);
                outputCreated = false;
                ReportProgress(100);
            }
            finally
            {
                try
                {
                    if (outputCreated)
                        File.Delete(outputStagePath);
                }
                finally
                {
                    if (rowsCreated)
                        File.Delete(rowsPath);
                }
            }
        }

        /// <summary>
        /// Get the headers for the a resource file header line provided.
        /// </summary>
        /// <param name="headerLine"></param>
        /// <returns></returns>
        public static Dictionary<string, int> GetResourceHeaders(string headerLine)
        {
            return GetResourceHeaders(SplitLine(headerLine, Constants.Delimiters.TabChar));
        }

        /// <summary>
        /// Get the headers for the a resource file header line provided.
        /// </summary>
        /// <param name="headerLine"></param>
        /// <returns></returns>
        public static Dictionary<string, int> GetResourceHeaders(string[] headers)
        {
            Dictionary<string, int> headerMap = [];
            for (int i = 0; i < headers.Length; i++)
                headerMap.Add(headers[i], i);

            return headerMap;
        }

        internal static CsvReaderOptions CreateCsvReaderOptions(char delimiter)
        {
            Constants.Delimiters.ValidateFileDelimiter(delimiter, nameof(delimiter));
            return new CsvReaderOptions
            {
                Separator = delimiter,
                HasHeader = false,
                Mode = CsvMode.Rfc4180,
                Quote = '"',
                Escape = '\\',
                TrimWhitespace = true,
                TrimInsideQuotes = false,
                WhitespaceCharacters = " \t\v\f".Replace(delimiter.ToString(), string.Empty),
                SkipEmptyLines = true,
                AllowLineBreaksInQuotes = true,
                MalformedData = CsvMalformedDataHandling.Throw,
                FieldCountMismatch = CsvFieldCountHandling.Allow,
                MissingField = CsvMissingFieldHandling.Throw,
                TrackLineNumbers = true,
                DetectSeparatorHint = false,
                PoolStrings = false
            };
        }

        internal static string[] GetFields(CsvReader parser)
        {
            string[] fields = new string[parser.FieldCount];
            for (int index = 0; index < fields.Length; index++)
                fields[index] = parser.GetString(index).Trim();
            return fields;
        }


		public static string[] SplitLine(string record, char delimiter)
		{
            if (string.IsNullOrEmpty(record) || (!record.Contains(delimiter) && !record.Contains(Constants.Quote)))
				return [record];

            List<string> results = [];
            StringBuilder field = new();
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
            return [.. results];
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
        /// Read owning row dictionaries with the existing header, trimming and measurement-sharing behavior.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="arguments"></param>
        /// <param name="inputColumnNames"></param>
        /// <param name="parser"></param>
        private static IEnumerable<InputRow> ReadInputRows(
            DoWorkEventArgs e,
            FileInterpretationParameters arguments,
            CsvReader parser,
            List<string> inputColumnNames)
        {
            bool hasHeader = false;
            while (parser.Read())
            {
                string[] headerNames = GetFields(parser);
                if (!headerNames.Any(h => !string.IsNullOrWhiteSpace(h)))
                    continue;

                inputColumnNames.AddRange(headerNames);
                hasHeader = true;
                break;
            }

            if (!hasHeader)
                throw new InvalidDataException("The input file does not contain a non-empty header record.");

            int numberOfColumns = inputColumnNames.Count;
            bool[] antibioticColumns = new bool[numberOfColumns];
            for (int x = 0; x < numberOfColumns; x++)
            {
                string columnName = inputColumnNames[x];
                antibioticColumns[x] = !string.IsNullOrWhiteSpace(columnName)
                    && IsolateInterpretation.ValidAntibioticFieldNameRegex.IsMatch(columnName);
            }

            Dictionary<string, string> canonicalizedAntibioticValues = new(StringComparer.Ordinal);
            int canonicalizedAntibioticCharacters = 0;

            while (true)
            {
                if (IsCancelled(e, arguments))
                    yield break;

                if (!parser.Read())
                    break;
                string[] values = GetFields(parser);

                if (values.Length >= numberOfColumns && values.Any(v => !string.IsNullOrWhiteSpace(v)))
                {
                    Dictionary<string, string> rowValues = new(values.Count(v => !string.IsNullOrWhiteSpace(v)));
                    long characters = 0;
                    int measurements = 0;

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

                            rowValues.Add(inputColumnNames[x], value);
                            characters += value.Length;
                            if (antibioticColumns[x])
                                measurements++;
                        }

                    yield return new(rowValues, characters, measurements);
                }
            }
        }

        private readonly record struct InputRow(Dictionary<string, string> Values, long Characters, int Measurements);

        private static bool IsCancelled(DoWorkEventArgs e, FileInterpretationParameters arguments)
        {
            if (arguments.Worker?.CancellationPending == true)
                e.Cancel = true;
            return e.Cancel;
        }

        private static void WriteDictionary(BinaryWriter writer, Dictionary<string, string> values)
        {
            writer.Write(values.Count);
            foreach (var value in values)
            {
                writer.Write(value.Key);
                writer.Write(value.Value);
            }
        }

        private static Dictionary<string, string> ReadDictionary(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0)
                throw new InvalidDataException("Invalid staged row field count.");
            Dictionary<string, string> values = new(count);
            for (int index = 0; index < count; index++)
                values.Add(reader.ReadString(), reader.ReadString());
            return values;
        }

        private static IEnumerable<Tuple<Dictionary<string, string>, Dictionary<string, string>>> ReadStagedRows(
            BinaryReader reader, long rowCount)
        {
            for (long row = 0; row < rowCount; row++)
                yield return new(ReadDictionary(reader), ReadDictionary(reader));
        }

        /// <summary>
        /// Interpret and stage one bounded batch in input order.
        /// </summary>
        private static void InterpretAndStageBatch(
            DoWorkEventArgs e,
            FileInterpretationParameters arguments,
            InterpretationConfiguration interpretationConfig,
            List<string> inputColumnNames,
            List<Dictionary<string, string>> rowValueSets,
            BinaryWriter writer,
            List<string> interpretationHeaders,
            HashSet<string> seenHeaders)
        {
            int rowCount = rowValueSets.Count;
            int blockSize = Math.Max(1, rowCount / Environment.ProcessorCount);
            int totalBlocks = (rowCount / blockSize) + (rowCount % blockSize == 0 ? 0 : 1);
            int effectiveGuidelineYear = arguments.GuidelineYear == -1
                ? Convert.ToInt32(interpretationConfig.GuidelineYear)
                : arguments.GuidelineYear;
            Tuple<Dictionary<string, string>, Dictionary<string, string>>[] interpretationResults =
                new Tuple<Dictionary<string, string>, Dictionary<string, string>>[rowCount];

            HashSet<Tuple<string, string, string>> keys = [];
            foreach (var row in rowValueSets)
            {
                if (IsCancelled(e, arguments))
                    return;
                if (!row.TryGetValue(Constants.KeyFields.ORGANISM, out string organism))
                    continue;
                foreach (string drug in row.Keys.Where(k => IsolateInterpretation.ValidAntibioticFieldNameRegex.IsMatch(k)))
                {
                    if (keys.Count >= MaxBatchPreheatKeys)
                        break;
                    AntibioticComponents antibiotic = new(drug);
                    keys.Add(new(organism.Trim(), antibiotic.Guideline, drug));
                }
                if (keys.Count >= MaxBatchPreheatKeys)
                    break;
            }

            // Preheat breakpoint cache.
            AntibioticSpecificInterpretationRules.PreheatBreakpointCache(
                interpretationConfig.UserDefinedBreakpoints,
                effectiveGuidelineYear,
                interpretationConfig.PrioritizedBreakpointTypes,
                interpretationConfig.PrioritizedSitesOfInfection,
                [.. keys],
                arguments.Worker,
                reportProgress: false);

            if (IsCancelled(e, arguments))
                return;

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

                }
            });

            if (System.Threading.Volatile.Read(ref isCancelled) == 1)
                e.Cancel = true;

            if (IsCancelled(e, arguments))
                return;
            foreach (var row in interpretationResults)
            {
                if (IsCancelled(e, arguments))
                    return;
                foreach (string header in row.Item2.Keys)
                    if (seenHeaders.Add(header))
                        interpretationHeaders.Add(header);
                WriteDictionary(writer, row.Item1);
                WriteDictionary(writer, row.Item2);
            }
        }

        /// <summary>
        /// Saves the output in the format specified by the interpretation configuration.
        /// </summary>
        /// <param name="arguments"></param>
        /// <param name="interpretationConfig"></param>
        /// <param name="interpretationResults"></param>
        private static void WriteOutputRows(DoWorkEventArgs e,
            FileInterpretationParameters arguments,
            InterpretationConfiguration interpretationConfig,
            List<string> inputColumnNames,
            IEnumerable<Tuple<Dictionary<string, string>, Dictionary<string, string>>> interpretationResults,
            List<string> interpretationHeaders,
            TextWriter writer,
            Action<long> rowWritten)
        {
            if (IsCancelled(e, arguments))
                return;

            const string InterpSuffix = "_INTERP";

            List<string> outputHeaders = [.. inputColumnNames];
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
                antibioticFields = [.. outputHeaders.Where(h => IsolateInterpretation.ValidAntibioticFieldNameRegex.IsMatch(h))];

                // The antibiotics are transposed vertically. Remove the horizontal columns, and append the 3 vertical columns.
                outputHeaders = [.. outputHeaders.Except(antibioticFields), .. OutputAntibioticColumns.VerticalAntibioticFields];
            }

            writer.WriteLine(ToLine(outputHeaders, Constants.Delimiters.TabChar));
            long completedRows = 0;

            foreach (Tuple<Dictionary<string, string>, Dictionary<string, string>> row in interpretationResults)
            {
                if (IsCancelled(e, arguments))
                    return;

                if (interpretationConfig.HorizontalAntibioticResults)
                {
                    List<string> thisRow = [];

                    foreach (string h in outputHeaders)
                    {
                        if (row.Item1.ContainsKey(h))
                            thisRow.Add(row.Item1[h]);

                        else if (h.EndsWith(InterpSuffix))
                        {
                            string drugCode = h[..^InterpSuffix.Length];

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
                    List<string> repeatedValueList = [];

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
                                List<string> antibioticRowValues = [antibiotic, row.Item1[antibiotic]];

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
                rowWritten(++completedRows);
            }
        }

        #endregion

    }
}
