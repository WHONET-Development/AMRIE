# Getting Support for AMRIE

Welcome to the **AMRIE** (Antimicrobial Resistance Test Interpretation Engine) support guide. This document explains where to find documentation, how to get help, and how to distinguish between software defects and clinical guideline questions.

---

## 1. Documentation & Self-Service Resources

Before opening an issue, please consult the documentation in [`docs/`](docs/index.md):

- **New to AMRIE?**
  - [WinUI 3 Desktop Quickstart](docs/tutorials/quickstart-winui.md)
  - [Command-Line (CLI) Quickstart](docs/tutorials/quickstart-cli.md)
  - [Library Integration Quickstart](docs/tutorials/quickstart-library.md)
- **Configuring & Processing Files:**
  - [Batch File Processing Guide](docs/how-to/batch-processing-guide.md)
  - [Defining Custom Breakpoints](docs/how-to/custom-breakpoints.md)
  - [Configuration JSON Schema (`SampleConfig.json`)](docs/reference/configuration-schema.md)
  - [Error Codes & Troubleshooting](docs/reference/error-codes-and-logs.md)
- **Clinical Rules & Methodology:**
  - [Interpretation Algorithm Specification](docs/architecture/interpretation-algorithm.md)
  - [Rule Precedence & Taxonomic Hierarchy](docs/architecture/rule-precedence-hierarchy.md)
  - [Clinical Methodology & Guidelines](docs/architecture/clinical-methodology.md)

---

## 2. Reporting Issues on GitHub

We use [GitHub Issues](https://github.com/WHONET-Development/AMRIE/issues) to track software bugs, clinical breakpoint discrepancies, and feature requests:

| Issue Type | When to Use | Template |
| :--- | :--- | :--- |
| **Clinical Breakpoint / Rule Discrepancy** | An organism + antibiotic + measurement produces an interpretation (`S`/`I`/`SDD`/`R`) that differs from published CLSI, EUCAST, or SFM tables. | [Breakpoint Discrepancy Form](.github/ISSUE_TEMPLATE/breakpoint_discrepancy.yml) |
| **Software Bug Report** | Application crashes, CLI exceptions, file parsing failures, or UI rendering bugs. | [Bug Report Form](.github/ISSUE_TEMPLATE/bug_report.yml) |
| **Feature Request** | Proposing new CLI options, API methods, UI workflows, or performance enhancements. | [Feature Request Form](.github/ISSUE_TEMPLATE/feature_request.yml) |

> [!WARNING]
> **Never attach files containing real patient health information (PHI) or personally identifiable information (PII).** Always use synthetic isolate rows or redact all patient/hospital identifiers before sharing sample files.

---

## 3. Clinical & Surveillance Community Support

- **WHONET Official Website & Help Center:** For broader questions about WHONET surveillance workflows, BacLink data conversion, or laboratory configuration, visit **[https://whonet.org](https://whonet.org)**.
- **International Guideline Authorities:** For questions regarding the underlying clinical rationale of official breakpoints, please consult the primary standard-setting organizations directly:
  - **CLSI:** [https://clsi.org/](https://clsi.org/)
  - **EUCAST:** [https://www.eucast.org/](https://www.eucast.org/)
  - **SFM (CA-SFM):** [https://www.sfm-microbiologie.org/](https://www.sfm-microbiologie.org/)

---

## 4. Clinical Disclaimer

AMRIE is provided by Mass General Brigham Incorporated / Brigham and Women's Hospital on an **"AS IS"** basis under the [MGB Open Access License 1.0](License.md) for research, surveillance, and informational purposes. It is not a certified medical device and does not substitute for professional clinical judgment or official laboratory validation.

