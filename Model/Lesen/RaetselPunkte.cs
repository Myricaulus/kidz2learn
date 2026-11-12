namespace Kidz2Learn.Model.Lesen;

/// <summary>
///     Points (per channel, base and bonus alike) for a "Wer bin ich?" riddle. They pay for what has
///     to be read to decide: on level 1 the first clue alone rules the other options out, so only it
///     counts; on level 2 all clues count, at <see cref="SchwerFaktor" />, since the pattern of a
///     riddle is learned after a few readings. A riddle the child already solves reliably pays less:
///     the share of correct answers among its last 20 attempts lowers the points linearly, down to
///     <see cref="MinBekanntFaktor" /> at 100 %. A wrong pick before the right one halves them.
/// </summary>
public static class RaetselPunkte
{
    public const double SchwerFaktor = 0.7;
    public const double MinBekanntFaktor = 0.5;
    public const int Min = 1;

    /// <param name="level">The riddle definition's difficulty level (1 = other kinds, 2 = similar things).</param>
    /// <param name="anteilRichtig">Share of correct answers among this riddle's last attempts, 0..1, before this one.</param>
    public static int Berechne(int level, IReadOnlyList<string> hinweise, double anteilRichtig, bool mitFehlgriff)
    {
        double punkte = level <= 1
            ? LesePunkte.ForText(hinweise[0])
            : LesePunkte.ForText([.. hinweise]) * SchwerFaktor;
        punkte *= 1 - (1 - MinBekanntFaktor) * Math.Clamp(anteilRichtig, 0, 1);
        if (mitFehlgriff)
            punkte /= 2;
        return Math.Max(Min, (int)Math.Round(punkte, MidpointRounding.AwayFromZero));
    }

    /// <summary>Share of correct answers in <paramref name="kompetenz" />'s window; 0 without attempts.</summary>
    public static double AnteilRichtig(Kompetenzniveau kompetenz)
    {
        var fenster = Math.Min(kompetenz.Versuche, kompetenz.Historie.Length);
        return fenster == 0 ? 0 : (double)kompetenz.CountRichtig() / fenster;
    }
}
