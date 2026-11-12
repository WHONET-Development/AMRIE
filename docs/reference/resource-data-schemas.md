# Resource Data Schemas Reference

`AMRIE.Library` loads its clinical knowledge base from six tab-delimited (`\t`) UTF-8 text files located in the `Resources/` directory beneath `AppContext.BaseDirectory`.

This reference documents the schema, primary/foreign keys, and key columns of each resource table.

---

## Table of Contents

1. [`Organisms.txt` — Taxonomic Catalog](#1-organismstxt--taxonomic-catalog)
2. [`Antibiotics.txt` — Antimicrobial Catalog](#2-antibioticstxt--antimicrobial-catalog)
3. [`Breakpoints.txt` — Clinical Breakpoints & ECOFFs](#3-breakpointstxt--clinical-breakpoints--ecoffs)
4. [`ExpectedResistancePhenotypes.txt` — Intrinsic Resistance Rules](#4-expectedresistancephenotypestxt--intrinsic-resistance-rules)
5. [`ExpertInterpretationRules.txt` — Phenotypic & Cascading Expert Rules](#5-expertinterpretationrulestxt--phenotypic--cascading-expert-rules)
6. [`QC_Ranges.txt` — Quality Control Reference Strain Ranges](#6-qc_rangestxt--quality-control-reference-strain-ranges)
7. [WHONET Antibiotic Column Naming Convention](#7-whonet-antibiotic-column-naming-convention)

---

## 1. `Organisms.txt` — Taxonomic Catalog

Contains the microbial taxonomy and hierarchical grouping codes used to join isolate organism codes (`WHONET_ORG_CODE`) with breakpoints, intrinsic resistance rules, and expert rules.

| Column | Type | Description |
| :--- | :--- | :--- |
| **`WHONET_ORG_CODE`** | `string` | Three-character WHONET organism code (e.g., `eco`, `sau`, `spn`, `kpn`). |
| **`REPLACED_BY`** | `string` | Replacement organism code when `TAXONOMIC_STATUS = 'O'`. |
| **`ORGANISM`** | `string` | Full scientific display name (e.g., *Escherichia coli*). |
| **`TAXONOMIC_STATUS`** | `char` | `'C'` for current/active taxonomic names; `'O'` for outdated/deprecated synonyms. **AMRIE evaluates only `'C'` records.** |
| **`COMMON`** | `string` | `'X'` if the organism is a commonly encountered clinical isolate. |
| **`COMMON_COMMENSAL`** | `string` | `'X'` if classified as a common skin/mucosal commensal. |
| **`ORGANISM_TYPE`** | `string` | High-level type (e.g., Bacteria, Fungi, Parasite). |
| **`ANAEROBE`** | `string` | `'X'` if the organism is an obligate anaerobe; blank otherwise. |
| **`MORPHOLOGY`** | `string` | Microscopic morphology description (e.g., cocci, bacilli). |
| **`SUBKINGDOM_CODE`** | `string` | `'+'` for Gram-positive; `'-'` for Gram-negative. |
| **`FAMILY_CODE`** | `string` | Taxonomic family group code (e.g., `EBC` for *Enterobacterales*). |
| **`GENUS_GROUP`** | `string` | Multi-genus or streptococcal group code. |
| **`GENUS_CODE`** | `string` | Genus code (e.g., `STA` for *Staphylococcus*, `ENT` for *Enterococcus*, `HA-` for *Haemophilus*). |
| **`SPECIES_GROUP`** | `string` | Species complex/group code (e.g., `svi` for viridans group streptococci). |
| **`SEROVAR_GROUP`** | `string` | Subspecies/serovar grouping code (e.g., `ECO`, `HIN`, typhoidal *Salmonella*). |
| **`SCT_CODE` / `SCT_TEXT`** | `string` | SNOMED CT concept identifier and preferred term. |
| **`GBIF_TAXON_ID`** | `string` | Global Biodiversity Information Facility taxonomic identifier. |
| **`KINGDOM`..`GENUS`** | `string` | Standard Linnaean taxonomy columns (`KINGDOM`, `PHYLUM`, `CLASS`, `ORDER`, `FAMILY`, `GENUS`). |

---

## 2. `Antibiotics.txt` — Antimicrobial Catalog

Defines all antimicrobial agents, guideline participation flags, pharmacological classes, and test potencies.

| Column | Type | Description |
| :--- | :--- | :--- |
| **`WHONET_ABX_CODE`** | `string` | Three-letter WHONET antibiotic code (e.g., `AMP`, `AMC`, `CIP`, `VAN`, `CRO`). Primary lookup key. |
| **`WHO_CODE`..`USER_CODE`** | `string` | Cross-reference codes (`WHO_CODE`, `DIN_CODE`, `JAC_CODE`, `EUCAST_CODE`, `USER_CODE`). |
| **`ANTIBIOTIC`** | `string` | Full display name of the antimicrobial agent (e.g., `Amoxicillin/Clavulanic acid`). |
| **`GUIDELINES`** | `string` | Summary of guidelines referencing this drug. |
| **`CLSI`, `EUCAST`, `SFM`, `SRGA`, `BSAC`, `DIN`, `NEO`, `AFA`** | `string` | Guideline inclusion flags. Contains `'X'` if the antibiotic is active under that guideline. |
| **`POTENCY`** | `string` | Standard disk diffusion potency (e.g., `10ug`, `30ug`, `20/10ug`). |
| **`ATC_CODE`** | `string` | World Health Organization Anatomical Therapeutic Chemical (ATC) code (e.g., `J01CR02`). Used for prefix joins in `ExpectedResistancePhenotypes.txt`. |
| **`CLASS`** | `string` | Antimicrobial class name (e.g., `Penicillins`, `Cephems`, `Macrolides`, `Fluoroquinolones`). Used by `ExpertInterpretationRules.txt` (`AFFECTED_ANTIBIOTICS`). |
| **`SUBCLASS`** | `string` | Finer antimicrobial subclass classification. |
| **`PROF_CLASS`** | `string` | Resistance profile class (e.g., `CEPH3` for third-generation cephalosporins, used by `ESBL-AMPC-PROBABLE` rule criteria). |
| **`WHO_AWARE`** | `string` | WHO AWaRe classification (`Access`, `Watch`, `Reserve`). |
| **`CIA_CATEGORY`** | `string` | WHO Critically Important Antimicrobials category. |
| **`HUMAN` / `VETERINARY`** | `string` | `'X'` indicating human or veterinary clinical applicability. |
| **`LOINC*`** | `string` | LOINC codes (`LOINCCOMP`, `LOINCGEN`, `LOINCDISK`, `LOINCMIC`, `LOINCETEST`, etc.). |

---

## 3. `Breakpoints.txt` — Clinical Breakpoints & ECOFFs

Stores quantitative susceptibility thresholds across guidelines, years, test methods, hosts, and sites of infection. User-defined breakpoint files (`SampleUserDefinedBreakpoints.txt`) use this exact same schema.

| Column | Type | Description |
| :--- | :--- | :--- |
| **`GUIDELINES`** | `string` | Standard authority (`CLSI`, `EUCAST`, `SFM`, `SRGA`, `BSAC`, `DIN`, `NEO`, `AFA`, or `USER`). |
| **`YEAR`** | `int` | Publication year of the guideline edition (e.g., `2026`). |
| **`TEST_METHOD`** | `string` | `DISK` (disk diffusion zone diameter in mm) or `MIC` (minimal inhibitory concentration in $\mu\text{g/mL}$, used for both broth/agar dilution and ETest). |
| **`POTENCY`** | `string` | Disk content for `DISK` breakpoints (e.g., `30ug`). |
| **`ORGANISM_CODE`** | `string` | Target organism or taxonomic group code (e.g., `spn`, `STA`, `EBC`, `ANA`). |
| **`ORGANISM_CODE_TYPE`** | `string` | Column in `Organisms.txt` that `ORGANISM_CODE` matches against (`SEROVAR_GROUP`, `WHONET_ORG_CODE`, `SPECIES_GROUP`, `GENUS_CODE`, `GENUS_GROUP`, `FAMILY_CODE`, `ANAEROBE+SUBKINGDOM_CODE`, `ANAEROBE`). |
| **`BREAKPOINT_TYPE`** | `string` | `Human`, `Animal`, or `ECOFF` (Epidemiological Cut-Off Value). |
| **`HOST`** | `string` | Host species (e.g., `Human`, `Cattle`, `Swine`, `Dogs`, `Horses`). |
| **`SITE_OF_INFECTION`** | `string` | Clinical indication or administration route (e.g., `Meningitis`, `Non-meningitis`, `Uncomplicated urinary tract infection`, `Oral`, `Parenteral`). |
| **`REFERENCE_TABLE`** | `string` | Source table in the published guideline document (e.g., `Table 2A`). |
| **`REFERENCE_SEQUENCE`** | `string` | Ordering sequence within the reference table. |
| **`WHONET_ABX_CODE`** | `string` | Three-letter antibiotic code (`AMP`, `CRO`, etc.). |
| **`WHONET_TEST`** | `string` | Full WHONET test identifier combining drug, guideline, method, and potency (e.g., `CRO_NM`, `AMP_ED10`). |
| **`R`** | `decimal?` | Resistant threshold (`<= R` for Disk Diffusion; `>= R` for MIC). |
| **`I`** | `string` | Intermediate range or value (e.g., `14-17` or `2`). |
| **`SDD`** | `string` | Susceptible-Dose Dependent range or value. |
| **`S`** | `decimal?` | Susceptible threshold (`>= S` for Disk Diffusion; `<= S` for MIC). |
| **`ECV_ECOFF`** | `decimal?` | Epidemiological cut-off threshold when `BREAKPOINT_TYPE = ECOFF` (`WT` vs. `NWT`). |
| **`ECV_ECOFF_TENTATIVE`** | `string` | `'X'` if the ECOFF is designated as tentative. |

---

## 4. `ExpectedResistancePhenotypes.txt` — Intrinsic Resistance Rules

Defines organism-drug combinations where the species or taxonomic group is intrinsically resistant to the antimicrobial agent, bypassing quantitative breakpoint lookup.

| Column | Type | Description |
| :--- | :--- | :--- |
| **`GUIDELINE`** | `string` | Guideline authority (`CLSI`, `EUCAST`, `SFM`, etc.). |
| **`REFERENCE_TABLE`** | `string` | Source table reference in the guideline document. |
| **`ORGANISM_CODE`** | `string` | Target organism or taxonomic group code. |
| **`ORGANISM_CODE_TYPE`** | `string` | Hierarchy column in `Organisms.txt` (`SEROVAR_GROUP` through `ANAEROBE`). |
| **`EXCEPTION_ORGANISM_CODE`** | `string` | Comma-separated organism codes excluded from this intrinsic resistance rule. |
| **`EXCEPTION_ORGANISM_CODE_TYPE`** | `string` | Hierarchy column in `Organisms.txt` used to evaluate `EXCEPTION_ORGANISM_CODE`. |
| **`ABX_CODE`** | `string` | Target three-letter `WHONET_ABX_CODE` or `ATC_CODE` prefix. |
| **`ABX_CODE_TYPE`** | `string` | `'WHONET_ABX_CODE'` (exact match) or `'ATC_CODE'` (prefix match on `Antibiotics.ATC_CODE`). |
| **`ANTIBIOTIC_EXCEPTIONS`** | `string` | Comma-separated `WHONET_ABX_CODE` values excluded when matching via `ATC_CODE`. |

---

## 5. `ExpertInterpretationRules.txt` — Phenotypic & Cascading Expert Rules

Defines isolate-level rules where phenotypic markers or indicator drug results override the interpretation of other antimicrobial classes.

| Column | Type | Description |
| :--- | :--- | :--- |
| **`RULE_CODE`** | `string` | Rule identifier (`ESBL-CONFIRMED`, `ESBL-AMPC-PROBABLE`, `BLNAR-HFLU`, `MRS`, `ICR`). |
| **`DESCRIPTION`** | `string` | Human-readable rule summary. |
| **`ORGANISM_CODE`** | `string` | Target organism group (`EBC`, `HIN`, `STA`, `STR`). |
| **`ORGANISM_CODE_TYPE`** | `string` | Hierarchy level in `Organisms.txt` (`FAMILY_CODE`, `SEROVAR_GROUP`, `GENUS_CODE`). |
| **`RULE_CRITERIA`** | `string` | Boolean expression (`AND` / `OR`) combining drug results (`OXA=NS`, `AMP=NS`), profile classes (`CEPH3=R`), or isolate fields (`ESBL=+`, `MECA_PCR=+`, `INDUC_CLI=+`). |
| **`AFFECTED_ANTIBIOTICS`** | `string` | Comma-separated list of 3-letter `WHONET_ABX_CODE` values or `Antibiotics.CLASS` names to override as Resistant (`R!`). |
| **`ANTIBIOTIC_EXCEPTIONS`** | `string` | Comma-separated 3-letter `WHONET_ABX_CODE` values exempt from the class override (e.g., `CPT,BPR` for `MRS`). |

---

## 6. `QC_Ranges.txt` — Quality Control Reference Strain Ranges

Defines acceptable minimum and maximum measurements for ATCC / NCTC quality control reference strains (clasped in `QualityControlRange.cs`).

| Column | Type | Description |
| :--- | :--- | :--- |
| **`GUIDELINE`** | `string` | Guideline authority (`CLSI`, `EUCAST`, etc.). |
| **`YEAR`** | `int` | Publication year. |
| **`STRAIN`** | `string` | Reference strain designation (e.g., `ATCC 25922`, `ATCC 25923`, `ATCC 27853`). |
| **`WHONET_ORG_CODE`** | `string` | Three-letter organism code of the QC strain (`eco`, `sau`, `pae`, etc.). |
| **`WHONET_ABX_CODE`** | `string` | Three-letter antibiotic code. |
| **`ABX_TEST`** | `string` | Full WHONET test code. |
| **`METHOD`** | `string` | Test method (`DISK` or `MIC`). |
| **`MEDIUM`** | `string` | Growth medium specification. |
| **`MINIMUM` / `MAXIMUM`** | `decimal` | Lower and upper acceptable QC bounds (`IN` vs. `OUT`). |

---

## 7. WHONET Antibiotic Column Naming Convention

Input data files and test codes must conform to `IsolateInterpretation.ValidAntibioticFieldNameRegex`:

```
<ABX_CODE>_<GUIDELINE_CHAR><METHOD_CHAR>[POTENCY]
```

- **`<ABX_CODE>`:** 3-letter uppercase WHONET antibiotic code (e.g., `PEN`, `AMC`, `CRO`, `VAN`) or user-defined antibiotic code following the exact pattern `X_##` (e.g., `X_01` through `X_99`). Single-digit (`X_1`) or multi-digit variants (`X_100`) are invalid under WHONET rules.
- **`<GUIDELINE_CHAR>`:**
  - `N` = **CLSI** (formerly NCCLS)
  - `E` = **EUCAST**
  - `F` = **SFM** (France)
  - `S` = **SRGA**, `B` = **BSAC**, `D` = **DIN**, `T` = **NEO**, `A` = **AFA**
- **`<METHOD_CHAR>[POTENCY]`:**
  - `D<Potency>` = **Disk Diffusion** with disk potency (e.g., `PEN_ND10` = CLSI Penicillin 10 units disk; `SXT_ND1_2` = CLSI Trimethoprim/Sulfamethoxazole 1.25/23.75 $\mu\text{g}$ disk).
  - `M` = **MIC** (e.g., `CRO_NM` = CLSI Ceftriaxone MIC; `AMP_EM` = EUCAST Ampicillin MIC).
  - `E` = **ETest** gradient strip (e.g., `VAN_NE` = CLSI Vancomycin ETest).

