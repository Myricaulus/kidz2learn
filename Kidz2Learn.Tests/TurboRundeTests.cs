using Kidz2Learn.Model;
using Kidz2Learn.Model.Tasks;
using Kidz2Learn.Model.Tasks.TaskDefs;
using Xunit;

namespace Kidz2Learn.Tests;

public class TurboRundeTests
{
    [Theory]
    [InlineData(3, 4, null, ArithOperator.Addition, 7)]
    [InlineData(null, 4, 7, ArithOperator.Addition, 3)]
    [InlineData(3, null, 7, ArithOperator.Addition, 4)]
    [InlineData(9, 4, null, ArithOperator.Subtraction, 5)]
    [InlineData(null, 4, 5, ArithOperator.Subtraction, 9)]
    [InlineData(9, null, 5, ArithOperator.Subtraction, 4)]
    public void Loesung_IsTheMissingNumber(int? x, int? y, int? z, ArithOperator op, int erwartet)
    {
        Assert.Equal(erwartet, TurboRunde.Loesung((x, y, z, op)));
    }

    [Fact]
    public void Loesung_SolvesEveryGeneratedTask()
    {
        var rng = new Random(5);
        for (var i = 0; i < 500; i++)
        {
            var aufgabe = ArithTaskRegistry.TurboAufgabe(rng);
            var (x, y, z, op) = aufgabe;
            var loesung = TurboRunde.Loesung(aufgabe);
            var a = x ?? loesung;
            var b = y ?? loesung;
            var c = z ?? loesung;
            Assert.Equal(c, op == ArithOperator.Addition ? a + b : a - b);
        }
    }

    [Theory]
    [InlineData(3, 1, true)]
    [InlineData(2, 0, false)]
    [InlineData(6, 2, true)]
    [InlineData(6, 3, false)]
    public void Geschafft_NeedsThreeRight_AndAtMostAQuarterWrong(int richtig, int falsch, bool erwartet)
    {
        Assert.Equal(erwartet, TurboRunde.Geschafft(richtig, falsch));
    }

    [Fact]
    public async Task TheMixerOffersTurboAsAShortRound_OfTwentyToThirtySeconds()
    {
        var generator = new AdaptiveTaskGenerator(new FakeSkillMasteryStore(), new Random(9));

        for (var i = 0; i < 50; i++)
        {
            var chosen = await generator.ChooseAnyAsync([Skill.Math.Turbo10]);
            Assert.Equal("turbo-runde", chosen.View);
            var dauer = ((EventLaunchOptions)chosen.Payload).TargetDuration!.Value;
            Assert.InRange(dauer.TotalSeconds, 20, 30);
        }
    }
}
