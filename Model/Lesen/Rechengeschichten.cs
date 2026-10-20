namespace Kidz2Learn.Model.Lesen;

public enum Rechenart
{
    Plus,
    Minus
}

/// <summary>The three numbers of a story.</summary>
public enum Zahl
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
///     getting or giving away things. Of its three numbers - before, the change, and now - two are in
///     the text. The one "Wie viele ...?" question mostly asks for the third, which has to be
///     calculated, sometimes for one in the text (<see cref="GenerateAblesen" />), so the question has
///     to be read either way. The options are all three numbers plus the result of the wrong
///     operation, so neither copying a number from the text nor the other arithmetic sign gets
///     through. One question per story, since calculating one is already hard work.
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

    /// <summary>Level 2: the subject is repeated instead of a pronoun, and "jetzt" is asked - just calculating forward.</summary>
    public static WFrageSerie GenerateRechnenLeicht(Rechenart art, Random rng) =>
        Generate(art, Zahl.Jetzt, Zahl.Jetzt, mitPronomen: false, rng);

    /// <summary>Level 3: any number can be left out and is asked, with a pronoun in the story.</summary>
    public static WFrageSerie GenerateRechnen(Rechenart art, Random rng)
    {
        var fehlt = (Zahl)rng.Next(3);
        return Generate(art, fehlt, fehlt, mitPronomen: true, rng);
    }

    /// <summary>
    ///     The same stories as <see cref="GenerateRechnen" />/<see cref="GenerateRechnenLeicht" />, but
    ///     asking for a number that is in the text - so a story can't be answered by calculating
    ///     without reading what the question wants.
    /// </summary>
    public static WFrageSerie GenerateAblesen(Random rng)
    {
        var art = (Rechenart)rng.Next(2);
        var mitPronomen = rng.Next(2) == 0;
        var fehlt = mitPronomen ? (Zahl)rng.Next(3) : Zahl.Jetzt;
        var imText = Enum.GetValues<Zahl>().Where(z => z != fehlt).ToList();
        return Generate(art, fehlt, imText[rng.Next(imText.Count)], mitPronomen, rng);
    }

    private static WFrageSerie Generate(Rechenart art, Zahl fehlt, Zahl gefragt, bool mitPronomen, Random rng)
    {
        var figuren = Figuren.OrderBy(_ => rng.Next()).Take(2).ToList();
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

        return Build(art, fehlt, gefragt, mitPronomen, figuren[0], figuren[1], dinge, vorher, aenderung, rng);
    }

    /// <param name="fehlt">The number the story leaves out.</param>
    /// <param name="gefragt">The number the question asks for: <paramref name="fehlt" /> to calculate it, any other to read it off.</param>
    /// <param name="mitPronomen">"Jetzt hat er 7." instead of "Jetzt hat der Igel 7."</param>
    /// <param name="aenderung">How many things <paramref name="wer" /> gets (<see cref="Rechenart.Plus" />) or gives away.</param>
    public static WFrageSerie Build(Rechenart art, Zahl fehlt, Zahl gefragt, bool mitPronomen, Figur wer,
        Figur partner, string dinge, int vorher, int aenderung, Random rng)
    {
        var jetzt = art == Rechenart.Plus ? vorher + aenderung : vorher - aenderung;
        var subjekt = Gross(wer.Nominativ);
        var er = mitPronomen ? wer.Pronomen : wer.Nominativ;

        var text = (art, fehlt) switch
        {
            (Rechenart.Plus, Zahl.Vorher) =>
                $"{subjekt} hat {partner.VonDativ} {aenderung} neue {dinge} bekommen. Jetzt hat {er} {jetzt}.",
            (Rechenart.Plus, Zahl.Aenderung) =>
                $"{subjekt} hatte {vorher} {dinge}. Dann hat {er} {partner.VonDativ} neue bekommen. Jetzt hat {er} {jetzt}.",
            (Rechenart.Plus, _) =>
                $"{subjekt} hatte {vorher} {dinge}. Dann hat {er} {partner.VonDativ} {aenderung} neue bekommen.",
            (_, Zahl.Vorher) =>
                $"{subjekt} hat {partner.Dativ} {aenderung} {dinge} geschenkt. Jetzt hat {er} noch {jetzt}.",
            (_, Zahl.Aenderung) =>
                $"{subjekt} hatte {vorher} {dinge}. Dann hat {er} {partner.Dativ} welche geschenkt. Jetzt hat {er} noch {jetzt}.",
            _ =>
                $"{subjekt} hatte {vorher} {dinge}. Dann hat {er} {partner.Dativ} {aenderung} geschenkt."
        };

        var optionen = Optionen(art, fehlt, vorher, aenderung, jetzt, rng).OrderBy(_ => rng.Next()).ToList();
        var (frage, antwort) = gefragt switch
        {
            Zahl.Vorher => ($"Wie viele {dinge} hatte {wer.Nominativ} vorher?", vorher),
            Zahl.Aenderung when art == Rechenart.Plus =>
                ($"Wie viele {dinge} hat {wer.Nominativ} {partner.VonDativ} bekommen?", aenderung),
            Zahl.Aenderung => ($"Wie viele {dinge} hat {wer.Nominativ} {partner.Dativ} geschenkt?", aenderung),
            _ => ($"Wie viele {dinge} hat {wer.Nominativ} jetzt?", jetzt)
        };

        return new WFrageSerie(text,
            [new WFrageAufgabe(text, FrageRolle.WieViele, frage, optionen, antwort.ToString())]);
    }

    /// <summary>
    ///     The story's three numbers plus what the wrong operation on the two given ones yields
    ///     (7 + 3 where the text needs 7 - 3) - or, where that is no new positive number, any other one.
    /// </summary>
    private static List<WFrageOption> Optionen(Rechenart art, Zahl fehlt, int vorher, int aenderung,
        int jetzt, Random rng)
    {
        var zahlen = new HashSet<int> { vorher, aenderung, jetzt };
        var falscheRechnung = (art, fehlt) switch
        {
            (Rechenart.Plus, Zahl.Vorher) => jetzt + aenderung,
            (Rechenart.Plus, Zahl.Aenderung) => jetzt + vorher,
            (Rechenart.Plus, _) => vorher - aenderung,
            (_, Zahl.Vorher) => jetzt - aenderung,
            (_, Zahl.Aenderung) => vorher + jetzt,
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
