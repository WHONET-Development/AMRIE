# Quickstart: AMRIE Library Integration (.NET)

This tutorial walks you through embedding `AMRIE.Library` in your own .NET application so you can interpret antimicrobial susceptibility test (AST) measurements programmatically. It covers adding the library reference, loading a configuration, interpreting a single measurement, and processing a whole data file.

By the end of this tutorial you will have:
- Added a reference to `AMRIE.Library` in your project.
- Loaded an `InterpretationConfiguration` from the default settings and from a JSON file.
- Called `IsolateInterpretation.GetSingleInterpretation()` to interpret one measurement.
- Called `IO_Library.InterpretDataFileBounded()` to process a WHONET-formatted file.
- Understood the interpretation category codes returned by the engine.

---

## Prerequisites

- **.NET 10 SDK** (`10.0.100` or later) **or** **.NET Framework 4.8 Developer Pack** (Windows only).
- `AMRIE.Library` multi-targets `net10.0` and `net48`, so it integrates with both modern and legacy .NET applications.
- The AMRIE source repository (or a NuGet package reference if one has been published for your version).

> [!IMPORTANT]
> The `AMRIE.Library` namespace is `AMRIE.Library`. Do **not** use `using AMR_Engine;` — that old namespace no longer exists and will cause compile errors. All public types are under the `AMRIE.Library` namespace.

---

## Step 1 — Add a Reference to AMRIE.Library

### Project Reference (source checkout)

Add a project reference to `AMRIE.Library` in your `.csproj`:

```xml
<ItemGroup>
  <ProjectReference Include="..\AMRIE.Library\AMRIE.Library.csproj" />
</ItemGroup>
```

Your project must target a compatible framework. `AMRIE.Library` supports `net10.0` and `net48`:

```xml
<!-- For a cross-platform .NET 10 app: -->
<TargetFramework>net10.0</TargetFramework>

<!-- For a Windows legacy .NET Framework 4.8 app: -->
<TargetFramework>net48</TargetFramework>
```

> [!IMPORTANT]
> The `Resources/` folder (containing `Breakpoints.txt`, `Organisms.txt`, `Antibiotics.txt`, etc.) must be present in the application's base directory at runtime — specifically at `AppContext.BaseDirectory/Resources/`. When using a project reference, add the following to your `.csproj` to copy the resource files on build:
>
> ```xml
> <ItemGroup>
>   <None Include="..\AMRIE.Library\Resources\**" CopyToOutputDirectory="PreserveNewest" />
> </ItemGroup>
> ```

---

## Step 2 — Add the Using Directive

At the top of any source file that uses AMRIE types, add:

```csharp
using AMRIE.Library;
```

This single namespace contains all the public API surface you need:
- `InterpretationConfiguration` — configuration container and factory methods.
- `IsolateInterpretation` — single-isolate and single-measurement interpretation.
- `IO_Library` — bounded streaming file processor.
- `FileInterpretationParameters` — parameters object for file processing.

---

## Step 3 — Load a Configuration

The interpretation engine is controlled by an `InterpretationConfiguration` object. There are two ways to obtain one.

### Option A — Default Configuration

The default configuration applies the latest bundled guideline year, enables the standard expert rules (MRS, BLNAR, ICR), and prioritises Human breakpoint types:

```csharp
using AMRIE.Library;

InterpretationConfiguration config = InterpretationConfiguration.DefaultConfiguration();
```

This is the quickest way to get started. The default configuration has the following key settings:
- `GuidelineYear`: the current breakpoint table revision year (e.g., 2026).
- `RoundHalfDilutions`: `true` — MIC values at half-dilution steps are rounded before breakpoint lookup.
- `IncludeInterpretationComments`: `true` — expert rule override markers (`!`) are included in the output.
- `EnabledExpertInterpretationRules`: MRS (methicillin-resistant staphylococci), BLNAR (*H. influenzae* beta-lactamase), ICR (inducible clindamycin resistance).
- `PrioritizedBreakpointTypes`: `["Human"]` — Animal and ECOFF breakpoints are excluded.

### Option B — Load from a JSON Configuration File

For reproducible, institutionally-defined settings, load a configuration from a JSON file (see [`SampleConfig.json`](../../AMRIE.Library/Resources/SampleConfig.json) for a complete example):

```csharp
using AMRIE.Library;

InterpretationConfiguration config = InterpretationConfiguration.ReadConfiguration("SampleConfig.json");
```

The `ReadConfiguration` method handles comments (`// ...`) and trailing commas in the JSON, and it validates the loaded configuration (filtering out unrecognised sites of infection, loading any `UserDefinedBreakpointsFile`, etc.).

> [!NOTE]
> `ReadConfiguration` resolves `UserDefinedBreakpointsFile` relative to `AppContext.BaseDirectory`, the same directory where the `Resources/` folder lives. Use forward slashes or the platform path separator in the JSON value.

---

## Step 4 — Interpret a Single Measurement

`IsolateInterpretation.GetSingleInterpretation()` is the simplest API entry point. It takes a configuration, an organism code, an antibiotic column code, and a measurement, and returns the interpretation category as a string.

```csharp
using AMRIE.Library;

// Load the default configuration
InterpretationConfiguration config = InterpretationConfiguration.DefaultConfiguration();

// Interpret Streptococcus pneumoniae ("spn") against
// Amoxicillin-clavulanate MIC ("AMC_NM") at 2 µg/mL
string interpretation = IsolateInterpretation.GetSingleInterpretation(
    config,
    organismCode: "spn",
    antibioticCode: "AMC_NM",
    measurement: "2");

Console.WriteLine($"Interpretation: {interpretation}");
// Output: Interpretation: S
```

**Method signature:**

```csharp
public static string GetSingleInterpretation(
    InterpretationConfiguration interpretationConfig,
    string organismCode,
    string antibioticCode,
    string measurement)
```

| Parameter | Description |
| :--- | :--- |
| `interpretationConfig` | The loaded configuration — controls guideline year, expert rules, breakpoint type priority, etc. |
| `organismCode` | WHONET organism code (e.g., `eco` = *E. coli*, `sau` = *S. aureus*, `spn` = *S. pneumoniae*). Case-insensitive. |
| `antibioticCode` | WHONET antibiotic column name: three-letter drug code, underscore, guideline code, test method suffix. Examples: `AMC_NM` (Amox/Clav MIC), `PEN_ND10` (Penicillin disk 10 µg), `GEN_NE` (Gentamicin ETest). |
| `measurement` | The quantitative value as a string. For MICs, this is the µg/mL value (e.g., `"0.5"`, `"2"`, `">32"`). For disk diffusion, the zone diameter in mm (e.g., `"19"`). Prefix operators `<=`, `>=`, `<`, `>` are supported. |

**Return value:** A string containing the clinical category code (`S`, `I`, `SDD`, `R`, `NS`, `WT`, `NWT`), optionally followed by comment markers (`!` for expert-rule override, `*` for QC flag) when `IncludeInterpretationComments` is `true`. Returns an empty string when no applicable breakpoint is found.

> [!NOTE]
> To strip comment markers from the result, call `IsolateInterpretation.RemoveComments(interpretation)` before using the value.

**Additional examples:**

```csharp
// E. coli ("eco") Ciprofloxacin MIC 0.5 µg/mL — EUCAST 2026
InterpretationConfiguration eucastConfig = InterpretationConfiguration.ReadConfiguration("SampleConfig.json");
string ecoResult = IsolateInterpretation.GetSingleInterpretation(eucastConfig, "eco", "CIP_NM", "0.5");
Console.WriteLine(ecoResult); // e.g., "I"

// S. aureus ("sau") Oxacillin disk 1 µg — MRS expert rule may apply
InterpretationConfiguration defaultConfig = InterpretationConfiguration.DefaultConfiguration();
string sauResult = IsolateInterpretation.GetSingleInterpretation(defaultConfig, "sau", "OXA_ND1", "6");
// If MRSA is detected via MRS expert rule: "R!" (R with expert-rule comment)
string cleanResult = IsolateInterpretation.RemoveComments(sauResult); // "R"
```

---

## Step 5 — Process a Data File

For batch processing of WHONET-formatted delimited text files, use `IO_Library.InterpretDataFileBounded()`. This method streams the file in bounded batches (up to 2,048 rows or 4 MB of characters per batch), interprets rows in parallel, and writes the output atomically — the output file is only published after all rows succeed.

```csharp
using System.ComponentModel;
using AMRIE.Library;

// Build the parameters object for file interpretation
var parameters = new FileInterpretationParameters(
    inputFile_:    @"AMRIE.Library\Resources\SampleInputFile.txt",  // Input WHONET file
    delimiter_:    '|',                                              // Pipe-delimited
    guidelineYear_: -1,                                              // -1 = use year from config file
    configFile_:   @"AMRIE.Library\Resources\SampleConfig.json",    // Configuration
    outputFile_:   @"output_interpreted.txt",                        // Output path
    worker_:       null                                              // No BackgroundWorker (synchronous)
);

// Wrap parameters in DoWorkEventArgs (required by the method signature)
var workArgs = new DoWorkEventArgs(parameters);

// Run interpretation — blocks until complete
IO_Library.InterpretDataFileBounded(null, workArgs);

Console.WriteLine("Interpretation complete. Results written to output_interpreted.txt");
```

**Constructor signature for `FileInterpretationParameters`:**

```csharp
public FileInterpretationParameters(
    string inputFile_,
    char delimiter_,
    int guidelineYear_,
    string configFile_,
    string outputFile_,
    BackgroundWorker worker_)
```

| Parameter | Description |
| :--- | :--- |
| `inputFile_` | Full path to the WHONET-formatted input data file. |
| `delimiter_` | Single character: `'|'`, `','`, `';'`, or `'\t'`. Must be one of the four accepted delimiters. |
| `guidelineYear_` | Guideline year as an integer (e.g., `2026`). Pass `-1` to use the year specified in the configuration file. |
| `configFile_` | Full path to the JSON configuration file. |
| `outputFile_` | Full path for the output file. The directory must already exist. |
| `worker_` | A `BackgroundWorker` instance for progress reporting and cancellation support, or `null` for synchronous use. |

> [!NOTE]
> When running in a UI application (e.g., WinForms or WPF), pass a `BackgroundWorker` to `worker_` and handle its `ProgressChanged` and `RunWorkerCompleted` events to keep the UI responsive. The engine calls `worker.ReportProgress(percentage)` with values from 0 to 100 as it processes the file.

**Output file format:**

The output is a **tab-delimited** text file. Each antibiotic measurement column from the input (e.g., `PEN_ND10`) is immediately followed by a corresponding interpretation column (e.g., `PEN_ND10_INTERP`). All other columns are preserved unchanged.

When `HorizontalAntibioticResults` is `false` in the configuration, the output is **vertical**: one row per antibiotic per isolate, with three appended columns (`ANTIBIOTIC_CODE`, `ANTIBIOTIC_MEASUREMENT`, `ANTIBIOTIC_INTERPRETATION`).

---

## Step 6 — Reading Interpretation Results

Whether you use `GetSingleInterpretation` or `InterpretDataFileBounded`, the category codes returned are:

| Code | Description |
| :--- | :--- |
| `S` | **Susceptible** — Infection likely treatable with standard dosing. |
| `I` | **Intermediate** — Susceptibility at higher-than-standard exposure; pharmacokinetic optimization may achieve clinical efficacy. |
| `SDD` | **Susceptible-Dose Dependent** — Treatment requires optimized dosing regimen (CLSI-specific). |
| `R` | **Resistant** — Treatment unlikely to succeed even with higher doses. |
| `NS` | **Non-Susceptible** — Used when only a susceptible breakpoint exists; test result falls above it. |
| `WT` | **Wild-Type** — No acquired resistance mechanisms detected (ECOFF-based). |
| `NWT` | **Non-Wild-Type** — Acquired resistance mechanism likely present (ECOFF-based). |
| *(empty string)* | No applicable breakpoint found for this organism/antibiotic combination. |

**Comment markers** (present when `IncludeInterpretationComments` is `true`):

| Marker | Meaning |
| :--- | :--- |
| `!` | Result was overridden by an expert interpretation rule (e.g., `R!` = Resistant due to MRS/MRSA rule). |
| `*` | Additional comment or QC flag is associated with this result. |

Call `IsolateInterpretation.RemoveComments(interpretation)` to strip markers if you only need the category code for downstream processing.

---

## Next Steps

You are now ready to integrate AMRIE into your own application. Continue with:

- **[API Reference](../reference/api-reference.md)** — Complete C# API reference for all public types in `AMRIE.Library`, including `InterpretationConfiguration`, `IsolateInterpretation`, `IO_Library`, `FileInterpretationParameters`, and the breakpoint/organism data model classes.
- **[Configuration Schema](../reference/configuration-schema.md)** — JSON schema documentation for `SampleConfig.json`, including all fields, their types, default values, and the effect of each setting on interpretation behaviour.
