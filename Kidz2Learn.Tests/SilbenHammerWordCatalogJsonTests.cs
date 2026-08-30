using System.Diagnostics;
using Kidz2Learn.Model;
using Xunit;

namespace Kidz2Learn.Tests;

/// <summary>
///     Regression tests for the generated catalog itself (Model/SilbenHammerWords.g.cs, produced
///     by WaveSplit/GenerateSilbenHammerWords.py) - it's now compile-time data (no more runtime
///     JSON fetch/parse), so these just sanity-check the compiled array and the once-built index,
///     instead of exercising a deserialization path.
/// </summary>
public class SilbenHammerWordCatalogJsonTests
{
    [Fact]
    public void Data_IsNonEmptyWithEntriesOfBothTiers()
    {
        Assert.True(SilbenHammerWords.Data.Count > 1000, $"Expected a sizeable catalog, got {SilbenHammerWords.Data.Count}");
        Assert.All(SilbenHammerWords.Data, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.Word));
            Assert.NotEmpty(e.Syllables);
            Assert.All(e.Syllables, s => Assert.False(string.IsNullOrWhiteSpace(s)));
        });
        Assert.Contains(SilbenHammerWords.Data, e => e.Tier == WordTier.Grundschule);
        Assert.Contains(SilbenHammerWords.Data, e => e.Tier == WordTier.Schlaukopf);
    }

    [Fact]
    public void SyllableIndex_BuildsFromTheRealCatalogWithinABudget()
    {
        // Regression guard for the "Lade Wort takes 2-3 seconds" bug: SilbenHammerSyllableIndex
        // used to dedupe each pool entry via List.Contains (O(n) per insertion) and was rebuilt on
        // every Silbenhammer burst - quadratic in a common syllable's word count, on ten-thousand-
        // plus words, several times a minute. 5s is a very generous ceiling (should be well under
        // 100ms) so this only fails if the O(n^2) shape (or the per-burst rebuild) comes back, not
        // on a merely slow CI box.
        var sw = Stopwatch.StartNew();
        var index = SilbenHammerSyllableIndex.Build(SilbenHammerWords.Data);
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 5000,
            $"Building the syllable index took {sw.ElapsedMilliseconds}ms - expected well under 5000ms");
        Assert.NotEmpty(index.FirstSyllablePool);
    }

    [Fact]
    public void Index_IsCachedAcrossCalls()
    {
        Assert.Same(SilbenHammerWords.Index, SilbenHammerWords.Index);
    }
}
