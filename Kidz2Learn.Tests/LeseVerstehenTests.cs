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
        foreach (var generate in new Func<Random, WFrageAufgabe>[] { WFragen.GenerateLeicht, WFragen.GenerateSchwer })
        for (var i = 0; i < 300; i++)
        {
            var aufgabe = generate(Rng);
            Assert.Single(aufgabe.Optionen, o => o.Label == aufgabe.Richtig);
            Assert.StartsWith(aufgabe.Fragewort + " ", aufgabe.Frage);
            Assert.EndsWith("?", aufgabe.Frage);
            Assert.Contains(aufgabe.Highlight, aufgabe.Text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Leicht_OnlyUsesThreePartSentences()
    {
        for (var i = 0; i < 200; i++)
            Assert.Equal(3, WFragen.GenerateLeicht(Rng).Optionen.Count);
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
        foreach (var def in TaskRegistry.AllQuatsch.Cast<BaseTaskDefinition>().Concat(TaskRegistry.AllWFrage))
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
        Assert.Equal(12, lang);
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
