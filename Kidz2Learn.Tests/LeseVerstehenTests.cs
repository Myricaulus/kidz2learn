using Kidz2Learn.Components.TaskViews;
using Kidz2Learn.Model;
using Kidz2Learn.Model.Lesen;
using Kidz2Learn.Model.Tasks;
using Xunit;

namespace Kidz2Learn.Tests;

public class QuatschSaetzeTests
{
    private static readonly Random Rng = new(4711);

    [Fact]
    public void EveryLexikonNoun_IsInTheKnownWordList()
    {
        // Recorded audio is optional (e.g. the V-words have none yet) - LeseSatz/WFrageView just
        // don't offer playback for those.
        foreach (var nomen in QuatschSaetze.Lexikon)
            Assert.True(WordMeta.Data.ContainsKey(nomen.Wort), $"'{nomen.Wort}' is not a WordMeta word");
    }

    [Fact]
    public void EveryAussage_HasNounsOnBothSides_AndNoNounOnBoth()
    {
        foreach (var aussage in QuatschSaetze.Aussagen)
        {
            Assert.Contains(QuatschSaetze.Lexikon, n => aussage.Wahr(n));
            Assert.Contains(QuatschSaetze.Lexikon, n => aussage.Falsch(n));
            Assert.DoesNotContain(QuatschSaetze.Lexikon, n => aussage.Wahr(n) && aussage.Falsch(n));
        }
    }

    [Fact]
    public void HandwrittenPools_HaveBothTrueAndNonsenseSentences()
    {
        Assert.Contains(QuatschSaetze.Leicht, s => s.Stimmt);
        Assert.Contains(QuatschSaetze.Leicht, s => !s.Stimmt);
        Assert.Contains(QuatschSaetze.Schwer, s => s.Stimmt);
        Assert.Contains(QuatschSaetze.Schwer, s => !s.Stimmt);
    }

    [Fact]
    public void Generators_ProduceCapitalizedSentencesWithNoLeftoverPlaceholders()
    {
        foreach (var generate in new Func<Random, QuatschSatz>[] { QuatschSaetze.GenerateLeicht, QuatschSaetze.GenerateSchwer })
        for (var i = 0; i < 300; i++)
        {
            var satz = generate(Rng);
            Assert.True(char.IsUpper(satz.Text[0]), satz.Text);
            Assert.EndsWith(".", satz.Text);
            Assert.DoesNotContain("{", satz.Text);
        }
    }

    [Fact]
    public void Generators_AreRoughlyBalancedBetweenTrueAndNonsense()
    {
        foreach (var generate in new Func<Random, QuatschSatz>[] { QuatschSaetze.GenerateLeicht, QuatschSaetze.GenerateSchwer })
        {
            var trueCount = Enumerable.Range(0, 1000).Count(_ => generate(Rng).Stimmt);
            Assert.InRange(trueCount, 400, 600);
        }
    }

    [Fact]
    public void Comparisons_AreTrueExactlyWhenTheSizeOrderMatches()
    {
        var bySatz = QuatschSaetze.Lexikon.Where(n => n.Groesse is not null).ToList();
        for (var i = 0; i < 500; i++)
        {
            var satz = QuatschSaetze.GenerateSchwer(Rng);
            var groesser = satz.Text.Contains(" ist größer als ");
            if (!groesser && !satz.Text.Contains(" ist kleiner als "))
                continue;

            var parts = satz.Text.TrimEnd('.').Split(groesser ? " ist größer als " : " ist kleiner als ");
            var a = bySatz.Single(n => string.Equals(n.Nominativ, parts[0], StringComparison.OrdinalIgnoreCase));
            var b = bySatz.Single(n => n.Nominativ == parts[1]);
            Assert.Equal(groesser ? a.Groesse > b.Groesse : a.Groesse < b.Groesse, satz.Stimmt);
        }
    }
}

public class WFragenTests
{
    [Fact]
    public void ShortAndLongQuestionWords_ComeUpAboutEquallyOften()
    {
        var rng = new Random(9);
        var fragen = Enumerable.Range(0, 400)
            .SelectMany(i => (i % 2 == 0 ? WFragen.GenerateLeicht(rng) : WFragen.GenerateSchwer(rng)).Fragen)
            .ToList();
        var lang = fragen.Count(f => !WFragen.IstKurz(f.Fragewort));

        Assert.InRange((double)lang / fragen.Count, 0.3, 0.6);
    }

    private static readonly Random Rng = new(815);

    [Fact]
    public void EveryRahmen_HasExactlyOneSubject_AndEveryPhraseAppearsInTheSentence()
    {
        foreach (var rahmen in WFragen.Rahmen)
        {
            Assert.Single(rahmen.Teile, t => t.Rolle == FrageRolle.Wer);
            Assert.InRange(rahmen.Teile.Length, 3, 4);
            foreach (var teil in rahmen.Teile)
                Assert.Contains(teil.Phrase, rahmen.Text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(rahmen.Verb, rahmen.Text);
        }
    }

    [Fact]
    public void EveryRahmen_HasDistinctAnswersAndRoles()
    {
        foreach (var rahmen in WFragen.Rahmen)
        {
            Assert.Equal(rahmen.Teile.Length, rahmen.Teile.Select(t => t.Antwort).Distinct().Count());
            Assert.Equal(rahmen.Teile.Length, rahmen.Teile.Select(t => t.Rolle).Distinct().Count());
        }
    }

    [Fact]
    public void Generators_AlwaysOfferTheCorrectAnswer_AndStartTheQuestionWithItsQuestionWord()
    {
        foreach (var generate in new Func<Random, WFrageSerie>[] { WFragen.GenerateLeicht, WFragen.GenerateSchwer })
        for (var i = 0; i < 300; i++)
        foreach (var aufgabe in generate(Rng).Fragen)
        {
            Assert.Single(aufgabe.Optionen, o => o.Label == aufgabe.Richtig);
            Assert.StartsWith(aufgabe.Fragewort + " ", aufgabe.Frage);
            Assert.EndsWith("?", aufgabe.Frage);
        }
    }

    [Fact]
    public void Serie_AsksDifferentPartsOfTheSameSentence()
    {
        foreach (var generate in new Func<Random, WFrageSerie>[] { WFragen.GenerateLeicht, WFragen.GenerateSchwer })
        for (var i = 0; i < 300; i++)
        {
            var serie = generate(Rng);
            Assert.InRange(serie.Fragen.Count, 2, 3);
            Assert.All(serie.Fragen, f => Assert.Equal(serie.Text, f.Text));
            Assert.Equal(serie.Fragen.Count, serie.Fragen.Select(f => f.Rolle).Distinct().Count());
        }
    }

    [Fact]
    public void Leicht_OnlyUsesThreePartSentences_WithTwoQuestions()
    {
        for (var i = 0; i < 200; i++)
        {
            var serie = WFragen.GenerateLeicht(Rng);
            Assert.Equal(2, serie.Fragen.Count);
            Assert.All(serie.Fragen, f => Assert.Equal(3, f.Optionen.Count));
        }
    }

    [Fact]
    public void WerFrage_NamesTheObject_NotTheSubject()
    {
        var rahmen = WFragen.Rahmen.Single(r => r.Text.StartsWith("Die Tante wäscht"));
        Assert.Equal("Wer wäscht den Hund?", WFragen.Frage(rahmen, rahmen.Subjekt));
    }

    [Fact]
    public void OtherQuestions_KeepTheObject_UnlessItIsWhatIsAsked()
    {
        var rahmen = WFragen.Rahmen.Single(r => r.Text.StartsWith("Der Igel polstert"));
        var womit = rahmen.Teile.Single(t => t.Rolle == FrageRolle.Womit);
        var was = rahmen.Teile.Single(t => t.Rolle == FrageRolle.Was);

        Assert.Equal("Womit polstert der Igel sein Nest?", WFragen.Frage(rahmen, womit));
        Assert.Equal("Was polstert der Igel?", WFragen.Frage(rahmen, was));
    }

    [Fact]
    public void EveryQuestionWord_IsUsed_AndHasItsOwnColor()
    {
        foreach (var rolle in Enum.GetValues<FrageRolle>())
        {
            Assert.Contains(WFragen.Rahmen, r => r.Teile.Any(t => t.Rolle == rolle));
            Assert.True(WFrageView.Farben.ContainsKey(rolle), rolle.ToString());
        }
        Assert.Equal(WFrageView.Farben.Count, WFrageView.Farben.Values.Distinct().Count());
    }

    [Theory]
    [InlineData("Die Oma gibt", FrageRolle.Wem, "Wem gibt die Oma einen Knochen?")]
    [InlineData("Die Oma gibt", FrageRolle.Was, "Was gibt die Oma dem Hund?")]
    [InlineData("Die Oma gibt", FrageRolle.Wer, "Wer gibt dem Hund einen Knochen?")]
    [InlineData("Der Hase spielt", FrageRolle.MitWem, "Mit wem spielt der Hase?")]
    [InlineData("Der Hase spielt", FrageRolle.Wer, "Wer spielt mit der Maus?")]
    [InlineData("Die Katze springt im", FrageRolle.Wer, "Wer springt im Garten?")]
    [InlineData("Die Katze springt im", FrageRolle.Wohin, "Wohin springt die Katze?")]
    [InlineData("Der Bauer holt", FrageRolle.WieViele, "Wie viele Eier holt der Bauer?")]
    [InlineData("Der Bauer holt", FrageRolle.Woher, "Woher holt der Bauer fünf Eier?")]
    [InlineData("Der Hund bellt", FrageRolle.Wie, "Wie bellt der Hund?")]
    public void NewQuestionWords_ReadAsGermanQuestions(string satzAnfang, FrageRolle rolle, string erwartet)
    {
        var rahmen = WFragen.Rahmen.Single(r => r.Text.StartsWith(satzAnfang));
        Assert.Equal(erwartet, WFragen.Frage(rahmen, rahmen.Teile.Single(t => t.Rolle == rolle)));
    }

    [Theory]
    [InlineData(FrageRolle.MitWem, "Mitwem")]
    [InlineData(FrageRolle.WieViele, "Wieviele")]
    [InlineData(FrageRolle.Wohin, "Wohin")]
    public void QuestionWordAudio_IsOneWord(FrageRolle rolle, string erwartet)
    {
        Assert.Equal(erwartet, WFragen.FragewortAudio(rolle));
    }
}

public class RaetselTests
{
    private static readonly Random Rng = new(1312);

    [Fact]
    public void EveryRaetsel_HasThreeClues_AndTwoDistinctSimilarThings()
    {
        foreach (var raetsel in RaetselSammlung.Alle)
        {
            Assert.Equal(3, raetsel.Hinweise.Length);
            foreach (var hinweis in raetsel.Hinweise)
                Assert.True(hinweis.EndsWith('.') || hinweis.EndsWith('!'), hinweis);
            Assert.Equal(2, raetsel.Aehnliche.Distinct().Count());
            Assert.DoesNotContain(raetsel.Loesung, raetsel.Aehnliche);
        }
    }

    [Fact]
    public void Solutions_AreUnique()
    {
        var loesungen = RaetselSammlung.Alle.Select(r => r.Loesung).ToList();
        Assert.Equal(loesungen.Count, loesungen.Distinct().Count());
    }

    [Fact]
    public void UnpassendPairs_NameExistingSolutionsOfDifferentKinds()
    {
        var byLoesung = RaetselSammlung.Alle.ToDictionary(r => r.Loesung);
        foreach (var (a, b) in RaetselSammlung.Unpassend)
        {
            Assert.True(byLoesung.ContainsKey(a), a);
            Assert.True(byLoesung.ContainsKey(b), b);
            // Same-kind pairs never meet at level 1 anyway, so listing one would be dead weight.
            Assert.NotEqual(byLoesung[a].Bereich, byLoesung[b].Bereich);
        }
    }

    [Fact]
    public void Generators_OfferThreeDistinctOptions_IncludingTheSolution()
    {
        foreach (var generate in new Func<Random, RaetselAufgabe>[] { RaetselSammlung.GenerateLeicht, RaetselSammlung.GenerateSchwer })
        for (var i = 0; i < 300; i++)
        {
            var aufgabe = generate(Rng);
            Assert.Equal(3, aufgabe.Optionen.Distinct().Count());
            Assert.Single(aufgabe.Optionen, o => o == aufgabe.Richtig);
        }
    }

    [Fact]
    public void Leicht_DrawsWrongOptionsFromTwoOtherKinds_NeverFromUnpassendPairs()
    {
        var byLoesung = RaetselSammlung.Alle.ToDictionary(r => r.Loesung);
        for (var i = 0; i < 500; i++)
        {
            var aufgabe = RaetselSammlung.GenerateLeicht(Rng);
            var loesung = byLoesung[aufgabe.Richtig];
            var ablenker = aufgabe.Optionen.Where(o => o != aufgabe.Richtig).Select(o => byLoesung[o]).ToList();

            Assert.All(ablenker, a => Assert.NotEqual(loesung.Bereich, a.Bereich));
            Assert.NotEqual(ablenker[0].Bereich, ablenker[1].Bereich);
            Assert.All(ablenker, a => Assert.DoesNotContain(RaetselSammlung.Unpassend, p =>
                (p.A == loesung.Loesung && p.B == a.Loesung) || (p.B == loesung.Loesung && p.A == a.Loesung)));
        }
    }

    [Fact]
    public void Schwer_OffersTheHandPickedSimilarThings()
    {
        for (var i = 0; i < 200; i++)
        {
            var aufgabe = RaetselSammlung.GenerateSchwer(Rng);
            var raetsel = RaetselSammlung.Alle.Single(r => r.Loesung == aufgabe.Richtig);
            Assert.Equal(raetsel.Aehnliche.Append(raetsel.Loesung).Order(), aufgabe.Optionen.Order());
        }
    }
}

public class WordAudioTests
{
    [Theory]
    [InlineData("Der")]
    [InlineData("Hund.")]
    [InlineData("hund")]
    public void PathFor_FindsKnownWordsRegardlessOfCaseAndPunctuation(string token)
    {
        Assert.NotNull(WordAudio.PathFor(token));
    }

    [Fact]
    public void PathFor_UnknownWord_IsNull()
    {
        Assert.Null(WordAudio.PathFor("Schniefnase"));
    }
}

public class LeseVerstehenRegistryTests
{
    [Fact]
    public void EveryLeseVerstehenView_IsRegistered()
    {
        foreach (var def in TaskRegistry.AllQuatsch.Cast<BaseTaskDefinition>().Concat(TaskRegistry.AllWFrage)
                     .Concat(TaskRegistry.AllRaetsel))
            Assert.True(TaskPresentationRegistry.IsRegistered(def.View), def.View);
    }

    [Fact]
    public void NewSkillIds_DoNotCollideWithExistingAbbreviations()
    {
        var abbreviations = SkillRegistry.All.Keys.Select(StringAbbreviator.Abbreviate).ToList();
        Assert.Equal(abbreviations.Count, abbreviations.Distinct().Count());
    }
}

public class LesePunkteTests
{
    [Fact]
    public void LongerSentences_EarnMorePoints_WithinBounds()
    {
        var kurz = LesePunkte.ForText("Der Hund bellt.");
        var lang = LesePunkte.ForText("Weil der Hamster müde ist, schläft er ein.");

        Assert.Equal(LesePunkte.Min, kurz);
        Assert.Equal(4, lang);
        Assert.True(lang > kurz);
        Assert.Equal(LesePunkte.Max, LesePunkte.ForText(string.Join(' ', Enumerable.Repeat("Wort", 30))));
    }

    [Fact]
    public void MultipleTexts_AreCountedTogether()
    {
        Assert.Equal(
            LesePunkte.ForText("Die Tante hört im Auto Radio. Wo hört die Tante Radio?"),
            LesePunkte.ForText("Die Tante hört im Auto Radio.", "Wo hört die Tante Radio?"));
    }
}

public class NomenTests
{
    private static readonly Random Rng = new(4711);

    private static readonly (NomenRaster.Groesse Groesse, NomenRaster.WortArt Art)[] Varianten =
    [
        .. from groesse in Enum.GetValues<NomenRaster.Groesse>()
            from art in Enum.GetValues<NomenRaster.WortArt>()
            select (groesse, art)
    ];

    private static IEnumerable<string> AlleNomen =>
        NomenSammlung.Konkret.Concat(NomenSammlung.Abstrakt.Select(a => a.Wort));

    private static IEnumerable<string> AlleKeineNomen =>
        NomenSammlung.KeineNomen.Concat(NomenSammlung.Abstrakt.SelectMany(a => a.Verwandte));

    [Fact]
    public void NoWord_IsListedAsNounAndAsNonNoun()
    {
        var nomen = AlleNomen.ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(AlleKeineNomen, nomen.Contains);
    }

    [Fact]
    public void Nouns_AreCapitalized_AndNonNounsLowercase()
    {
        Assert.All(AlleNomen, n => Assert.True(char.IsUpper(n[0]), n));
        Assert.All(AlleKeineNomen, k => Assert.Equal(k.ToLowerInvariant(), k));
    }

    [Fact]
    public void EveryVariant_FillsItsGridWithDistinctWords_AndItsRangeOfNouns()
    {
        foreach (var (groesse, art) in Varianten)
        for (var i = 0; i < 300; i++)
        {
            var (spalten, anzahl, minNomen, maxNomen) = NomenRaster.Masse(groesse);
            var aufgabe = NomenSammlung.Generate(groesse, art, Rng);
            Assert.Equal(spalten, aufgabe.Spalten);
            Assert.Equal(anzahl, aufgabe.Felder.Count);
            Assert.Equal(anzahl,
                aufgabe.Felder.Select(w => w.Text).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.InRange(aufgabe.AnzahlTreffer, minNomen, maxNomen);
        }
    }

    [Fact]
    public void Stufe_AddsOneLevelPerStepOnEitherKnob()
    {
        Assert.Equal(1, NomenRaster.Stufe(NomenRaster.Groesse.Klein, NomenRaster.WortArt.Konkret));
        Assert.Equal(2, NomenRaster.Stufe(NomenRaster.Groesse.Klein, NomenRaster.WortArt.Gemischt));
        Assert.Equal(2, NomenRaster.Stufe(NomenRaster.Groesse.Mittel, NomenRaster.WortArt.Konkret));
        Assert.Equal(4, NomenRaster.Stufe(NomenRaster.Groesse.Gross, NomenRaster.WortArt.Gemischt));
        Assert.Equal(5, NomenRaster.Stufe(NomenRaster.Groesse.Gross, NomenRaster.WortArt.Abstrakt));
    }

    [Fact]
    public void Generators_WriteTheWholeGridInLowerCase()
    {
        foreach (var (groesse, art) in Varianten)
        for (var i = 0; i < 100; i++)
            Assert.All(NomenSammlung.Generate(groesse, art, Rng).Felder,
                w => Assert.Equal(w.Text.ToLowerInvariant(), w.Text));
    }

    [Fact]
    public void Konkret_OnlyUsesTouchableNouns()
    {
        var konkret = NomenSammlung.Konkret.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var groesse in Enum.GetValues<NomenRaster.Groesse>())
        for (var i = 0; i < 200; i++)
            Assert.All(NomenSammlung.Generate(groesse, NomenRaster.WortArt.Konkret, Rng).Felder.Where(w => w.Treffer),
                w => Assert.Contains(w.Text, konkret));
    }

    [Fact]
    public void Abstrakt_PutsARelatedNonNounNextToEveryAbstractNounThatHasOne()
    {
        foreach (var groesse in Enum.GetValues<NomenRaster.Groesse>())
        for (var i = 0; i < 200; i++)
        {
            var woerter = NomenSammlung.Generate(groesse, NomenRaster.WortArt.Abstrakt, Rng).Felder
                .Select(w => w.Text).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var nomen in NomenSammlung.Abstrakt.Where(a => woerter.Contains(a.Wort)))
                if (nomen.Verwandte.Length > 0)
                    Assert.Contains(nomen.Verwandte, woerter.Contains);
        }
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 5)]
    [InlineData(2, 0)]
    [InlineData(3, -1)]
    [InlineData(5, -3)]
    public void Punkte_HalveForOneMistake_VanishForTwo_AndGoNegativeFromThree(int fehler, int expected)
    {
        Assert.Equal(expected, SuchRasterWertung.Punkte(10, fehler));
    }
}

public class SpiegelbuchstabenTests
{
    private static readonly Random Rng = new(4711);

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void Build_FillsASquareGridWithMirrorLetters_AQuarterToAThirdBeingTheTarget(int seite)
    {
        for (var i = 0; i < 300; i++)
        {
            var raster = Spiegelbuchstaben.Build(seite, 5, Rng);
            Assert.Equal(seite, raster.Spalten);
            Assert.Equal(seite * seite, raster.Felder.Count);
            Assert.All(raster.Felder, f => Assert.Contains(f.Text, Spiegelbuchstaben.Buchstaben));
            Assert.All(raster.Felder, f => Assert.Equal(f.Text == raster.Ziel, f.Treffer));
            Assert.InRange(raster.AnzahlTreffer, seite * seite / 4, seite * seite / 3);
        }
    }

    [Fact]
    public void EveryLetter_ComesUpAsTarget()
    {
        var ziele = Enumerable.Range(0, 200).Select(_ => Spiegelbuchstaben.GenerateMittel(Rng).Ziel).ToHashSet();
        Assert.Equal(Spiegelbuchstaben.Buchstaben.ToHashSet(), ziele);
    }

    [Fact]
    public void Levels_AreFourByFourAndFiveByFive()
    {
        Assert.Equal(16, Spiegelbuchstaben.GenerateMittel(Rng).Felder.Count);
        Assert.Equal(25, Spiegelbuchstaben.GenerateSchwer(Rng).Felder.Count);
    }

    [Fact]
    public void AboutHalfTheGrids_ArePackedTight()
    {
        var eng = Enumerable.Range(0, 400).Count(_ => Spiegelbuchstaben.GenerateMittel(Rng).Eng == true);
        Assert.InRange(eng, 150, 250);
    }
}
