using Kidz2Learn.Model;
using Xunit;

namespace Kidz2Learn.Tests;

public class StringAbbreviatorTests
{
    [Theory]
    [InlineData("add_1_5", "a15")]
    [InlineData("sub_10", "s10")]
    [InlineData("add_10_no_carry", "a10nc")] // "10" segment is numeric so it's kept whole, "no"/"carry" become first letters
    [InlineData("read_syllables", "rs")]
    [InlineData("read_precise", "rp")]
    [InlineData("GraphemPhonem", "g")] // single segment, no underscore
    public void Abbreviate_MatchesKnownSkillIds(string skillId, string expected)
    {
        Assert.Equal(expected, StringAbbreviator.Abbreviate(skillId));
    }

    [Fact]
    public void Abbreviate_EmptyOrWhitespace_ReturnsInputUnchanged()
    {
        Assert.Equal("", StringAbbreviator.Abbreviate(""));
        Assert.Equal("   ", StringAbbreviator.Abbreviate("   "));
    }

    [Fact]
    public void Abbreviate_AllRegisteredSkillIds_ProduceDistinctAbbreviations()
    {
        // Guards against silent SilbenLog.GenId / ArithemticLog id collisions between skills,
        // which ConstStringCollisionChecker is meant to catch but nothing currently calls.
        var abbreviations = SkillRegistry.All.Keys
            .Select(StringAbbreviator.Abbreviate)
            .ToList();

        var duplicates = abbreviations
            .GroupBy(a => a)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.True(duplicates.Count == 0, $"Colliding abbreviations: {string.Join(", ", duplicates)}");
    }
}
