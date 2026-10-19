namespace Kidz2Learn.Model.Lesen;

/// <summary>One tile of a search grid; <see cref="Treffer" /> tiles are the ones to tap.</summary>
public sealed record RasterFeld(string Text, bool Treffer);

/// <summary>
///     Payload of a "such-raster" task: tap every <see cref="Treffer" /> tile ("Tippe alle
///     <see cref="Ziel" /> an!"). <see cref="LogKey" /> names the task's <c>SilbenLog</c> entry,
///     one per kind and level, since the grids themselves never repeat.
/// </summary>
public sealed record SuchRaster(
    string Ziel,
    int Spalten,
    IReadOnlyList<RasterFeld> Felder,
    int BasisPunkte,
    string LogKey)
{
    public int AnzahlTreffer => Felder.Count(f => f.Treffer);
}

/// <summary>
///     Scoring of a search grid, by the mistakes of the first hand-in only - correcting afterwards
///     is for learning and earns nothing back. A mistake is a wrongly tapped tile or a missed one.
/// </summary>
public static class SuchRasterWertung
{
    /// <summary>
    ///     A clean grid earns <paramref name="volle" /> points, one mistake half of them, two
    ///     nothing, and every mistake beyond that costs a point.
    /// </summary>
    public static int Punkte(int volle, int fehler)
    {
        return fehler switch
        {
            0 => volle,
            1 => volle / 2,
            _ => 2 - fehler
        };
    }

    /// <summary>Up to one mistake still counts as solved for mastery.</summary>
    public static bool Geschafft(int fehler)
    {
        return fehler <= 1;
    }
}
