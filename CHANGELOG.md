# Changelog

All notable changes to this project are documented in this file.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

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
