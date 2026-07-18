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
}
