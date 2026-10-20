namespace Kidz2Learn.Model.Lesen;

public enum FrageRolle
{
    Wer,
    Wen,
    Was,
    Wo,
    Womit,
    Wann
}

/// <summary>
///     One answerable part of a sentence: <see cref="Phrase" /> is how it appears in the text
///     ("im Sand"), <see cref="Antwort" /> is the bare noun ("Sand").
/// </summary>
public sealed record SatzTeil(FrageRolle Rolle, string Phrase, string Antwort);

/// <summary>
///     A sentence plus its answerable parts. <see cref="Verb" /> is the finite verb exactly as
///     questions use it ("Wo buddelt der Hund?"); the subject is the <see cref="FrageRolle.Wer" /> part.
/// </summary>
public sealed record WFrageRahmen(string Text, string Verb, params SatzTeil[] Teile)
{
    public SatzTeil Subjekt => Teile.Single(t => t.Rolle == FrageRolle.Wer);
}

public sealed record WFrageOption(string Label, string AudioWort);

/// <summary>
///     Payload of a "W-Fragen" task. Every option is a part of the same sentence, so finding a
///     matching keyword in the text is never enough - only reading the question word tells which
///     part is meant.
/// </summary>
public sealed record WFrageAufgabe(
    string Text,
    FrageRolle Rolle,
    string Frage,
    IReadOnlyList<WFrageOption> Optionen,
    string Richtig)
{
    public string Fragewort => Rolle.ToString();
}

/// <summary>
///     Payload of a "W-Fragen" task: several questions about the same sentence, each asking for a
///     different part, so the question word is the only thing that changes between them.
/// </summary>
public sealed record WFrageSerie(string Text, IReadOnlyList<WFrageAufgabe> Fragen);

public static class WFragen
{
    private static SatzTeil Wer(string phrase, string antwort) => new(FrageRolle.Wer, phrase, antwort);
    private static SatzTeil Wen(string phrase, string antwort) => new(FrageRolle.Wen, phrase, antwort);
    private static SatzTeil Was(string phrase, string antwort) => new(FrageRolle.Was, phrase, antwort);
    private static SatzTeil Wo(string phrase, string antwort) => new(FrageRolle.Wo, phrase, antwort);
    private static SatzTeil Womit(string phrase, string antwort) => new(FrageRolle.Womit, phrase, antwort);
    private static SatzTeil Wann(string phrase, string antwort) => new(FrageRolle.Wann, phrase, antwort);

    public static readonly IReadOnlyList<WFrageRahmen> Rahmen =
    [
        new("Der Hund buddelt im Sand ein Loch.", "buddelt",
            Wer("der Hund", "Hund"), Wo("im Sand", "Sand"), Was("ein Loch", "Loch")),
        new("Die Oma backt am Morgen eine Torte.", "backt",
            Wer("die Oma", "Oma"), Wann("am Morgen", "Morgen"), Was("eine Torte", "Torte")),
        new("Der Vater malt mit dem Pinsel ein Haus.", "malt",
            Wer("der Vater", "Vater"), Womit("mit dem Pinsel", "Pinsel"), Was("ein Haus", "Haus")),
        new("Die Maus versteckt im Turm eine Nuss.", "versteckt",
            Wer("die Maus", "Maus"), Wo("im Turm", "Turm"), Was("eine Nuss", "Nuss")),
        new("Der Affe isst im Baum eine Banane.", "isst",
            Wer("der Affe", "Affe"), Wo("im Baum", "Baum"), Was("eine Banane", "Banane")),
        new("Die Tante trinkt am Abend Milch.", "trinkt",
            Wer("die Tante", "Tante"), Wann("am Abend", "Abend"), Was("Milch", "Milch")),
        new("Der Ritter putzt mit dem Besen den Turm.", "putzt",
            Wer("der Ritter", "Ritter"), Womit("mit dem Besen", "Besen"), Was("den Turm", "Turm")),
        new("Die Ente schwimmt am Morgen auf dem See.", "schwimmt",
            Wer("die Ente", "Ente"), Wann("am Morgen", "Morgen"), Wo("auf dem See", "See")),
        new("Der Bauer fährt im Dorf Roller.", "fährt",
            Wer("der Bauer", "Bauer"), Wo("im Dorf", "Dorf"), Was("Roller", "Roller")),
        new("Der Matrose sieht in der Nacht den Mond.", "sieht",
            Wer("der Matrose", "Matrose"), Wann("in der Nacht", "Nacht"), Was("den Mond", "Mond")),
        new("Die Frau schneidet mit dem Messer die Tomate.", "schneidet",
            Wer("die Frau", "Frau"), Womit("mit dem Messer", "Messer"), Was("die Tomate", "Tomate")),
        new("Der Hase findet im Garten eine Birne.", "findet",
            Wer("der Hase", "Hase"), Wo("im Garten", "Garten"), Was("eine Birne", "Birne")),
        new("Der Esel trägt im Dorf eine Kiste.", "trägt",
            Wer("der Esel", "Esel"), Wo("im Dorf", "Dorf"), Was("eine Kiste", "Kiste")),
        new("Die Ameise trägt im Wald ein Blatt.", "trägt",
            Wer("die Ameise", "Ameise"), Wo("im Wald", "Wald"), Was("ein Blatt", "Blatt")),
        new("Der Vogel baut im Baum ein Nest.", "baut",
            Wer("der Vogel", "Vogel"), Wo("im Baum", "Baum"), Was("ein Nest", "Nest")),
        new("Die Frau fährt am Abend mit dem Auto.", "fährt",
            Wer("die Frau", "Frau"), Wann("am Abend", "Abend"), Womit("mit dem Auto", "Auto")),
        new("Der Arbeiter baut mit dem Hammer ein Haus.", "baut",
            Wer("der Arbeiter", "Arbeiter"), Womit("mit dem Hammer", "Hammer"), Was("ein Haus", "Haus")),
        new("Die Oma füttert am Morgen die Ente.", "füttert",
            Wer("die Oma", "Oma"), Wann("am Morgen", "Morgen"), Wen("die Ente", "Ente")),
        new("Der Hamster schläft am Tag in der Kiste.", "schläft",
            Wer("der Hamster", "Hamster"), Wann("am Tag", "Tag"), Wo("in der Kiste", "Kiste")),
        new("Die Tante hört im Auto Radio.", "hört",
            Wer("die Tante", "Tante"), Wo("im Auto", "Auto"), Was("Radio", "Radio")),
        new("Im Winter schläft der Igel im Laub.", "schläft",
            Wer("der Igel", "Igel"), Wann("im Winter", "Winter"), Wo("im Laub", "Laub")),
        new("Der Igel polstert sein Nest mit Laub.", "polstert",
            Wer("der Igel", "Igel"), Was("sein Nest", "Nest"), Womit("mit Laub", "Laub")),
        new("In der Nacht fängt der Igel eine Maus.", "fängt",
            Wer("der Igel", "Igel"), Wann("in der Nacht", "Nacht"), Was("eine Maus", "Maus")),

        // Four parts - only offered at level 2.
        new("Die Oma liest am Abend im Haus ein Buch.", "liest",
            Wer("die Oma", "Oma"), Wann("am Abend", "Abend"), Wo("im Haus", "Haus"), Was("ein Buch", "Buch")),
        new("Der Vater schlägt am Morgen mit dem Hammer einen Nagel.", "schlägt",
            Wer("der Vater", "Vater"), Wann("am Morgen", "Morgen"), Womit("mit dem Hammer", "Hammer"),
            Was("einen Nagel", "Nagel")),
        new("Die Tante wäscht am Abend mit Seife den Hund.", "wäscht",
            Wer("die Tante", "Tante"), Wann("am Abend", "Abend"), Womit("mit Seife", "Seife"),
            Wen("den Hund", "Hund")),
        new("Der Hund jagt am Morgen im Garten die Maus.", "jagt",
            Wer("der Hund", "Hund"), Wann("am Morgen", "Morgen"), Wo("im Garten", "Garten"),
            Wen("die Maus", "Maus")),
        new("Die Maus sucht in der Nacht im Haus eine Rosine.", "sucht",
            Wer("die Maus", "Maus"), Wann("in der Nacht", "Nacht"), Wo("im Haus", "Haus"),
            Was("eine Rosine", "Rosine")),
        new("Der Vater isst am Abend mit der Gabel eine Tomate.", "isst",
            Wer("der Vater", "Vater"), Wann("am Abend", "Abend"), Womit("mit der Gabel", "Gabel"),
            Was("eine Tomate", "Tomate")),
        new("Der Ritter putzt am Morgen mit dem Tuch sein Schwert.", "putzt",
            Wer("der Ritter", "Ritter"), Wann("am Morgen", "Morgen"), Womit("mit dem Tuch", "Tuch"),
            Was("sein Schwert", "Schwert"))
    ];

    /// <summary>Level 1: three-part sentences, two questions, options are the sentence phrases ("im Sand").</summary>
    public static WFrageSerie GenerateLeicht(Random rng)
    {
        var pool = Rahmen.Where(r => r.Teile.Length <= 3).ToList();
        return BuildSerie(pool[rng.Next(pool.Count)], usePhrases: true, fragen: 2, rng);
    }

    /// <summary>
    ///     Level 2: any sentence, one question less than it has parts, options are bare nouns
    ///     ("Sand") - the preposition no longer hints at the question word, the child has to locate
    ///     the part in the sentence.
    /// </summary>
    public static WFrageSerie GenerateSchwer(Random rng)
    {
        var rahmen = Rahmen[rng.Next(Rahmen.Count)];
        return BuildSerie(rahmen, usePhrases: false, fragen: rahmen.Teile.Length - 1, rng);
    }

    /// <summary>Asks for <paramref name="fragen" /> different parts of the sentence, in random order.</summary>
    public static WFrageSerie BuildSerie(WFrageRahmen rahmen, bool usePhrases, int fragen, Random rng)
    {
        var gefragt = rahmen.Teile.OrderBy(_ => rng.Next()).Take(fragen);
        return new WFrageSerie(rahmen.Text, gefragt.Select(teil => Build(rahmen, teil, usePhrases, rng)).ToList());
    }

    public static WFrageAufgabe Build(WFrageRahmen rahmen, SatzTeil gefragt, bool usePhrases, Random rng)
    {
        var optionen = rahmen.Teile
            .Select(t => new WFrageOption(usePhrases ? t.Phrase : t.Antwort, t.Antwort))
            .OrderBy(_ => rng.Next())
            .ToList();

        return new WFrageAufgabe(
            rahmen.Text,
            gefragt.Rolle,
            Frage(rahmen, gefragt),
            optionen,
            usePhrases ? gefragt.Phrase : gefragt.Antwort);
    }

    /// <summary>
    ///     Builds the question with the sentence's object kept in ("Wann fängt der Igel eine Maus?",
    ///     "Wer wäscht den Hund?") - transitive verbs read wrong without it, and the Wer form puts
    ///     subject and object side by side on purpose (who does it vs. to whom).
    /// </summary>
    public static string Frage(WFrageRahmen rahmen, SatzTeil gefragt)
    {
        var objekt = rahmen.Teile.FirstOrDefault(t => t.Rolle is FrageRolle.Was or FrageRolle.Wen);

        if (gefragt.Rolle == FrageRolle.Wer)
        {
            var rest = objekt ?? rahmen.Teile.FirstOrDefault(t => t.Rolle == FrageRolle.Wo);
            return rest is null ? $"Wer {rahmen.Verb}?" : $"Wer {rahmen.Verb} {rest.Phrase}?";
        }

        var frage = $"{gefragt.Rolle} {rahmen.Verb} {rahmen.Subjekt.Phrase}";
        if (objekt is not null && objekt != gefragt)
            frage += $" {objekt.Phrase}";
        return frage + "?";
    }
}
