namespace Kidz2Learn.Model.Lesen;

/// <summary>
///     "Spiegelbuchstaben": a <see cref="SuchRaster" /> of only b, d, p and q - the letters that are
///     mirror images of each other - in which one of them is to be found. The target fills
///     roughly a quarter to a third of the grid, so counting doesn't help.
/// </summary>
public static class Spiegelbuchstaben
{
    public static readonly IReadOnlyList<string> Buchstaben = ["b", "d", "p", "q"];

    public const int PunkteMittel = 5;
    public const int PunkteSchwer = 8;

    /// <summary>Level 2: 4×4 grid.</summary>
    public static SuchRaster GenerateMittel(Random rng)
    {
        return Build(4, PunkteMittel, rng);
    }

    /// <summary>Level 3: 5×5 grid.</summary>
    public static SuchRaster GenerateSchwer(Random rng)
    {
        return Build(5, PunkteSchwer, rng);
    }

    public static SuchRaster Build(int seite, int punkte, Random rng)
    {
        var anzahl = seite * seite;
        var ziel = Buchstaben[rng.Next(Buchstaben.Count)];
        var andere = Buchstaben.Where(b => b != ziel).ToList();
        var treffer = rng.Next(anzahl / 4, anzahl / 3 + 1);

        var felder = Enumerable.Range(0, anzahl)
            .Select(i => i < treffer
                ? new RasterFeld(ziel, true)
                : new RasterFeld(andere[rng.Next(andere.Count)], false))
            .OrderBy(_ => rng.Next())
            .ToList();
        return new SuchRaster(ziel, seite, felder, punkte, $"bdpq{seite}");
    }
}
