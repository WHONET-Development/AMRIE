# Contributing to AMRIE

Thank you for your interest in contributing to **AMRIE** (Antimicrobial Resistance Test Interpretation Engine), maintained by the [WHONET](https://whonet.org) development team at Brigham and Women's Hospital / Mass General Brigham.

Whether you are reporting a clinical breakpoint discrepancy, improving documentation, or contributing C# code, this guide outlines our workflow and legal requirements.

---

## Table of Contents

1. [License & Legal Prerequisites](#1-license--legal-prerequisites)
2. [Patient Privacy & Data De-Identification](#2-patient-privacy--data-de-identification)
3. [Ways to Contribute](#3-ways-to-contribute)
4. [Development Environment Setup](#4-development-environment-setup)
5. [Coding Standards & Architecture Rules](#5-coding-standards--architecture-rules)
6. [Testing & Verification](#6-testing--verification)
7. [Pull Request Process](#7-pull-request-process)

---

## 1. License & Legal Prerequisites

AMRIE is distributed under the **[MGB Open Access License 1.0](License.md)**. Before contributing or distributing modified versions, please note:

- **Non-Commercial Academic Use (Section 3):** Use and distribution of the software and materials are licensed solely for non-commercial, non-revenue-generating, academic, and public health purposes.
- **Pre-Requisite Sharing-Back Obligation (Section 4):** If you share or distribute AMRIE or its resource tables with any modifications, you **must** deliver a copy of your modifications back to the Licensor via a Pull Request to [WHONET-Development/AMRIE](https://github.com/WHONET-Development/AMRIE).
- **License Grant on Contributions (Section 4a–4b):** By submitting a Pull Request or delivering a modification, you grant Mass General Brigham Incorporated a perpetual, worldwide, non-exclusive, royalty-free, irrevocable license to use, reproduce, modify, and distribute your contribution, and you license other recipients under the terms of the MGB Open Access License 1.0.
- **Preservation of Notices:** You may not remove or alter any copyright, trademark, authorship attribution, or third-party notices in the source code or `licenses/` directory.

---

## 2. Patient Privacy & Data De-Identification

> [!CAUTION]
> **Never commit real patient health information (PHI) or personally identifiable information (PII) to this repository, in issues, or in pull requests.**

Under **Section 4(d)** of the [MGB Open Access License 1.0](License.md), by submitting any contribution (including test files, sample laboratory datasets, or issue attachments), you represent and warrant that:
1. The contribution does **not** contain any patient data or personal data, OR any such data has been **fully de-identified** in strict accordance with applicable laws (including **HIPAA** Safe Harbor / Expert Determination standards and **GDPR**).
2. All sample isolates and patient identifiers in `AMRIE.Library/Resources/SampleInputFile.txt` and `AMRIE.Library.Tests` must be purely synthetic or fully anonymized.

---

## 3. Ways to Contribute

- **Clinical Breakpoint or Rule Discrepancies:** Use our [Clinical Breakpoint / Rule Discrepancy Issue Template](.github/ISSUE_TEMPLATE/breakpoint_discrepancy.yml) to report mismatches between AMRIE's output and published CLSI, EUCAST, or SFM tables.
- **Software Bug Reports:** Use our [Bug Report Issue Template](.github/ISSUE_TEMPLATE/bug_report.yml) for crashes, UI issues, file parsing errors, or CLI defects.
- **Feature Requests:** Propose enhancements using the [Feature Request Issue Template](.github/ISSUE_TEMPLATE/feature_request.yml).
- **Code & Documentation Pull Requests:** Submit improvements to `AMRIE.Library`, `AMRIE.CLI`, `AMRIE.WinUI`, or the `docs/` directory.

---

## 4. Development Environment Setup

1. **Fork and Clone the Repository:**
   ```powershell
   git clone https://github.com/<your-username>/AMRIE.git
   cd AMRIE
   ```
2. **Install Required Tooling:**
   - **.NET 10 SDK** (`10.0.100+` as specified in [`global.json`](global.json)).
   - **.NET Framework 4.8 Developer Pack** (for building the `net48` target of `AMRIE.Library`).
   - **Visual Studio 2022 / 2026** with Desktop Development (.NET) and WinUI Application Development workloads.
   - **Windows 10/11 SDK** (`10.0.26100.0`).

👉 *For detailed instructions, see [Developer Setup Guide](docs/how-to/developer-setup.md).*

---

## 5. Coding Standards & Architecture Rules

- **Multi-Targeting Compatibility (`AMRIE.Library`):**
  - `AMRIE.Library` compiles against both `net10.0` and `net48` (using C# 14 language syntax with `PolyfillLib` on `.NET Framework 4.8`).
  - Every change to `AMRIE.Library` must compile cleanly on **both** target frameworks:
    ```powershell
    dotnet build "AMRIE.Library\AMRIE.Library.csproj" --framework net10.0
    dotnet build "AMRIE.Library\AMRIE.Library.csproj" --framework net48
    ```
- **Memory & Streaming Constraints:**
  - Do not introduce whole-file in-memory retention in `IO_Library`. File processing must adhere to the bounded batch limits (`BatchRowLimit = 2048`, `BatchCharacterLimit = 4194304`).
- **WinUI 3 Design Guidelines (`AMRIE.WinUI`):**
  - Follow Microsoft Fluent Design principles (`MicaBackdrop`, theme-aware brushes, `CommunityToolkit.Mvvm` patterns).
  - Avoid ternary operators inside `{x:Bind}` XAML expressions; use ViewModel properties or `IValueConverter` implementations.
  - Ensure all models bound to XAML use public `{ get; set; }` accessors rather than `init;` records.
- **Third-Party Dependencies:**
  - Avoid adding new NuGet dependencies to `AMRIE.Library` unless strictly necessary, as downstream applications (like WHONET on `.NET Framework 4.8`) must manage assembly binding redirects.
  - If a new dependency is added to any project, you **must** update [`licenses/THIRD-PARTY-NOTICES.md`](licenses/THIRD-PARTY-NOTICES.md), place its license file in `licenses/`, and include it in the WiX installer manifests (`AMRIE.WinUI.Installer/Package.wxs` and `AMRIE.WinForms.Installer/Windows Installer.wxs`).

---

## 6. Testing & Verification

Before opening a Pull Request, run the full test suite and verify that all unit and matrix tests pass:

```powershell
# Run all xUnit tests
dotnet test "AMRIE.Library.Tests\AMRIE.Library.Tests.csproj"

# Verify CLI batch processing on the bundled sample dataset
dotnet run --project "AMRIE.CLI\AMRIE.CLI.csproj" -- FILE "AMRIE.Library\Resources\SampleConfig.json" "|" "AMRIE.Library\Resources\SampleInputFile.txt" "$env:TEMP\AMRIE-test-output.txt"
```

- All new interpretation logic, measurement parsing changes, or expert rule modifications **must** include corresponding unit tests in `AMRIE.Library.Tests`.

---

## 7. Pull Request Process

1. Create a descriptive feature or bugfix branch from `main` (e.g., `fix/eucast-disk-potency` or `docs/update-cli-reference`).
2. Make your changes with clear, atomic commits.
3. Update relevant documentation in [`docs/`](docs/index.md) and add an entry under `[Unreleased]` in [`CHANGELOG.md`](CHANGELOG.md).
4. Open a Pull Request against `WHONET-Development/AMRIE` and complete the PR checklist.

