# How to Define Custom Breakpoints

User-defined breakpoints let you supplement or override the standard CLSI/EUCAST/SFM breakpoints shipped with AMRIE — for example, to add institution-specific thresholds, provisional breakpoints not yet in a published guideline, or organism–drug combinations missing from the built-in tables.

## How user-defined breakpoints work

When `UserDefinedBreakpointsFile` is set in the configuration, AMRIE loads those breakpoints at startup and merges them with the built-in table.

> [!IMPORTANT]
> User-defined breakpoints take **precedence** over built-in breakpoints when a match is found. See the [Rule Precedence Hierarchy](../architecture/rule-precedence-hierarchy.md) for the full resolution order.

---

## Breakpoint file format

The file uses the **same tab-delimited schema** as the built-in `Breakpoints.txt` resource. The first row is a header:

```
GUIDELINES	YEAR	TEST_METHOD	POTENCY	ORGANISM_CODE	ORGANISM_CODE_TYPE	BREAKPOINT_TYPE	HOST	SITE_OF_INFECTION	REFERENCE_TABLE	REFERENCE_SEQUENCE	WHONET_ABX_CODE	WHONET_TEST	R	I	SDD	S	ECV_ECOFF	ECV_ECOFF_TENTATIVE	DATE_ENTERED	DATE_MODIFIED	COMMENTS
```

### Column reference

| Column | Required | Description / Example |
|---|---|---|
| `GUIDELINES` | Yes | Guideline source: `CLSI`, `EUCAST`, `SFM`, or a custom label |
| `YEAR` | Yes | Guideline year (e.g., `2026`) |
| `TEST_METHOD` | Yes | `MIC`, `DISK`, or `ETEST` |
| `POTENCY` | Disk only | Disk potency in µg (e.g., `10`); leave blank for MIC |
| `ORGANISM_CODE` | Yes | WHONET organism code or group code (e.g., `eco`, `ENT`, `GCT`) |
| `ORGANISM_CODE_TYPE` | Yes | `WHONET_ORG_CODE`, `GENUS_CODE`, `GENUS_GROUP`, `ANAEROBE`, etc. |
| `BREAKPOINT_TYPE` | Yes | `Human`, `Animal`, or `ECOFF` |
| `HOST` | Yes | Typically `Human` or `Animal` |
| `SITE_OF_INFECTION` | No | Leave blank to match all sites |
| `REFERENCE_TABLE` | No | Free text reference (for audit purposes) |
| `REFERENCE_SEQUENCE` | No | Numeric sequence within the reference |
| `WHONET_ABX_CODE` | Yes | WHONET antibiotic code (e.g., `AMK`, `CIP`) |
| `WHONET_TEST` | Yes | Column code used in data files (e.g., `AMK_NM`, `CIP_ND5`) |
| `R` | No | Resistant breakpoint (MIC mg/L or disk mm) |
| `I` | No | Intermediate breakpoint |
| `SDD` | No | Susceptible Dose-Dependent breakpoint |
| `S` | No | Susceptible breakpoint |
| `ECV_ECOFF` | No | Epidemiological Cut-Off value |
| `ECV_ECOFF_TENTATIVE` | No | Tentative ECOFF |
| `DATE_ENTERED` | Yes | ISO date `YYYY-MM-DD` |
| `DATE_MODIFIED` | No | ISO date `YYYY-MM-DD` |
| `COMMENTS` | No | Free text |

For MIC breakpoints: a row with `R=4` and `S=1` classifies MIC ≤ 1 as S, MIC ≥ 4 as R, and MIC 2 as I.

> [!NOTE]
> A sample user-defined breakpoints file is installed to `Resources\SampleUserDefinedBreakpoints.txt`. Use it as a starting template.

For the full schema specification, see [Reference: Resource Data Schemas](../reference/resource-data-schemas.md).

---

## Steps

### 1. Create your breakpoints file

Copy `Resources\SampleUserDefinedBreakpoints.txt` as a starting point:

```powershell
copy "C:\Program Files\AMR Interpretation Engine\Resources\SampleUserDefinedBreakpoints.txt" `
     "C:\MyBreakpoints\custom_breakpoints.txt"
```

Edit the file in a text editor or spreadsheet, keeping the tab-delimited format and header row intact.

### 2. Register the file in your configuration

In your `config.json`, set `UserDefinedBreakpointsFile` to the path relative to the application's resource root:

```json
{
  "UserDefinedBreakpointsFile": "Resources\\custom_breakpoints.txt"
}
```

> [!WARNING]
> The path is resolved relative to the application's install directory (the system resource root), not the working directory. Use a path inside the `Resources\` subdirectory of the install folder, or use an absolute path.

### 3. Test your breakpoints

Use CLI single-interpretation mode to verify a specific organism–drug combination:

```powershell
AMRIE.CLI.exe `
    --mode SINGLE_INTERPRETATION `
    --config "path\to\config.json" `
    --organism eco `
    --antibiotic AMK `
    --test-method MIC `
    --value 4
```

The result is printed as JSON, including the matched breakpoint guideline, year, and interpreted category.

> [!NOTE]
> Run several test cases covering your new breakpoint rows — both values expected to fall at the boundary (e.g., MIC = breakpoint) and values clearly in each category — to confirm the thresholds are loaded correctly.

### 4. Run a batch validation

Process a small synthetic test file against your config and inspect the output:

```powershell
AMRIE.CLI.exe `
    --mode FILE `
    --input  "path\to\test_isolates.txt" `
    --config "path\to\config.json" `
    --output "path\to\test_output.txt" `
    --delimiter PIPE
```

Compare the `_INTERP` columns to your expected results.

---

## See Also

- [Batch Processing Guide](./batch-processing-guide.md)
- [Reference: Resource Data Schemas](../reference/resource-data-schemas.md)
- [Architecture: Rule Precedence Hierarchy](../architecture/rule-precedence-hierarchy.md)
- [CONTRIBUTING.md](../../CONTRIBUTING.md)
