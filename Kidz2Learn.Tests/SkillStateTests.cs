using Kidz2Learn.Entities;
using Xunit;

namespace Kidz2Learn.Tests;

public class SkillStateTests
{
    private static SkillState WithAttempts(params bool[] correctFlags)
    {
        var state = new SkillState { Id = "test_skill" };
        foreach (var correct in correctFlags)
            state.AttemptsHistory.Add(new SkillAttempt { Correct = correct });
        return state;
    }

    [Fact]
    public void RecentAccuracy_FewerThanFiveAttempts_IsNull()
    {
        var state = WithAttempts(true, true, true, true);

        Assert.Null(state.RecentAccuracy);
    }

    [Fact]
    public void RecentAccuracy_AtLeastFiveAttempts_IsCorrectRatio()
    {
        var state = WithAttempts(true, true, true, true, false);

        Assert.Equal(0.8f, state.RecentAccuracy);
    }

    [Fact]
    public void RecentAccuracy_MoreThanWindowSize_OnlyCountsTheRecentWindow()
    {
        // The ring buffer drops the oldest entries automatically once it's full, so an old streak
        // of failures "falls out" after enough new attempts. Simulates the exact "vergesslich,
        // nicht die Fehler von vor 3 Jahren" behavior that was asked for. Uses AttemptHistorySize
        // directly rather than a hardcoded number, since that's exactly the kind of thing that's
        // meant to be tunable (started at 20, bumped to 50 - see SkillMigrationHelper v2).
        var windowSize = SkillState.AttemptHistorySize;
        var state = WithAttempts(Enumerable.Repeat(false, windowSize).ToArray());
        Assert.Equal(0f, state.RecentAccuracy);

        for (var i = 0; i < windowSize; i++)
            state.AttemptsHistory.Add(new SkillAttempt { Correct = true });

        Assert.Equal(1f, state.RecentAccuracy);
    }
}
