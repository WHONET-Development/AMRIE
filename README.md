# AMRIE
## Antimicrobial Resistance Test Interpretation Engine

This software was created by the [WHONET](https://whonet.org) development team to facilitate
the interpretation of antibiotic test measurements according to the published guidelines.
For more information about the guidelines, please visit their websites: [CLSI](https://clsi.org/), [EUCAST](https://www.eucast.org/), [SFM](https://www.sfm-microbiologie.org/)

You can install this software on Windows using the command `winget install WHONET.AMRIE` or by downloading the latest release from GitHub.

There are three sets of needs that the solution aims to support:
1. Interactive use through one of the two desktop applications
2. Command line use
3. Library integration with an external system

The library targets .NET 10 LTS and .NET Framework 4.8 and uses CsvHelper 34.0.0 for delimited-file parsing. Its .NET 10 target can be integrated with software on any platform that supports .NET 10, including Windows, GNU/Linux and macOS. The .NET Framework target supports WHONET on Windows without requiring a .NET 10 runtime.

The interactive and command line interfaces exercise various interpretation features, demonstrating how one might incorporate the library into their systems. The user
interfaces can be used to generate interpretations as an end goal, but we expect that most groups will have a greater need for library integration to provide the interpretations for
their own work.

The interactive applications may be used to generate interpretations for an entire data file, or explore ad hoc interpretations and view filtered resources.
The ad hoc functionality allows you to select from lists of organisms and antibiotics, and provide an associated test measurement to interpret.
To generate interpretations for an entire data file using the graphical interface or command line application, the input data file must use WHONET naming conventions for the 
data columns, measurements, organism codes, etc. Sample data, configuration files and system resources (such as organism, antibiotic and breakpoint lists)
are provided in the installation package's `Resources` folder.

The command line interface is another option which can be used to generate interpretations. The command line application can be called as needed from another application, from a batch script, etc.

We also provide SQL queries that demonstrate certain concepts, but which are
not used by the code. These concepts are implemented in C# to assist others if
they choose to make their own implementations, and also to eliminate any
dependence on a certain database technology which could restrict cross-platform compatibility.

To improve processing performance without the help of the database, the system has automatic caching built in,
and includes a way to "preheat" the cache to maximize interpretation performance if additional information about the input data set can be provided ahead of time.
To observe the caching behavior's effect, notice the difference in processing speed between the first and second times you press the "Interpret data file" button on the graphical user interface.
Subsequent runs of the system will utilize the breakpoint cache, greatly improving processing performance. The same behavior will automatically be present when the library is integrated in a
3rd party system.

## Library integration

Reference `Interpretation Engine\Interpretation Engine.csproj` from a .NET 10 or .NET Framework 4.8 application; MSBuild selects the matching target. The engine loads its tab-delimited data files from the `Resources` directory beneath `AppContext.BaseDirectory`; keep those resources beside the application when deploying the library. A project reference copies the engine's configured runtime resources and dependencies to the application's output directory. If deploying the assembly separately, include the resolved dependencies and resource files. The .NET Framework target requires its compatible CsvHelper, PolyfillLib, System.Text.Json and transitive dependencies; verify assembly binding redirects in the consuming application's configuration. For .NET 10, keep the application's generated dependency and runtime manifests.

File parsing uses CsvHelper directly. Supported delimiters are comma, TAB, semicolon, and pipe; the CLI accepts `TAB` or exactly one of those delimiter characters. Other API delimiter values throw `ArgumentException` before parsing. UTF BOM detection, quoted and multiline fields, trimming, and variable field counts are configured explicitly. Malformed quoted records raise CsvHelper's `CsvFormatException` with line tracking enabled; CsvHelper recovery and string pooling are disabled.

`IO_Library.InterpretDataFileBounded` is the file-processing entry point used by the CLI, both desktop interfaces and the performance harness's file mode. It processes owning batches of at most 2,048 rows or 4,194,304 UTF-16 input-value characters. The original `InterpretDataFile` public entry remains a compatibility delegate to this same bounded implementation; no whole-file-retention implementation remains in the library. A single oversized row is processed alone, without truncation. Parser buffers/lookahead and the largest individual record can exceed the nominal character budget. Breakpoint and intrinsic-resistance caches use exact keys and FIFO eviction: respectively 262,144 entries/16,777,216 retained key characters and 131,072 entries/4,194,304 key characters. Oversized keys are evaluated normally but not cached; eviction can affect speed, never interpretations. Preheat hints are limited to 32,768 keys per batch. Resource catalogs and global column metadata remain resource/header-sized rather than row-sized.

Owning input/result dictionaries are temporarily disk-staged so the exact first-seen global interpretation headers, column collisions, horizontal/vertical layout and row order remain unchanged. A completed UTF-8 output is flushed and published by same-directory rename. Until publication, cancellation or processing errors leave an existing destination unchanged; temporary stages are cleaned up. The destination directory must exist and be writable, with room for both row staging and the finished output (temporary disk use grows with file size). Input is read under a read-only sharing handle and closes before publication, so input/output may still refer to the same path. Progress is monotonic across interpretation, output writing and publication. No production memory sampling or forced garbage collection is added.

The WinUI MSI bundles a self-contained CLI for the same runtime identifier as the GUI; both use the GUI's published private .NET runtime and CsvHelper DLL. Standalone CLI and legacy desktop builds remain framework-dependent.

For a single result, use the public configuration and interpretation APIs:

```csharp
using AMR_Engine;

InterpretationConfiguration configuration = InterpretationConfiguration.DefaultConfiguration();
string result = IsolateInterpretation.GetSingleInterpretation(configuration, "spn", "AMC_NM", "2");
```

The example returns the interpretation for a pneumococcal amoxicillin/clavulanic-acid MIC. Configure the guideline year, enabled expert rules, breakpoint types, infection sites, ETest rounding, and intrinsic-resistance handling through `InterpretationConfiguration` before calling the engine. For batch files, call `IO_Library.InterpretDataFileBounded` with a `DoWorkEventArgs` containing `FileInterpretationParameters`; it uses the same installed resources.

## Building and running

Install a stable .NET 10 SDK (10.0.100 or later in the 10.0 feature bands selected by `global.json`). Building the .NET Framework target also requires the .NET Framework 4.8 targeting pack or compatible reference assemblies. Its C# 14 language version is an explicit, unsupported override of the C# 7.3 framework default; not every newer language feature or runtime API is available. Windows desktop and installer builds also require Visual Studio with WinUI build tools and the Windows SDK. Use a Visual Studio Developer PowerShell for Release builds so the existing code-signing tools and certificate are available. On Windows, Release builds sign the .NET Framework engine DLL as the former WHONET engine project did.

WHONET references the engine project from the sibling AMRIE checkout. Keep that checkout on the `NET_48` branch and restore both solutions' NuGet dependencies before building WHONET. Its framework regression tests must run in an isolated test host so the engine's resources and assembly binding configuration are resolved from the test output directory.

Build `Antimicrobial Interpretation System.sln` in Debug or Release using the x64 solution platform to include both MSI projects. `BuildAndRun.ps1` builds and launches WinUI; `Publish-WinUI.ps1` builds its MSI or publishes unpackaged binaries. The legacy MSI requires an architecture-matched .NET 10 Desktop Runtime on the destination machine; the WinUI MSI includes its private runtime. The release-to-winget workflow only submits existing release assets and does not install a .NET SDK.

To interpret the bundled pipe-delimited sample (the generated output is tab-delimited):

```powershell
dotnet run --project "Interpretation CLI\Interpretation CLI.csproj" -- FILE "Interpretation Engine\Resources\SampleConfig.json" "|" "Interpretation Engine\Resources\SampleInputFile.txt" "$env:TEMP\AMRIE-sample-output.txt"
dotnet test "Interpretation Engine.Tests\Interpretation Engine.Tests.csproj"
dotnet build "Interpretation Engine\Interpretation Engine.csproj" --framework net48
```
