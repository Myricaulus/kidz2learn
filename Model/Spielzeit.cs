namespace Kidz2Learn.Model;

/// <summary>
///     Game time earned with points (the playtime bar, separate from the YouTube bar): every full
///     bar credits <see cref="MinutenProFuellung" /> minutes. The n-th fill of a day needs
///     <see cref="Grundziel" /> + (n-1) * <see cref="Steigerung" /> points, so the day's count
///     (<see cref="Fuellungen" />) outlives reloads and Koop/solo alike. A full bar only pays out
///     once a 💣 task was solved during that fill: until then it stays full (the "wall") and further
///     points are lost. After a payout the bar is empty - points past the target never carry over.
/// </summary>
/// <remarks>Pure state, no I/O - <c>SpielzeitService</c> persists and displays it.</remarks>
public sealed class Spielzeit
{
    public const int Grundziel = 30;
    public const int Steigerung = 5;
    public const int MinutenProFuellung = 10;

    public Spielzeit(DateOnly heute, SpielzeitStand? stand = null)
    {
        stand ??= new SpielzeitStand(heute, 0, 0, 0, false);
        Datum = stand.Datum;
        Fuellungen = Math.Max(0, stand.Fuellungen);
        Minuten = stand.Minuten;
        Punkte = Math.Max(0, stand.Punkte);
        MauerWeg = stand.MauerWeg;
        NeuerTag(heute);
    }

    public SpielzeitStand Stand => new(Datum, Fuellungen, Minuten, Punkte, MauerWeg);

    /// <summary>The day <see cref="Fuellungen" /> counts for.</summary>
    public DateOnly Datum { get; private set; }

    /// <summary>Fills credited on <see cref="Datum" />.</summary>
    public int Fuellungen { get; private set; }

    /// <summary>Credited minutes not yet handed to a game. Credited minutes are never taken back.</summary>
    public int Minuten { get; set; }

    /// <summary>Points in the current fill, 0..<see cref="Ziel" />.</summary>
    public float Punkte { get; private set; }

    /// <summary>A 💣 was solved during the current fill, so the wall is down.</summary>
    public bool MauerWeg { get; private set; }

    public int Ziel => Grundziel + Fuellungen * Steigerung;

    /// <summary>The bar is full and waits for a 💣 - the picker forces one.</summary>
    public bool BrauchtBombe => Punkte >= Ziel && !MauerWeg;

    /// <summary>Adds (or, for a penalty, removes) points; returns 1 when that paid out a fill.</summary>
    public int AddPunkte(float punkte, DateOnly heute)
    {
        NeuerTag(heute);
        Punkte = Math.Max(0, Punkte + punkte);
        return Gutschreiben();
    }

    /// <summary>A 💣 task was solved; returns 1 when that paid out a fill (the bar was already full).</summary>
    public int BombeGeloest(DateOnly heute)
    {
        NeuerTag(heute);
        MauerWeg = true;
        return Gutschreiben();
    }

    private int Gutschreiben()
    {
        if (Punkte < Ziel)
            return 0;
        if (!MauerWeg)
        {
            Punkte = Ziel;
            return 0;
        }

        Punkte = 0;
        Fuellungen++;
        Minuten += MinutenProFuellung;
        MauerWeg = false;
        return 1;
    }

    private void NeuerTag(DateOnly heute)
    {
        if (Datum == heute)
            return;
        Datum = heute;
        Fuellungen = 0;
    }
}

/// <summary>What <see cref="Spielzeit" /> keeps across reloads.</summary>
public sealed record SpielzeitStand(DateOnly Datum, int Fuellungen, int Minuten, float Punkte, bool MauerWeg);
