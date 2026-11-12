# AMRIE Library API Reference

Namespace: `AMRIE.Library`  
Assembly: `AMRIE.Library`

This reference documents the public API surface of `AMRIE.Library` for embedding the interpretation engine in host applications.

---

## Table of Contents

1. [InterpretationConfiguration](#interpretationconfiguration)
2. [IsolateInterpretation](#isolateinterpretation)
3. [IO_Library](#io_library)
4. [FileInterpretationParameters](#fileinterpretationparameters)
5. [Constants — InterpretationCodes](#constants--interpretationcodes)
6. [Caching & Performance APIs](#caching--performance-apis)

---

## `InterpretationConfiguration`

Container for all runtime options that control the interpretation engine. Serializes to/from JSON.

```csharp
public class InterpretationConfiguration
```

### Properties

| Property | Type | Default | Description |
| :------- | :--- | :------ | :---------- |
| `RoundHalfDilutions` | `bool` | `true` | When `true`, ETest half-dilution values are rounded to the nearest standard dilution before breakpoint lookup. |
| `IncludeInterpretationComments` | `bool` | `false` | When `true`, comment modifiers (`!` for expert-rule override, `*` for intrinsic resistance) are retained in output. |
| `UseIntrinsicResistanceRules` | `bool` | `true` | When `true`, intrinsic resistance phenotype rules are applied before breakpoint evaluation. |
| `EnabledExpertInterpretationRules` | `List<string>` | `null` | Codes of enabled expert rules. `null` = all defaults active; empty list = all expert rules disabled. |
| `GuidelineYear` | `long` | `0` | Breakpoint table edition year. `0` resolves to the current bundled year at runtime. |
| `PrioritizedBreakpointTypes` | `List<string>` | `null` | Ordered preference list for breakpoint type selection (e.g., `["Human", "Animal", "ECOFF"]`). `null` = no filtering, all types eligible. |
| `PrioritizedSitesOfInfection` | `List<string>` | `null` | Ordered preference list for site-of-infection breakpoint selection. `null` = default engine order applies. |
| `DisabledSitesOfInfection` | `List<string>` | `null` | Sites of infection to exclude entirely. `null` or empty = no sites disabled. |
| `HorizontalAntibioticResults` | `bool` | `true` | When `true`, output is wide format (one row per isolate with `_INTERP` columns). When `false`, output is tall/vertical format (one row per antibiotic measurement). |
| `UserDefinedBreakpointsFile` | `string` | `null` | Relative path (from `AppContext.BaseDirectory`) to a user-defined breakpoints file in Breakpoints.txt schema format. |
| `UserDefinedBreakpoints` | `List<Breakpoint>` | `[]` | Loaded breakpoints from `UserDefinedBreakpointsFile`. Populated automatically by `ReadConfiguration()`. |

### Methods

#### `static DefaultConfiguration()`

Returns a new `InterpretationConfiguration` pre-populated with the recommended default settings.

```csharp
public static InterpretationConfiguration DefaultConfiguration()
```

**Returns:** `InterpretationConfiguration`

**Default values applied:**

| Property | Default value |
| :------- | :------------ |
| `RoundHalfDilutions` | `true` |
| `IncludeInterpretationComments` | `true` |
| `UseIntrinsicResistanceRules` | `true` |
| `EnabledExpertInterpretationRules` | `["MRStaph", "BLNAR", "ICR"]` |
| `GuidelineYear` | Current bundled breakpoint revision year |
| `PrioritizedBreakpointTypes` | `["Human"]` |
| `PrioritizedSitesOfInfection` | Full default site-of-infection order |

#### `static ReadConfiguration(string configFile)`

Deserializes a JSON configuration file into an `InterpretationConfiguration` instance. Property names are matched case-insensitively. JSON comments and trailing commas are permitted.

```csharp
public static InterpretationConfiguration ReadConfiguration(string configFile)
```

**Parameters:**

| Name | Type | Description |
| :--- | :--- | :---------- |
| `configFile` | `string` | Absolute path to the JSON configuration file. |

**Returns:** `InterpretationConfiguration`

**Behavior notes:**
- Empty arrays for `EnabledExpertInterpretationRules`, `PrioritizedBreakpointTypes`, and `PrioritizedSitesOfInfection` are normalized to `null` after deserialization.
- If `UserDefinedBreakpointsFile` is set and the resolved file exists (`AppContext.BaseDirectory + UserDefinedBreakpointsFile`), breakpoints are loaded automatically.
- `UpdateSitesOfInfection()` is called to validate and merge site-of-infection lists.

**Example:**

```csharp
InterpretationConfiguration config = InterpretationConfiguration.ReadConfiguration("/etc/amrie/lab.json");
```

#### `Clone()`

Creates a deep copy of this configuration.

```csharp
public InterpretationConfiguration Clone()
```

**Returns:** `InterpretationConfiguration` — an independent copy; modifications to the clone do not affect the original.

---

## `IsolateInterpretation`

Performs antimicrobial susceptibility interpretation for a single isolate (one data row). The static `GetSingleInterpretation` method is the primary entry point for single-result lookup; construct instances directly for full-row batch work.

```csharp
public class IsolateInterpretation
```

### Static Methods

#### `static GetSingleInterpretation(InterpretationConfiguration, string, string, string)`

Interprets one organism/antibiotic/measurement combination using the supplied configuration.

```csharp
public static string GetSingleInterpretation(
    InterpretationConfiguration interpretationConfig,
    string organismCode,
    string antibioticCode,
    string measurement)
```

**Parameters:**

| Name | Type | Description |
| :--- | :--- | :---------- |
| `interpretationConfig` | `InterpretationConfiguration` | Runtime interpretation settings. |
| `organismCode` | `string` | WHONET organism code (e.g., `eco`, `sau`, `spn`). |
| `antibioticCode` | `string` | Full WHONET antibiotic field name including guideline and method suffix (e.g., `AMP_NM`, `VAN_NE`). |
| `measurement` | `string` | Raw measurement string (MIC, disk zone, or ETest value). May include `<` / `>` modifiers. |

**Returns:** `string` — interpretation code (see [InterpretationCodes](#constants--interpretationcodes)). May include `!` (expert-rule override) or `*` (intrinsic resistance) modifiers when `IncludeInterpretationComments` is `true`.

**Example:**

```csharp
var config = InterpretationConfiguration.ReadConfiguration("lab.json");
string result = IsolateInterpretation.GetSingleInterpretation(config, "eco", "AMP_NM", "8");
// result => "R"
```

#### `static RemoveComments(string interpretation)`

Strips `!` and `*` comment modifiers from an interpretation string.

```csharp
public static string RemoveComments(string interpretation)
```

**Parameters:**

| Name | Type | Description |
| :--- | :--- | :---------- |
| `interpretation` | `string` | Raw interpretation string possibly containing modifiers. |

**Returns:** `string` — interpretation with `!` and `*` removed.

### Static Fields

| Field | Type | Description |
| :---- | :--- | :---------- |
| `ValidAntibioticFieldNameRegex` | `Regex` | Compiled regex that matches valid WHONET antibiotic column names (e.g., `PEN_ND10`, `AMP_EM`, `FOX_FE`). |
| `ValidAntibioticCode` | `Regex` | Compiled regex that matches the 3-letter (or user-defined `X_##`) antibiotic code portion only. |

### Constructor

```csharp
public IsolateInterpretation(
    Dictionary<string, string> dataRowValues,
    List<string> columnNames,
    List<string> enabledExpertInterpretationRules,
    List<Breakpoint> userDefinedBreakpoints,
    int guidelineYear = -1,
    List<string> prioritizedBreakpointTypes = null,
    List<string> prioritizedSitesOfInfection = null,
    bool roundHalfDilutions = true,
    bool useIntrinsicResistanceRules = true)
```

Prepares one data row for interpretation. Expert rules are applied during construction. Call `GetAllInterpretations()` to retrieve results.

| Parameter | Type | Description |
| :-------- | :--- | :---------- |
| `dataRowValues` | `Dictionary<string,string>` | Column name → value mapping for the isolate row (must include `ORGANISM` key). |
| `columnNames` | `List<string>` | Ordered list of all column names from the file header (used by expert rules). |
| `enabledExpertInterpretationRules` | `List<string>` | Active expert rule codes; `null` = all defaults. |
| `userDefinedBreakpoints` | `List<Breakpoint>` | Additional user-supplied breakpoints. |
| `guidelineYear` | `int` | Breakpoint table year; `-1` uses the config default. |
| `prioritizedBreakpointTypes` | `List<string>` | Breakpoint type preference order. |
| `prioritizedSitesOfInfection` | `List<string>` | Site-of-infection preference order. |
| `roundHalfDilutions` | `bool` | Whether to round half-dilution ETest values. |
| `useIntrinsicResistanceRules` | `bool` | Whether to apply intrinsic resistance rules. |

### Instance Methods

#### `GetAllInterpretations()`

Returns interpretations for all antibiotic columns in the row that have not already been handled by expert rules.

```csharp
public Dictionary<string, string> GetAllInterpretations()
```

**Returns:** `Dictionary<string, string>` — antibiotic field name → interpretation code.

---

## `IO_Library`

Provides file-level batch interpretation with bounded memory usage. Output is staged to a temporary file and atomically published only after successful completion.

```csharp
public class IO_Library
```

### Methods

#### `static InterpretDataFileBounded(object s, DoWorkEventArgs e)`

Interprets an entire delimited input file and writes tab-delimited output. Rows are processed in parallel within configurable batches.

```csharp
public static void InterpretDataFileBounded(object s, DoWorkEventArgs e)
```

**Parameters:**

| Name | Type | Description |
| :--- | :--- | :---------- |
| `s` | `object` | The `BackgroundWorker` sender, or `null` when calling from CLI/non-UI contexts. |
| `e` | `DoWorkEventArgs` | Work arguments. `e.Argument` must be a `FileInterpretationParameters` instance. Set `e.Cancel = true` to request cancellation; the method checks this between batches. |

**Behavior:**
- Reads configuration from `FileInterpretationParameters.ConfigFile`.
- Processes rows in batches bounded by `BatchRowLimit` (default 2 048) and `BatchCharacterLimit` (default 4 MiB).
- Parallelizes interpretation within each batch across `Environment.ProcessorCount` threads.
- Stages results in a temporary `.tmp` file; on success, atomically replaces `OutputFile` via `File.Move(..., overwrite: true)`.
- Cleans up temporary files even if the operation fails or is cancelled.
- Reports progress (0–100) via `BackgroundWorker.ReportProgress` when a worker is supplied.

#### `static InterpretDataFile(object s, DoWorkEventArgs e)`

Compatibility alias for `InterpretDataFileBounded`. Identical behavior.

```csharp
public static void InterpretDataFile(object s, DoWorkEventArgs e)
```

#### `static string[] SplitLine(string record, char delimiter)`

Splits a delimited record string respecting RFC 4180 quoting rules.

```csharp
public static string[] SplitLine(string record, char delimiter)
```

#### `static string ToLine(IEnumerable<string> values, char delimiter)`

Joins field values into a delimited record string, quoting fields that contain the delimiter, quotes, or line breaks.

```csharp
public static string ToLine(IEnumerable<string> values, char delimiter)
```

---

## `FileInterpretationParameters`

Input parameter container for `IO_Library.InterpretDataFileBounded`.

```csharp
public class FileInterpretationParameters
```

### Constructor

```csharp
public FileInterpretationParameters(
    string inputFile,
    char delimiter,
    int guidelineYear,
    string configFile,
    string outputFile,
    BackgroundWorker worker)
```

The constructor validates that `delimiter` is one of the four supported characters (TAB, comma, semicolon, pipe).

### Properties

| Property | Type | Description |
| :------- | :--- | :---------- |
| `InputFile` | `string` (readonly) | Absolute or relative path to the input delimited data file. |
| `Delimiter` | `char` (readonly) | Column delimiter character. Must be `\t`, `,`, `;`, or `\|`. |
| `GuidelineYear` | `int` (readonly) | Override guideline year; `-1` defers to the value in the configuration file. |
| `ConfigFile` | `string` (readonly) | Path to the JSON configuration file. |
| `OutputFile` | `string` (readonly) | Destination path for the interpreted output file (tab-delimited UTF-8). |
| `Worker` | `BackgroundWorker` (readonly) | Optional `BackgroundWorker` for progress reporting and cancellation; `null` for synchronous/CLI usage. |

---

## `Constants` — `InterpretationCodes`

All interpretation result codes produced by the engine.

```csharp
public class Constants
{
    public class InterpretationCodes { ... }
}
```

### Primary Interpretation Codes

| Constant | Value | Meaning |
| :------- | :---: | :------ |
| `Susceptible` | `"S"` | Isolate is susceptible at standard dosing. |
| `SusceptibleDoseDependent` | `"SDD"` | Susceptible with higher or more frequent dosing. |
| `Intermediate` | `"I"` | Intermediate / indeterminate (CLSI terminology). |
| `NonSusceptible` | `"NS"` | Non-susceptible; used when only an S breakpoint exists. |
| `Resistant` | `"R"` | Isolate is resistant. |
| `WildType` | `"WT"` | Wild-type (ECOFF; no acquired resistance mechanism detected). |
| `NonWildType` | `"NWT"` | Non-wild-type (ECOFF; reduced susceptibility). |
| `InRange` | `"IN"` | Within QC reference range. |
| `OutOfRange` | `"OUT"` | Outside QC reference range. |
| `Uninterpretable` | `""` | No applicable breakpoint found; result is blank. |

### Comment Modifiers

Comment modifiers may be appended to a primary code when `IncludeInterpretationComments` is `true`.

| Constant | Value | Meaning |
| :------- | :---: | :------ |
| `ExclamationPoint` | `"!"` | Result was set or overridden by an expert interpretation rule. |
| `Asterisk` | `"*"` | Result is based on intrinsic resistance (species-level). |
| `QuestionMark` | `"?"` | Measurement is outside the validated range for this test. |

**Example:** `"R!"` = Resistant due to an expert rule override. `"R*"` = Resistant due to intrinsic resistance.

Use `IsolateInterpretation.RemoveComments(string)` to strip modifiers.

---

## Caching & Performance APIs

### `AntibioticSpecificInterpretationRules.PreheatBreakpointCache(...)`

Populates the bounded breakpoint lookup cache ahead of batch processing to minimize thread contention on cache misses during parallel execution.

```csharp
public static void PreheatBreakpointCache(
    List<Breakpoint> userDefinedBreakpoints,
    int guidelineYear,
    List<string> prioritizedBreakpointTypes,
    List<string> prioritizedSitesOfInfection,
    List<Tuple<string, string, string>> distinctInterpretationKeys,
    BackgroundWorker worker = null)
```

**Parameters:**

| Name | Type | Description |
| :--- | :--- | :---------- |
| `userDefinedBreakpoints` | `List<Breakpoint>` | Additional user-defined breakpoints from configuration. |
| `guidelineYear` | `int` | Guideline revision year. |
| `prioritizedBreakpointTypes` | `List<string>` | Ordered preference list for breakpoint types. |
| `prioritizedSitesOfInfection` | `List<string>` | Ordered preference list for sites of infection. |
| `distinctInterpretationKeys` | `List<Tuple<string, string, string>>` | List of `(OrganismCode, Guideline, FullAntibioticField)` tuples to pre-resolve. |
| `worker` | `BackgroundWorker` | Optional worker for cancellation checking and progress reporting. |

### `BoundedCache<TKey, TValue>` & `BoundedCacheStatistics`

`AMRIE.Library` internally uses bounded FIFO caches to constrain memory usage when evaluating breakpoints and intrinsic resistance rules across large datasets.

```csharp
internal readonly record struct BoundedCacheStatistics(
    int Entries,
    long KeyCharacters,
    long Evictions,
    long UncachedKeys);
```

| Field | Type | Description |
| :---- | :--- | :---------- |
| `Entries` | `int` | Current number of entries in the cache. |
| `KeyCharacters` | `long` | Approximate memory footprint of stored key characters. |
| `Evictions` | `long` | Total number of FIFO evictions since cache initialization. |
| `UncachedKeys` | `long` | Keys processed without caching because key character size exceeded maximum bounds. |

---

## See Also

- [CLI Reference](cli-reference.md)
- [Configuration Schema](configuration-schema.md)
- [Resource Data Schemas](resource-data-schemas.md)
- [Error Codes and Logs](error-codes-and-logs.md)
