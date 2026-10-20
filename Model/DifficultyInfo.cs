namespace Kidz2Learn.Model;

/// <summary>
///     Kid-facing presentation of a task's <see cref="Difficulty" /> (HUD indicator) and the point
///     factor it earns. Every point a correct answer earns is scaled once, centrally in
///     <c>TaskSessionController.RecordSuccess</c>; views that award points outside of it (Silbenhammer's
///     per-word points) scale through <see cref="Scale" /> themselves.
/// </summary>
/// <remarks>
///     TODO: derive the factor from how far the task is above the learner's mastery rather than
///     from the pool-relative <see cref="Difficulty" /> - see GitHub issue #1.
/// </remarks>
public static class DifficultyInfo
{
    public static string Icon(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Normal => "🎈",
        Difficulty.Hard => "💪",
        Difficulty.Extreme => "💣",
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty))
    };

    public static string Label(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Normal => "Spielen",
        Difficulty.Hard => "Arbeiten",
        Difficulty.Extreme => "Bombe",
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty))
    };

    public static double PointFactor(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Normal => 1.0,
        Difficulty.Hard => 1.5,
        Difficulty.Extreme => 2.0,
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty))
    };

    public static int Scale(int points, Difficulty difficulty)
    {
        return (int)Math.Round(points * PointFactor(difficulty), MidpointRounding.AwayFromZero);
    }

    /// <summary>
    ///     Weight of a wrong answer's penalty: guessing through the options of an easy task costs
    ///     more than a slip on a hard one.
    /// </summary>
    public static double PenaltyFactor(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Normal => 1.5,
        Difficulty.Hard => 0.5,
        Difficulty.Extreme => 0.2,
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty))
    };

    /// <summary>
    ///     Base (and, separately, bonus) points for the <paramref name="wrongCount" />-th wrong answer
    ///     to the same question: -10 per wrong answer so far, as in the Silben tasks, weighted by
    ///     <see cref="PenaltyFactor" />.
    /// </summary>
    public static int Penalty(int wrongCount, Difficulty difficulty)
    {
        return -(int)Math.Round(10 * wrongCount * PenaltyFactor(difficulty), MidpointRounding.AwayFromZero);
    }
}
