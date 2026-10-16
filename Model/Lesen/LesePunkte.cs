namespace Kidz2Learn.Model.Lesen;

/// <summary>
///     Points for the sentence-reading tasks, scaled by how much there is to read. Word count
///     stands in for reading time: measured time would reward dawdling, while word count tracks
///     the effort. Calibrated so a session reaches the 100-point reward after roughly 5-10
///     minutes: a slow reader needs ~2.5 s per word, so a typical 7-word sentence takes ~20 s and
///     earns ~4 base points (plus the same as decaying bonus). On the Lesedetektiv about half the
///     tasks are level 2, which <c>DifficultyInfo</c> pays ×1.5, so the average task earns ~5 -
///     about 20 tasks to the top.
/// </summary>
public static class LesePunkte
{
    public const float PointsPerWord = 0.56f;
    public const int Min = 3;
    public const int Max = 10;

    public static int ForText(params string[] texts)
    {
        var words = texts.Sum(t => t.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
        return Math.Clamp((int)Math.Round(words * PointsPerWord), Min, Max);
    }
}
