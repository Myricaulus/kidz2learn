using Kidz2Learn.Entities;
using Kidz2Learn.Model;
using Kidz2Learn.Model.Merkmale;
using Xunit;

namespace Kidz2Learn.Tests;

/// <summary>Made-up children with one known trait each; the profile has to find it.</summary>
public class MerkmalRechnerTests
{
    private static readonly DateTime Start = new(2026, 10, 1, 19, 0, 0);

    private static IReadOnlyList<MerkmalErgebnis> Profil(IEnumerable<Beobachtung> beobachtungen) =>
        MerkmalRechner.Berechne(beobachtungen, Start.AddDays(-1), Start.AddDays(60));

    private static MerkmalErgebnis Merkmal(IReadOnlyList<MerkmalErgebnis> profil, string id) =>
        profil.Single(m => m.Id == id);

    private static Beobachtung Silben(int i, string ziel, string antwort, params string[] falsche) => new()
    {
        Zeit = Start.AddDays(i / 20).AddMinutes(i % 20),
        View = "silben-multiple-choice",
        Skills = [Skill.ReadPrecise],
        Versuch = 1,
        Richtig = antwort == ziel,
        DauerMs = 3000,
        Ziel = ziel,
        Antwort = antwort,
        Optionen = [ziel, .. falsche]
    };

    [Fact]
    public void NoObservations_EveryFeatureHasNoData()
    {
        Assert.All(Profil([]), m => Assert.Equal(MerkmalStatus.KeineDaten, m.Status));
    }

    [Fact]
    public void ChildPickingMirroredLetters_ShowsSpiegeln()
    {
        // Offered b/d mirror and a vowel distractor every time; picks the mirrored one in 2 of 3.
        var beobachtungen = Enumerable.Range(0, 30)
            .Select(i => Silben(i, "Bein", i % 3 == 0 ? "Bein" : "dein", "dein", "Bain"));

        var m3 = Merkmal(Profil(beobachtungen), "M3");

        Assert.Equal(MerkmalStatus.Dafuer, m3.Status);
        Assert.True(m3.Wert > 0.5);
    }

    [Fact]
    public void MirrorErrorsHereButCleanMirrorGrids_IsContradictory()
    {
        var silben = Enumerable.Range(0, 30)
            .Select(i => Silben(i, "Bein", i % 3 == 0 ? "Bein" : "dein", "dein", "Bain"));
        var gitter = Enumerable.Range(0, 10).Select(i => new Beobachtung
        {
            Zeit = Start.AddDays(i).AddHours(1),
            View = "such-raster",
            Skills = [Skill.ReadMirrorLetters],
            Versuch = 1,
            Richtig = true,
            Werte = new Dictionary<string, string> { ["felder"] = "25" },
            Items = []
        });

        Assert.Equal(MerkmalStatus.Widerspruechlich, Merkmal(Profil([.. silben, .. gitter]), "M3").Status);
    }

    [Fact]
    public void ChildNeverFallingForMirrors_SpeaksAgainstSpiegeln()
    {
        // Picks a wrong option now and then, but never the mirrored one, over many tasks.
        var beobachtungen = Enumerable.Range(0, 300)
            .Select(i => Silben(i, "Bein", i % 4 == 0 ? "Bain" : "Bein", "dein", "Bain"));

        Assert.Equal(MerkmalStatus.Dagegen, Merkmal(Profil(beobachtungen), "M3").Status);
    }

    [Fact]
    public void SubtractingOnesTheWrongWayRound_ShowsKleinerVomGroesseren()
    {
        var beobachtungen = Enumerable.Range(0, 12).Select(i => new Beobachtung
        {
            Zeit = Start.AddMinutes(i),
            View = "arith-numpad",
            Skills = [Skill.Math.Sub20],
            Versuch = 1,
            Richtig = i % 2 == 0,
            Werte = new Dictionary<string, string>
            {
                ["a"] = "12", ["b"] = "3", ["op"] = "-", ["antwort"] = i % 2 == 0 ? "9" : "11"
            }
        });

        Assert.Equal(MerkmalStatus.Dafuer, Merkmal(Profil(beobachtungen), "M16").Status);
    }

    [Fact]
    public void FewTasksWithoutMistakes_StayUnclear()
    {
        // Five tasks offering a mirrored and another wrong spelling, all answered right: a mirror
        // problem could still hide in that little data.
        var beobachtungen = Enumerable.Range(0, 5).Select(i => Silben(i, "Bein", "Bein", "dein", "Bain"));

        Assert.Equal(MerkmalStatus.Unklar, Merkmal(Profil(beobachtungen), "M3").Status);
    }

    private static Beobachtung Minus(int i, int a, int b, int antwort) => new()
    {
        Zeit = Start.AddMinutes(i),
        View = "arith-numpad",
        Skills = [Skill.Math.Sub20],
        Versuch = 1,
        Richtig = antwort == a - b,
        Werte = new Dictionary<string, string>
        {
            ["a"] = a.ToString(), ["b"] = b.ToString(), ["op"] = "-", ["antwort"] = antwort.ToString()
        }
    };

    [Fact]
    public void ThreeOfEightSubtractionsTheWrongWayRound_ShowsKleinerVomGroesseren()
    {
        Beobachtung[] aufgaben =
        [
            Minus(0, 12, 3, 11), Minus(1, 14, 6, 12), Minus(2, 13, 5, 12), Minus(3, 15, 7, 8),
            Minus(4, 11, 4, 7), Minus(5, 16, 8, 8), Minus(6, 12, 5, 7), Minus(7, 14, 7, 7)
        ];

        Assert.Equal(MerkmalStatus.Dafuer, Merkmal(Profil(aufgaben), "M16").Status);
    }

    [Fact]
    public void EightSubtractionsAllRight_StayUnclear()
    {
        var beobachtungen = Enumerable.Range(0, 8).Select(i => Minus(i, 12 + i % 4, 6 + i % 3, 12 + i % 4 - 6 - i % 3));

        Assert.Equal(MerkmalStatus.Unklar, Merkmal(Profil(beobachtungen), "M16").Status);
    }

    [Fact]
    public void TwoWeakSourcesPointingTheSameWay_AddUp()
    {
        // Each source alone: 2 of 10 short ones wrong, 0 of 10 long ones - too little on its own.
        var fragen = Enumerable.Range(0, 20).Select(i => new Beobachtung
        {
            Zeit = Start.AddMinutes(i),
            View = "w-frage",
            Skills = [Skill.ReadWhQuestion],
            Versuch = 1,
            Richtig = i >= 2,
            Werte = new Dictionary<string, string> { ["frage"] = i < 10 ? "Wo" : "Wann" }
        }).ToList();
        string[] kurz = ["Hund", "Brot", "Haus", "Ast", "Ei", "Hut", "Bus", "Tor", "Rad", "Zug"];
        string[] lang = ["Garten", "Rakete", "Banane", "Schule", "Fenster", "Tomate", "Laterne", "Fahrrad", "Tasche", "Wolken"];
        var turbo = new Beobachtung
        {
            Zeit = Start.AddHours(1),
            View = "lese-turbo",
            Skills = [Skill.TurboRead],
            Versuch = 1,
            Items = kurz.Select((w, i) => new BeobachtungsItem(w, true, i < 2 ? "falsch" : "richtig", 900))
                .Concat(lang.Select(w => new BeobachtungsItem(w, true, "richtig", 900))).ToList()
        };

        Assert.Equal(MerkmalStatus.Unklar, Merkmal(Profil(fragen), "M5").Status);
        Assert.Equal(MerkmalStatus.Unklar, Merkmal(Profil([turbo]), "M5").Status);
        Assert.Equal(MerkmalStatus.Dafuer, Merkmal(Profil([.. fragen, turbo]), "M5").Status);
    }

    [Fact]
    public void SlowLetterByLetterReading_ShowsNotAutomated()
    {
        var woerter = new[] { "Hund", "Garten", "Brot", "Rakete", "Ei", "Banane", "Haus", "Schule", "Ast", "Fenster" };
        var items = woerter.Select(w => new BeobachtungsItem(w, true, "richtig", 800 + 400 * w.Length)).ToList();
        var beobachtungen = Enumerable.Range(0, 2).Select(i => new Beobachtung
        {
            Zeit = Start.AddDays(i),
            View = "lese-turbo",
            Skills = [Skill.TurboRead],
            Versuch = 1,
            Items = items
        });

        var m6 = Merkmal(Profil(beobachtungen), "M6");

        Assert.Equal(MerkmalStatus.Dafuer, m6.Status);
        Assert.All(m6.Quellen, q => Assert.True(q.Wert > 0.5));
    }

    [Fact]
    public void ShortQuestionWordsWrongLongOnesRight_ShowsKurzeWoerter()
    {
        var beobachtungen = Enumerable.Range(0, 40).Select(i =>
        {
            var kurz = i % 2 == 0;
            return new Beobachtung
            {
                Zeit = Start.AddDays(i / 10).AddMinutes(i % 10),
                View = "w-frage",
                Skills = [Skill.ReadWhQuestion],
                Versuch = 1,
                Richtig = !kurz || i % 4 == 0,
                Werte = new Dictionary<string, string> { ["frage"] = kurz ? "Wo" : "Wann" }
            };
        });

        Assert.Equal(MerkmalStatus.Dafuer, Merkmal(Profil(beobachtungen), "M5").Status);
    }

    [Fact]
    public void FeatureWithoutTask_ExplainsWhatIsMissing()
    {
        var m1 = Merkmal(Profil([]), "M1");

        Assert.Equal(MerkmalStatus.KeineDaten, m1.Status);
        Assert.NotNull(m1.Hinweis);
    }

    [Fact]
    public void OnlyObservationsInsideTheWindowCount()
    {
        var alt = Enumerable.Range(0, 30)
            .Select(i => Silben(i, "Bein", i % 3 == 0 ? "Bein" : "dein", "dein", "Bain"));

        var profil = MerkmalRechner.Berechne(alt, Start.AddDays(30), Start.AddDays(60));

        Assert.Equal(MerkmalStatus.KeineDaten, Merkmal(profil, "M3").Status);
    }
}
