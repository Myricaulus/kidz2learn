using Kidz2Learn.Model;
using Xunit;

namespace Kidz2Learn.Tests;

public class DifficultyInfoTests
{
    [Theory]
    [InlineData(Difficulty.Normal, 5, 5)]
    [InlineData(Difficulty.Hard, 5, 8)]
    [InlineData(Difficulty.Hard, 4, 6)]
    [InlineData(Difficulty.Extreme, 5, 10)]
    [InlineData(Difficulty.Hard, 0, 0)]
    public void Scale_AppliesTheFactorAndRoundsHalfUp(Difficulty difficulty, int points, int expected)
    {
        Assert.Equal(expected, DifficultyInfo.Scale(points, difficulty));
    }

    [Fact]
    public void HarderMeansMorePoints_AndEveryLevelHasIconAndLabel()
    {
        var levels = Enum.GetValues<Difficulty>();
        for (var i = 1; i < levels.Length; i++)
            Assert.True(DifficultyInfo.PointFactor(levels[i]) > DifficultyInfo.PointFactor(levels[i - 1]));

        foreach (var level in levels)
        {
            Assert.False(string.IsNullOrWhiteSpace(DifficultyInfo.Icon(level)));
            Assert.False(string.IsNullOrWhiteSpace(DifficultyInfo.Label(level)));
        }
    }
}
