# Clinical Data & Guideline Attributions

**AMRIE** (Antimicrobial Resistance Test Interpretation Engine) incorporates curated antimicrobial susceptibility testing (AST) resource tables in `AMRIE.Library/Resources/` (`Breakpoints.txt`, `QC_Ranges.txt`, `ExpectedResistancePhenotypes.txt`, `ExpertInterpretationRules.txt`, `Organisms.txt`, and `Antibiotics.txt`).

These tables facilitate the automated interpretation of quantitative laboratory measurements according to internationally published standards. We gratefully acknowledge the scientific work and publications of the following standards organizations and public health bodies:

---

## 1. Clinical and Laboratory Standards Institute (CLSI)

- **Website:** [https://clsi.org/](https://clsi.org/)
- **Referenced Standards:**
  - *CLSI M100* — Performance Standards for Antimicrobial Susceptibility Testing
  - *CLSI M23* — Development of In Vitro Susceptibility Testing Criteria and Quality Control Parameters
  - *CLSI M45* — Methods for Antimicrobial Dilution and Disk Susceptibility Testing of Infrequently Isolated or Fastidious Bacteria
  - *CLSI VET01S* — Performance Standards for Antimicrobial Disk and Dilution Susceptibility Tests for Bacteria Isolated From Animals
- **Notice:** CLSI clinical breakpoints, Susceptible-Dose Dependent (SDD) criteria, Epidemiological Cutoff Values (ECVs), intrinsic resistance tables, and quality control ranges are derived from published CLSI documents. Users should consult official CLSI publications for complete clinical footnotes, testing conditions, and dosage regimens.

---

## 2. European Committee on Antimicrobial Susceptibility Testing (EUCAST)

- **Website:** [https://www.eucast.org/](https://www.eucast.org/)
- **Referenced Standards:**
  - *EUCAST Clinical Breakpoint Tables* for interpretation of MICs and zone diameters
  - *EUCAST Expected Resistant Phenotypes* (Intrinsic Resistance and Exceptional Phenotypes)
  - *EUCAST Routine and Extended Internal Quality Control Tables*
  - *EUCAST MIC and Zone Diameter Distributions and ECOFFs*
- **Notice:** EUCAST breakpoint tables and intrinsic resistance rules are maintained by the European Society of Clinical Microbiology and Infectious Diseases (ESCMID) and the European Committee on Antimicrobial Susceptibility Testing. Consult [eucast.org](https://www.eucast.org/) for official clinical guidance and dosages.

---

## 3. Société Française de Microbiologie (SFM / CA-SFM)

- **Website:** [https://www.sfm-microbiologie.org/](https://www.sfm-microbiologie.org/)
- **Referenced Standards:**
  - *Comité de l'antibiogramme de la Société Française de Microbiologie (CA-SFM / EUCAST)* — Recommandations annuelles et Recommandations Vétérinaires
- **Notice:** SFM human and veterinary breakpoint criteria are compiled from official CA-SFM publications.

---

## 4. World Health Organization (WHO) & WHONET

- **Website:** [https://whonet.org/](https://whonet.org/)
- **Organization:** WHO Collaborating Centre for Surveillance of Antimicrobial Resistance, Division of Infectious Diseases, Brigham and Women's Hospital / Mass General Brigham, Boston, Massachusetts, USA.
- **Referenced Ontologies & Classifications:**
  - WHONET three-letter organism codes (`WHONET_ORG_CODE`), antibiotic codes (`WHONET_ABX_CODE`), and test codes (`WHONET_TEST`).
  - WHO Anatomical Therapeutic Chemical (ATC) classification codes (`ATC_CODE`).
  - WHO AWaRe (Access, Watch, Reserve) antibiotic classification (`WHO_AWARE`) and Critically Important Antimicrobials (`CIA_CATEGORY`).
  - Logical Observation Identifiers Names and Codes (`LOINC`) and SNOMED CT (`SCT_CODE`) mappings.
  - Global Biodiversity Information Facility (`GBIF`) taxonomic identifiers.

---

## Clinical Use & Warranty Disclaimer

The resource tables and interpretation algorithms in AMRIE are provided solely for research, epidemiological surveillance, and informational purposes under the [MGB Open Access License 1.0](../License.md). They do not constitute medical advice or a certified in-vitro diagnostic (IVD) medical device. Clinical laboratories are responsible for verifying all breakpoints and rules against current official guideline documents prior to patient reporting.

