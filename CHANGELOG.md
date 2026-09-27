# Changelog

All notable changes to this project are documented in this file.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

---

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

### Added — `amrie` CLI Alias (`Interpretation CLI`)

- `amrie.exe` — copy of `Interpretation CLI.exe` with a short, space-free name, built via post-build copy target.
- `amrie.cmd` — Windows batch wrapper for `amrie` in cmd/PowerShell sessions.
- `amrie` — POSIX shell wrapper for WSL/Git Bash sessions.
- **System `PATH` registration** via MSI `Environment` element — `amrie` works from any terminal after install.
- **App Paths registry entry** (`HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\amrie.exe`) — `amrie` is launchable from `Win+R` and Start.
- `App Paths` entry also registered for `Interpretation CLI.exe` (backward compatibility).
- `Program.cs` updated to display the actual process name (`amrie` or `Interpretation CLI.exe`) in help text dynamically.

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
