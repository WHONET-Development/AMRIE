# Solution-Wide Agent Index Entry Point

Scope: Solution-wide semantic inventory covering all projects in the AMRIE solution. Referenced projects resolve directly to source; external packages are bound as metadata references. Source remains authoritative.

## Central Index Architecture

The solution-wide agent index is centralized at:
`W:\AMRIE\.agent-index\`

All AMRIE projects (`AMRIE.Library`, `AMRIE.CLI`, `AMRIE.Desktop`, `AMRIE.WinForms`, `AMRIE.WinUI`, `AMRIE.WinForms.Installer`, `AMRIE.WinUI.Installer`, `AMRIE.Library.Tests`, `AMRIE.Library.DataGenerator`) are indexed through this single index home.

### Tracked vs Generated Artifacts

- **Tracked in Git**: `INDEX.md`, `projects.json`, `annotations.json`, and tools in `tools/` (`Update-Index.ps1`, `Build-Isolated.ps1`, `Test-Index.ps1`, `Indexer.cs`).
- **Locally Generated (Ignored)**: `manifest.json`, `files.jsonl`, `symbols.jsonl`, `relationships.jsonl`, `routes.jsonl`, `state.jsonl`, and `local/` evaluation caches.

Before querying missing or stale outputs on a fresh checkout, run `Update-Index.ps1` without `-Verify`. Never commit generated `.jsonl` or `manifest.json` files to Git.

## Query Protocol & Index-First Workflow

Before performing broad searches, scanning directories, or reading dozens of source files across projects, agents **must** query this index:

1. **Verify Freshness**:
   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .agent-index\tools\Update-Index.ps1 -Verify
   ```
2. **Find Starting Points (Routes)**: Query `routes.jsonl` by task topic:
   ```powershell
   $index = '.agent-index'
   Get-Content "$index\routes.jsonl" | ConvertFrom-Json | Where-Object { $_.task -match 'interpretation|breakpoint|WinUI' }
   ```
3. **Resolve Exact Declarations**: Pre-filter `symbols.jsonl` with `Select-String` before `ConvertFrom-Json` to get the exact file, start/end line, kind, and containing project:
   ```powershell
   Select-String -Path "$index\symbols.jsonl" -Pattern 'IsolateInterpretation|InterpretationLibrary' |
     ForEach-Object { $_.Line | ConvertFrom-Json } |
     Select-Object project,id,path,start,end
   ```
4. **Trace Call Graphs & Cross-Project Usages**: Pre-filter `relationships.jsonl` for callers, callees, and event subscriptions:
   ```powershell
   # Who calls this method across ANY project?
   Select-String -Path "$index\relationships.jsonl" -Pattern 'GetInterpretation' |
     ForEach-Object { $_.Line | ConvertFrom-Json } |
     Where-Object { $_.to -like '*GetInterpretation*' }
   ```
5. **Inspect Global & Static State**: Query `state.jsonl` for static variables, constants, and caches:
   ```powershell
   Select-String -Path "$index\state.jsonl" -Pattern '"project":"AMRIE\.Library"' |
     ForEach-Object { $_.Line | ConvertFrom-Json }
   ```
6. **Targeted Reads**: Always pass the exact `start` and `end` line numbers from `symbols.jsonl` to your file-reading tool instead of reading whole files.

## Index-First Trigger Table

| If the task is... | Use this Index File | Example Query |
| :--- | :--- | :--- |
| "Where is this method/class called or referenced?" | `relationships.jsonl` $\rightarrow$ `symbols.jsonl` | `Where-Object { $_.to -like '*TargetMethod*' }` |
| "What static fields or caches affect this?" | `state.jsonl` | `Where-Object { $_.project -eq 'AMRIE.Library' }` |
| "How do I start a specific task or workflow?" | `routes.jsonl` | `Where-Object { $_.task -match 'breakpoint' }` |
| "Which project owns this file, and is it compiled?" | `files.jsonl` | `Where-Object { $_.path -like '*SingleInterpretationPage*' }` |
| "Is the index synchronized with current git HEAD?" | `manifest.json` | `Update-Index.ps1 -Verify` |

## Isolated Builds

Use the unified isolated build tool to validate changes without modifying repository build outputs or running Release signing events:

```powershell
# Build AMRIE.Library into isolated scratch bin
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .agent-index\tools\Build-Isolated.ps1 -Project AMRIE.Library

# Build AMRIE.Library.Tests and dependencies
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .agent-index\tools\Build-Isolated.ps1 -Project AMRIE.Library.Tests

# Build AMRIE.CLI candidate
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .agent-index\tools\Build-Isolated.ps1 -Project AMRIE.CLI -Label candidate
```

Binaries are placed strictly under `.agent-index\local\<label>\<platform>\bin\` with pre/post-build events suppressed.

## Index Regeneration & Verification

To regenerate the index after source modifications:
```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .agent-index\tools\Update-Index.ps1
```

To run the complete automated index test suite (freshness verification, cross-project routing, deterministic regeneration, and staleness rejection):
```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .agent-index\tools\Test-Index.ps1
```

## Record Schemas & Limitations

- `manifest.json` (schema 2): Contains solution-wide metadata, projects inventory, semantic error counts, input file SHA-256 hashes, and tool versions.
- `files.jsonl`: Relative paths, SHA-256 hashes, line counts, generated flags, declared types, and large-member anchors. Includes C#, XAML, and WiX files.
- `symbols.jsonl`: Fully qualified documentation IDs, symbol name, kind, containing type, accessibility, static flag, file path, and start/end lines.
- `relationships.jsonl`: Direct semantic call edges, event subscriptions, overrides, and direct field/property state reads and writes.
- `state.jsonl`: Static fields and members of designated state types.
- `routes.jsonl`: Curated workflow task entry points.
- Direct references do not provide complete alias analysis or dynamic runtime reflection resolution. Source code remains authoritative.

