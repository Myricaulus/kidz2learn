using Kidz2Learn.Model;
using Xunit;

namespace Kidz2Learn.Tests;

public class SilbenHammerScoringTests
{
    [Theory]
    [InlineData(0, 50)]
    [InlineData(10, 1)]
    [InlineData(-2, 100)]
    [InlineData(4, 12)]
    [InlineData(-1, 71)]
    public void ComputeScore_MatchesExpectedCurve(int streak, int expected)
    {
        Assert.Equal(expected, SilbenHammerScoring.ComputeScore(streak));
    }

    [Fact]
    public void ComputeScore_IsMonotonicallyDecreasingInStreak()
    {
        var previous = SilbenHammerScoring.ComputeScore(-20);
        for (var streak = -19; streak <= 20; streak++)
        {
            var current = SilbenHammerScoring.ComputeScore(streak);
            Assert.True(current <= previous, $"Score at streak {streak} ({current}) should be <= score at {streak - 1} ({previous})");
            previous = current;
        }
    }

    [Fact]
    public void ComputeScore_NeverBelowOneOrAboveHundred()
    {
        for (var streak = -50; streak <= 50; streak++)
            Assert.InRange(SilbenHammerScoring.ComputeScore(streak), 1, 100);
    }
}
