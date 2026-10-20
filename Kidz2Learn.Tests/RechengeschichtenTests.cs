using System.Text.RegularExpressions;
using Kidz2Learn.Model;
using Kidz2Learn.Model.Lesen;
using Kidz2Learn.Model.Tasks;
using Xunit;

namespace Kidz2Learn.Tests;

public class RechengeschichtenTests
{
    private static readonly Random Rng = new(2024);

    private static readonly Figur Igel = Rechengeschichten.Figuren.Single(f => f.Nominativ == "der Igel");
    private static readonly Figur Oma = Rechengeschichten.Figuren.Single(f => f.Nominativ == "die Oma");
    private static readonly Figur Hase = Rechengeschichten.Figuren.Single(f => f.Nominativ == "der Hase");

    private static IEnumerable<WFrageSerie> Viele(Func<Random, WFrageSerie> generate) =>
        Enumerable.Range(0, 300).Select(_ => generate(Rng));

    private static IEnumerable<WFrageSerie> Rechnen() =>
        Viele(r => Rechengeschichten.GenerateRechnen(Rechenart.Plus, r))
            .Concat(Viele(r => Rechengeschichten.GenerateRechnen(Rechenart.Minus, r)))
            .Concat(Viele(r => Rechengeschichten.GenerateRechnenLeicht(Rechenart.Plus, r)))
            .Concat(Viele(r => Rechengeschichten.GenerateRechnenLeicht(Rechenart.Minus, r)));

    private static List<int> ZahlenIm(string text) =>
        Regex.Matches(text, @"\d+").Select(m => int.Parse(m.Value)).ToList();

    [Fact]
    public void EveryStory_AsksOneQuestion_WithFourDistinctOptions()
    {
        foreach (var serie in Rechnen().Concat(Viele(Rechengeschichten.GenerateAblesen)))
        {
            var frage = Assert.Single(serie.Fragen);
            Assert.Equal(4, frage.Optionen.Select(o => o.Label).Distinct().Count());
            Assert.Single(frage.Optionen, o => o.Label == frage.Richtig);
            Assert.StartsWith(frage.Fragewort + " ", frage.Frage);
            Assert.All(frage.Optionen, o => Assert.InRange(int.Parse(o.Label), 1, 20));
        }
    }

    [Fact]
    public void Rechnen_GivesTwoNumbers_AndAsksForTheThird()
    {
        foreach (var serie in Rechnen())
        {
            var imText = ZahlenIm(serie.Text);
            var antwort = int.Parse(serie.Fragen.Single().Richtig);
            Assert.Equal(2, imText.Count);
            Assert.DoesNotContain(antwort, imText);
            Assert.All(imText.Append(antwort), z => Assert.InRange(z, 2, Rechengeschichten.Maximum));
        }
    }

    [Fact]
    public void Ablesen_AsksForANumberInTheText()
    {
        foreach (var serie in Viele(Rechengeschichten.GenerateAblesen))
            Assert.Contains(int.Parse(serie.Fragen.Single().Richtig), ZahlenIm(serie.Text));
    }

    [Fact]
    public void RechnenLeicht_RepeatsTheSubject_AndAsksForNow()
    {
        foreach (var serie in Viele(r => Rechengeschichten.GenerateRechnenLeicht(Rechenart.Plus, r)))
        {
            Assert.DoesNotMatch(@"\b(er|sie)\b", serie.Text);
            Assert.EndsWith("jetzt?", serie.Fragen.Single().Frage);
        }
    }

    [Theory]
    [InlineData(Rechenart.Plus, Zahl.Vorher, 4, 3,
        "Der Igel hat von der Oma 3 neue Äpfel bekommen. Jetzt hat er 7.", 10)]
    [InlineData(Rechenart.Plus, Zahl.Jetzt, 7, 2,
        "Der Igel hatte 7 Äpfel. Dann hat er von der Oma 2 neue bekommen.", 5)]
    [InlineData(Rechenart.Minus, Zahl.Aenderung, 7, 3,
        "Der Igel hatte 7 Äpfel. Dann hat er der Oma welche geschenkt. Jetzt hat er noch 4.", 11)]
    [InlineData(Rechenart.Minus, Zahl.Jetzt, 7, 3,
        "Der Igel hatte 7 Äpfel. Dann hat er der Oma 3 geschenkt.", 10)]
    public void Stories_ReadAsGerman_AndOfferTheWrongOperation(Rechenart art, Zahl fehlt, int vorher,
        int aenderung, string text, int falscheRechnung)
    {
        var serie = Rechengeschichten.Build(art, fehlt, fehlt, true, Igel, Oma, "Äpfel", vorher, aenderung, Rng);

        Assert.Equal(text, serie.Text);
        Assert.Contains(serie.Fragen.Single().Optionen, o => o.Label == falscheRechnung.ToString());
    }

    [Fact]
    public void WithoutPronoun_TheSubjectIsRepeated()
    {
        var serie = Rechengeschichten.Build(Rechenart.Minus, Zahl.Vorher, Zahl.Vorher, false, Oma, Hase, "Kekse", 7,
            3, Rng);
        Assert.Equal("Die Oma hat dem Hasen 3 Kekse geschenkt. Jetzt hat die Oma noch 4.", serie.Text);
    }

    [Theory]
    [InlineData(Zahl.Vorher, "Wie viele Nüsse hatte der Igel vorher?", "2")]
    [InlineData(Zahl.Aenderung, "Wie viele Nüsse hat der Igel vom Hasen bekommen?", "5")]
    [InlineData(Zahl.Jetzt, "Wie viele Nüsse hat der Igel jetzt?", "7")]
    public void TheQuestion_NamesTheThingsAndThePartner(Zahl gefragt, string frage, string antwort)
    {
        var aufgabe = Rechengeschichten.Build(Rechenart.Plus, Zahl.Jetzt, gefragt, true, Igel, Hase, "Nüsse", 2, 5, Rng)
            .Fragen.Single();

        Assert.Equal(frage, aufgabe.Frage);
        Assert.Equal(antwort, aufgabe.Richtig);
    }

    [Fact]
    public async Task Rechnen_TrainsItsOwnSkillFully_AndReadingAndArithmeticAtHalfWeight()
    {
        var store = new FakeSkillMasteryStore();
        var generator = new AdaptiveTaskGenerator(store, new Random(3));
        IChosenTask task;
        do task = await generator.ChooseAnyAsync([Skill.ReadStoryMath]);
        while (task.Skills.Count < 3);
        await task.Success(new Kompetenzniveau(), 5000);

        var weights = store.AdjustCalls.ToDictionary(c => c.SkillId, c => c.Weight);
        Assert.Equal(3, weights.Count);
        Assert.Equal(1f, weights[Skill.ReadStoryMath]);
        Assert.Equal(0.5f, weights[Skill.ReadWhQuestion]);
        Assert.Contains(weights, w => ArithTaskRegistry.SimpleSkills.Contains(w.Key) && w.Value == 0.5f);
    }

    [Fact]
    public void WordProblems_AreTwoLevelsOfCalculating_PlusReadingOffWithoutArithmetic()
    {
        var textaufgaben = TaskRegistry.AllWFrage.Where(d => d.Skills.Contains(Skill.ReadStoryMath)).ToList();
        Assert.All(textaufgaben, d => Assert.Contains(Skill.ReadWhQuestion, d.Skills));

        var rechnen = textaufgaben.Where(d => d.Skills.Any(ArithTaskRegistry.SimpleSkills.Contains)).ToList();
        Assert.Equal([2, 2, 3, 3], rechnen.Select(d => d.DifficultyLevel).Order());
        Assert.Single(textaufgaben.Except(rechnen));
    }
}
