# How to Package AMRIE as a Windows Installer (MSI)

This guide covers building the WinUI and WinForms MSI installers using the WiX v5 Toolset. Run all commands from the repository root.

## Prerequisites

- .NET 10 SDK (`10.0.100+`)
- **WiX v5 Toolset** — installed automatically as a NuGet SDK (`WixToolset.Sdk/5.0.1`) when you restore the solution; no separate WiX installation required
- Windows 10/11 build machine (the WinUI publish step is Windows-only)

> [!NOTE]
> The WiX SDK is declared in the `.wixproj` files and is downloaded on first restore via `dotnet restore`. You do not need to install WiX globally.

---

## WinUI MSI (`AMRIE.WinUI.Installer`)

The WinUI installer uses `Publish-WinUI.ps1`, which builds the MSI in a single step. The installer project automatically:

1. Restores and **publishes** `AMRIE.WinUI` (self-contained, unpackaged, `WindowsPackageType=None`).
2. Builds and embeds `AMRIE.CLI` (self-contained).
3. Harvests all published files via WiX `<Files>` globbing.
4. Attempts code-signing with `signtool.exe` if available (non-fatal if absent).

### Build the WinUI MSI

```powershell
# Default: x64 Release, version = yy.M.d
.\Publish-WinUI.ps1

# ARM64 release
.\Publish-WinUI.ps1 -Platform ARM64 -Configuration Release

# Explicit version string
.\Publish-WinUI.ps1 -Version "26.11.11" -Platform x64
```

### Output

```
AMRIE.WinUI.Installer\bin\<Platform>\Release\en-us\AMR_Interpretation_Engine_WinUI_<Platform>.msi
```

For example, an x64 release produces:
```
AMRIE.WinUI.Installer\bin\x64\Release\en-us\AMR_Interpretation_Engine_WinUI_x64.msi
```

---

## WinForms MSI (`AMRIE.WinForms.Installer`)

The WinForms installer is built directly via `dotnet build`. Unlike the WinUI installer, it references the WinForms project's **build output** directly (not a publish profile), so the WinForms project must be built first.

### 1. Build AMRIE.WinForms

```powershell
dotnet build AMRIE.WinForms\AMRIE.WinForms.csproj -c Release
```

### 2. Build the WinForms MSI

```powershell
dotnet build "AMRIE.WinForms.Installer\AMRIE.WinForms.Installer.wixproj" `
    -c Release `
    -p:Platform=x64 `
    -p:Version="26.11.11"
```

> [!NOTE]
> The WinForms installer checks for **.NET 10 Desktop Runtime** at install time. If the runtime is absent, the installer blocks with a prompt. The WinUI installer is self-contained and does not require a pre-installed runtime.

### Output

```
AMRIE.WinForms.Installer\bin\<Platform>\Release\en-us\<OutputName>.msi
```

---

## What the installers include

| Component | WinUI MSI | WinForms MSI |
|---|---|---|
| Main GUI executable | `AMRIE.WinUI.exe` | `AMRIE.WinForms.exe` |
| `AMRIE.CLI.exe` (self-contained) | ✅ Added to PATH | ✅ |
| `AMRIE.Library.dll` | ✅ (embedded in self-contained publish) | ✅ |
| `Resources\` (Breakpoints, config samples) | ✅ | ✅ |
| `licenses\` directory | ✅ | ✅ |
| Start Menu + Desktop shortcuts | ✅ | ✅ |
| System PATH update | ✅ (install folder) | — |

---

## Code Signing

The WinUI installer project runs `signtool.exe` automatically in a post-build step using SHA-256 and the Sectigo timestamp server:

```
signtool.exe sign /tr http://timestamp.sectigo.com /td sha256 /fd sha256 /d "AMRIE WinUI" /a "<msi path>"
```

If `signtool.exe` is not on `PATH` (e.g., local dev builds), the step is skipped and the build succeeds. For CI/CD release builds, ensure a code-signing certificate is installed and `signtool.exe` is available.

> [!WARNING]
> Distributing unsigned MSI files may trigger Windows SmartScreen warnings for end users. Always sign release builds intended for public distribution.

---

## See Also

- [Building and Testing](./building-and-testing.md)
- [Winget Publishing](./winget-publishing.md)
- [Developer Setup](./developer-setup.md)
