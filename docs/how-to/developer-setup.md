# How to Set Up a Development Environment

This guide walks you through cloning the AMRIE repository and getting a working development environment. It assumes familiarity with .NET development and Git.

## Prerequisites

| Tool | Requirement | Notes |
|---|---|---|
| **.NET 10 SDK** | `10.0.100` or later (same major) | Pinned via [`global.json`](../../global.json); `rollForward: latestMajor` |
| **.NET Framework 4.8 Developer Pack** | Required for `net48` target of `AMRIE.Library` | Download from Microsoft |
| **Visual Studio 2022 / 2026** | Workloads: *Desktop Development (.NET)* + *WinUI Application Development* | Required for WinUI project; VS Code works for Library and CLI |
| **Windows 10 SDK `10.0.26100.0`** | Included in the WinUI workload | |
| **WiX v4/v5 Toolset** | Only needed to build MSI installers | See [Packaging: WiX MSI](./packaging-wix-msi.md) |
| **Windows 10 version 1809+** | Required to run `AMRIE.WinUI` | |

> [!NOTE]
> Developers on non-Windows systems (macOS, Linux) can still build and test `AMRIE.Library` and `AMRIE.CLI` — only `AMRIE.WinUI` and the WiX installer projects are Windows-only.

## Steps

### 1. Clone the repository

```powershell
git clone https://github.com/WHONET-Development/AMRIE.git
cd AMRIE
```

### 2. Restore NuGet packages

```powershell
dotnet restore AMRIE.sln
```

This restores dependencies for all projects in the solution, including WiX SDK packages if present.

### 3. Open in your editor

**Visual Studio 2022 / 2026** (recommended for WinUI development):

```powershell
start AMRIE.sln
```

**VS Code** (for Library / CLI work):

```powershell
code .
```

> [!IMPORTANT]
> To build `AMRIE.WinUI`, you must open the solution in Visual Studio with the **WinUI Application Development** workload installed. VS Code does not support the Windows App SDK build pipeline.

### 4. Verify the SDK version

```powershell
dotnet --version
```

The output should start with `10.`. If it doesn't, check that the .NET 10 SDK is installed and that `global.json` is being respected (`dotnet sdk check`).

### 5. Build and run

Proceed to [Building and Testing](./building-and-testing.md) for build commands and test instructions.

## Troubleshooting

| Problem | Likely cause | Fix |
|---|---|---|
| `NETSDK1045`: target framework not supported | .NET 10 SDK not installed or PATH issue | Install SDK, restart terminal |
| WinUI project won't load | Missing WinUI workload | Modify VS install → add *WinUI Application Development* |
| `net48` target fails to build | .NET Framework 4.8 Developer Pack missing | Install from Microsoft Download Center |
| NuGet restore fails offline | No network access | Run `dotnet restore` with VPN or on a network-connected machine |

## See Also

- [Building and Testing](./building-and-testing.md)
- [Packaging: WiX MSI](./packaging-wix-msi.md)
- [CONTRIBUTING.md](../../CONTRIBUTING.md)
