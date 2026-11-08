using System.Text.Encodings.Web;
using System.Text.Json;
using Interpretation_Engine.Performance;

if (args.Length == 1 && args[0].Equals("--help", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine(PerformanceCommandLine.Usage);
    return 0;
}

if (!PerformanceCommandLine.TryParse(args, out PerformanceOptions? options, out string error) || options is null)
{
    Console.Error.WriteLine(error);
    return 2;
}

BaselineReport report = PerformanceRunner.Run(options);
JsonSerializerOptions jsonOptions = new()
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
};
string reportJson = JsonSerializer.Serialize(report, jsonOptions);
Console.WriteLine(reportJson);

if (options.ReportFile is not null)
{
    Directory.CreateDirectory(Path.GetDirectoryName(options.ReportFile)!);
    using FileStream reportStream = new(options.ReportFile, FileMode.CreateNew, FileAccess.Write, FileShare.None);
    using StreamWriter reportWriter = new(reportStream);
    reportWriter.WriteLine(reportJson);
}

return 0;
