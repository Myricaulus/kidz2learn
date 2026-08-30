namespace Kidz2Learn.Model;

/// <summary>
///     Pure formula turning a per-syllable "clean streak" (consecutive rounds resolved without a
///     "struggled" press first, can go negative) into a 1-100 pick weight for
///     <see cref="SilbenHammerSelector" />. Default/unseen (streak 0) is 50; 10 clean rounds in a
///     row settles near 1 ("practically never picked again"); a struggled round pushes the streak
///     negative, which pushes the score back above 50 (picked more often than default).
/// </summary>
public static class SilbenHammerScoring
{
    private const double DecayBase = 0.7;

    public static int ComputeScore(int cleanStreak)
    {
        var raw = 50.0 * Math.Pow(DecayBase, cleanStreak);
        return (int)Math.Clamp(Math.Round(raw), 1, 100);
    }
}
