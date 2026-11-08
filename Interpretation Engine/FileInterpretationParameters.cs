using System;
using System.ComponentModel;

namespace AMR_Engine
{
    /// <summary>
    /// Container for parameters needed to interpret a data file.
    /// </summary>
    public class FileInterpretationParameters
    {
        public FileInterpretationParameters(string inputFile_, char delimiter_, int guidelineYear_,
            string configFile_, string outputFile_, BackgroundWorker worker_)
        {
            InputFile = inputFile_;
            Delimiter = delimiter_;
            GuidelineYear = guidelineYear_;
            ConfigFile = configFile_;
            OutputFile = outputFile_;
            Worker = worker_;
        }

        public readonly string InputFile;
        public readonly char Delimiter;
        public readonly int GuidelineYear;
        public readonly string ConfigFile;
        public readonly string OutputFile;
        public readonly BackgroundWorker Worker;

        internal int BatchRowLimit { get; set; } = 2048;
        internal long BatchCharacterLimit { get; set; } = 4194304;
        internal Action<FileProcessingProgress> ProgressObserver { get; set; }
    }

    internal sealed record FileProcessingProgress(string Stage, long RowsProcessed,
        long MeasurementsProcessed, int BatchRows, long BatchCharacters);
}