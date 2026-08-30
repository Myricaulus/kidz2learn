using System.Diagnostics;
using Kidz2Learn.Model;
using Xunit;

namespace Kidz2Learn.Tests;

/// <summary>
///     Sanity checks for the generated catalog (Model/SilbenHammerWords.g.cs, produced by
///     WaveSplit/GenerateSilbenHammerWords.py) and the syllable index built from it.
/// </summary>
public class SilbenHammerWordsTests
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
        // Must stay linear in catalog size (see SilbenHammerSyllableIndex.Build's remarks). 5s is
        // a very generous ceiling for ten-thousand-plus words (should be well under 100ms) so this
        // only fails if that stops being true, not on a merely slow CI box.
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
