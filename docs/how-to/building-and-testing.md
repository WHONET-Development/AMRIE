# How to Build and Test AMRIE

This guide covers building the solution, individual projects, and running the test suite. Run all commands from the repository root unless otherwise stated.

## Prerequisites

- .NET 10 SDK (`10.0.100+`) — see [Developer Setup](./developer-setup.md)
- .NET Framework 4.8 Developer Pack (for `net48` target builds)

---

## Build

### Build the entire solution

```powershell
dotnet build AMRIE.sln
```

### Build individual projects

```powershell
# AMRIE.Library — both target frameworks
dotnet build AMRIE.Library\AMRIE.Library.csproj --framework net10.0
dotnet build AMRIE.Library\AMRIE.Library.csproj --framework net48

# AMRIE.CLI
dotnet build AMRIE.CLI\AMRIE.CLI.csproj

# AMRIE.WinUI (Windows only)
dotnet build AMRIE.WinUI\AMRIE.WinUI.csproj
```

> [!IMPORTANT]
> `AMRIE.Library` targets both `net10.0` and `net48`. **Always verify that changes compile cleanly on both frameworks** before opening a pull request. A build that passes on `net10.0` but fails on `net48` will break downstream consumers.

---

## Run the WinUI App (development)

Use `BuildAndRun.ps1` as the entry point. It builds and launches in unpackaged desktop mode (no Developer Mode required):

```powershell
# Default: Debug, unpackaged
.\BuildAndRun.ps1

# Release configuration
.\BuildAndRun.ps1 -Configuration Release

# Packaged mode via WinApp CLI (requires winapp tool)
.\BuildAndRun.ps1 -Packaged

# Launch detached (non-blocking shell)
.\BuildAndRun.ps1 -Detach
```

---

## Publish WinUI (MSI or unpackaged binaries)

Use `Publish-WinUI.ps1`. Version defaults to `yy.M.d` (build date), or pass `-Version` to override:

```powershell
# Build MSI (default: x64 Release)
.\Publish-WinUI.ps1

# Build MSI for ARM64
.\Publish-WinUI.ps1 -Platform ARM64

# Publish unpackaged binaries only (no installer)
.\Publish-WinUI.ps1 -Type Unpackaged -Platform x64

# Explicit version
.\Publish-WinUI.ps1 -Version "26.11.11"
```

**Output locations:**

| Type | Path |
|---|---|
| MSI | `AMRIE.WinUI.Installer\bin\<Platform>\Release\en-us\AMR_Interpretation_Engine_WinUI_<Platform>.msi` |
| Unpackaged | `AMRIE.WinUI\bin\<Platform>\Release\net10.0-windows10.0.26100.0\win-<Platform>\publish\` |

---

## Run Tests

The test suite uses **xUnit** with **coverlet** for coverage.

### Run all tests

```powershell
dotnet test AMRIE.Library.Tests\AMRIE.Library.Tests.csproj
```

### Run with code coverage

```powershell
dotnet test AMRIE.Library.Tests\AMRIE.Library.Tests.csproj --collect:"XPlat Code Coverage"
```

Coverage reports are written to `TestResults/` in Cobertura XML format. Use a tool like `reportgenerator` to convert them to HTML.

### Run tests for a specific framework

The test project targets `net10.0`. To exercise `net48` behavior, build the Library under `net48` and run integration commands against the CLI:

```powershell
# Verify CLI batch processing on the sample dataset
dotnet run --project AMRIE.CLI\AMRIE.CLI.csproj -- FILE "AMRIE.Library\Resources\SampleConfig.json" "|" "AMRIE.Library\Resources\SampleInputFile.txt" "$env:TEMP\AMRIE-test-output.txt"
```

---

## Dual-Target Verification Checklist

Before every pull request touching `AMRIE.Library`:

```powershell
dotnet build "AMRIE.Library\AMRIE.Library.csproj" --framework net10.0
dotnet build "AMRIE.Library\AMRIE.Library.csproj" --framework net48
dotnet test  "AMRIE.Library.Tests\AMRIE.Library.Tests.csproj"
```

> [!NOTE]
> `AMRIE.Library` uses `PolyfillLib` to backport C# 14 language features to `.NET Framework 4.8`. Avoid any APIs that are unavailable on `net48` without a polyfill.

---

## See Also

- [Developer Setup](./developer-setup.md)
- [Packaging: WiX MSI](./packaging-wix-msi.md)
- [Batch Processing Guide](./batch-processing-guide.md)
- [CONTRIBUTING.md](../../CONTRIBUTING.md)
