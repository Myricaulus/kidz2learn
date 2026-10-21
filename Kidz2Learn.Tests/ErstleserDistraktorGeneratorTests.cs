using Kidz2Learn.Model;
using Kidz2Learn.Model.Merkmale;
using Kidz2Learn.Model.Tasks;
using Xunit;

namespace Kidz2Learn.Tests;

public class ErstleserDistraktorGeneratorTests
{
    private static IEnumerable<KeyValuePair<string, WordInfo>> Woerter =>
        WordMeta.Data.Where(w => w.Key.Length >= 3 && w.Value.audio);

    [Theory]
    [InlineData("Bein", "Bein", "Dein", Distraktorart.Spiegel)]
    [InlineData("Bein", "Bein", "Bien", Distraktorart.Vokalpaar)]
    [InlineData("Bein", "Bein", "Beun", Distraktorart.Vokalpaar)]
    [InlineData("Bein", "Bein", "Bin", Distraktorart.Vokalpaar)]
    [InlineData("Banane", "Ba-na-ne", "Nabane", Distraktorart.Reihenfolge)]
    [InlineData("Banane", "Ba-na-ne", "Bnaane", Distraktorart.Reihenfolge)]
    [InlineData("Banane", "Ba-na-ne", "Bane", Distraktorart.Auslassung)]
    [InlineData("Dose", "Do-se", "Tose", Distraktorart.Stimmhaft)]
    [InlineData("Dose", "Do-se", "Duse", Distraktorart.Sonstige)]
    public void Kandidaten_SortsEachMistakeIntoItsKind(string wort, string silben, string option, Distraktorart art)
    {
        Assert.Contains((option, art), ErstleserDistraktorGenerator.Kandidaten(wort, silben));
    }

    [Theory]
    [InlineData("Bein", "Bain")]
    [InlineData("Rad", "Rat")]
    [InlineData("Seife", "Saife")]
    [InlineData("Feder", "Veder")]
    public void Kandidaten_LeavesOutSpellingsThatSoundTheSame(string wort, string option)
    {
        Assert.DoesNotContain(ErstleserDistraktorGenerator.Kandidaten(wort), k => k.Text == option);
    }

    [Fact]
    public void Kandidaten_EveryOneIsOneClassifiedMistakeAway()
    {
        foreach (var (wort, info) in Woerter)
        foreach (var (text, _) in ErstleserDistraktorGenerator.Kandidaten(wort, info.filename))
            Assert.DoesNotContain(FehlerartRechner.Bestimme(wort, text),
                new[] { Fehlerart.Keine, Fehlerart.Auslaut, Fehlerart.AnderesWort });
    }

    [Fact]
    public void EveryKind_HasEnoughWords()
    {
        foreach (var art in Enum.GetValues<Distraktorart>())
            Assert.True(Woerter.Count(w => ErstleserDistraktorGenerator.Kandidaten(w.Key, w.Value.filename)
                .Any(k => k.Art == art)) >= 30, art.ToString());
    }

    [Fact]
    public void Generate_TakesTwoDifferentKinds_TheRequestedOneFirst()
    {
        var r = new Random(5);
        for (var i = 0; i < 50; i++)
        {
            var distraktoren = ErstleserDistraktorGenerator.Generate("Bein", "Bein", 2, r, Distraktorart.Spiegel);
            var arten = ErstleserDistraktorGenerator.Kandidaten("Bein", "Bein")
                .Where(k => distraktoren.Contains(k.Text)).Select(k => k.Art).ToList();

            Assert.Equal("Dein", distraktoren[0]);
            Assert.Equal(2, arten.Distinct().Count());
        }
    }

    [Fact]
    public void ReadPrecise_OffersEveryKindInAGoodShareOfTasks()
    {
        var def = SilbenTaskRegistry.All.Single(d => d.Skills.Contains(Skill.ReadPrecise));
        var r = new Random(6);
        var aufgaben = Enumerable.Range(0, 1200).Select(_ => def.Generator(r)).ToList();

        foreach (var art in Enum.GetValues<Distraktorart>())
        {
            var mit = aufgaben.Count(a =>
            {
                var wort = a.correct.Replace("-", "");
                var kandidaten = ErstleserDistraktorGenerator.Kandidaten(wort, a.correct);
                return a.options.Any(o => kandidaten.Contains((o, art)));
            });
            Assert.True(mit >= 1200 / 6, $"{art}: {mit}");
        }
    }
}
