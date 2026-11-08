using AMR_Engine;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;

namespace Interpretation_Engine.Performance;

internal sealed class FileMemorySampler : IDisposable
{
    private readonly object sync = new();
    private readonly StreamWriter writer;
    private readonly Process process = Process.GetCurrentProcess();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly Timer timer;
    private readonly long batchCharacterLimit;
    private FileProcessingProgress progress = new("initializing", 0, 0, 0, 0);
    private ExceptionDispatchInfo? error;
    private bool stopped;
    private int maxBatchRows;
    private long maxBatchCharacters;
    private long oversizedBatches;
    private long samples;
    private int maxBreakpointEntries;
    private long maxBreakpointCharacters;
    private int maxIntrinsicEntries;
    private long maxIntrinsicCharacters;

    public FileMemorySampler(string path, long batchCharacterLimit)
    {
        this.batchCharacterLimit = batchCharacterLimit;
        writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write,
            FileShare.Read, 65536), new UTF8Encoding(false)) { AutoFlush = true };
        timer = new Timer(Sample, null, Timeout.Infinite, Timeout.Infinite);
        Sample(null);
        timer.Change(250, 250);
    }

    public void Observe(FileProcessingProgress value)
    {
        lock (sync)
        {
            error?.Throw();
            progress = value;
            maxBatchRows = Math.Max(maxBatchRows, value.BatchRows);
            maxBatchCharacters = Math.Max(maxBatchCharacters, value.BatchCharacters);
            if (value.Stage == "interpreting" && value.BatchCharacters > batchCharacterLimit)
                oversizedBatches++;
            CaptureCacheBounds();
        }
    }

    private void CaptureCacheBounds()
    {
        var caches = AntibioticSpecificInterpretationRules.CacheStatistics;
        maxBreakpointEntries = Math.Max(maxBreakpointEntries, caches.Breakpoints.Entries);
        maxBreakpointCharacters = Math.Max(maxBreakpointCharacters, caches.Breakpoints.KeyCharacters);
        maxIntrinsicEntries = Math.Max(maxIntrinsicEntries, caches.Intrinsic.Entries);
        maxIntrinsicCharacters = Math.Max(maxIntrinsicCharacters, caches.Intrinsic.KeyCharacters);
    }

    private void Sample(object? state)
    {
        lock (sync)
        {
            if (stopped || error is not null)
                return;
            try
            {
                process.Refresh();
                CaptureCacheBounds();
                var caches = AntibioticSpecificInterpretationRules.CacheStatistics;
                writer.WriteLine(JsonSerializer.Serialize(new
                {
                    ElapsedMilliseconds = clock.Elapsed.TotalMilliseconds,
                    progress.Stage, progress.RowsProcessed, progress.MeasurementsProcessed,
                    progress.BatchRows, progress.BatchCharacters,
                    ManagedBytes = GC.GetTotalMemory(false),
                    HeapCommittedBytes = GC.GetGCMemoryInfo().TotalCommittedBytes,
                    WorkingSetBytes = process.WorkingSet64,
                    PrivateBytes = process.PrivateMemorySize64,
                    BreakpointCache = caches.Breakpoints,
                    IntrinsicCache = caches.Intrinsic
                }));
                samples++;
            }
            catch (IOException exception)
            {
                error = ExceptionDispatchInfo.Capture(exception);
            }
            catch (System.ComponentModel.Win32Exception exception)
            {
                error = ExceptionDispatchInfo.Capture(exception);
            }
        }
    }

    public FileProcessingReport Report
    {
        get
        {
            lock (sync)
            {
                error?.Throw();
                CaptureCacheBounds();
                var caches = AntibioticSpecificInterpretationRules.CacheStatistics;
                return new(progress.RowsProcessed, progress.MeasurementsProcessed, maxBatchRows,
                    maxBatchCharacters, oversizedBatches, samples, maxBreakpointEntries,
                    maxBreakpointCharacters, maxIntrinsicEntries, maxIntrinsicCharacters,
                    caches.Breakpoints.Evictions, caches.Intrinsic.Evictions);
            }
        }
    }

    public void Dispose()
    {
        timer.Dispose();
        lock (sync)
        {
            if (stopped)
            {
                error?.Throw();
                return;
            }
            stopped = true;
            try
            {
                writer.Dispose();
            }
            finally
            {
                process.Dispose();
            }
            error?.Throw();
        }
    }
}

internal sealed record FileProcessingReport(long ActualRows, long ActualMeasurements,
    int MaxBatchRows, long MaxBatchCharacters, long OversizedSingleRowBatches, long MemorySamples,
    int MaxBreakpointCacheEntries, long MaxBreakpointCacheCharacters,
    int MaxIntrinsicCacheEntries, long MaxIntrinsicCacheCharacters,
    long BreakpointEvictions, long IntrinsicEvictions);
