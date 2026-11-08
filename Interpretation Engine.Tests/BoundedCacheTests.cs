using AMR_Engine;

namespace Interpretation_Engine.Tests;

[CollectionDefinition("Bounded cache integration", DisableParallelization = true)]
public sealed class BoundedCacheCollection;

[Collection("Bounded cache integration")]
public sealed class BoundedCacheTests
{
    [Fact]
    public void EntryEvictionIsFifoAndRecomputesExactValuesIncludingNull()
    {
        BoundedCache<string, string?> cache = new(2, 100);
        int calls = 0;
        Assert.Null(cache.GetOrAdd("a", 1, () => { calls++; return null; }));
        Assert.Equal("b-value", cache.GetOrAdd("b", 1, () => "b-value"));
        Assert.Null(cache.GetOrAdd("a", 1, () => throw new InvalidOperationException()));
        cache.GetOrAdd("c", 1, () => "c-value");
        Assert.Null(cache.GetOrAdd("a", 1, () => { calls++; return null; }));
        Assert.Equal(2, calls);
        Assert.Equal(2, cache.Statistics.Entries);
        Assert.Equal(2, cache.Statistics.Evictions);
        Assert.Equal(2, cache.Statistics.KeyCharacters);
    }

    [Fact]
    public void CharacterLimitEvictsAndOversizedKeysAreNeverRetained()
    {
        BoundedCache<string, string> cache = new(100, 5);
        cache.GetOrAdd("abc", 3, () => "one");
        cache.GetOrAdd("def", 3, () => "two");
        Assert.Equal(new BoundedCacheStatistics(1, 3, 1, 0), cache.Statistics);
        int calls = 0;
        for (int index = 0; index < 2; index++)
            Assert.Equal("oversized", cache.GetOrAdd("abcdef", 6, () => { calls++; return "oversized"; }));
        Assert.Equal(2, calls);
        Assert.Equal(new BoundedCacheStatistics(1, 3, 1, 2), cache.Statistics);
        Assert.Equal("two", cache.GetOrAdd("def", 3, () => throw new InvalidOperationException()));
    }

    [Fact]
    public void ClearResetsEvictionQueueAndFailedFactoriesAreNotCached()
    {
        BoundedCache<string, int> cache = new(1, 10);
        cache.GetOrAdd("old", 3, () => 1);
        cache.Clear();
        Assert.Equal(default, cache.Statistics);
        Assert.Throws<IOException>(() => cache.GetOrAdd("failed", 6, () => throw new IOException("explicit")));
        Assert.Equal(default, cache.Statistics);
        Assert.Equal(2, cache.GetOrAdd("new", 3, () => 2));
        Assert.Equal(3, cache.GetOrAdd("next", 4, () => 3));
        Assert.Equal(new BoundedCacheStatistics(1, 4, 1, 0), cache.Statistics);
    }

    [Fact]
    public void ConcurrentLookupsKeepBothBoundsAndComputeSharedKeyOnce()
    {
        BoundedCache<string, int> shared = new(16, 128);
        int calls = 0;
        Parallel.For(0, 1000, _ => Assert.Equal(7, shared.GetOrAdd("shared", 6, () =>
        {
            Interlocked.Increment(ref calls);
            return 7;
        })));
        Assert.Equal(1, calls);
        Parallel.For(0, 10000, index =>
        {
            string key = index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(index, shared.GetOrAdd(key, key.Length, () => index));
            Assert.InRange(shared.Statistics.Entries, 0, 16);
            Assert.InRange(shared.Statistics.KeyCharacters, 0, 128);
        });
        Assert.True(shared.Statistics.Evictions > 0);
    }

    [Fact]
    public void TupleKeysPreserveCaseConfigurationYearAndNullVersusEmpty()
    {
        BoundedCache<(string, string, int, string?), int> cache = new(10, 100);
        (string, string, int, string?)[] keys =
        [ ("org", "CLSI", 2026, null), ("org", "CLSI", 2026, ""),
          ("ORG", "CLSI", 2026, ""), ("org", "EUCAST", 2026, ""),
          ("org", "CLSI", 2025, ""), ("org", "CLSI", 2026, "priority-2;priority-1") ];
        for (int index = 0; index < keys.Length; index++)
        {
            int value = index;
            Assert.Equal(value, cache.GetOrAdd(keys[index], 1, () => value));
        }
        for (int index = 0; index < keys.Length; index++)
            Assert.Equal(index, cache.GetOrAdd(keys[index], 1, () => -1));
    }

    [Fact]
    public void ProductionCachesEvictWithoutChangingInterpretations()
    {
        AntibioticSpecificInterpretationRules.ClearBreakpoints();
        string expected = IsolateInterpretation.GetSingleInterpretation(
            InterpretationConfiguration.DefaultConfiguration(), "spn", "AMC_NM", "4");
        for (int index = 0; index < AntibioticSpecificInterpretationRules.MaxBreakpointCacheEntries + 32; index++)
        {
            var rules = new AntibioticSpecificInterpretationRules("cache-unknown-" + index,
                "AMC_NM", "2", [], guidelineYear: 2026);
            Assert.Equal(string.Empty, rules.GetInterpretation());
        }
        var statistics = AntibioticSpecificInterpretationRules.CacheStatistics;
        Assert.InRange(statistics.Breakpoints.Entries, 1, AntibioticSpecificInterpretationRules.MaxBreakpointCacheEntries);
        Assert.InRange(statistics.Breakpoints.KeyCharacters, 1, AntibioticSpecificInterpretationRules.MaxBreakpointCacheCharacters);
        Assert.InRange(statistics.Intrinsic.Entries, 1, AntibioticSpecificInterpretationRules.MaxIntrinsicCacheEntries);
        Assert.InRange(statistics.Intrinsic.KeyCharacters, 1, AntibioticSpecificInterpretationRules.MaxIntrinsicCacheCharacters);
        Assert.True(statistics.Breakpoints.Evictions > 0);
        Assert.True(statistics.Intrinsic.Evictions > 0);
        Assert.Equal(expected, IsolateInterpretation.GetSingleInterpretation(
            InterpretationConfiguration.DefaultConfiguration(), "spn", "AMC_NM", "4"));
        AntibioticSpecificInterpretationRules.ClearBreakpoints();
        Assert.Equal(default, AntibioticSpecificInterpretationRules.CacheStatistics.Breakpoints);
    }
}
