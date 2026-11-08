# Interpretation Engine Performance Harness

This opt-in .NET 8 console app measures the existing CPU interpretation engine. It has a one-way project reference to `Interpretation Engine` and adds no packages or CUDA dependencies.

## Modes

- `--mode file` calls the engine's normal `IO_Library.InterpretDataFile` entry point. This loads the whole input, so it is limited to 128 MiB by default. Use it for the approximately 40 MB comprehensive base corpus, not the 50 GiB expanded corpus.
- `--mode stream` (default) reads rows with the same `TextFieldParser`, preheats the engine breakpoint cache from a key scan, then interprets bounded batches through the public `IsolateInterpretation` API. It can process the expanded corpus without retaining the whole file or output in memory. This is intentionally reported as a different execution path from `IO_Library.InterpretDataFile`.

Bounded stream mode currently requires the configuration's horizontal antibiotic output setting; it reports an error for vertical-output configurations rather than silently changing the output shape.

Stream mode discards output unless `--output` is supplied. Whole-file mode writes to a temporary output file when `--output` is omitted, then deletes that file after measuring it. Existing output and report paths are never overwritten. JSON reports are written only when `--report` is supplied.

## Examples

```powershell
dotnet run --project "Interpretation Engine.Performance\Interpretation Engine.Performance.csproj" -- --input "C:\data\2026-clsi-eucast-boundary-input.txt" --mode file --manifest "C:\data\2026-clsi-eucast-manifest.json" --report "C:\data\cpu-base.json"
dotnet run --project "Interpretation Engine.Performance\Interpretation Engine.Performance.csproj" -- --input "C:\data\2026-clsi-eucast-boundary-input.txt" --mode stream --batch-size 1024 --max-rows 10000 --manifest "C:\data\2026-clsi-eucast-manifest.json" --report "C:\data\cpu-pilot.json"
```

Use `--help` for all options. Reports contain per-pass timing, throughput, allocations, process peak working set, input/output metadata, run parameters, and OS/runtime/CPU information. In stream mode, key scanning, breakpoint preheat, input parsing, interpretation, and output are timed separately. The whole-file engine API does not expose those internal stages, so its report contains end-to-end timing only.
