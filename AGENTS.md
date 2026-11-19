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
3. **Symbol & Batch Inspection**: Call `lhatse_get_symbol_context` (supports single `symbol` or batched `symbols[]`, `startLine`/`endLine` pagination, and call-site `snippet`s) and `lhatse_search_symbols` (`query` or `queries[]`).
4. **File Outlines & Relationship Tracing**: Call `lhatse_explore_files(includeMembers: true)` for fast file declaration outlines, `lhatse_trace_relationships` (`symbol` or `symbols[]`), or `lhatse_find_call_path` to trace call graphs across AMRIE projects.
5. **Shared State & Literals**: Call `lhatse_inspect_state` (`accessKind`, `callerProject`, `callerPathFilter`) to audit shared/static state and `lhatse_search_literals` (`query`/`queries[]`) for SQL/resource/literal lookups.
6. **Pre-Edit Blast Radius & Live AST**: Call `lhatse_change_impact` before modifying shared contracts and `lhatse_analyze_file_ast` (`path` or `paths[]`) to verify syntax live.
7. **Isolated Builds & Tests**: Call `lhatse_sync_or_build` with `action: "build_isolated"` or `action: "test_isolated"` (e.g. `project: "AMRIE.Library.Tests"`) to compile and run tests via the Lhatse host runner without touching repository binaries.

Always use Lhatse tools as a mandatory *first resort* before reading large source files, scanning directories, or running broad text searches across the solution.

Lhatse on GitHub: https://github.com/ACx2CF/Lhatse