using Kidz2Learn.Model.Tasks;
using Xunit;

namespace Kidz2Learn.Tests;

public class SilbenTaskRegistryTests
{
    private static readonly Random Rng = new(42);

    [Fact]
    public void EveryGenerator_AlwaysIncludesTheCorrectAnswerAmongOptions()
    {
        // This is the invariant SilbenChallenge.CheckAnswer relies on: it compares the user's
        // pick against task.correct with hyphens stripped, so that value must always be one of
        // the offered options (case-insensitively) or the correct answer would be unpickable.
        foreach (var def in SilbenTaskRegistry.All)
        for (var i = 0; i < 15; i++)
        {
            var (correct, options) = def.Generator(Rng);
            var correctAnswer = correct.Replace("-", "");

            Assert.Contains(options, o => o.Equals(correctAnswer, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void EveryGenerator_NeverProducesDuplicateOptions()
    {
        foreach (var def in SilbenTaskRegistry.All)
        for (var i = 0; i < 15; i++)
        {
            var (_, options) = def.Generator(Rng);

            Assert.Equal(options.Length, options.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
    }
}
