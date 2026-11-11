namespace AMRIE.Library.DataGenerator;

internal sealed record GeneratorOptions(string OutputDirectory, RequestedInputSize? TargetSize);

internal static class GeneratorCommandLine
{
    public const string Usage = "Usage: Interpretation Engine.DataGenerator [output-directory] [--output-directory PATH] [--target-size <integer>MB|<integer>GB]";
    private const string DefaultOutputDirectoryName = "amrie-data-generator";

    public static bool TryParse(string[] args, out GeneratorOptions? options, out string error)
    {
        string? outputDirectory = null;
        RequestedInputSize? targetSize = null;
        bool targetSizeSpecified = false;
        bool outputDirectorySpecified = false;

        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            if (argument == "--target-size")
            {
                if (targetSizeSpecified)
                    return Fail("Specify --target-size only once.", out options, out error);
                if (++index >= args.Length)
                    return Fail("--target-size requires a value such as 50MB or 10GB.", out options, out error);
                if (!RequestedInputSize.TryParse(args[index], out RequestedInputSize parsedSize))
                    return Fail($"Invalid target size '{args[index]}'. Use a positive integer followed by MB or GB.", out options, out error);

                targetSize = parsedSize;
                targetSizeSpecified = true;
            }
            else if (argument == "--output-directory")
            {
                if (outputDirectorySpecified || outputDirectory is not null)
                    return Fail("Specify --output-directory only once.", out options, out error);
                if (++index >= args.Length || string.IsNullOrWhiteSpace(args[index]))
                    return Fail("--output-directory requires a path.", out options, out error);

                outputDirectory = args[index];
                outputDirectorySpecified = true;
            }
            else if (argument.StartsWith("--", StringComparison.Ordinal))
                return Fail($"Unknown option '{argument}'.", out options, out error);
            else
            {
                if (outputDirectorySpecified || outputDirectory is not null)
                    return Fail("Specify the output directory only once.", out options, out error);
                outputDirectory = argument;
            }
        }

        outputDirectory ??= Path.Combine(Directory.GetCurrentDirectory(), "artifacts", DefaultOutputDirectoryName);
        options = new GeneratorOptions(Path.GetFullPath(outputDirectory), targetSize);
        error = string.Empty;
        return true;
    }

    private static bool Fail(string message, out GeneratorOptions? options, out string error)
    {
        options = null;
        error = message + Environment.NewLine + Usage;
        return false;
    }
}
