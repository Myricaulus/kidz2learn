using Kidz2Learn.Model;
using Xunit;

namespace Kidz2Learn.Tests;

public class SilbenHammerSelectorTests
{
    private static SilbenHammerWordEntry W(string word, params string[] syllables)
    {
        return new SilbenHammerWordEntry(word, syllables, WordTier.Grundschule);
    }

    private static SilbenHammerSyllableIndex Idx(params SilbenHammerWordEntry[] words)
    {
        return SilbenHammerSyllableIndex.Build(words);
    }

    [Fact]
    public async Task PickNextWordAsync_FirstCall_PicksFromFirstSyllablePool()
    {
        // No ratings recorded yet - both candidate first syllables default to score 50, so
        // whichever wins the roll, it must still be a first-syllable pick (no threshold fallback
        // can trigger at the default score).
        var words = new[] { W("Banane", "Ba", "na", "ne"), W("Opa", "O", "pa") };
        var selector = new SilbenHammerSelector(Idx(words), new FakeSilbenHammerRatingStore(), new Random(1));

        var picked = await selector.PickNextWordAsync();

        Assert.NotNull(picked);
        Assert.Equal(SilbenHammerSyllableKey.Normalize(picked!.Syllables[0]), selector.TargetSyllable);
    }

    [Fact]
    public async Task PickNextWordAsync_WeightsTowardHigherScoredSyllable()
    {
        // "ba" has been read cleanly 10 times in a row (score ~1, well mastered); "o" is unseen
        // (score 50). A fresh selector is built per trial since TargetSyllable/budget accumulate
        // state after the first pick - only the very first pick of each trial is observed.
        var words = new[] { W("Ball", "Ba", "ll"), W("Oma", "O", "ma") };
        var index = Idx(words);
        var streaks = new Dictionary<string, int> { ["ba"] = 10 };
        var rng = new Random(7);

        var baCount = 0;
        var oCount = 0;
        for (var i = 0; i < 300; i++)
        {
            var selector = new SilbenHammerSelector(index, new FakeSilbenHammerRatingStore(streaks), rng);
            await selector.PickNextWordAsync();
            if (selector.TargetSyllable == "ba") baCount++;
            else if (selector.TargetSyllable == "o") oCount++;
        }

        Assert.True(oCount > baCount,
            $"Expected the higher-scored (less mastered) syllable to be picked more often (o={oCount}, ba={baCount})");
    }

    [Fact]
    public async Task PickNextWordAsync_FallsBackToInnerPoolWhenFirstSyllableAlreadyWellMastered()
    {
        // Only "ba" exists as a first syllable here, so the first-pool pick is deterministic; its
        // streak (5 clean rounds in a row -> score 8) is below the default threshold (15), so the
        // selector must fall back to an inner/last-position syllable instead.
        var words = new[] { W("Banane", "Ba", "na", "ne"), W("Baden", "Ba", "den") };
        var streaks = new Dictionary<string, int> { ["ba"] = 5 };
        var selector = new SilbenHammerSelector(Idx(words), new FakeSilbenHammerRatingStore(streaks), new Random(3));

        await selector.PickNextWordAsync();

        Assert.NotEqual("ba", selector.TargetSyllable);
        Assert.Contains(selector.TargetSyllable, new[] { "na", "ne", "den" });
    }

    [Fact]
    public async Task PickNextWordAsync_FollowUpWords_AllContainTheSameTargetSyllable()
    {
        // Every word starts with "Ba" - the first-syllable pool has exactly one key, so the fresh
        // pick is deterministic regardless of RNG, isolating the follow-up behaviour under test.
        var words = new[]
        {
            W("Banane", "Ba", "na", "ne"),
            W("Baden", "Ba", "den"),
            W("Basar", "Ba", "sar"),
            W("Backe", "Ba", "cke"),
            W("Ball", "Ba", "ll")
        };
        var options = new SilbenHammerSelectorOptions { FollowUpRounds = 3 };
        var selector = new SilbenHammerSelector(Idx(words), new FakeSilbenHammerRatingStore(), new Random(11), options);

        await selector.PickNextWordAsync();
        var target = selector.TargetSyllable;
        Assert.NotNull(target);

        for (var i = 0; i < 3; i++)
        {
            var next = await selector.PickNextWordAsync();
            Assert.Equal(target, selector.TargetSyllable);
            Assert.Contains(next!.Syllables, s => SilbenHammerSyllableKey.Normalize(s) == target);
        }
    }

    [Fact]
    public async Task PickNextWordAsync_SwitchesTargetSyllableWhenPoolRunsDryBeforeFollowUpBudgetIsSpent()
    {
        // "ba"'s own word pool (Banane, Basar) is only 2 words deep, but the follow-up budget below
        // asks for 5 - once both are used, PickFreshTargetAsync must move on to a different
        // syllable rather than returning null or silently re-picking an already-used "ba" word.
        // "ba" is the only FirstSyllablePool key here, so the only way a fresh pick can land
        // anywhere else is via the MasteredFirstSyllableThreshold redirect to the inner pool - the
        // four RecordCleanAsync calls push its streak/score just below that threshold (12 < 15)
        // between the two "ba" picks and the exhaustion, mimicking syllables answered cleanly
        // in between, same as SilbenHammerView.OnCorrect does against the real store. This is the
        // TargetSyllable change SilbenHammerView.LoadNextWordInBurstAsync relies on to detect
        // "burst restarted early" and reset its own word counter to match.
        var words = new[] { W("Banane", "Ba", "na", "ne"), W("Basar", "Ba", "sar") };
        var options = new SilbenHammerSelectorOptions { FollowUpRounds = 5 };
        var store = new FakeSilbenHammerRatingStore();
        var selector = new SilbenHammerSelector(Idx(words), store, new Random(1), options);

        await selector.PickNextWordAsync(); // fresh pick: "ba", consumes one of the two "ba" words
        Assert.Equal("ba", selector.TargetSyllable);
        await selector.PickNextWordAsync(); // follow-up: consumes the other "ba" word
        Assert.Equal("ba", selector.TargetSyllable);

        for (var i = 0; i < 4; i++)
            await store.RecordCleanAsync("ba");

        var third = await selector.PickNextWordAsync();

        Assert.NotNull(third);
        Assert.NotEqual("ba", selector.TargetSyllable);
    }

    [Fact]
    public async Task PickNextWordAsync_NeverRepeatsAWordWithinABurst()
    {
        var words = new[]
        {
            W("Banane", "Ba", "na", "ne"),
            W("Baden", "Ba", "den"),
            W("Basar", "Ba", "sar"),
            W("Backe", "Ba", "cke"),
            W("Ball", "Ba", "ll")
        };
        var options = new SilbenHammerSelectorOptions { FollowUpRounds = 4 };
        var selector = new SilbenHammerSelector(Idx(words), new FakeSilbenHammerRatingStore(), new Random(5), options);

        var seen = new HashSet<string>();
        for (var i = 0; i < 5; i++)
        {
            var word = await selector.PickNextWordAsync();
            Assert.True(seen.Add(word!.Key), $"'{word.Key}' was picked more than once within the burst");
        }
    }

    [Fact]
    public async Task PickNextWordAsync_FreshPick_CanChooseASyllableWithNoPossibleFollowUp()
    {
        // Unique-to-one-word syllables must stay selectable - they still need practice, they just
        // can't have a same-syllable follow-up. Only a *connected* syllable failing to produce a
        // follow-up would be a bug (covered by SilbenHammerSyllableIndexTests and the "all contain
        // the same target syllable" test above).
        var words = new[] { W("Reis", "Reis") };
        var selector = new SilbenHammerSelector(Idx(words), new FakeSilbenHammerRatingStore(), new Random(1));

        var picked = await selector.PickNextWordAsync();

        Assert.Equal("reis", selector.TargetSyllable);
        Assert.Equal("Reis", picked!.Word);
    }

    [Fact]
    public async Task RemainingWordsForTargetSyllable_ExcludesAlreadyUsedWords()
    {
        var words = new[]
        {
            W("Banane", "Ba", "na", "ne"),
            W("Baden", "Ba", "den"),
            W("Ball", "Ba", "ll")
        };
        var selector = new SilbenHammerSelector(Idx(words), new FakeSilbenHammerRatingStore(), new Random(1));

        var first = await selector.PickNextWordAsync();
        var remaining = selector.RemainingWordsForTargetSyllable();

        Assert.DoesNotContain(first!.Word, remaining);
        Assert.Equal(2, remaining.Count);
    }

    [Fact]
    public void RemainingWordsForTargetSyllable_BeforeAnyPick_IsEmpty()
    {
        var words = new[] { W("Banane", "Ba", "na", "ne") };
        var selector = new SilbenHammerSelector(Idx(words), new FakeSilbenHammerRatingStore(), new Random(1));

        Assert.Empty(selector.RemainingWordsForTargetSyllable());
    }

    [Fact]
    public async Task PickNextWordAsync_UnknownSyllable_StartsAtDefaultScoreFifty()
    {
        Assert.Equal(50, SilbenHammerScoring.ComputeScore(0));

        var words = new[] { W("Banane", "Ba", "na", "ne") };
        var selector = new SilbenHammerSelector(Idx(words), new FakeSilbenHammerRatingStore(), new Random(1));

        var picked = await selector.PickNextWordAsync();

        Assert.NotNull(picked);
        Assert.Equal("ba", selector.TargetSyllable);
    }
}
