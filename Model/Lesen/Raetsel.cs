namespace Kidz2Learn.Model.Lesen;

/// <summary>
///     Coarse kind of thing a riddle is about. Level 1 draws its wrong options from other
///     <see cref="RaetselBereich" />s, so the first clue (which always names the kind: "Ich bin ein
///     Tier.") already rules them out.
/// </summary>
public enum RaetselBereich
{
    Tier,
    Essen,
    Fahrzeug,
    Werkzeug,
    Himmel,
    Natur,
    Fantasie,
    Mensch,
    Gebaeude
}

/// <summary>
///     One "Wer bin ich?" riddle. <see cref="Hinweise" /> go from general to specific: the first
///     names the <see cref="Bereich" /> and also fits both <see cref="Aehnliche" />, the last one
///     alone tells the solution apart from them.
/// </summary>
public sealed record Raetsel(string Loesung, RaetselBereich Bereich, string[] Hinweise, string[] Aehnliche);

/// <summary>Payload of a "Wer bin ich?" task.</summary>
public sealed record RaetselAufgabe(IReadOnlyList<string> Hinweise, IReadOnlyList<string> Optionen, string Richtig)
{
    /// <summary>The clue highlighted after a wrong pick - the most specific one, which always decides.</summary>
    public string Entscheidend => Hinweise[^1];
}

public static class RaetselSammlung
{
    private static Raetsel R(string loesung, RaetselBereich bereich, string[] hinweise, params string[] aehnliche)
    {
        return new Raetsel(loesung, bereich, hinweise, aehnliche);
    }

    public static readonly IReadOnlyList<Raetsel> Alle =
    [
        R("Hund", RaetselBereich.Tier, ["Ich bin ein Tier.", "Ich habe vier Beine.", "Ich belle laut."],
            "Hase", "Esel"),
        R("Ente", RaetselBereich.Tier, ["Ich bin ein Tier.", "Ich habe Federn.", "Ich schwimme auf dem See und mache Quak."],
            "Taube", "Huhn"),
        R("Maus", RaetselBereich.Tier, ["Ich bin ein Tier.", "Ich bin klein und habe einen langen Schwanz.", "Ich fresse gern Käse."],
            "Hamster", "Hase"),
        R("Esel", RaetselBereich.Tier, ["Ich bin ein Tier.", "Ich trage schwere Säcke.", "Ich rufe I-A!"],
            "Pferd", "Kuh"),
        R("Affe", RaetselBereich.Tier, ["Ich bin ein Tier.", "Ich klettere auf Bäume.", "Ich esse gern Bananen."],
            "Bär", "Katze"),
        R("Ameise", RaetselBereich.Tier, ["Ich bin ein Tier.", "Ich bin winzig klein.", "Ich trage ein Blatt, das größer ist als ich."],
            "Maus", "Biene"),
        R("Hase", RaetselBereich.Tier, ["Ich bin ein Tier.", "Ich habe lange Ohren.", "Ich hoppele über die Wiese."],
            "Esel", "Maus"),
        R("Fisch", RaetselBereich.Tier, ["Ich bin ein Tier.", "Ich lebe im Wasser.", "Ich habe keine Beine, aber Flossen."],
            "Ente", "Frosch"),
        R("Biene", RaetselBereich.Tier, ["Ich bin ein Tier.", "Ich kann fliegen.", "Ich mache Honig."],
            "Taube", "Ente"),
        R("Kuh", RaetselBereich.Tier, ["Ich bin ein Tier.", "Ich lebe auf dem Bauernhof.", "Ich gebe Milch und mache Muh."],
            "Esel", "Schwein"),
        R("Igel", RaetselBereich.Tier, ["Ich bin ein Tier.", "Im Winter schlafe ich.", "Ich habe Stacheln."],
            "Bär", "Hamster"),
        R("Dino", RaetselBereich.Tier, ["Ich bin ein Tier.", "Ich bin riesig groß.", "Ich lebte vor langer Zeit, heute gibt es nur noch meine Knochen."],
            "Elefant", "Wal"),

        R("Banane", RaetselBereich.Essen, ["Man kann mich essen.", "Ich bin gelb.", "Ich bin lang und krumm."],
            "Zitrone", "Birne"),
        R("Eis", RaetselBereich.Essen, ["Man kann mich essen.", "Ich bin süß.", "Ich bin kalt und schmelze in der Sonne."],
            "Torte", "Bonbon"),
        R("Erdbeere", RaetselBereich.Essen, ["Man kann mich essen.", "Ich bin rot.", "Ich habe viele kleine Kerne außen."],
            "Tomate", "Kirsche"),
        R("Nuss", RaetselBereich.Essen, ["Man kann mich essen.", "Ich wachse an einem Baum.", "Ich habe eine harte Schale."],
            "Birne", "Mandarine"),
        R("Brot", RaetselBereich.Essen, ["Man kann mich essen.", "Der Bäcker backt mich.", "Man schmiert Butter auf mich."],
            "Torte", "Brezel"),
        R("Tomate", RaetselBereich.Essen, ["Man kann mich essen.", "Ich bin rot und rund.", "Aus mir macht man Ketchup."],
            "Apfel", "Kirsche"),
        R("Milch", RaetselBereich.Essen, ["Man kann mich trinken.", "Ich bin weiß.", "Ich komme von der Kuh."],
            "Saft", "Wasser"),

        R("Rakete", RaetselBereich.Fahrzeug, ["Ich bin ein Fahrzeug.", "Ich fliege sehr hoch.", "Ich fliege bis zum Mond."],
            "Flugzeug", "Hubschrauber"),
        R("Roller", RaetselBereich.Fahrzeug, ["Ich bin ein Fahrzeug.", "Ich habe zwei kleine Räder.", "Man tritt mich mit einem Fuß an."],
            "Fahrrad", "Auto"),
        R("Schiff", RaetselBereich.Fahrzeug, ["Ich bin ein Fahrzeug.", "Ich fahre auf dem Wasser.", "Der Matrose arbeitet auf mir."],
            "Auto", "Zug"),
        R("Lastauto", RaetselBereich.Fahrzeug, ["Ich bin ein Fahrzeug.", "Ich fahre auf der Straße.", "Ich bin groß und bringe schwere Kisten weg."],
            "Fahrrad", "Roller"),

        R("Hammer", RaetselBereich.Werkzeug, ["Ich bin ein Werkzeug.", "Ich bin schwer.", "Ich schlage Nägel in die Wand."],
            "Säge", "Zange"),
        R("Besen", RaetselBereich.Werkzeug, ["Ich bin ein Werkzeug.", "Ich habe einen langen Stiel.", "Ich fege den Boden."],
            "Schaufel", "Pinsel"),
        R("Pinsel", RaetselBereich.Werkzeug, ["Ich bin ein Werkzeug.", "Ich habe einen Stiel und weiche Haare.", "Mit mir malt man mit Farbe."],
            "Besen", "Kamm"),
        R("Schere", RaetselBereich.Werkzeug, ["Ich bin ein Werkzeug.", "Ich bin scharf.", "Ich habe zwei Klingen und schneide Papier."],
            "Messer", "Säge"),

        R("Mond", RaetselBereich.Himmel, ["Ich bin oben am Himmel.", "Man sieht mich in der Nacht.", "Mal bin ich rund, mal krumm."],
            "Stern", "Wolke"),
        R("Sonne", RaetselBereich.Himmel, ["Ich bin oben am Himmel.", "Ich bin heiß und hell.", "Wenn ich scheine, ist es Tag."],
            "Mond", "Stern"),
        R("Wolke", RaetselBereich.Himmel, ["Ich bin oben am Himmel.", "Ich bin weiß und weich.", "Aus mir fällt Regen."],
            "Mond", "Sonne"),

        R("Vulkan", RaetselBereich.Natur, ["Ich bin sehr groß und hoch.", "In mir ist es heiß.", "Manchmal spucke ich Feuer."],
            "Berg", "Turm"),
        R("See", RaetselBereich.Natur, ["Ich bin draußen in der Natur.", "Ich bin aus Wasser.", "Die Ente schwimmt auf mir."],
            "Wald", "Wiese"),

        R("Geist", RaetselBereich.Fantasie, ["Mich gibt es nur in Geschichten.", "Man kann durch mich durchsehen.", "Ich rufe Huuu!"],
            "Drache", "Einhorn"),
        R("Einhorn", RaetselBereich.Fantasie, ["Mich gibt es nur in Geschichten.", "Ich sehe aus wie ein Pferd.", "Ich habe ein Horn auf der Stirn."],
            "Drache", "Geist"),
        R("Drache", RaetselBereich.Fantasie, ["Mich gibt es nur in Geschichten.", "Ich habe große Flügel.", "Ich spucke Feuer."],
            "Einhorn", "Geist"),

        R("Ritter", RaetselBereich.Mensch, ["Ich bin ein Mensch.", "Ich wohne in einer Burg.", "Ich trage ein Schwert und eine Rüstung."],
            "König", "Matrose"),
        R("Matrose", RaetselBereich.Mensch, ["Ich bin ein Mensch.", "Ich bin oft auf dem Meer.", "Ich arbeite auf einem Schiff."],
            "Bauer", "Ritter"),
        R("Bauer", RaetselBereich.Mensch, ["Ich bin ein Mensch.", "Ich fahre einen Traktor.", "Ich füttere die Kühe und Schweine."],
            "Ritter", "Bäcker"),
        R("Oma", RaetselBereich.Mensch, ["Ich bin ein Mensch.", "Ich bin schon alt.", "Ich bin die Mama von Mama oder Papa."],
            "Tante", "Opa"),

        R("Schule", RaetselBereich.Gebaeude, ["Ich bin ein großes Haus.", "Viele Kinder kommen jeden Morgen zu mir.", "In mir lernen Kinder lesen und rechnen."],
            "Turm", "Burg"),
        R("Burg", RaetselBereich.Gebaeude, ["Ich bin ein großes Haus.", "Ich habe dicke Mauern und Türme.", "In mir wohnen Ritter."],
            "Schule", "Haus")
    ];

    /// <summary>
    ///     Pairs that must never meet as solution and level-1 wrong option even though their
    ///     <see cref="RaetselBereich" />s differ, because a clue of one fits the other too (both
    ///     spit fire, both fly up into the sky, ...). Checked in both directions.
    /// </summary>
    public static readonly IReadOnlyList<(string A, string B)> Unpassend =
    [
        ("Vulkan", "Drache"),
        ("Vulkan", "Rakete"),
        ("Dino", "Drache"),
        ("Einhorn", "Esel"),
        ("Mond", "Rakete"),
        ("Sonne", "Rakete"),
        ("Wolke", "Rakete"),
        ("Mond", "Biene"),
        ("Sonne", "Biene"),
        ("Wolke", "Biene"),
        ("Mond", "Ente"),
        ("Sonne", "Ente"),
        ("Wolke", "Ente"),
        ("See", "Fisch"),
        ("See", "Ente"),
        ("See", "Schiff"),
        ("Milch", "Kuh"),
        ("Matrose", "Schiff"),
        ("Ritter", "Burg"),
        ("Bauer", "Kuh"),
        ("Affe", "Banane"),
        ("Eis", "Sonne")
    ];

    private static bool Passt(Raetsel loesung, Raetsel ablenker)
    {
        return ablenker.Bereich != loesung.Bereich &&
               !Unpassend.Any(p => (p.A == loesung.Loesung && p.B == ablenker.Loesung) ||
                                   (p.B == loesung.Loesung && p.A == ablenker.Loesung));
    }

    /// <summary>
    ///     Level 1: wrong options are solutions of riddles from two other kinds, so any single clue -
    ///     at the latest the first one - is enough.
    /// </summary>
    public static RaetselAufgabe GenerateLeicht(Random rng)
    {
        var raetsel = Alle[rng.Next(Alle.Count)];
        var ablenker = Alle
            .Where(a => Passt(raetsel, a))
            .OrderBy(_ => rng.Next())
            .DistinctBy(a => a.Bereich)
            .Take(2)
            .Select(a => a.Loesung);
        return Build(raetsel, ablenker, rng);
    }

    /// <summary>
    ///     Level 2: wrong options are things of the same kind that the first clue fits as well -
    ///     the child has to read on to the clue that decides.
    /// </summary>
    public static RaetselAufgabe GenerateSchwer(Random rng)
    {
        var raetsel = Alle[rng.Next(Alle.Count)];
        return Build(raetsel, raetsel.Aehnliche, rng);
    }

    public static RaetselAufgabe Build(Raetsel raetsel, IEnumerable<string> ablenker, Random rng)
    {
        var optionen = ablenker.Append(raetsel.Loesung).OrderBy(_ => rng.Next()).ToList();
        return new RaetselAufgabe(raetsel.Hinweise, optionen, raetsel.Loesung);
    }
}
