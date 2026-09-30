using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using AMR_Engine;

namespace Interpretation_CLI
{
	public class Program
	{
		public static int Main(string[] args)
		{
			const char newLine = '\n';
			if (!(args.Length == 5 && args[0].ToUpperInvariant() == Constants.CommandLineModes.File) 
				&& !(args.Length == 6 && args[0].ToUpperInvariant() == Constants.CommandLineModes.SingleInterpretation))
			{
				Console.WriteLine(Translations.Resources.InvalidCommandLineArguments);
				WriteCliParameterInfoToConsole();
			}

			else if (args[0].ToUpperInvariant() == Constants.CommandLineModes.File)
			{
				try
				{
					// Whole-file mode. All parameters are required.
					// FILE {Config file} {Delimiter} {Input file} {Output file}
					string configFile = Path.GetFullPath(args[1]);
					string delimiterArg = args[2];
					string inputFile = args[3];
					string outputFile = args[4];

					char delimiter;
					if (delimiterArg == Constants.Delimiters.TabPlaceholder)
						delimiter = Constants.Delimiters.TabChar;
					else
						delimiter = delimiterArg[0];

					AMR_Engine.FileInterpretationParameters fileArgs =
						new(inputFile, delimiter, -1, configFile,
						outputFile, null);

					DoWorkEventArgs e = new(fileArgs);
					IO_Library.InterpretDataFile(null, e);
					return 0;
				}
				catch (Exception ex)
				{
					Console.WriteLine(string.Join(newLine, ex.Message, ex.StackTrace));
				}
			}

			else if (args[0].ToUpperInvariant() == Constants.CommandLineModes.SingleInterpretation)
			{
				try
				{
					// Single-interpretation mode. All parameters are required.
					// SINGLE_INTERPRETATION {Config file} {Organism code} {Antibiotic code} {Measurement} {Output file}
					string configFile = Path.GetFullPath(args[1]);
					string organismCode = args[2];
					string antibioticCode = args[3];
					string measurement = args[4];
					string outputFile = args[5];

					InterpretationConfiguration interpretationConfig =
						InterpretationConfiguration.ReadConfiguration(configFile);

					string interpretation =
						IsolateInterpretation.GetSingleInterpretation(
							interpretationConfig, organismCode, antibioticCode, measurement);

					if (!interpretationConfig.IncludeInterpretationComments)
						interpretation = IsolateInterpretation.RemoveComments(interpretation);

					SingleInterpretation interpObject = new(organismCode, antibioticCode, measurement, interpretation);
					string interpJson = JsonSerializer.Serialize(interpObject);

					Console.WriteLine(interpretation);

					using StreamWriter outputWriter = new(outputFile);
					outputWriter.WriteLine(interpJson);

					return 0;
				}
				catch (Exception ex)
				{
					Console.WriteLine(string.Join(newLine, ex.Message, ex.StackTrace, string.Empty));
				}
			}

			return 1;
		}

		private static void WriteCliParameterInfoToConsole()
		{
			string processName = Path.GetFileName(Environment.ProcessPath ?? "AMRIE.exe");
			Console.WriteLine();
			Console.WriteLine(Translations.Resources.WholeFileMode);
			Console.WriteLine($"\"{processName}\" FILE {{Config file}} {{Delimiter character (use 'TAB' or the delimiter character}} {{Input file}} {{Output file}}");
			Console.WriteLine();
			Console.WriteLine(Translations.Resources.SingleInterpretationMode);
			Console.WriteLine($"\"{processName}\" SINGLE_INTERPRETATION {{Config file}} {{Organism code}} {{Antibiotic code}} {{Measurement}} {{Output file}}");
		}
	}
}
