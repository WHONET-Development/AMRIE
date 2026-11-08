using Interpretation_Engine.DataGenerator;

if (args.Length > 1)
    throw new ArgumentException("Usage: Interpretation Engine.DataGenerator [output-directory]");

string outputDirectory = args.Length == 1
    ? Path.GetFullPath(args[0])
    : Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "amrie-data-generator");

GenerationManifest manifest = new DatasetGenerator().Generate(outputDirectory);
Console.WriteLine($"Generated {manifest.TotalMeasurementCount:N0} measurements in {manifest.IsolateRows:N0} rows.");
Console.WriteLine($"Input: {manifest.InputFile} ({manifest.InputFileBytes:N0} bytes)");
Console.WriteLine($"Manifest: {manifest.ManifestFile}");
