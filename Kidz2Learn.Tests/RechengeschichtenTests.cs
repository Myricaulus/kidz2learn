using System.Text.RegularExpressions;
using Kidz2Learn.Model;
using Kidz2Learn.Model.Lesen;
using Kidz2Learn.Model.Tasks;
using Xunit;

namespace Kidz2Learn.Tests;

public class RechengeschichtenTests
{
    private static readonly Random Rng = new(2024);

    private static IEnumerable<WFrageSerie> Viele() =>
        Enumerable.Range(0, 300).SelectMany(_ => new[]
            { Rechengeschichten.GeneratePlus(Rng), Rechengeschichten.GenerateMinus(Rng) });

    private static List<int> ZahlenIm(string text) =>
        Regex.Matches(text, @"\d+").Select(m => int.Parse(m.Value)).ToList();

    [Fact]
    public void EveryStory_AsksOneQuestion_WithFourDistinctOptions()
    {
        foreach (var serie in Viele())
        {
            Assert.Single(serie.Fragen);
            foreach (var frage in serie.Fragen)
            {
                Assert.Equal(4, frage.Optionen.Select(o => o.Label).Distinct().Count());
                Assert.Single(frage.Optionen, o => o.Label == frage.Richtig);
                Assert.StartsWith(frage.Fragewort + " ", frage.Frage);
                Assert.All(frage.Optionen, o => Assert.InRange(int.Parse(o.Label), 1, 20));
            }
        }
    }

    [Fact]
    public void TheTextGivesTwoNumbers_AndTheQuestionAsksForTheThird()
    {
        foreach (var serie in Viele())
        {
            var imText = ZahlenIm(serie.Text);
            var antwort = int.Parse(serie.Fragen.Single().Richtig);
            Assert.Equal(2, imText.Count);
            Assert.DoesNotContain(antwort, imText);
            Assert.All(imText.Append(antwort), z => Assert.InRange(z, 2, Rechengeschichten.Maximum));
        }
    }

    [Theory]
    [InlineData(Rechenart.Plus, Unbekannt.Vorher, 4, 3,
        "Der Igel hat von der Oma 3 neue Äpfel bekommen. Jetzt hat er 7.", 10)]
    [InlineData(Rechenart.Plus, Unbekannt.Jetzt, 7, 2,
        "Der Igel hatte 7 Äpfel. Dann hat er von der Oma 2 neue bekommen.", 5)]
    [InlineData(Rechenart.Minus, Unbekannt.Aenderung, 7, 3,
        "Der Igel hatte 7 Äpfel. Dann hat er der Oma welche geschenkt. Jetzt hat er noch 4.", 11)]
    [InlineData(Rechenart.Minus, Unbekannt.Jetzt, 7, 3,
        "Der Igel hatte 7 Äpfel. Dann hat er der Oma 3 geschenkt.", 10)]
    public void Stories_ReadAsGerman_AndOfferTheWrongOperation(Rechenart art, Unbekannt unbekannt, int vorher,
        int aenderung, string text, int falscheRechnung)
    {
        var igel = Rechengeschichten.Figuren.Single(f => f.Nominativ == "der Igel");
        var oma = Rechengeschichten.Figuren.Single(f => f.Nominativ == "die Oma");
        var serie = Rechengeschichten.Build(art, unbekannt, igel, oma, "Äpfel", vorher, aenderung, Rng);

        Assert.Equal(text, serie.Text);
        Assert.All(serie.Fragen, f => Assert.Contains(f.Optionen, o => o.Label == falscheRechnung.ToString()));
    }

    [Theory]
    [InlineData(Unbekannt.Vorher, "Wie viele Nüsse hatte der Igel vorher?", "2")]
    [InlineData(Unbekannt.Aenderung, "Wie viele Nüsse hat der Igel vom Hasen bekommen?", "5")]
    [InlineData(Unbekannt.Jetzt, "Wie viele Nüsse hat der Igel jetzt?", "7")]
    public void TheQuestion_NamesTheThingsAndThePartner(Unbekannt unbekannt, string frage, string antwort)
    {
        var igel = Rechengeschichten.Figuren.Single(f => f.Nominativ == "der Igel");
        var hase = Rechengeschichten.Figuren.Single(f => f.Nominativ == "der Hase");
        var aufgabe = Rechengeschichten.Build(Rechenart.Plus, unbekannt, igel, hase, "Nüsse", 2, 5, Rng).Fragen.Single();

        Assert.Equal(frage, aufgabe.Frage);
        Assert.Equal(antwort, aufgabe.Richtig);
    }

    [Fact]
    public async Task WordProblems_TrainTheirOwnSkillFully_AndReadingAndArithmeticAtHalfWeight()
    {
        var store = new FakeSkillMasteryStore();
        var generator = new AdaptiveTaskGenerator(store, new Random(3));

        var task = await generator.ChooseAnyAsync([Skill.ReadStoryMath]);
        await task.Success(new Kompetenzniveau(), 5000);

        var weights = store.AdjustCalls.ToDictionary(c => c.SkillId, c => c.Weight);
        Assert.Equal(1f, weights[Skill.ReadStoryMath]);
        Assert.Equal(0.5f, weights[Skill.ReadWhQuestion]);
        Assert.Equal(0.5f, weights.Single(w => SkillRegistry.Get(w.Key).Domain == TaskDomain.Math
                                               && w.Key != Skill.ReadStoryMath).Value);
    }

    [Fact]
    public void WordProblems_ShowUpInTheLesedetektivAndTheMatheMix()
    {
        var textaufgaben = TaskRegistry.AllWFrage.Where(d => d.Skills.Contains(Skill.ReadStoryMath)).ToList();
        Assert.Equal(2, textaufgaben.Count);
        Assert.All(textaufgaben, d => Assert.Contains(Skill.ReadWhQuestion, d.Skills));
        Assert.All(textaufgaben, d => Assert.Equal(3, d.DifficultyLevel));
        Assert.All(textaufgaben, d => Assert.Contains(d.Skills, s => ArithTaskRegistry.SimpleSkills.Contains(s)));
    }
}
