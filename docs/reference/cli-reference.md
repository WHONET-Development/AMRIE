# AMRIE CLI Reference

`AMRIE.exe` is a command-line interface for the AMRIE antimicrobial resistance interpretation engine. It supports two mutually exclusive operating modes.

---

## Synopsis

```
AMRIE.exe FILE <ConfigFile> <Delimiter> <InputFile> <OutputFile>
AMRIE.exe SINGLE_INTERPRETATION <ConfigFile> <OrganismCode> <AntibioticCode> <Measurement> <OutputFile>
```

Mode tokens are case-insensitive (`file`, `FILE`, and `File` are all accepted).

---

## Exit Codes

| Code | Meaning |
| ---: | :------ |
| `0`  | Success |
| `1`  | Error — an unhandled exception occurred; message and stack trace are written to stdout |

---

## Mode: `FILE`

Interprets an entire delimited data file. All arguments are required.

```
AMRIE.exe FILE <ConfigFile> <Delimiter> <InputFile> <OutputFile>
```

### Arguments

| Position | Name | Type | Required | Description |
| :------: | :--- | :--- | :------: | :---------- |
| 0 | `FILE` | literal | ✓ | Mode selector (case-insensitive). |
| 1 | `ConfigFile` | file path | ✓ | Path to the JSON configuration file. Resolved to an absolute path before use. |
| 2 | `Delimiter` | token | ✓ | Column delimiter used in the input file. See [Supported Delimiters](#supported-delimiters). |
| 3 | `InputFile` | file path | ✓ | Path to the input delimited data file. Must have a header row. |
| 4 | `OutputFile` | file path | ✓ | Path where the interpreted output file will be written (tab-delimited, UTF-8). |

### Supported Delimiters

| Token | Character | Notes |
| :---- | :-------: | :---- |
| `TAB` | `\t` | Use the literal string `TAB`; do not use a tab character on the command line. |
| `,`   | `,`  | Comma. Pass the character directly. |
| `;`   | `;`  | Semicolon. Pass the character directly. |
| `\|`   | `\|`  | Pipe. Pass the character directly. |

### Output Format

The output is always **tab-delimited UTF-8**. The layout depends on `HorizontalAntibioticResults` in the configuration:

- **`true` (default):** Each input row becomes one output row. An `<ABX_CODE>_INTERP` column is inserted immediately after each corresponding measurement column.
- **`false`:** Antibiotic columns are transposed vertically. Each antibiotic measurement becomes its own output row with three appended columns: `ANTIBIOTIC_CODE`, `ANTIBIOTIC_MEASUREMENT`, `ANTIBIOTIC_INTERPRETATION`.

### Example

```bat
AMRIE.exe FILE "C:\configs\lab.json" TAB "C:\data\isolates.txt" "C:\output\interpreted.txt"
AMRIE.exe FILE "C:\configs\lab.json" , "C:\data\isolates.csv" "C:\output\interpreted.txt"
```

---

## Mode: `SINGLE_INTERPRETATION`

Interprets a single organism/antibiotic/measurement combination and writes JSON output. All arguments are required.

```
AMRIE.exe SINGLE_INTERPRETATION <ConfigFile> <OrganismCode> <AntibioticCode> <Measurement> <OutputFile>
```

### Arguments

| Position | Name | Type | Required | Description |
| :------: | :--- | :--- | :------: | :---------- |
| 0 | `SINGLE_INTERPRETATION` | literal | ✓ | Mode selector (case-insensitive). |
| 1 | `ConfigFile` | file path | ✓ | Path to the JSON configuration file. Resolved to an absolute path before use. |
| 2 | `OrganismCode` | string | ✓ | WHONET organism code (e.g., `eco`, `sau`, `spn`). |
| 3 | `AntibioticCode` | string | ✓ | Full WHONET antibiotic field name including guideline and method suffix (e.g., `AMP_NM`, `CRO_EM`). See [WHONET Antibiotic Column Naming Convention](resource-data-schemas.md#7-whonet-antibiotic-column-naming-convention). |
| 4 | `Measurement` | string | ✓ | MIC, disk diffusion, or ETest result (e.g., `2`, `>32`, `<0.5`, `22`). |
| 5 | `OutputFile` | file path | ✓ | Path where the JSON result will be written. |

### JSON Output Format

The interpretation result is written to `OutputFile` as a JSON object. The interpretation string is also echoed to stdout.

```json
{
  "OrganismCode": "eco",
  "AntibioticCode": "AMP_NM",
  "Measurement": "8",
  "Interpretation": "R"
}
```

If `IncludeInterpretationComments` is `false` in the configuration (the default), comment modifiers (`!`, `*`) are stripped from the interpretation before serialization and console output.

### Example

```bat
AMRIE.exe SINGLE_INTERPRETATION "C:\configs\lab.json" eco AMP_NM 8 "C:\output\result.json"
AMRIE.exe SINGLE_INTERPRETATION "C:\configs\lab.json" sau OXA_NM ">2" "C:\output\result.json"
```

---

## See Also

- [Configuration Schema](configuration-schema.md)
- [API Reference](api-reference.md)
- [Resource Data Schemas](resource-data-schemas.md)
- [Error Codes and Logs](error-codes-and-logs.md)
