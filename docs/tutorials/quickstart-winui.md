# Quickstart: AMRIE Desktop Application (WinUI 3)

This tutorial walks you through installing the AMRIE desktop application on Windows and using it to interpret antimicrobial susceptibility test (AST) results for the first time. No programming knowledge is required.

By the end of this tutorial you will have:
- Installed AMRIE from the Windows Installer (MSI).
- Launched the application and explored its main navigation.
- Configured the interpretation engine settings.
- Loaded a sample data file and viewed interpreted results.

> [!IMPORTANT]
> AMRIE processes clinical microbiology data that may include **Protected Health Information (PHI)**. Ensure you operate AMRIE on systems that are compliant with your institution's data governance and privacy policies (e.g., HIPAA, GDPR) before loading any real patient data.

---

## Prerequisites

Before you begin, confirm the following:

- **Operating system:** Windows 10 version 1809 (October 2018 Update, build 17763) or later, or Windows 11 (any release). The WinUI 3 edition requires at least this version of Windows.
- **Architecture:** x64 or ARM64. Use the matching installer for your hardware.
- **Administrator access:** Required to run the MSI installer.
- **Internet connection:** Required only to download the installer from GitHub.

---

## Step 1 — Download the Installer

AMRIE is published as a self-contained Windows Installer package (`.msi`). It bundles the .NET 10 runtime, all application binaries, clinical resource files, and licenses — you do not need to install .NET separately.

1. Open a browser and navigate to the [AMRIE GitHub Releases page](https://github.com/WHONET-Development/AMRIE/releases).
2. Find the latest release (at the top of the list).
3. Under **Assets**, download the file that matches your hardware:
   - `AMR_Interpretation_Engine_WinUI_x64.msi` — for most desktop and laptop PCs.
   - `AMR_Interpretation_Engine_WinUI_arm64.msi` — for ARM-based devices (e.g., Snapdragon-powered laptops, Surface Pro X).

> [!NOTE]
> The WinUI MSI is self-contained, meaning it carries its own copy of the .NET 10 runtime. The file is larger than the WinForms MSI but requires no separate runtime installation step, which is ideal for clinical environments where internet access from workstations may be restricted.

---

## Step 2 — Run the Installer

1. Locate the downloaded `.msi` file in your Downloads folder.
2. Double-click the file to launch the Windows Installer wizard.
3. On the **Welcome** screen, click **Next**.
4. Read the **License Agreement** (MGB Open Access License 1.0). If you accept the terms, select **I accept the terms in the License Agreement** and click **Next**.
5. On the **Destination Folder** screen, the default installation path is `C:\Program Files\WHONET\AMRIE\`. You may change this if your organization requires a different location, but the default is recommended. Click **Next**.
6. Click **Install**. If a **User Account Control** prompt appears, click **Yes** to allow the installer to proceed.
7. When the installation completes, click **Finish**.

The installer creates a **Start Menu** entry and a **Desktop shortcut** for AMRIE.

---

## Step 3 — Launch AMRIE

Open the application using either method:

- Click **Start → All Apps → AMRIE** (or search for "AMRIE" in the Start Menu search bar).
- Double-click the **AMRIE** shortcut on your Desktop.

The application opens to the **Home** page, which provides quick access to the three main features:

- **Single Interpretation** — Evaluate one antibiotic measurement at a time interactively.
- **Batch Processing** — Load and interpret an entire laboratory data file.
- **Resource Explorer** — Browse the bundled organism, antibiotic, and breakpoint tables.

The left-hand navigation rail lets you switch between these sections at any time.

---

## Step 4 — Review the Settings

Before interpreting data, it is worth familiarizing yourself with the interpretation settings. Click the **Settings** item at the bottom of the left navigation rail (the gear icon).

The Settings page has two cards:

- **Appearance** — Choose between Light mode, Dark mode, or the system default. This preference is saved between sessions.
- **About AMRIE** — Displays the application version, the .NET runtime version, and links to the CLSI, EUCAST, SFM, and WHONET websites.

> [!NOTE]
> Interpretation settings such as the guideline year, enabled expert rules, breakpoint type priorities (Human / Animal / ECOFF), and sites of infection are configured through a **JSON configuration file** (`SampleConfig.json`) rather than through the Settings page UI. See [`SampleConfig.json`](../../AMRIE.Library/Resources/SampleConfig.json) for an annotated example, and the [Configuration Schema reference](../reference/configuration-schema.md) for a full field-by-field description. The How-To guide on [Custom Breakpoints](../how-to/custom-breakpoints.md) explains how to create and load a configuration file tailored to your laboratory's protocols.

---

## Step 5 — Load a Data File

AMRIE reads **WHONET-formatted delimited text files**. The input file must have a header row followed by data rows, where each antibiotic measurement occupies its own column. The column name encodes the antibiotic code, guideline, and test method in the WHONET naming convention (e.g., `PEN_ND10` = Penicillin, disk diffusion, 10 µg disk; `AMC_NM` = Amoxicillin-clavulanate, MIC).

A sample input file is included with the installation at:
```
<install folder>\Resources\SampleInputFile.txt
```

This file uses the pipe character (`|`) as the field delimiter and contains synthetic laboratory records with columns for patient demographics, organism code (`ORGANISM`), and many antibiotic measurement columns.

To load a file for batch interpretation:

1. Navigate to **Batch Processing** in the left navigation rail.
2. Click **Browse** (or the file open icon) next to the **Input File** field, and select your data file (or the sample file above).
3. Select the **delimiter** that matches your file. The sample file uses `|` (pipe).
4. If you have a configuration file, browse for it in the **Configuration** field. Otherwise, the application uses the default configuration (CLSI 2026 breakpoints, Human breakpoint type priority, standard expert rules enabled).
5. Specify an **Output File** path where the interpreted results will be saved.
6. Click **Run** to begin interpretation.

A progress indicator shows how many rows have been processed. For the sample file (623 records), interpretation completes in under a second on typical hardware.

> [!NOTE]
> The output file format mirrors the input file, with an additional `_INTERP` column appended immediately after each antibiotic measurement column. For example, if the input contains a `PEN_ND10` column, the output will contain `PEN_ND10` (the original measurement) followed by `PEN_ND10_INTERP` (the interpretation: `S`, `I`, `SDD`, `R`, or `NS`).

---

## Step 6 — View Results

Once the batch run completes, open the output file in a text editor or spreadsheet application to review the interpreted results. Each `_INTERP` column will contain one of the following clinical category codes:

| Code | Meaning |
| :--- | :--- |
| `S` | Susceptible |
| `I` | Intermediate |
| `SDD` | Susceptible-Dose Dependent |
| `R` | Resistant |
| `NS` | Non-Susceptible |
| `WT` | Wild-Type (ECOFF category) |
| `NWT` | Non-Wild-Type (ECOFF category) |
| *(blank)* | No applicable breakpoint found |

Rows where the expert rules (MRS, BLNAR, ICR) overrode a breakpoint-based result may show an `!` suffix (e.g., `R!`) when **Include Interpretation Comments** is enabled in the configuration file.

For the **Single Interpretation** workflow, navigate to the **Single Interpretation** page, select a guideline year, enter an organism code (e.g., `spn` for *Streptococcus pneumoniae*), an antibiotic code (e.g., `AMC_NM` for Amoxicillin-clavulanate MIC), and a numeric measurement (e.g., `2`). The interpreted category appears immediately.

---

## Next Steps

You are now ready to interpret your own laboratory data with AMRIE. The following guides cover more advanced topics:

- **[Custom Breakpoints](../how-to/custom-breakpoints.md)** — Learn how to define your own breakpoint table for locally-validated antibiotic concentrations not covered by the standard guidelines.
- **[Batch Processing Guide](../how-to/batch-processing-guide.md)** — Optimize AMRIE for large-volume laboratory files, understand output format options (horizontal vs. vertical antibiotic layout), and schedule automated batch runs.
