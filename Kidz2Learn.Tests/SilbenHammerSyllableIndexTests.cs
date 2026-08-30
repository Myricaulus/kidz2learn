using Kidz2Learn.Model;
using Xunit;

namespace Kidz2Learn.Tests;

public class SilbenHammerSyllableIndexTests
{
    private static SilbenHammerWordEntry W(string word, params string[] syllables)
    {
        return new SilbenHammerWordEntry(word, syllables, WordTier.Grundschule);
    }

    [Fact]
    public void Build_AnyPositionPool_ContainsEveryWordAlsoListedInFirstOrInnerPool()
    {
        // Regression test: AnyPositionPool used to come out empty for virtually every syllable
        // (a shared dedup-tracking bug - see Build's remarks), which broke the "same syllable"
        // follow-up rounds entirely since SilbenHammerSelector reads follow-ups exclusively from
        // AnyPositionPool.
        var words = new[]
        {
            W("Banane", "Ba", "na", "ne"),
            W("Baden", "Ba", "den"),
            W("Ball", "Ba", "ll")
        };

        var index = SilbenHammerSyllableIndex.Build(words);

        Assert.Equal(3, index.FirstSyllablePool["ba"].Count);
        Assert.Equal(3, index.AnyPositionPool["ba"].Count);
        Assert.Equal(index.FirstSyllablePool["ba"].Select(w => w.Word).ToHashSet(),
            index.AnyPositionPool["ba"].Select(w => w.Word).ToHashSet());
    }

    [Fact]
    public void Build_AWordListedTwiceForTheSameSyllable_OnlyCountsOnceInAnyPositionPool()
    {
        // "Bonbon" contains "bon" twice (positions 0 and 1) - AnyPositionPool must still list the
        // word only once for "bon".
        var words = new[] { W("Bonbon", "Bon", "bon") };

        var index = SilbenHammerSyllableIndex.Build(words);

        Assert.Single(index.AnyPositionPool["bon"]);
    }
}
