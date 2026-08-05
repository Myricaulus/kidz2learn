using Kidz2Learn.Model.Tasks;
using Kidz2Learn.Model.Tasks.TaskDefs;
using Xunit;

namespace Kidz2Learn.Tests;

public class SilbenDebugOverrideTests
{
    [Theory]
    [InlineData("read_syllables")]
    [InlineData("read_precise")]
    [InlineData("GraphemPhonem")]
    public void TryForce_WithoutExplicitOptions_MatchesTheRealGeneratorsOptionCount(string skillId)
    {
        // TECH_DEBT.md #13: a hardcoded distractor count (4) produced 5 options for read_precise
        // instead of the real 3. The override should match whatever count the forced skill's own
        // generator actually produces, for every skill - not just the one that was reported.
        var candidates = SilbenTaskRegistry.All;
        var realCount = candidates.First(c => c.Skills.Contains(skillId)).Generator(Random.Shared).options.Length;

        var overrideForSkill = new SilbenDebugOverride("Sonnensystem", skillId, null);
        var forced = overrideForSkill.TryForce(candidates);

        Assert.NotNull(forced);
        var (correct, options) = forced.Generator(Random.Shared);
        Assert.Equal(realCount, options.Length);
        // Same normalization SilbenMultipleChoiceView.CheckAnswer applies before comparing -
        // "correct" can carry syllable dashes (read_syllables/read_precise filenames) that options
        // themselves never have.
        Assert.Contains(options, o => o.Equals(correct.Replace("-", ""), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TryForce_WithExplicitOptions_UsesThemVerbatimPlusTarget()
    {
        var candidates = SilbenTaskRegistry.All;
        var overrideForSkill = new SilbenDebugOverride("Sonnensystem", "read_precise", ["Sonnenblume", "Sonnenschein"]);

        var forced = overrideForSkill.TryForce(candidates);

        Assert.NotNull(forced);
        var (_, options) = forced.Generator(Random.Shared);
        Assert.Equal(3, options.Length);
        Assert.Contains(options, o => o.Equals("Sonnensystem", StringComparison.OrdinalIgnoreCase));
    }
}
