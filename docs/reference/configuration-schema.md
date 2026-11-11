# AMRIE Configuration Schema Reference

The AMRIE configuration file is a UTF-8 JSON document. It is supplied to the CLI via the `ConfigFile` argument and loaded at runtime by `InterpretationConfiguration.ReadConfiguration()`.

> [!NOTE]
> The JSON parser accepts **comments** (`// ...` and `/* ... */`) and **trailing commas**, so you may annotate your configuration file freely.

---

## Properties

### `RoundHalfDilutions`

| Attribute | Value |
| :-------- | :---- |
| **Type** | `boolean` |
| **Default** | `true` |
| **Required** | No |

When `true`, ETest (gradient strip) half-dilution MIC values (e.g., `1.5`) are rounded to the nearest standard doubling-dilution before breakpoint lookup. This matches EUCAST and CLSI rounding guidance for ETest results.

---

### `IncludeInterpretationComments`

| Attribute | Value |
| :-------- | :---- |
| **Type** | `boolean` |
| **Default** | `false` |
| **Required** | No |

When `true`, the engine appends modifier characters to interpretation codes:

| Modifier | Meaning |
| :------: | :------ |
| `!` | Result was set or overridden by an expert interpretation rule |
| `*` | Result is based on species-level intrinsic resistance |
| `?` | Measurement is outside the validated range for this test |

---

### `UseIntrinsicResistanceRules`

| Attribute | Value |
| :-------- | :---- |
| **Type** | `boolean` |
| **Default** | `true` |
| **Required** | No |

When `true`, the engine consults `ExpectedResistancePhenotypes.txt` before breakpoint lookup. Any organism–drug combination listed as intrinsically resistant is reported as `R` (with optional `*` modifier) regardless of the measured value.

> [!NOTE]
> This property is **not** present in `SampleConfig.json` but is a fully valid configuration property. Omitting it from the file is equivalent to specifying `true`.

---

### `EnabledExpertInterpretationRules`

| Attribute | Value |
| :-------- | :---- |
| **Type** | `string[]` |
| **Default** | `null` (all default rules active) |
| **Required** | No |

An array of expert rule codes to enable. The engine applies these phenotypic cascade rules after individual antibiotic interpretations.

| Behavior | Configuration |
| :------- | :------------ |
| All default rules active | Omit the property, or set to `null` |
| Specific rules only | List the desired rule codes |
| All expert rules disabled | Set to an empty array `[]` |

**Known rule codes** (from `ExpertInterpretationRules.txt`):

| Code | Description |
| :--- | :---------- |
| `ESBL-CONFIRMED` | ESBL confirmed: set all penicillins and cephalosporins to R |
| `ESBL-AMPC-PROBABLE` | Probable ESBL/AmpC: set relevant cephalosporins to R |
| `BLNAR-HFLU` | Beta-lactamase negative ampicillin-resistant *H. influenzae* |
| `MRS` | Methicillin-resistant *Staphylococcus* (MRSA/MRSE) |
| `ICR` | *Enterococcus* intrinsic class resistance cascade |

> [!NOTE]
> When `ReadConfiguration()` deserializes an empty JSON array `[]` for this property, it normalizes it to `null` internally — meaning all default rules will be active. Use a non-empty array listing only the rules you want, or omit the property entirely.

---

### `GuidelineYear`

| Attribute | Value |
| :-------- | :---- |
| **Type** | `integer` |
| **Default** | `0` (resolves to current bundled revision year at runtime) |
| **Required** | No |

The publication year of the breakpoint guideline edition to use (e.g., `2026`). The engine selects breakpoints with `YEAR <= GuidelineYear`, using the most recent available entry per organism/drug/method combination.

Setting this to a past year allows reproducible retrospective interpretation against older guideline editions.

---

### `PrioritizedBreakpointTypes`

| Attribute | Value |
| :-------- | :---- |
| **Type** | `string[]` |
| **Default** | `null` (all types eligible, no preference) |
| **Required** | No |

Ordered preference list for `BREAKPOINT_TYPE` selection. The engine tries each type in array order and uses the first one with an applicable breakpoint.

**Known values:**

| Value | Description |
| :---- | :---------- |
| `"Human"` | Clinical human medicine breakpoints |
| `"Animal"` | Veterinary medicine breakpoints |
| `"ECOFF"` | Epidemiological cut-off values (wild-type vs. non-wild-type) |

**Example:** `["Human", "Animal", "ECOFF"]` — prefer human breakpoints; fall back to animal, then ECOFF.

> [!NOTE]
> An empty array `[]` is normalized to `null` by `ReadConfiguration()`.

---

### `PrioritizedSitesOfInfection`

| Attribute | Value |
| :-------- | :---- |
| **Type** | `string[]` |
| **Default** | `null` (default engine order) |
| **Required** | No |

Ordered preference list for site-of-infection breakpoint selection. The engine tries each site in array order. Sites not listed are appended automatically in default engine order. Sites in `DisabledSitesOfInfection` are excluded.

**Default engine order** (first site with an applicable breakpoint wins):

```
Non-meningitis, Non-endocarditis, Parenteral, (Blank),
Uncomplicated urinary tract infection,
Infections originating from the urinary tract,
Meningitis, Endocarditis, Endocarditis with combination treatment,
Intravenous, Oral, Inhaled, Investigational agent,
Extraintestinal, Abscesses, Genital, Intestinal, Liposomal,
Mammary gland, Mastitis, Metritis, Non-pneumonia,
Other infections, Other indications, Pneumonia, Prophylaxis,
Respiratory, Screen, Skin, Soft tissue, Wounds
```

---

### `DisabledSitesOfInfection`

| Attribute | Value |
| :-------- | :---- |
| **Type** | `string[]` |
| **Default** | `null` (no sites disabled) |
| **Required** | No |

Sites of infection to exclude from breakpoint lookup. Any site listed here will not be used even if it has an applicable breakpoint, and it will not appear in the effective `PrioritizedSitesOfInfection` list.

---

### `HorizontalAntibioticResults`

| Attribute | Value |
| :-------- | :---- |
| **Type** | `boolean` |
| **Default** | `true` |
| **Required** | No |

Controls output layout in `FILE` mode:

| Value | Layout |
| :---: | :----- |
| `true` | **Wide/horizontal:** One output row per input isolate. Each antibiotic measurement column is followed by a `<FIELD>_INTERP` column. |
| `false` | **Tall/vertical:** Antibiotic columns are transposed. One output row per antibiotic measurement, with three appended columns: `ANTIBIOTIC_CODE`, `ANTIBIOTIC_MEASUREMENT`, `ANTIBIOTIC_INTERPRETATION`. |

---

### `UserDefinedBreakpointsFile`

| Attribute | Value |
| :-------- | :---- |
| **Type** | `string` |
| **Default** | `null` |
| **Required** | No |

Relative path from `AppContext.BaseDirectory` to a user-defined breakpoints file. The file must follow the same tab-delimited schema as the built-in `Breakpoints.txt` (see [Resource Data Schemas — Breakpoints.txt](resource-data-schemas.md#3-breakpointstxt--clinical-breakpoints--ecoffs)).

User-defined breakpoints are merged with built-in breakpoints. When a user-defined entry matches the same organism/drug/guideline/year/method/site combination as a built-in entry, the user-defined entry takes precedence.

---

## Complete Annotated Example

```json
{
  "$schema": "../docs/reference/amrie-config.schema.json",

  // Round ETest half-dilution values to the nearest standard dilution (recommended).
  "RoundHalfDilutions": true,

  // Set to true to include ! (expert rule) and * (intrinsic resistance) modifiers in output.
  "IncludeInterpretationComments": false,

  // Omitting UseIntrinsicResistanceRules is equivalent to true.
  // "UseIntrinsicResistanceRules": true,

  // Enable only these expert rules; omit property or set null for all defaults.
  "EnabledExpertInterpretationRules": [
    "BLNAR-HFLU",
    "MRS",
    "ICR"
  ],

  // Use the 2026 breakpoint edition. Set to 0 or omit for current bundled year.
  "GuidelineYear": 2026,

  // Prefer human breakpoints, fall back to animal, then ECOFF.
  "PrioritizedBreakpointTypes": [ "Human", "Animal", "ECOFF" ],

  // Empty array = no preference override; default engine site order applies.
  "PrioritizedSitesOfInfection": [],

  // No sites disabled.
  "DisabledSitesOfInfection": [],

  // Wide output: one row per isolate with _INTERP columns.
  "HorizontalAntibioticResults": true,

  // Optional: path relative to the AMRIE executable directory.
  // "UserDefinedBreakpointsFile": "Resources\\MyBreakpoints.txt"
}
```

---

## See Also

- [JSON Schema for config files](amrie-config.schema.json)
- [CLI Reference](cli-reference.md)
- [API Reference](api-reference.md)
- [Resource Data Schemas](resource-data-schemas.md)
- [Error Codes and Logs](error-codes-and-logs.md)
