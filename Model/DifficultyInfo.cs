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
        Difficulty.Extreme => "🔥",
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty))
    };

    public static string Label(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Normal => "Spielen",
        Difficulty.Hard => "Arbeiten",
        Difficulty.Extreme => "Anstrengend",
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
}
