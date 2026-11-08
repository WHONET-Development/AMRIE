# Interpretation Engine Performance Harness

This opt-in .NET 10 console app measures the existing CPU interpretation engine. It has a one-way project reference to `Interpretation Engine` and adds no packages or CUDA dependencies.

## Modes

- `--mode file` calls the engine's production `IO_Library.InterpretDataFileBounded` entry point, which uses bounded owning batches, bounded exact caches and disk-staged publication. The original `InterpretDataFile` public API delegates to the same implementation for compatibility. The existing 128 MiB safety limit remains opt-in overridable with `--max-whole-file-mb`; larger jobs require sufficient temporary disk space, not whole-file memory retention.
- `--mode stream` (default) retains its independent `TextFieldParser` path and global key scan, then interprets bounded row batches through the public `IsolateInterpretation` API. Its global key inventory is not bounded; it is a comparison path, not a production memory-plateau test.

Bounded stream mode currently requires the configuration's horizontal antibiotic output setting; it reports an error for vertical-output configurations rather than silently changing the output shape.

Stream mode discards output unless `--output` is supplied. Whole-file mode writes to a temporary output file when `--output` is omitted, then deletes that file after measuring it. Existing output and report paths are never overwritten. JSON reports are written only when `--report` is supplied.

## Examples

```powershell
dotnet run --project "Interpretation Engine.Performance\Interpretation Engine.Performance.csproj" -- --input "C:\data\2026-clsi-eucast-boundary-input.txt" --mode file --manifest "C:\data\2026-clsi-eucast-manifest.json" --report "C:\data\cpu-base.json"
dotnet run --project "Interpretation Engine.Performance\Interpretation Engine.Performance.csproj" -- --input "C:\data\2026-clsi-eucast-boundary-input.txt" --mode stream --batch-size 1024 --max-rows 10000 --manifest "C:\data\2026-clsi-eucast-manifest.json" --report "C:\data\cpu-pilot.json"
```

Use `--help` for all options. Reports contain per-pass timing, throughput, allocations, process peak working set, input/output metadata, run parameters, and OS/runtime/CPU information. In stream mode, key scanning, breakpoint preheat, input parsing, interpretation, and output are timed separately. The whole-file engine API does not expose those internal stages, so its report contains end-to-end timing only.

For an instrumented production-path run, add `--memory-report "C:\data\memory.jsonl" --mode file --runs 1`. Samples are streamed to a new JSONL file every 250 ms, not accumulated in memory. They contain actual completed input rows, stage, retained batch size/characters, unforced managed-heap estimates, GC-committed bytes, current working set/private bytes and exact cache counts/characters/evictions. Sampling failures are propagated. The pass report adds independently observed row/measurement totals and maximum batch/cache values; ordinary file-mode totals with a manifest remain manifest-derived. No forced collections are used. Do not compare instrumented timings to uninstrumented controls as a matched timing result.

Use three separate fresh processes with `--runs 1` for matched runtime/allocation/peak comparisons. To test a plateau, also sample a longer repeated and a high-cardinality workload, verify output bytes/hash independently, and inspect memory versus actual rows after the cache/startup warmup. A provisional working-set envelope is `max(96 MiB, 20% of the post-warmup median)`; choose and report a warmup that covers the workload's cache saturation. A process-lifetime peak alone is not a plateau result. Oversized individual records and wider headers require a correspondingly larger envelope.
