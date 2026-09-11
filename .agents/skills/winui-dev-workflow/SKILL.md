---
name: winui-dev-workflow
description: "Build, run, and diagnose WinUI 3 applications with WinApp CLI (winapp) and dotnet."
---

# WinUI 3 Developer Workflow

## Daily Commands
- **Run project**:
  ```powershell
  winapp run "Interpretation Desktop\InterpretationDesktop.csproj" --debug-output
  ```
- **Detach run**:
  ```powershell
  winapp run "Interpretation Desktop\InterpretationDesktop.csproj" --detach
  ```
- **Helper script**:
  ```powershell
  .\BuildAndRun.ps1
  ```
- **Build with dotnet**:
  ```powershell
  dotnet build "Interpretation Desktop\InterpretationDesktop.csproj" -nr:false
  ```

## Gotchas & Rules
- **XAML ternary operations**: WinUI 3 `{x:Bind}` does not support C# ternary operators (`? :`). Bind directly to viewmodel properties or converters.
- **Data models bound to XAML**: Must have public `{ get; set; }` properties. Positional C# records with `init;` accessors will cause `CS8852` in `XamlTypeInfo.g.cs`.
- **File pickers on Desktop**: WinRT `FileOpenPicker` and `FileSavePicker` require HWND initialization via `WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd)`.
- **MSBuild worker lock**: If `obj/` or `input.json` is reported locked by another process, run `dotnet build-server shutdown` to release MSBuild node reuse handles.
