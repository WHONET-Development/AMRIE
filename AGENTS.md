# AMRIE Project Navigation & Lhatse Integration

For all code discovery, route lookups, symbol definitions, cross-project call hierarchies, and architectural navigation across the AMRIE solution, use the external **Lhatse MCP Server** (`http://127.0.0.1:13661/mcp`).

Projects covered include:
- `AMRIE.Library`
- `AMRIE.CLI`
- `AMRIE.Desktop` (inventory)
- `AMRIE.WinForms`
- `AMRIE.WinUI`
- `AMRIE.WinForms.Installer` (inventory)
- `AMRIE.WinUI.Installer` (inventory)
- `AMRIE.Library.Tests`
- `AMRIE.Library.DataGenerator`

### Working with the Lhatse MCP Server (Recommended First Resort)
When the Lhatse MCP server is present in your environment (`lhatse_*` tools available):
1. **Health & Freshness**: Call `lhatse_status` to verify index freshness and view indexed projects. If stale or dirty, call `lhatse_sync_or_build(action: "update")`.
2. **Feature & Task Routes**: Call `lhatse_find_routes` to discover curated entry points and task workflows.
3. **Symbol Inspection**: Call `lhatse_get_symbol_context` to fetch declaration metadata, callers, callees, and disk source lines (with live Roslyn AST re-anchoring if edited).
4. **Relationship & Path Tracing**: Call `lhatse_trace_relationships` or `lhatse_find_call_path` to trace call graphs across AMRIE projects.
5. **Shared State**: Call `lhatse_inspect_state` to audit readers vs. writers of shared/static state.
6. **Pre-Edit Blast Radius**: Call `lhatse_change_impact` before modifying shared contracts or utilities.
7. **Isolated Builds**: Call `lhatse_sync_or_build(action: "build_isolated")` or run `powershell.exe -NoProfile -ExecutionPolicy Bypass -File Lhatse\tools\Build-Isolated.ps1 -Project <ProjectName>`.

Always use Lhatse tools as a mandatory *first resort* before reading large source files, scanning directories, or running broad text searches across the solution.

Lhatse on GitHub: https://github.com/ACx2CF/Lhatse