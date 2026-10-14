namespace Kidz2Learn.Model.Lesen;

/// <summary>Payload of a "Quatsch-Detektor" task: one sentence, true or nonsense.</summary>
public sealed record QuatschSatz(string Text, bool Stimmt);

public enum Genus
{
    M,
    F,
    N
}

[Flags]
public enum Merkmal
{
    None = 0,
    Tier = 1 << 0,
    Mensch = 1 << 1,
    Essbar = 1 << 2,
    Ding = 1 << 3,
    Gebaeude = 1 << 4,
    Pflanze = 1 << 5,
    Fliegt = 1 << 6,
    VierBeine = 1 << 7,
    Federn = 1 << 8,
    Raeder = 1 << 9,
    AmBaum = 1 << 10,
    Bellt = 1 << 11,

    /// <summary>Makes sounds/voices on its own (radio, TV) - kept out of "can speak"/"barks" nonsense.</summary>
    Toent = 1 << 12
}

/// <summary>
///     A noun from <see cref="WordMeta.Data" /> (the child's known word list), tagged with the
///     crisp, kid-obvious facts the sentence templates check against.
/// </summary>
public sealed record Nomen(string Wort, Genus Genus, Merkmal Merkmale, int? Groesse = null)
{
    public string Nominativ => $"{Genus switch { Genus.M => "der", Genus.F => "die", _ => "das" }} {Wort}";
    public string Akkusativ => $"{Genus switch { Genus.M => "den", Genus.F => "die", _ => "das" }} {Wort}";

    public bool Ist(Merkmal m)
    {
        return (Merkmale & m) != 0;
    }
}

/// <summary>
///     A statement template with "{N}" (nominative) or "{A}" (accusative) as the noun slot.
///     <see cref="Wahr" /> and <see cref="Falsch" /> are deliberately not complements of each
///     other: nouns where the statement would be debatable for a child (can a duck be eaten? can a
///     radio speak?) are in neither set and so never appear with that template.
/// </summary>
public sealed record Aussage(string Positiv, string? Negativ, Func<Nomen, bool> Wahr, Func<Nomen, bool> Falsch);

public static class QuatschSaetze
{
    public static readonly IReadOnlyList<Nomen> Lexikon =
    [
        new("Hund", Genus.M, Merkmal.Tier | Merkmal.VierBeine | Merkmal.Bellt, 4),
        new("Maus", Genus.F, Merkmal.Tier | Merkmal.VierBeine, 2),
        new("Hase", Genus.M, Merkmal.Tier | Merkmal.VierBeine, 3),
        new("Esel", Genus.M, Merkmal.Tier | Merkmal.VierBeine, 5),
        new("Hamster", Genus.M, Merkmal.Tier | Merkmal.VierBeine, 2),
        new("Vogel", Genus.M, Merkmal.Tier | Merkmal.Fliegt | Merkmal.Federn, 3),
        new("Taube", Genus.F, Merkmal.Tier | Merkmal.Fliegt | Merkmal.Federn, 3),
        new("Ente", Genus.F, Merkmal.Tier | Merkmal.Fliegt | Merkmal.Federn, 3),
        new("Ameise", Genus.F, Merkmal.Tier, 1),
        new("Affe", Genus.M, Merkmal.Tier),

        new("Oma", Genus.F, Merkmal.Mensch),
        new("Tante", Genus.F, Merkmal.Mensch),
        new("Vater", Genus.M, Merkmal.Mensch),
        new("Frau", Genus.F, Merkmal.Mensch),
        new("Ritter", Genus.M, Merkmal.Mensch),
        new("Bauer", Genus.M, Merkmal.Mensch),
        new("Matrose", Genus.M, Merkmal.Mensch),

        new("Banane", Genus.F, Merkmal.Essbar | Merkmal.AmBaum, 3),
        new("Birne", Genus.F, Merkmal.Essbar | Merkmal.AmBaum, 3),
        new("Mandarine", Genus.F, Merkmal.Essbar | Merkmal.AmBaum),
        new("Nuss", Genus.F, Merkmal.Essbar | Merkmal.AmBaum, 1),
        new("Tomate", Genus.F, Merkmal.Essbar),
        new("Erdbeere", Genus.F, Merkmal.Essbar, 2),
        new("Brot", Genus.N, Merkmal.Essbar),
        new("Torte", Genus.F, Merkmal.Essbar),
        new("Butter", Genus.F, Merkmal.Essbar),
        new("Bonbon", Genus.N, Merkmal.Essbar, 1),
        new("Rosine", Genus.F, Merkmal.Essbar, 1),

        new("Auto", Genus.N, Merkmal.Ding | Merkmal.Raeder, 6),
        new("Lastauto", Genus.N, Merkmal.Ding | Merkmal.Raeder, 7),
        new("Roller", Genus.M, Merkmal.Ding | Merkmal.Raeder),
        new("Rakete", Genus.F, Merkmal.Ding | Merkmal.Fliegt),
        new("Hammer", Genus.M, Merkmal.Ding),
        new("Besen", Genus.M, Merkmal.Ding),
        new("Messer", Genus.N, Merkmal.Ding),
        new("Pinsel", Genus.M, Merkmal.Ding),
        new("Eimer", Genus.M, Merkmal.Ding),
        new("Kiste", Genus.F, Merkmal.Ding),
        new("Seife", Genus.F, Merkmal.Ding),
        new("Radio", Genus.N, Merkmal.Ding | Merkmal.Toent),
        new("Fernseher", Genus.M, Merkmal.Ding | Merkmal.Toent),

        new("Haus", Genus.N, Merkmal.Gebaeude, 8),
        new("Turm", Genus.M, Merkmal.Gebaeude, 9),
        new("Schule", Genus.F, Merkmal.Gebaeude, 8),
        new("Baum", Genus.M, Merkmal.Pflanze),
        new("Tanne", Genus.F, Merkmal.Pflanze)
    ];

    public static readonly IReadOnlyList<Aussage> Aussagen =
    [
        new("{N} ist ein Tier.", "{N} ist kein Tier.",
            n => n.Ist(Merkmal.Tier),
            n => n.Ist(Merkmal.Mensch | Merkmal.Essbar | Merkmal.Ding | Merkmal.Gebaeude | Merkmal.Pflanze)),
        new("{N} kann fliegen.", "{N} kann nicht fliegen.",
            n => n.Ist(Merkmal.Fliegt),
            // Ants: some do fly, a child can't be expected to know which.
            n => !n.Ist(Merkmal.Fliegt) && n.Wort != "Ameise"),
        new("Man kann {A} essen.", "Man kann {A} nicht essen.",
            n => n.Ist(Merkmal.Essbar),
            n => n.Ist(Merkmal.Ding | Merkmal.Gebaeude)),
        new("{N} hat vier Beine.", null,
            n => n.Ist(Merkmal.VierBeine),
            n => n.Ist(Merkmal.Federn | Merkmal.Mensch | Merkmal.Essbar | Merkmal.Ding)),
        new("{N} hat Federn.", "{N} hat keine Federn.",
            n => n.Ist(Merkmal.Federn),
            n => n.Ist(Merkmal.VierBeine | Merkmal.Mensch | Merkmal.Essbar | Merkmal.Ding)),
        new("{N} hat Räder.", "{N} hat keine Räder.",
            n => n.Ist(Merkmal.Raeder),
            n => n.Ist(Merkmal.Tier | Merkmal.Mensch | Merkmal.Essbar | Merkmal.Gebaeude | Merkmal.Pflanze)),
        new("{N} wächst auf einem Baum.", "{N} wächst nicht auf einem Baum.",
            n => n.Ist(Merkmal.AmBaum),
            n => n.Ist(Merkmal.Tier | Merkmal.Mensch | Merkmal.Ding)),
        new("{N} bellt.", "{N} bellt nicht.",
            n => n.Ist(Merkmal.Bellt),
            n => !n.Ist(Merkmal.Bellt | Merkmal.Toent)),
        new("{N} kann sprechen.", "{N} kann nicht sprechen.",
            n => n.Ist(Merkmal.Mensch),
            n => n.Ist(Merkmal.Ding | Merkmal.Essbar | Merkmal.Gebaeude | Merkmal.Pflanze) && !n.Ist(Merkmal.Toent))
    ];

    /// <summary>Short, mostly 3-6 word everyday-scene sentences.</summary>
    public static readonly IReadOnlyList<QuatschSatz> Leicht =
    [
        new("Im Meer ist Wasser.", true),
        new("Der Mond scheint in der Nacht.", true),
        new("Die Sonne ist hell.", true),
        new("Die Ente schwimmt auf dem See.", true),
        new("Die Torte ist süß.", true),
        new("Im Winter ist es kalt.", true),
        new("Das Eis ist kalt.", true),
        new("Der Vogel sitzt im Nest.", true),
        new("Mit dem Pinsel kann man malen.", true),
        new("Die Tante trinkt Milch.", true),
        new("Die Maus frisst eine Nuss.", true),
        new("Die Rakete fliegt zum Mond.", true),
        new("In der Schule lernt man lesen.", true),
        new("Der Matrose fährt auf dem Meer.", true),
        new("Im Sand kann man buddeln.", true),
        new("Mit Seife wäscht man die Hände.", true),

        new("Die Oma wohnt im Eimer.", false),
        new("Der Esel fährt Roller.", false),
        new("Im Meer schwimmt ein Fernseher.", false),
        new("Die Maus frisst den Turm.", false),
        new("Der Mond ist aus Butter.", false),
        new("Die Banane kann singen.", false),
        new("Der Vater badet in Milch.", false),
        new("Das Eis ist heiß.", false),
        new("Das Haus sitzt im Nest.", false),
        new("Der Baum geht in die Schule.", false),
        new("Der Hase schnarcht im Radio.", false),
        new("Die Sonne scheint in der Nacht.", false),
        new("Der Hund liest die Zeitung.", false),
        new("Die Nase ist am Fuß.", false),
        new("Der Matrose segelt über die Torte.", false),
        new("Die Tomate fährt Auto.", false)
    ];

    /// <summary>
    ///     Longer sentences where one small word flips the meaning (swapped roles, "hell"/"dunkel",
    ///     "nicht") - the precise-reading counterpart to <see cref="Leicht" />, where skimming for
    ///     a couple of nouns is enough.
    /// </summary>
    public static readonly IReadOnlyList<QuatschSatz> Schwer =
    [
        new("Die Tante trinkt am Morgen eine Tasse Milch.", true),
        new("Der Ritter reitet auf dem Esel ins Dorf.", true),
        new("Die Oma isst zum Frühstück ein Brot mit Butter.", true),
        new("Am Abend geht die Sonne unter und es wird dunkel.", true),
        new("Wenn es regnet, wird die Erde nass.", true),
        new("Weil der Hamster müde ist, schläft er ein.", true),
        new("Im Winter liegt oft Schnee auf dem Dach.", true),
        new("Der Vater putzt mit Seife und Wasser das Auto.", true),
        new("Die Tante malt mit dem Pinsel ein Bild.", true),
        new("Der Hund hat Angst und versteckt sich unter dem Bett.", true),
        new("Auf der Tanne wachsen keine Bananen.", true),
        new("Ein Fisch kann nicht auf einen Baum klettern.", true),

        new("Die Milch trinkt am Morgen eine Tasse Tante.", false),
        new("Der Esel reitet auf dem Ritter ins Dorf.", false),
        new("Die Nuss frisst eine kleine Maus.", false),
        new("Die Oma isst zum Frühstück einen Hammer mit Butter.", false),
        new("Am Abend geht die Sonne unter und es wird hell.", false),
        new("Wenn es regnet, wird die Erde trocken.", false),
        new("Weil die Rakete Hunger hat, isst sie eine Birne.", false),
        new("Im Sommer liegt oft Schnee auf dem Rasen.", false),
        new("Der Vater putzt mit Torte und Butter das Auto.", false),
        new("Die Tante malt mit dem Hamster ein Bild.", false),
        new("Der Hund hat Angst und versteckt sich unter einer Rosine.", false),
        new("Auf der Tanne wachsen viele Bananen.", false)
    ];

    /// <summary>Level 1: hand-written short sentences or a positive template, 50/50 true/nonsense.</summary>
    public static QuatschSatz GenerateLeicht(Random rng)
    {
        var stimmt = rng.Next(2) == 0;
        return rng.Next(2) == 0
            ? PickHandwritten(Leicht, stimmt, rng)
            : FromTemplate(stimmt, negated: false, rng);
    }

    /// <summary>Level 2: hand-written long sentences, negated templates or size comparisons.</summary>
    public static QuatschSatz GenerateSchwer(Random rng)
    {
        var stimmt = rng.Next(2) == 0;
        return rng.Next(3) switch
        {
            0 => PickHandwritten(Schwer, stimmt, rng),
            1 => FromTemplate(stimmt, negated: true, rng),
            _ => Comparison(stimmt, rng)
        };
    }

    private static QuatschSatz PickHandwritten(IReadOnlyList<QuatschSatz> pool, bool stimmt, Random rng)
    {
        var matching = pool.Where(s => s.Stimmt == stimmt).ToList();
        return matching[rng.Next(matching.Count)];
    }

    private static QuatschSatz FromTemplate(bool stimmt, bool negated, Random rng)
    {
        var candidates = negated ? Aussagen.Where(a => a.Negativ is not null).ToList() : Aussagen.ToList();
        var aussage = candidates[rng.Next(candidates.Count)];

        // A negated statement is true exactly for the nouns the positive one is false for.
        var positiveTruth = negated ? !stimmt : stimmt;
        var pool = Lexikon.Where(positiveTruth ? aussage.Wahr : aussage.Falsch).ToList();
        var nomen = pool[rng.Next(pool.Count)];

        var template = negated ? aussage.Negativ! : aussage.Positiv;
        var text = template
            .Replace("{N}", nomen.Nominativ)
            .Replace("{A}", nomen.Akkusativ);
        return new QuatschSatz(Capitalize(text), stimmt);
    }

    /// <summary>
    ///     "X ist größer/kleiner als Y", only between nouns at least two size classes apart so the
    ///     answer is never a judgement call (Hase vs. Ente).
    /// </summary>
    private static QuatschSatz Comparison(bool stimmt, Random rng)
    {
        var sized = Lexikon.Where(n => n.Groesse is not null).ToList();
        Nomen a, b;
        do
        {
            a = sized[rng.Next(sized.Count)];
            b = sized[rng.Next(sized.Count)];
        } while (Math.Abs(a.Groesse!.Value - b.Groesse!.Value) < 2);

        var groesser = rng.Next(2) == 0;
        var aIsBigger = a.Groesse > b.Groesse;
        // Swap the pair if needed so the sentence's truth matches the coin flip.
        if ((groesser == aIsBigger) != stimmt)
            (a, b) = (b, a);

        var text = $"{a.Nominativ} ist {(groesser ? "größer" : "kleiner")} als {b.Nominativ}.";
        return new QuatschSatz(Capitalize(text), stimmt);
    }

    private static string Capitalize(string text)
    {
        return char.ToUpperInvariant(text[0]) + text[1..];
    }
}
