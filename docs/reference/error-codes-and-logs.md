# Error Codes and Logs Reference

This reference covers all interpretation result codes, CLI exit codes, common error conditions, and troubleshooting guidance for AMRIE.

---

## CLI Exit Codes

| Code | Meaning |
| ---: | :------ |
| `0` | Success — interpretation completed normally. |
| `1` | Error — an unhandled exception occurred. The exception message and stack trace are written to stdout. |

---

## Interpretation Result Codes

Returned by `IsolateInterpretation.GetSingleInterpretation()` and written to output files by `IO_Library.InterpretDataFileBounded()`.

### Primary Codes

| Code | Constant | Meaning |
| :--: | :------- | :------ |
| `S` | `InterpretationCodes.Susceptible` | Susceptible at standard dosing. |
| `SDD` | `InterpretationCodes.SusceptibleDoseDependent` | Susceptible at higher or more frequent dosing (Susceptible-Dose Dependent). |
| `I` | `InterpretationCodes.Intermediate` | Intermediate / indeterminate (CLSI). Implies possible clinical success with high dosing or concentrated-site drugs. |
| `NS` | `InterpretationCodes.NonSusceptible` | Non-susceptible; used when only an S breakpoint exists (no I or R breakpoint defined). |
| `R` | `InterpretationCodes.Resistant` | Resistant under standard dosing. |
| `WT` | `InterpretationCodes.WildType` | Wild-type (ECOFF interpretation); no acquired resistance mechanism detected. |
| `NWT` | `InterpretationCodes.NonWildType` | Non-wild-type (ECOFF); reduced susceptibility or acquired mechanism likely. |
| `IN` | `InterpretationCodes.InRange` | QC measurement is within the acceptable reference range. |
| `OUT` | `InterpretationCodes.OutOfRange` | QC measurement is outside the acceptable reference range. |
| *(blank)* | `InterpretationCodes.Uninterpretable` | No applicable breakpoint found; result is blank/empty. |

### Comment Modifiers

Appended to a primary code when `IncludeInterpretationComments` is `true` in the configuration.

| Modifier | Constant | Meaning |
| :------: | :------- | :------ |
| `!` | `InterpretationCodes.ExclamationPoint` | Result was set or overridden by an expert interpretation rule (e.g., MRSA cascade). |
| `*` | `InterpretationCodes.Asterisk` | Result is based on species-level intrinsic resistance from `ExpectedResistancePhenotypes.txt`. |
| `?` | `InterpretationCodes.QuestionMark` | Measurement is outside the validated range for this test method. |

**Examples:**

| Output | Meaning |
| :----- | :------ |
| `R` | Resistant (clean result) |
| `R!` | Resistant via expert rule override |
| `R*` | Resistant via intrinsic resistance rule |
| `S?` | Susceptible but measurement is out of range |

Use `IsolateInterpretation.RemoveComments(string)` to strip modifiers from results.

---

## Measurement Validation Ranges

| Test type | Minimum | Maximum |
| :-------- | :-----: | :-----: |
| Disk diffusion (mm) | 6 | 80 |
| MIC / ETest (µg/mL) | 0.0001 | 2048 |

Measurements outside these ranges receive the `?` modifier (when comments are enabled) and may not match applicable breakpoints.

**Valid measurement modifiers:** `<` (less than) and `>` (greater than). The Unicode characters `≤` and `≥` are **not** supported and will cause interpretation failures.

---

## Logging and Output

AMRIE does not write to a log file. All diagnostic output is written to **stdout**:

| Event | Output |
| :---- | :----- |
| Invalid command-line arguments | Usage help message printed to stdout |
| Unhandled exception | Exception message + stack trace printed to stdout; exit code `1` |
| Successful `SINGLE_INTERPRETATION` | Interpretation string printed to stdout; JSON object written to `OutputFile` |
| Successful `FILE` | No stdout output; results written directly to `OutputFile` |

---

## Common Error Scenarios

### Invalid Arguments

| Symptom | Cause | Resolution |
| :------ | :---- | :--------- |
| Usage help printed; exit code `1` | Wrong number of arguments or unrecognized mode token | Verify argument count: `FILE` requires 5 total args; `SINGLE_INTERPRETATION` requires 6. Mode is case-insensitive. |
| `ArgumentException: Unsupported file delimiter ...` | Delimiter argument is not `TAB`, `,`, `;`, or `\|` | Use one of the four supported delimiter tokens. |

### Configuration Errors

| Symptom | Cause | Resolution |
| :------ | :---- | :--------- |
| `FileNotFoundException` on startup | `ConfigFile` path does not exist | Verify the path; use an absolute path or check the working directory. |
| `JsonException` during config load | Malformed JSON in config file | Validate JSON syntax. Note: comments and trailing commas are allowed. |
| Expert rules have no effect | `EnabledExpertInterpretationRules` is `[]` (empty) | An empty array disables all expert rules. Use `null` or omit the property to enable defaults, or list specific rule codes. |
| Wrong breakpoint year | `GuidelineYear` set to an unsupported value | Use a year for which breakpoints are bundled (e.g., `2026`). `0` resolves to the current bundled year. |

### Input File Errors

| Symptom | Cause | Resolution |
| :------ | :---- | :--------- |
| `InvalidDataException: The input file does not contain a non-empty header record.` | Input file is empty or first non-blank row is empty | Ensure the input file has a non-empty header row as its first data record. |
| `CsvException` or parse error | Malformed CSV/delimited data | Check for unmatched quotes, embedded newlines without quoting, or delimiter mismatch between argument and file. |
| All results are blank | Antibiotic column names do not match WHONET naming convention | Column names must match the pattern `<ABX>_<GUIDELINE><METHOD>[POTENCY]` (e.g., `AMP_NM`, `CRO_ED10`). See [WHONET Antibiotic Column Naming Convention](resource-data-schemas.md#7-whonet-antibiotic-column-naming-convention). |
| Organism not interpreted | `ORGANISM` column missing or empty | Input file must have an `ORGANISM` column containing WHONET organism codes. |

### User-Defined Breakpoints

| Symptom | Cause | Resolution |
| :------ | :---- | :--------- |
| User-defined breakpoints not applied | `UserDefinedBreakpointsFile` path not found | The path is resolved relative to `AppContext.BaseDirectory` (the AMRIE executable directory). Verify the file exists at that resolved path. |
| User-defined breakpoints silently ignored | File path resolves but file has wrong format | Ensure the file uses the same tab-delimited schema as `Breakpoints.txt`. See [Resource Data Schemas — Breakpoints.txt](resource-data-schemas.md#3-breakpointstxt--clinical-breakpoints--ecoffs). |

### Output File Errors

| Symptom | Cause | Resolution |
| :------ | :---- | :--------- |
| `IOException` / access denied | Output file is open in another process | Close the file in any other application before running AMRIE. |
| Output directory does not exist | Parent directory of `OutputFile` does not exist | Create the output directory before running AMRIE. |
| Partial output / empty file on error | Write failed mid-operation | AMRIE stages output to a temporary file and only publishes (via `File.Move`) on success; a partial run leaves no partial output file — the original file (if any) is preserved. |

---

## Troubleshooting Quick Reference

| Symptom | Likely Cause | Resolution |
| :------ | :----------- | :--------- |
| Exit code `1`, no output | Unhandled exception | Read the exception message on stdout for the specific cause. |
| All interpretations are blank | No matching breakpoints | Check `GuidelineYear`, `PrioritizedBreakpointTypes`, and that WHONET antibiotic column names are correct. |
| All interpretations are `R*` | `UseIntrinsicResistanceRules: true` matching all drugs | Verify organism code is correct; set `UseIntrinsicResistanceRules: false` to diagnose. |
| Expert rule cascade not firing | `EnabledExpertInterpretationRules: []` | Use `null` or list the desired rule codes. |
| Modifiers (`!`, `*`) in output | `IncludeInterpretationComments: true` | Set to `false` to suppress modifiers. |
| Output file not updated after error | Expected; by design | AMRIE uses atomic staging — partial runs do not corrupt the output file. |
| Wrong sites of infection selected | `PrioritizedSitesOfInfection` or `DisabledSitesOfInfection` misconfigured | Log the effective site order by reviewing `UpdateSitesOfInfection()` behavior in [API Reference](api-reference.md#interpretationconfiguration). |

---

## See Also

- [CLI Reference](cli-reference.md)
- [API Reference](api-reference.md)
- [Configuration Schema](configuration-schema.md)
- [Resource Data Schemas](resource-data-schemas.md)
