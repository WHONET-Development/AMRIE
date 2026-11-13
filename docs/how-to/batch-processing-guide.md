# How to Process Files in Batch Mode

This guide explains how to run AMRIE's batch file interpretation pipeline using the CLI or the .NET Library directly.

## Overview

AMRIE processes WHONET-format isolate files in bounded streaming batches (up to 2,048 rows or 4,194,304 characters per batch). This keeps memory usage flat regardless of file size.

---

## Input File Format

Input files use **pipe (`|`) delimiter** and follow the **WHONET column naming convention**.

### Header columns

The first row is a tab-or-pipe-separated header. Minimum required columns:

| Column | Description |
|---|---|
| `ORGANISM` | WHONET organism code (e.g., `eco`, `sau`, `pae`) |
| `<ABX>_NM` or `<ABX>_ND<potency>` | Antibiotic result columns — MIC (NM) or disk (ND) |

Additional epidemiological columns (`PATIENT_ID`, `SPEC_DATE`, `WARD`, etc.) pass through to the output unchanged.

### Example header (from `SampleInputFile.txt`)

```
ROW_IDX|COUNTRY_A|LABORATORY|PATIENT_ID|FIRST_NAME|LAST_NAME|SEX|AGE|DATE_BIRTH|WARD|INSTITUT|DEPARTMENT|WARD_TYPE|PAT_TYPE|SPEC_NUM|SPEC_DATE|SPEC_TYPE|SPEC_CODE|SPEC_REAS|DATE_DATA|ORGANISM|ORG_TYPE|BETA_LACT|MECA_PCR|INDUC_CLI|COMMENT|ORIGIN|ESBL|PEN_ND10|ERY_ND15|...
```

Antibiotic columns follow the WHONET naming pattern `<ABX_CODE>_<GUIDELINE><METHOD>[POTENCY]`:
- **`<ABX_CODE>`**: 3-letter standard code (e.g., `AMK`, `PEN`, `AMP`, `CRO`) or user-defined code `X_#` / `X_##` (e.g., `X_1` through `X_99`; zero-padded codes such as `X_01` are disallowed).
- **`<GUIDELINE>`**: Guideline identifier (`N` = CLSI, `E` = EUCAST, `F` = SFM).
- **`<METHOD>`**: Test method (`D` = Disk diffusion, `M` = MIC dilution, `E` = ETest gradient strip).
- **`[POTENCY]`**: Disk potency for disk diffusion tests (e.g., `10` for 10 µg, `30` for 30 µg, `1_2` for 1.25/23.75 µg).

Examples:
- `PEN_ND10` — Penicillin CLSI 10 µg disk diffusion
- `AMK_ED30` — Amikacin EUCAST 30 µg disk diffusion
- `CRO_NM` — Ceftriaxone CLSI MIC
- `AMP_EM` — Ampicillin EUCAST MIC
- `VAN_NE` — Vancomycin CLSI ETest
- `X_1_ND10` — User-defined antibiotic 1 CLSI 10 µg disk diffusion

> [!CAUTION]
> Never include real patient health information (PHI) in input files. All test isolates must be synthetic or fully de-identified per HIPAA Safe Harbor / GDPR standards.

---

## Configuration File (`SampleConfig.json`)

Place a JSON config file alongside your run. All fields are optional — omitting a field uses the default.

```json
{
  "RoundHalfDilutions": true,
  "IncludeInterpretationComments": false,
  "EnabledExpertInterpretationRules": [
    "BLNAR-HFLU",
    "MRS",
    "ICR"
  ],
  "GuidelineYear": 2026,
  "PrioritizedBreakpointTypes": [ "Human", "Animal", "ECOFF" ],
  "PrioritizedSitesOfInfection": [],
  "DisabledSitesOfInfection": [],
  "HorizontalAntibioticResults": true,
  "UserDefinedBreakpointsFile": "Resources\\MyCustomBreakpoints.txt"
}
```

### Field reference

| Field | Type | Default | Description |
|---|---|---|---|
| `RoundHalfDilutions` | bool | `true` | Round MIC values to nearest standard dilution step before comparing to breakpoints |
| `IncludeInterpretationComments` | bool | `false` | Append a comment column explaining each interpretation |
| `EnabledExpertInterpretationRules` | string[] | all defaults | Rule codes to apply (e.g., `MRS`, `BLNAR-HFLU`, `ICR`). `null` = all defaults; `[]` = none |
| `GuidelineYear` | int | current | CLSI/EUCAST guideline year to use for breakpoint lookup |
| `PrioritizedBreakpointTypes` | string[] | `["Human"]` | Order of preference: `"Human"`, `"Animal"`, `"ECOFF"`. `null` = no filter |
| `PrioritizedSitesOfInfection` | string[] | default order | Preferred infection sites (empty list = use default order) |
| `DisabledSitesOfInfection` | string[] | none | Sites to exclude entirely from breakpoint selection |
| `HorizontalAntibioticResults` | bool | `true` | Treat multiple antibiotic columns on the same row as a single isolate |
| `UserDefinedBreakpointsFile` | string | — | Path to a custom breakpoints file (relative to the app's resource root) |

---

## Running via CLI

```powershell
AMRIE.CLI.exe `
    --mode FILE `
    --input  "path\to\input.txt" `
    --config "path\to\config.json" `
    --output "path\to\output.txt" `
    --delimiter TAB
```

**Delimiter options:** `TAB`, `COMMA`, `SEMICOLON`, `PIPE`

**Short form (positional arguments):**

```powershell
dotnet run --project AMRIE.CLI\AMRIE.CLI.csproj -- `
    FILE "AMRIE.Library\Resources\SampleConfig.json" "|" `
    "AMRIE.Library\Resources\SampleInputFile.txt" `
    "$env:TEMP\amrie-output.txt"
```

---

## Running via Library (.NET)

```csharp
using AMRIE.Library;

var config = InterpretationConfiguration.ReadConfiguration("path/to/config.json");

var parameters = new FileInterpretationParameters
{
    InputFilePath  = "path/to/input.txt",
    OutputFilePath = "path/to/output.txt",
    Delimiter      = '|',
    Configuration  = config
};

IO_Library.InterpretDataFileBounded(parameters);
```

> [!NOTE]
> `InterpretDataFileBounded()` processes the file in streaming batches (≤ 2,048 rows / ≤ 4 MB of characters per batch) with an atomic publish pattern — the output file is only finalized when all batches complete successfully. It is safe for arbitrarily large files.

---

## Output Format

The output file has the same delimiter and all original columns as the input, plus appended interpretation result columns for each antibiotic:

| Original columns | `AMK_NM` | … | `AMK_NM_INTERP` | `AMK_NM_INTERP_COMMENTS` | … |
|---|---|---|---|---|---|
| (unchanged) | 4 | … | S | (if enabled) | … |

Interpretation values: **S** (Susceptible), **I** (Intermediate), **SDD** (Susceptible Dose-Dependent), **R** (Resistant), **NS** (Non-Susceptible), **WT** (Wild Type), **NWT** (Non-Wild Type).

---

## See Also

- [Custom Breakpoints](./custom-breakpoints.md)
- [Building and Testing](./building-and-testing.md)
- [Developer Setup](./developer-setup.md)
- [Reference: Resource Data Schemas](../reference/resource-data-schemas.md)
