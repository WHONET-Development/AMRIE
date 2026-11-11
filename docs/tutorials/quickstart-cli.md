# Quickstart: AMRIE Command-Line Interface (CLI)

This tutorial walks you through installing `AMRIE.CLI` and running your first antimicrobial susceptibility interpretation from the command line. It covers both whole-file batch processing and single-measurement lookups with JSON output.

By the end of this tutorial you will have:
- Installed (or built from source) the `AMRIE.CLI` tool.
- Run a batch interpretation of a WHONET-formatted data file.
- Retrieved a single-measurement interpretation as a JSON object.
- Understood exit codes and basic error handling.

---

## Prerequisites

Choose one of the following runtime options:

### Option A — Build from Source (Recommended for development)

- **.NET 10 SDK** (`10.0.100` or later). Download from [dotnet.microsoft.com](https://dotnet.microsoft.com/en-us/download/dotnet/10.0).
- The AMRIE source repository cloned locally.

### Option B — Install via WinGet (Recommended for end-user deployment)

- **Windows 10 version 1809 or later** (or Windows 11).
- **Windows Package Manager** (`winget`), which ships with Windows 10 1809+ and later.

> [!NOTE]
> `AMRIE.CLI` targets `.NET 10` exclusively. If you are embedding AMRIE in a .NET Framework 4.8 application, use `AMRIE.Library` directly — see the [Library Integration Quickstart](./quickstart-library.md).

---

## Step 1 — Install AMRIE.CLI

### Via WinGet (Option B)

The simplest path on a Windows machine is to install AMRIE using the Windows Package Manager:

```powershell
winget install WHONET.AMRIE
```

After installation, `AMRIE.CLI.exe` is available from any terminal window (the installer adds it to `PATH`).

### Build from Source (Option A)

Clone the repository and build the CLI project:

```powershell
# Clone the repository
git clone https://github.com/WHONET-Development/AMRIE.git
cd AMRIE

# Build the CLI project
dotnet build AMRIE.CLI\AMRIE.CLI.csproj
```

The compiled executable is placed at:
```
AMRIE.CLI\bin\Debug\net10.0\AMRIE.CLI.exe
```

For automation use, build in Release configuration:

```powershell
dotnet build AMRIE.CLI\AMRIE.CLI.csproj --configuration Release
```

> [!NOTE]
> The `Resources/` directory containing clinical breakpoint tables (`Breakpoints.txt`, `Organisms.txt`, etc.) must reside alongside the executable at runtime. The build output already places these files correctly. If you copy the executable elsewhere, copy the entire `Resources/` folder with it.

---

## Step 2 — Understand the Two Modes

`AMRIE.CLI` has two operating modes, selected by the first positional argument:

| Mode | Purpose | Arguments |
| :--- | :--- | :--- |
| `FILE` | Batch-interpret a whole delimited data file | 5 total (including mode) |
| `SINGLE_INTERPRETATION` | Interpret one organism/antibiotic/measurement triple | 6 total (including mode) |

If you call `AMRIE.CLI.exe` with the wrong number of arguments or an unrecognised mode, it prints usage information and exits with code `1`.

---

## Step 3 — FILE Mode: Batch File Interpretation

FILE mode reads a WHONET-formatted delimited text file, interprets every antibiotic measurement in every row using the specified configuration, and writes the results to an output file.

**Syntax:**
```
AMRIE.CLI.exe FILE <config-file> <delimiter> <input-file> <output-file>
```

| Argument | Description |
| :--- | :--- |
| `FILE` | Selects file-batch mode (case-insensitive). |
| `<config-file>` | Path to the JSON configuration file (e.g., `SampleConfig.json`). |
| `<delimiter>` | Column delimiter of the input file. Accepted tokens: `TAB`, `,`, `;`, `\|`. |
| `<input-file>` | Path to the WHONET-formatted input data file. |
| `<output-file>` | Path where the interpreted output file will be written. |

**Example — interpreting the bundled sample file:**

```powershell
AMRIE.CLI.exe FILE "AMRIE.Library\Resources\SampleConfig.json" "|" "AMRIE.Library\Resources\SampleInputFile.txt" "output_interpreted.txt"
```

What this command does:
1. Reads `SampleConfig.json` to load interpretation settings (guideline year 2026, Human breakpoint priority, MRS/BLNAR-HFLU/ICR expert rules enabled).
2. Opens `SampleInputFile.txt` (pipe-delimited WHONET format, 623 rows of synthetic laboratory data).
3. Interprets every antibiotic column in every row in parallel using bounded batch streaming.
4. Writes `output_interpreted.txt` — a tab-delimited file identical to the input but with an `_INTERP` column appended after each antibiotic measurement column (e.g., `PEN_ND10_INTERP`, `ERY_ND15_INTERP`).

> [!NOTE]
> The output is always **tab-delimited** regardless of the input delimiter. The column order in the output is deterministic: original columns appear first, each followed immediately by its `_INTERP` counterpart. This makes the output easy to open in Excel or load into a database.

**Using `dotnet run` instead of the compiled executable:**

If you have not built the project yet, you can run it directly with:

```powershell
dotnet run --project "AMRIE.CLI\AMRIE.CLI.csproj" -- FILE "AMRIE.Library\Resources\SampleConfig.json" "|" "AMRIE.Library\Resources\SampleInputFile.txt" "$env:TEMP\AMRIE-output.txt"
```

---

## Step 4 — SINGLE_INTERPRETATION Mode: One-Shot Lookup

SINGLE_INTERPRETATION mode interprets a single organism/antibiotic/measurement combination and writes the result to both stdout and a JSON file. This is ideal for scripting one-off queries or integrating AMRIE into shell pipelines.

**Syntax:**
```
AMRIE.CLI.exe SINGLE_INTERPRETATION <config-file> <organism-code> <antibiotic-code> <measurement> <output-file>
```

| Argument | Description |
| :--- | :--- |
| `SINGLE_INTERPRETATION` | Selects single-lookup mode (case-insensitive). |
| `<config-file>` | Path to the JSON configuration file. |
| `<organism-code>` | WHONET organism code (e.g., `spn` for *Streptococcus pneumoniae*, `eco` for *Escherichia coli*). |
| `<antibiotic-code>` | WHONET antibiotic column name, including test method suffix (e.g., `AMC_NM` for Amoxicillin-clavulanate MIC, `PEN_ND10` for Penicillin disk 10 µg). |
| `<measurement>` | Quantitative measurement value as a string (e.g., `2` for 2 µg/mL MIC, `19` for 19 mm zone diameter). |
| `<output-file>` | Path where the JSON result will be written. |

**Example — interpret *S. pneumoniae* Amoxicillin-clavulanate MIC = 2 µg/mL:**

```powershell
AMRIE.CLI.exe SINGLE_INTERPRETATION "AMRIE.Library\Resources\SampleConfig.json" "spn" "AMC_NM" "2" "result.json"
```

This command:
1. Loads the configuration from `SampleConfig.json`.
2. Looks up the CLSI 2026 breakpoint for *S. pneumoniae* (`spn`) tested against Amoxicillin-clavulanate MIC (`AMC_NM`).
3. Prints the interpreted category to **stdout** (e.g., `S`).
4. Writes the full result as a JSON object to `result.json`.

**Stdout output:**
```
S
```

---

## Step 5 — JSON Output Format

For SINGLE_INTERPRETATION mode, the JSON output file contains a structured object with four fields:

```json
{
  "OrganismCode": "spn",
  "AntibioticCode": "AMC_NM",
  "Measurement": "2",
  "Interpretation": "S"
}
```

| Field | Description |
| :--- | :--- |
| `OrganismCode` | The organism code you passed on the command line. |
| `AntibioticCode` | The antibiotic column code you passed. |
| `Measurement` | The raw measurement string you passed. |
| `Interpretation` | The interpretation result (`S`, `I`, `SDD`, `R`, `NS`, or blank). May include comment markers (`!`, `*`) if `IncludeInterpretationComments` is `true` in the config. |

> [!NOTE]
> When `IncludeInterpretationComments` is `false` in the configuration (the default in `SampleConfig.json`), comment markers are stripped from the interpretation before the JSON is written, so `Interpretation` will contain only the category code (e.g., `R`, not `R!`).

You can consume this JSON in a shell script or pipeline. For example, using PowerShell:

```powershell
$result = Get-Content "result.json" | ConvertFrom-Json
Write-Host "Result for $($result.OrganismCode): $($result.Interpretation)"
```

---

## Step 6 — Exit Codes

| Exit Code | Meaning |
| :--- | :--- |
| `0` | Success — the command completed without errors. |
| `1` | Error — invalid arguments, file not found, parse error, or an unhandled exception. The error message and stack trace are printed to stdout. |

In shell scripts, check `$LASTEXITCODE` (PowerShell) or `$?` / `$PIPESTATUS` (bash) to detect failures:

```powershell
AMRIE.CLI.exe FILE "SampleConfig.json" "|" "input.txt" "output.txt"
if ($LASTEXITCODE -ne 0) {
    Write-Error "AMRIE interpretation failed. Check the output above for details."
    exit 1
}
```

---

## Next Steps

You have now run your first batch and single-measurement interpretations with the AMRIE CLI. Continue with:

- **[CLI Reference](../reference/cli-reference.md)** — Complete argument reference, delimiter token table, and all configuration options available to the CLI.
- **[Batch Processing Guide](../how-to/batch-processing-guide.md)** — Best practices for large-volume processing, output format options (horizontal vs. vertical), and integration with ETL pipelines.
