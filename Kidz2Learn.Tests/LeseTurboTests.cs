using Kidz2Learn.Model;
using Kidz2Learn.Model.Lesen;
using Kidz2Learn.Model.Tasks;
using Kidz2Learn.Model.Tasks.TaskDefs;
using Xunit;

namespace Kidz2Learn.Tests;

public class LeseTurboTests
{
    [Fact]
    public void WordLists_AreLargeEnough_AndNoWordIsBothRealAndNonsense()
    {
        Assert.True(LeseTurboWords.Echt.Count >= 150);
        Assert.True(LeseTurboWords.Quatsch.Count >= 150);

        var echt = LeseTurboWords.Echt.ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(LeseTurboWords.Quatsch, echt.Contains);
        Assert.DoesNotContain(LeseTurboWords.Quatsch, q => WordMeta.Data.Keys.Contains(q, StringComparer.OrdinalIgnoreCase));
        Assert.DoesNotContain(LeseTurboWords.Quatsch, q => SilbenHammerWords.Data.Any(w => string.Equals(w.Word, q, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void NonsenseWords_AreLowerCaseLetters()
    {
        Assert.All(LeseTurboWords.Quatsch, q => Assert.Matches("^[a-zäöüß]{4,7}$", q));
    }

    [Fact]
    public void Naechstes_MixesRealAndNonsense_NeverRepeats_AndCapitalizesSomeNonsense()
    {
        var rng = new Random(3);
        var gezeigt = new HashSet<string>();
        var woerter = Enumerable.Range(0, 60).Select(_ => LeseTurbo.Naechstes(rng, gezeigt)).ToList();

        Assert.InRange(woerter.Count(w => w.Echt), 20, 40);
        Assert.Equal(woerter.Count, woerter.Select(w => w.Text.ToLowerInvariant()).Distinct().Count());
        var quatsch = woerter.Where(w => !w.Echt).ToList();
        Assert.Contains(quatsch, w => char.IsUpper(w.Text[0]));
        Assert.Contains(quatsch, w => char.IsLower(w.Text[0]));
    }

    [Theory]
    [InlineData(8, 0, true)]
    [InlineData(7, 0, false)]
    [InlineData(8, 2, true)]
    [InlineData(8, 3, false)]
    public void Geschafft_NeedsEightWords_AndEightyPercentRight(int richtig, int falsch, bool erwartet)
    {
        Assert.Equal(erwartet, LeseTurbo.Geschafft(richtig, falsch));
    }

    [Fact]
    public async Task TheLesedetektivOffersTheLeseTurbo_AsATwentyToThirtySecondRound()
    {
        var generator = new AdaptiveTaskGenerator(new FakeSkillMasteryStore(), new Random(4));

        for (var i = 0; i < 30; i++)
        {
            var chosen = await generator.ChooseAnyAsync([Skill.TurboRead]);
            Assert.Equal("lese-turbo", chosen.View);
            Assert.InRange(((EventLaunchOptions)chosen.Payload).TargetDuration!.Value.TotalSeconds, 20, 30);
        }
    }
}
