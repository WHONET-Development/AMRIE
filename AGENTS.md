# AMRIE Project Navigation

For all code discovery, route lookups, cross-project dependencies, and navigation across the AMRIE solution, start at:
`.agent-index\INDEX.md`

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

Run the documented freshness check before relying on generated file/line spans:
`powershell.exe -NoProfile -ExecutionPolicy Bypass -File .agent-index\tools\Update-Index.ps1 -Verify`
If `-Verify` fails or outputs are missing, run `Update-Index.ps1` (without `-Verify`) to regenerate; do not fall back to broad `grep` or directory scans. Regenerate with `Update-Index.ps1` after significant edits if you need fresh line spans.

Follow task routes from `annotations.json` / `routes.jsonl` into symbol IDs in source, then inspect relevant call graphs, cross-project references, and shared state in `state.jsonl`.

You must use the .agent-index system to gain understanding of the AMRIE solution, its dependencies, and its runtime behavior as a mandatory *first resort* before opening source files, scanning directories, or performing broad searches across the solution.

Use this system extensively during planning, but also during implementation phases as necessary. It is a highly efficient, low cost, mechanism to augment AI agents' awareness while minimizing the model costs and processing delays associated with reading large sections of text into the context window. The sought-after insight can *very often* be obtained directly from interactions with this index.
