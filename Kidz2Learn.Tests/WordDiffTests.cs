using Kidz2Learn.Pages.SilbenChallenge;
using Xunit;

namespace Kidz2Learn.Tests;

public class WordDiffTests
{
    [Fact]
    public void IdenticalWords_RequireNoCorrections()
    {
        var req = WordDiff.BuildRequirements("Hund", "Hund");

        Assert.Empty(req.RequiredMarks);
        Assert.Empty(req.RequiredSubstitutions);
        Assert.Empty(req.RequiredGaps);
    }

    [Fact]
    public void SingleWrongLetter_RequiresSubstitution()
    {
        // "Hand" -> replace 'a' at index 1 with 'u' to get "Hund"
        var req = WordDiff.BuildRequirements("Hund", "Hand");

        Assert.Empty(req.RequiredMarks);
        Assert.Empty(req.RequiredGaps);
        Assert.Equal(new Dictionary<int, char> { [1] = 'u' }, req.RequiredSubstitutions);
    }

    [Fact]
    public void MissingLetter_RequiresGap()
    {
        // "Bam" is missing the 'u' -> gap before index 2 ("Ba|m")
        var req = WordDiff.BuildRequirements("Baum", "Bam");

        Assert.Empty(req.RequiredMarks);
        Assert.Empty(req.RequiredSubstitutions);
        Assert.Equal(new Dictionary<int, char> { [2] = 'u' }, req.RequiredGaps);
    }

    [Fact]
    public void ExtraLetter_RequiresMark()
    {
        // "Sonnne" has one 'n' too many compared to "Sonne"
        var req = WordDiff.BuildRequirements("Sonne", "Sonnne");

        Assert.Single(req.RequiredMarks);
        Assert.Empty(req.RequiredSubstitutions);
        Assert.Empty(req.RequiredGaps);
    }

    [Theory]
    [InlineData("Hund", "Hund")]
    [InlineData("Hund", "Hand")]
    [InlineData("Baum", "Bam")]
    [InlineData("Sonne", "Sonnne")]
    [InlineData("Tante", "Tanne")]
    public void RequiredMarks_AreAlwaysValidIndicesIntoTheWrongWord(string correct, string wrong)
    {
        var req = WordDiff.BuildRequirements(correct, wrong);

        Assert.All(req.RequiredMarks, i => Assert.InRange(i, 0, wrong.Length - 1));
        Assert.All(req.RequiredSubstitutions.Keys, i => Assert.InRange(i, 0, wrong.Length - 1));
        Assert.All(req.RequiredGaps.Keys, i => Assert.InRange(i, 0, wrong.Length));
    }

    [Fact]
    public void Apply_BuildRequirementsSolution_AlwaysReconstructsCorrectWord()
    {
        // Sanity check: whatever WordDiff.BuildRequirements itself computes must, by construction,
        // be a valid solution once fed back through Apply.
        const string correct = "Hund";
        const string wrong = "Hand";
        var req = WordDiff.BuildRequirements(correct, wrong);

        var result = WordDiff.Apply(wrong, req.RequiredMarks, req.RequiredSubstitutions, req.RequiredGaps);

        Assert.Equal(correct, result, ignoreCase: true);
    }

    [Fact]
    public void Apply_AnyOfSeveralAmbiguousMarks_ReconstructsCorrectWord()
    {
        // TECH_DEBT.md #12 repro: "anfasssen" has three "s" where "anfassen" only needs two -
        // marking *any one* of them as extra must be accepted, not just whichever one
        // WordDiff.BuildRequirements happens to pick during backtracking.
        const string correct = "anfassen";
        const string wrong = "anfasssen";

        // The three "s" sit at indices 4, 5, 6 in "anfasssen".
        Assert.Equal('s', wrong[4]);
        Assert.Equal('s', wrong[5]);
        Assert.Equal('s', wrong[6]);

        foreach (var sIndex in new[] { 4, 5, 6 })
        {
            var result = WordDiff.Apply(wrong, new HashSet<int> { sIndex },
                new Dictionary<int, char>(), new Dictionary<int, char>());

            Assert.Equal(correct, result, ignoreCase: true);
        }
    }

    [Fact]
    public void Apply_WrongMarkedIndex_DoesNotReconstructCorrectWord()
    {
        // Marking a letter that isn't part of the actual mistake must still be rejected.
        const string correct = "Hund";
        const string wrong = "Hand";

        var result = WordDiff.Apply(wrong, new HashSet<int> { 0 }, new Dictionary<int, char>(),
            new Dictionary<int, char>());

        Assert.NotEqual(correct, result, StringComparer.OrdinalIgnoreCase);
    }
}
