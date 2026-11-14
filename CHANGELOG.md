# Changelog

All notable changes to this project are documented in this file.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

---

## [Unreleased] - 2026-11-12

### Added & Improved - `AMRIE.WinUI`

- **Single Interpretation (`SingleInterpretationPage`):**
  - Added **Expert Rules** lookup button and `ApplicableExpertRulesDialog` (matching WinForms `ExpertRuleDisplay`) using `ExpertInterpretationRule.GetApplicableExpertRules(...)`.
  - Added **Round half-dilutions (MIC)** and **Apply intrinsic resistance rules** checkboxes wired into `InterpretationConfiguration`.
  - Added drag-to-reorder (`CanDragItems="True"`) and Up/Down priority controls for **Breakpoint Types** and **Sites of Infection**.
  - Added **Clear** button (`ClearInputs`) alongside **Try an Example**, and updated **Try an Example** (`LoadExampleAsync`) to match the README/CLI/Library quickstart example (`spn`, `AMC_NM`, MIC `2`) without auto-interpreting until **Interpret** is clicked.
  - Updated `AutoSuggestBox` code badges to use `{ThemeResource TextOnAccentFillColorPrimaryBrush}` and updated all lookup and license modals (`ApplicableBreakpointsDialog`, `ApplicableIntrinsicRulesDialog`, `ApplicableExpertRulesDialog`, `BatchConfigDialog`, and `SettingsPage` notice dialogs) to inherit the active app theme (`WindowHelper.ApplyDialogTheme`) and use compact inline Close buttons without separate footer bars (`WindowHelper.ConfigureInlineCloseDialog`).
- **Batch Processing (`BatchProcessingPage`):**
  - Fixed the `ProgressBar` staying indeterminate during file processing so it tracks the `0–100%` progress reported by `IO_Library.InterpretDataFileBounded`.
  - Added completion summary stats (interpreted isolate count, elapsed time, and output file size).
  - Routed dropped `.json` files on `BatchProcessingPage` directly to `ConfigFilePath`.
  - Added visual **Edit...** configuration dialog (`BatchConfigDialog`) to inspect, create, or save `.json` batch configurations.
- **Resource Explorer (`ResourceExplorerPage`):**
  - Added initial loading `ProgressRing`, 180 ms debounced search filtering, `Ctrl+F` search focus shortcut, **Reset Filters** button, and theme-adaptive `S`/`R` contrast brushes (shared with `ApplicableBreakpointsDialog`).
- **Startup, UI Polish, Settings & CLI Consistency (`MainWindow`, `SettingsPage`, `AMRIE.CLI`):**
  - Improved 1st-screen startup responsiveness by deferring initial page inflation until window activation, removing duplicate settings file reads, and loading reference tables (`Breakpoints`, `Organisms`, `Antibiotics`) asynchronously off the UI thread.
  - Migrated WinUI ViewModels/models (`SingleInterpretationViewModel`, `BatchProcessingViewModel`, `ResourceExplorerViewModel`, `SelectableItem`) from `[ObservableProperty]` fields to C# `partial` properties (eliminating all `45` `MVVMTK0045` warnings).
  - Persisted window bounds and maximized state in `%LOCALAPPDATA%\AMRIE\settings.json` with display-area clamping.
  - Added keyboard navigation shortcuts (`Ctrl+1`, `Ctrl+2`, `Ctrl+3`, `Ctrl+,`).
  - Added loaded clinical guideline resource counts and formatted native WinUI rendering (`MarkdownViewerHelper`) for bundled `licenses/` Markdown notices (`CLINICAL-DATA-ATTRIBUTION.md`, `THIRD-PARTY-NOTICES.md`) plus `LICENSE-MGB.txt`.
  - Restored in-app 3-button viewer card for licenses and attributions on `SettingsPage`, removed obsolete external shell-launch handler, and added direct `HyperlinkButton` to AMRIE Documentation (`docs/index.md`).
  - Added user-defined antibiotic code (`X_#` / `X_##`, 1–99 without leading zeroes) tooltip, placeholder hints, and dynamic suggestion items on `SingleInterpretationPage`.
  - Updated `quickstart-winui.md` tutorial to align with the 3-card Settings layout, visual `BatchConfigDialog` editor, and rule lookup dialogs.
  - Updated `AMRIE.CLI` help fallback (`AMRIE.CLI.exe`) and launch profile name/arguments (`AMRIE.CLI`, `spn AMC_NM "2"`).

---

## [Unreleased] - 2026-11-08

### Changed - .NET 10 LTS

- Retargeted the engine, CLI, both desktop applications, tests, data generator, and performance harness to .NET 10 while retaining their Windows target platform versions.
- Selected the .NET 10 SDK with `global.json` and updated build/publish documentation, MSI payload paths, and the WinUI runtime label.
- Updated the legacy MSI prerequisite to the architecture-matched .NET 10 Desktop Runtime and supplied its missing build metadata; project-reference payload paths follow the new target frameworks automatically.
- Matched the legacy MSI's custom UI/actions to the build architecture and isolated the WinUI MSI's self-contained CLI build from cached framework-dependent solution assets, with matching GUI/CLI payload versions.
- Preserved page/dialog XAML resource subdirectories during WinUI publishing, fixing the pre-existing Debug launch failure without changing interpretation behavior.
- Kept NuGet package versions, interpretation logic, configuration, and bundled guideline resources unchanged.

### Validation

- The complete x64 solution, including both MSI projects, builds in Debug and Release with zero errors and zero new warnings; the 41 distinct existing MVVM toolkit warnings are unchanged.
- All 163 tests pass in each configuration. The standalone and MSI CLI outputs for all 622 bundled sample records are byte-identical to the pre-upgrade baseline; the single-interpretation result also matches.
- The Debug and Release MSI WinUI payloads launch successfully, navigate all four pages, and display the updated runtime label. Baseline outputs and validation logs are retained outside the repository.

## [Unreleased] – 2026-09-27

### Added — WinUI 3 Application (`AMRIE.WinUI`)

- **New WinUI 3 desktop application** (`AMRIE.WinUI`) replacing the legacy WinForms interface.
- Custom title bar with app icon, branding, and Mica/acrylic backdrop support.
- Navigation sidebar using `NavigationView` with pages:
  - **Single Interpretation** — interactive organism/antibiotic/measurement lookup.
  - **Batch Processing** — whole-file interpretation with progress reporting.
  - **Resource Explorer** — browse organisms, antibiotics, and breakpoints with year filtering.
  - **Settings** — theme selection, version info.
- Theme toggle (Light / Dark / System) in sidebar footer with 250 ms debounce.
- `MainWindow.ThemeChanged` static event so all pages stay in sync when theme is toggled from the sidebar.
- `SettingsPage` subscribes to `ThemeChanged` on load and unsubscribes on unload — theme radio buttons now always reflect the live theme regardless of where the toggle originated.
- `SettingsPage` displays app version dynamically from assembly metadata.
- Resource Explorer year dropdown defaults to current year and pre-selects it in the list.
- Robust title bar icon loading using `AppContext.BaseDirectory` with existence check.
- Unpackaged (self-contained) publish configuration for MSI distribution.

### Added — WinUI 3 MSI Installer (`AMRIE.WinUI.Installer`)

- New **WiX v5 MSI installer** project (`AMRIE.WinUI.Installer`) replacing the legacy WinForms installer.
- Installer UI: custom banner, dialog background, EULA, install-directory picker.
- **"Launch AMR Interpretation Engine" checkbox** on the final installer screen — launches the app immediately after install.
- Per-machine install scope to `Program Files`.
- Start Menu and Desktop shortcuts with correct app icon.
- Bundles both the WinUI 3 GUI and the Interpretation CLI in a single MSI.
- Upgrade behaviour: `MajorUpgrade` with `AllowDowngrades="yes"` — reinstalling or upgrading overwrites in place cleanly.
- Custom installer assets generator script (`generate_installer_assets.ps1`).
- Code-signing step with graceful skip when `signtool.exe` is unavailable.

### Changed — Interpretation CLI (`AMRIE.exe`)

- Renamed CLI project assembly output to **`AMRIE.exe`** (`<AssemblyName>AMRIE</AssemblyName>`).
- **System `PATH` registration** via MSI `Environment` element — `AMRIE.exe` is directly accessible from any terminal.
- **App Paths registry entry** (`HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\AMRIE.exe`) — launchable from `Win+R` and system run prompts.
- `Program.cs` updated to display the actual process name dynamically in usage and help text.

### Fixed — WinUI 3 Application

- Theme toggle button moved to `FooterMenuItems` to prevent double-invocation from `ItemInvoked`.
- `SettingsPage` theme radio buttons now sync immediately when theme is changed from the sidebar while Settings is open.
- `SettingsPage` radio buttons correctly pre-select the active theme on navigation.
- App icon now loads correctly in the title bar for unpackaged (non-MSIX) deployments.
- Removed stale `Package.appxmanifest` (not used for unpackaged MSI distribution).
- Fixed cabinet compression error (`WIX0001: The pipe has been ended`) caused by recursive `publish` directory nesting in the WinUI `.csproj`.

### Changed — Build & Project Structure

- `AMRIE.WinUI.csproj`: publish output cleaned to prevent recursive directory nesting breaking WiX cabinet compression.
- Solution updated to include new installer project.
- `.gitignore` updated for new project outputs.
- `Publish-WinUI.ps1` helper script updated.

---

## [1.0.0] – Prior (WinForms)

- Original WinForms desktop application (`Interpretation Interface`).
- Original WiX installer (`AMR Interpretation Engine Windows Installer`).
- `Interpretation CLI` — command-line interface for file-mode and single-interpretation use.
- Core engine library (`Interpretation Engine` / `AMR_Engine`).
