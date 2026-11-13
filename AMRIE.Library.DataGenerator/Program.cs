using AMRIE.Library.DataGenerator;

if (!GeneratorCommandLine.TryParse(args, out GeneratorOptions? options, out string error) || options is null)
{
    Console.Error.WriteLine(error);
    return 2;
}

GenerationManifest manifest = DatasetGenerator.Generate(options.OutputDirectory, options.TargetSize);
Console.WriteLine($"Generated {manifest.TotalMeasurementCount:N0} measurements in {manifest.IsolateRows:N0} rows.");
Console.WriteLine($"Input: {manifest.InputFile} ({manifest.InputFileBytes:N0} bytes)");
Console.WriteLine($"Manifest: {manifest.ManifestFile}");
if (manifest.RequestedSizeBytes > 0)
    Console.WriteLine($"Requested minimum: {manifest.RequestedSize} ({manifest.RequestedSizeBytes:N0} bytes); over target by {manifest.InputSizeOvershootBytes:N0} bytes.");

return 0;
