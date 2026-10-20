namespace Kidz2Learn.Model.Lesen;

public enum Rechenart
{
    Plus,
    Minus
}

/// <summary>Which of the three numbers of a story the text leaves out, so it has to be calculated.</summary>
public enum Unbekannt
{
    Vorher,
    Aenderung,
    Jetzt
}

/// <param name="Nominativ">"der Igel"</param>
/// <param name="Dativ">"dem Igel"</param>
/// <param name="Pronomen">"er"</param>
public sealed record Figur(string Nominativ, string Dativ, string Pronomen)
{
    /// <summary>"vom Igel", "von der Oma".</summary>
    public string VonDativ => Dativ.StartsWith("dem ") ? $"vom {Dativ[4..]}" : $"von {Dativ}";
}

/// <summary>
///     Word problems ("Textaufgaben") as a <see cref="WFrageSerie" />: a short story about someone
///     getting or giving away things, and one "Wie viele ...?" question per number of the story -
///     before, the change, and now. Two of them are in the text, the third has to be calculated, and
///     the options are all three numbers plus the result of the wrong operation, so neither copying a
///     number from the text nor the other arithmetic sign gets through.
/// </summary>
public static class Rechengeschichten
{
    /// <summary>
    ///     Highest number in a story: sums stay below 10, as in <see cref="Skill.Math.Add10NoCarry" />.
    ///     TODO: raise it with the learner's mastery of the matching arithmetic skill.
    /// </summary>
    public const int Maximum = 9;

    private const int Minimum = 2;

    public static readonly IReadOnlyList<Figur> Figuren =
    [
        new("der Opa", "dem Opa", "er"),
        new("die Oma", "der Oma", "sie"),
        new("der Vater", "dem Vater", "er"),
        new("die Mutter", "der Mutter", "sie"),
        new("die Tante", "der Tante", "sie"),
        new("der Ritter", "dem Ritter", "er"),
        new("der Hase", "dem Hasen", "er"),
        new("der Igel", "dem Igel", "er"),
        new("die Maus", "der Maus", "sie")
    ];

    public static readonly IReadOnlyList<string> Dinge =
        ["Äpfel", "Birnen", "Nüsse", "Eier", "Pilze", "Steine", "Blumen", "Kekse"];

    public static WFrageSerie GeneratePlus(Random rng) => Generate(Rechenart.Plus, rng);

    public static WFrageSerie GenerateMinus(Random rng) => Generate(Rechenart.Minus, rng);

    public static WFrageSerie Generate(Rechenart art, Random rng)
    {
        var figuren = Figuren.OrderBy(_ => rng.Next()).Take(2).ToList();
        var unbekannt = (Unbekannt)rng.Next(3);
        var dinge = Dinge[rng.Next(Dinge.Count)];

        int vorher, aenderung;
        if (art == Rechenart.Plus)
        {
            // vorher + aenderung <= Maximum, the two summands differ.
            do
            {
                vorher = rng.Next(Minimum, Maximum - Minimum + 1);
                aenderung = rng.Next(Minimum, Maximum - vorher + 1);
            } while (vorher == aenderung);
        }
        else
        {
            // vorher - aenderung >= Minimum, subtrahend and difference differ.
            do
            {
                vorher = rng.Next(2 * Minimum + 1, Maximum + 1);
                aenderung = rng.Next(Minimum, vorher - Minimum + 1);
            } while (aenderung == vorher - aenderung);
        }

        return Build(art, unbekannt, figuren[0], figuren[1], dinge, vorher, aenderung, rng);
    }

    /// <param name="aenderung">How many things <paramref name="wer" /> gets (<see cref="Rechenart.Plus" />) or gives away.</param>
    public static WFrageSerie Build(Rechenart art, Unbekannt unbekannt, Figur wer, Figur partner, string dinge,
        int vorher, int aenderung, Random rng)
    {
        var jetzt = art == Rechenart.Plus ? vorher + aenderung : vorher - aenderung;
        var subjekt = Gross(wer.Nominativ);
        var er = wer.Pronomen;

        var text = (art, unbekannt) switch
        {
            (Rechenart.Plus, Unbekannt.Vorher) =>
                $"{subjekt} hat {partner.VonDativ} {aenderung} neue {dinge} bekommen. Jetzt hat {er} {jetzt}.",
            (Rechenart.Plus, Unbekannt.Aenderung) =>
                $"{subjekt} hatte {vorher} {dinge}. Dann hat {er} {partner.VonDativ} neue bekommen. Jetzt hat {er} {jetzt}.",
            (Rechenart.Plus, _) =>
                $"{subjekt} hatte {vorher} {dinge}. Dann hat {er} {partner.VonDativ} {aenderung} neue bekommen.",
            (_, Unbekannt.Vorher) =>
                $"{subjekt} hat {partner.Dativ} {aenderung} {dinge} geschenkt. Jetzt hat {er} noch {jetzt}.",
            (_, Unbekannt.Aenderung) =>
                $"{subjekt} hatte {vorher} {dinge}. Dann hat {er} {partner.Dativ} welche geschenkt. Jetzt hat {er} noch {jetzt}.",
            _ =>
                $"{subjekt} hatte {vorher} {dinge}. Dann hat {er} {partner.Dativ} {aenderung} geschenkt."
        };

        var optionen = Optionen(art, unbekannt, vorher, aenderung, jetzt, rng);
        var aenderungsFrage = art == Rechenart.Plus
            ? $"Wie viele {dinge} hat {wer.Nominativ} {partner.VonDativ} bekommen?"
            : $"Wie viele {dinge} hat {wer.Nominativ} {partner.Dativ} geschenkt?";

        var fragen = new (string Frage, int Antwort)[]
            {
                ($"Wie viele {dinge} hatte {wer.Nominativ} vorher?", vorher),
                (aenderungsFrage, aenderung),
                ($"Wie viele {dinge} hat {wer.Nominativ} jetzt?", jetzt)
            }
            .OrderBy(_ => rng.Next())
            .Select(f => new WFrageAufgabe(text, FrageRolle.WieViele, f.Frage,
                optionen.OrderBy(_ => rng.Next()).ToList(), f.Antwort.ToString()))
            .ToList();

        return new WFrageSerie(text, fragen);
    }

    /// <summary>
    ///     The story's three numbers plus what the wrong operation on the two given ones yields
    ///     (7 + 3 where the text needs 7 - 3) - or, where that is no new positive number, any other one.
    /// </summary>
    private static List<WFrageOption> Optionen(Rechenart art, Unbekannt unbekannt, int vorher, int aenderung,
        int jetzt, Random rng)
    {
        var zahlen = new HashSet<int> { vorher, aenderung, jetzt };
        var falscheRechnung = (art, unbekannt) switch
        {
            (Rechenart.Plus, Unbekannt.Vorher) => jetzt + aenderung,
            (Rechenart.Plus, Unbekannt.Aenderung) => jetzt + vorher,
            (Rechenart.Plus, _) => vorher - aenderung,
            (_, Unbekannt.Vorher) => jetzt - aenderung,
            (_, Unbekannt.Aenderung) => vorher + jetzt,
            _ => vorher + aenderung
        };

        if (falscheRechnung < 1 || zahlen.Contains(falscheRechnung))
        {
            var andere = Enumerable.Range(1, Maximum + 1).Where(z => !zahlen.Contains(z)).ToList();
            falscheRechnung = andere[rng.Next(andere.Count)];
        }

        zahlen.Add(falscheRechnung);
        return zahlen.Select(z => new WFrageOption(z.ToString(), z.ToString())).ToList();
    }

    private static string Gross(string text) => char.ToUpperInvariant(text[0]) + text[1..];
}
