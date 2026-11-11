# AMRIE Documentation

**AMRIE** (Antimicrobial Resistance Test Interpretation Engine) is a clinical microbiology and public health software suite developed by the [WHONET](https://whonet.org) team at Brigham and Women's Hospital / Mass General Brigham. It automates the interpretation of quantitative antimicrobial susceptibility testing (AST) measurements into clinical categories (S, I, SDD, R, NS, WT, NWT) according to CLSI, EUCAST, and SFM guidelines. For a full project overview, see the [README](../README.md).

---

## Tutorials

Learning-oriented guides that walk you through AMRIE for the first time.

| Tutorial | Audience | Description |
| :--- | :--- | :--- |
| [WinUI Desktop Quickstart](./tutorials/quickstart-winui.md) | Clinical lab IT / end users | Install the MSI, launch the desktop app, load a data file, and view results. |
| [CLI Quickstart](./tutorials/quickstart-cli.md) | Bioinformaticians / automation engineers | Build or install the CLI, run file-mode batch processing and single-measurement lookups. |
| [Library Integration Quickstart](./tutorials/quickstart-library.md) | .NET developers | Embed `AMRIE.Library` in your own application to interpret measurements programmatically. |

---

## How-To Guides

Task-oriented guides for specific workflows.

| Guide | Description |
| :--- | :--- |
| [Developer Setup](./how-to/developer-setup.md) | Configure your development environment (SDK, IDE, workloads). |
| [Building and Testing](./how-to/building-and-testing.md) | Build all projects and run the xUnit regression suite. |
| [Packaging WiX MSI Installers](./how-to/packaging-wix-msi.md) | Build the self-contained WinUI MSI installer with WiX v5. |
| [WinGet Publishing](./how-to/winget-publishing.md) | Submit or update the `WHONET.AMRIE` WinGet package manifest. |
| [Batch File Processing](./how-to/batch-processing-guide.md) | Process large WHONET-formatted datasets efficiently. |
| [Custom Breakpoints](./how-to/custom-breakpoints.md) | Define and load your own breakpoint tables. |

---

## Reference

Information-oriented reference material for looking things up.

| Reference | Description |
| :--- | :--- |
| [Resource Data Schemas](./reference/resource-data-schemas.md) | Column schemas for `Breakpoints.txt`, `Organisms.txt`, `Antibiotics.txt`, and companion files. |
| [SQL Reference Queries](./reference/sql-reference-queries.md) | Example SQL queries for exploring the breakpoint and organism data. |
| [CLI Reference](./reference/cli-reference.md) | Complete `AMRIE.CLI.exe` argument reference for FILE and SINGLE_INTERPRETATION modes. |
| [API Reference](./reference/api-reference.md) | C# public API reference for `AMRIE.Library` classes and methods. |
| [Configuration Schema](./reference/configuration-schema.md) | JSON schema and field descriptions for `SampleConfig.json`. |
| [Error Codes and Logs](./reference/error-codes-and-logs.md) | Exit codes, exception types, and log format reference. |

---

## Architecture (Explanation)

Understanding-oriented background on how AMRIE works.

| Article | Description |
| :--- | :--- |
| [Interpretation Algorithm](./architecture/interpretation-algorithm.md) | The complete isolate rule cascade: expert rules, intrinsic resistance, and breakpoint lookup. |
| [Rule Precedence Hierarchy](./architecture/rule-precedence-hierarchy.md) | How MRS, BLNAR, ICR, ESBL, and other expert rules are ordered and evaluated. |
| [Memory and Caching](./architecture/memory-and-caching.md) | Bounded FIFO caches, batch preheating, and streaming architecture. |
| [Clinical Methodology](./architecture/clinical-methodology.md) | How CLSI, EUCAST, and SFM breakpoint sources are integrated and versioned. |
| [System Architecture](./architecture/system-architecture.md) | Project structure, multi-targeting, and component interaction diagram. |
