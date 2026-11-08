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

The library targets .NET 8 and uses CsvHelper 34.0.0 for delimited-file parsing. It can be integrated with software on any platform that supports .NET 8,
including Windows, GNU/Linux and macOS.

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

Reference `Interpretation Engine\Interpretation Engine.csproj` from a .NET 8 application. The engine loads its tab-delimited data files from the `Resources` directory beneath `AppContext.BaseDirectory`; keep those resources beside the application when deploying the library. A project reference copies the engine's configured runtime resources and CsvHelper dependency to the application's output directory. If deploying the assembly separately, include `CsvHelper.dll` (34.0.0), keep the application's generated dependency and runtime manifests, and copy the required resource files into `Resources` yourself.

File parsing retains the previous delimiter, quoting, Unicode whitespace, BOM detection, multiline line endings, trimming, and variable-field-count behavior. Malformed quoted records still raise `MalformedLineException`; CsvHelper's recovery and string pooling are not enabled. `IO_Library.InterpretDataFile` still retains the complete input and interpretation results in memory; the parser migration does not introduce bounded processing.

The WinUI MSI bundles a self-contained CLI for the same runtime identifier as the GUI; both use the GUI's published private .NET runtime and CsvHelper DLL. Standalone CLI and legacy desktop builds remain framework-dependent.

For a single result, use the public configuration and interpretation APIs:

```csharp
using AMR_Engine;

InterpretationConfiguration configuration = InterpretationConfiguration.DefaultConfiguration();
string result = IsolateInterpretation.GetSingleInterpretation(configuration, "spn", "AMC_NM", "2");
```

The example returns the interpretation for a pneumococcal amoxicillin/clavulanic-acid MIC. Configure the guideline year, enabled expert rules, breakpoint types, infection sites, ETest rounding, and intrinsic-resistance handling through `InterpretationConfiguration` before calling the engine. For batch files, `IO_Library.InterpretDataFile` accepts `FileInterpretationParameters` and uses the same installed resources.
