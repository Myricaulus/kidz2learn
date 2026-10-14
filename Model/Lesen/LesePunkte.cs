namespace Kidz2Learn.Model.Lesen;

/// <summary>
///     Points for the sentence-reading tasks, scaled by how much there is to read. Word count
///     stands in for reading time: measured time would reward dawdling, while word count tracks
///     the effort (a slow reader needs roughly 2.5 s per word, so a 3-4 s Silben word worth 5
///     points is about the same rate as 1.5 points per word).
/// </summary>
public static class LesePunkte
{
    public const float PointsPerWord = 1.5f;
    public const int Min = 4;
    public const int Max = 15;

    public static int ForText(params string[] texts)
    {
        var words = texts.Sum(t => t.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
        return Math.Clamp((int)Math.Round(words * PointsPerWord), Min, Max);
    }
}
