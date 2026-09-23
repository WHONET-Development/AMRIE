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
    }
}