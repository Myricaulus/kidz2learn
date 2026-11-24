using Kidz2Learn.Model.Lesen;
using Xunit;

namespace Kidz2Learn.Tests;

public class AbcSortierenTests
{
    private static readonly int[] Stufen = [1, 2, 3];
    private static readonly float[] Masteries = [0f, 0.4f, 0.6f, 0.8f, 1f];

    private static IEnumerable<AbcAufgabe> Viele(int stufe, float mastery, int seed = 4711, int anzahl = 100)
    {
        var rng = new Random(seed);
        for (var i = 0; i < anzahl; i++)
            yield return AbcSortieren.Generate(stufe, mastery, rng);
    }

    [Theory]
    [InlineData(1, 0f, 4)]
    [InlineData(2, 0f, 4)]
    [InlineData(3, 0f, 5)]
    [InlineData(1, 0.4f, 5)]
    [InlineData(2, 0.6f, 6)]
    [InlineData(3, 0.8f, 8)]
    [InlineData(3, 0.39f, 5)]
    public void WordCount_ByLevelAndMastery(int stufe, float mastery, int erwartet)
    {
        Assert.All(Viele(stufe, mastery, anzahl: 30), a => Assert.Equal(erwartet, a.Woerter.Count));
    }

    [Fact]
    public void EveryTask_HasDistinctWords_ShownUnsorted()
    {
        foreach (var stufe in Stufen)
        foreach (var mastery in Masteries)
        foreach (var a in Viele(stufe, mastery))
        {
            Assert.Equal(a.Woerter.Count, a.Woerter.Select(AbcSortieren.Schluessel).Distinct().Count());
            Assert.False(a.IstRichtig(a.Woerter), string.Join(" ", a.Woerter));
            Assert.True(a.IstRichtig(a.Richtig));
        }
    }

    [Fact]
    public void Level1_HasOnlyDifferentFirstLetters()
    {
        foreach (var mastery in Masteries)
        foreach (var a in Viele(1, mastery))
            Assert.Equal(a.Woerter.Count, a.Woerter.Select(w => AbcSortieren.Schluessel(w)[0]).Distinct().Count());
    }

    [Fact]
    public void Level2_HasAtLeastTwoWordsWithTheSameFirstLetter()
    {
        foreach (var mastery in Masteries)
        foreach (var a in Viele(2, mastery))
            Assert.True(a.Woerter.GroupBy(w => AbcSortieren.Schluessel(w)[0]).Any(g => g.Count() >= 2),
                string.Join(" ", a.Woerter));
    }

    [Fact]
    public void Level2_WithoutMastery_IsOtherwiseRandom_SoSometimesAllButTwoDiffer()
    {
        var aufgaben = Viele(2, 0f).ToList();
        Assert.Contains(aufgaben, a => a.Woerter.Select(w => AbcSortieren.Schluessel(w)[0]).Distinct().Count() == 3);
    }

    [Fact]
    public void Words_GetMoreAlike_WithLevelAndMastery()
    {
        double Mittel(int stufe, float mastery) => Viele(stufe, mastery).Average(a => a.MittlererAbstand);

        Assert.True(Mittel(3, 0f) < Mittel(2, 0f), $"{Mittel(3, 0f)} vs {Mittel(2, 0f)}");
        Assert.True(Mittel(3, 0.8f) < Mittel(3, 0f), $"{Mittel(3, 0.8f)} vs {Mittel(3, 0f)}");
        Assert.True(Mittel(1, 0.8f) < Mittel(1, 0f), $"{Mittel(1, 0.8f)} vs {Mittel(1, 0f)}");
    }

    [Theory]
    [InlineData("Zebra", "affe", "affe")]
    [InlineData("Ärger", "Arm", "Ärger")]
    [InlineData("Ofen", "Öl", "Ofen")]
    public void Order_IgnoresCase_AndSortsUmlautsLikeTheirVowel(string a, string b, string erstes)
    {
        Assert.Equal(erstes, AbcSortieren.Sortiert([a, b])[0]);
    }

    [Theory]
    [InlineData("Hund", "Haus", 1)]
    [InlineData("Mond", "Motor", 2)]
    [InlineData("Ei", "Eis", 2)]
    [InlineData("Arm", "Bein", 0)]
    public void Entscheidung_IsTheFirstDifferingLetter(string a, string b, int stelle)
    {
        Assert.Equal(stelle, AbcSortieren.Entscheidung(a, b));
    }

    [Fact]
    public void Markierung_MarksTheDecidingLetterAgainstEachNeighbour()
    {
        var a = new AbcAufgabe(3, 0, 1, ["Motor", "Mond", "Messer", "Ei", "Eis"]);
        Assert.Equal(["Ei", "Eis", "Messer", "Mond", "Motor"], a.Richtig);

        Assert.Empty(a.Markierung("Ei"));                    // prefix of "Eis": nothing of its own
        Assert.Equal([0, 2], a.Markierung("Eis").Order());   // "s" against Ei, "E" against Messer
        Assert.Equal([2], a.Markierung("Motor").Order());    // "t" against Mond
        Assert.Equal([1, 2], a.Markierung("Mond").Order());  // "o" against Messer, "n" against Motor
    }

    [Fact]
    public void Beobachtung_RecordsOrderDecisionsAndMisplacedWords()
    {
        var a = new AbcAufgabe(2, 1, 1, ["Hund", "Haus", "Arm", "Zebra", "Bein"]);
        var antwort = new[] { "Arm", "Bein", "Hund", "Haus", "Zebra" };

        var d = AbcSortieren.Beobachtung(a, antwort, 3, 1200);

        Assert.Equal("Arm < Bein < Haus < Hund < Zebra", d.Ziel);
        Assert.Equal("Arm < Bein < Hund < Haus < Zebra", d.Antwort);
        Assert.Equal(a.Woerter, d.Optionen);
        Assert.Equal("1,1,2,1", d.Werte!["entscheidung"]);
        Assert.Equal("2", d.Werte["falsch_platziert"]);
        Assert.Equal("3", d.Werte["zuege"]);
        Assert.Equal("1200", d.Werte["erster_zug_ms"]);
        Assert.Equal(["richtig", "richtig", "falsch", "falsch", "richtig"], d.Items!.Select(i => i.Ergebnis));
    }

    [Fact]
    public void Punkte_HalfOnTheSecondTry()
    {
        Assert.Equal(8, AbcSortieren.Punkte(4, 1));
        Assert.Equal(4, AbcSortieren.Punkte(4, 2));
    }

    [Fact]
    public void Pool_IsLargeAndClean()
    {
        Assert.True(AbcSortieren.Pool.Count > 3000, AbcSortieren.Pool.Count.ToString());
        Assert.DoesNotContain(AbcSortieren.Pool, w => w.Contains('ß') || "ÄÖÜäöü".Contains(w[0]) || w.Length > AbcSortieren.MaxBuchstaben);
    }
}
