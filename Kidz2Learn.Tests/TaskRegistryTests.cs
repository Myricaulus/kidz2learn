using Kidz2Learn.Model;
using Kidz2Learn.Model.Tasks;
using Kidz2Learn.Model.Tasks.TaskDefs;
using Xunit;

namespace Kidz2Learn.Tests;

public class TaskRegistryTests
{
    [Fact]
    public void All_IsUnionOfArithAndSilbenTasks()
    {
        // Also implicitly exercises the TaskRegistry static constructor, which throws if any
        // BaseTaskDefinition subtype in the assembly isn't registered in Tasks.
        Assert.Equal(
            TaskRegistry.AllArith.Count + TaskRegistry.AllSilben.Count + TaskRegistry.AllSilbenHammer.Count,
            TaskRegistry.All.Count);
    }

    [Fact]
    public void GetTasks_ReturnsSameListAsTypedAccessor()
    {
        Assert.Equal(TaskRegistry.AllArith, TaskRegistry.GetTasks<ArithTaskDefinition>());
        Assert.Equal(TaskRegistry.AllSilben, TaskRegistry.GetTasks<SilbenTaskDefinition>());
        Assert.Equal(TaskRegistry.AllSilbenHammer, TaskRegistry.GetTasks<SilbenHammerTaskDefinition>());
    }

    [Fact]
    public void EveryTaskDefinition_DeclaresAtLeastOneKnownSkill()
    {
        foreach (var task in TaskRegistry.All)
        foreach (var skillId in task.Skills)
            Assert.True(SkillRegistry.All.ContainsKey(skillId),
                $"Task references unknown skill id '{skillId}'");
    }
}

public class ArithTaskRegistryTests
{
    private static readonly Random Rng = new(1234);

    [Fact]
    public void All_IsUnionOfSimpleAndTurbo()
    {
        Assert.Equal(ArithTaskRegistry.Simple.Count + ArithTaskRegistry.Turbo.Count, ArithTaskRegistry.All.Count);
    }

    [Fact]
    public void Add15_AlwaysGeneratesNumbersFromOneToFour()
    {
        var def = ArithTaskRegistry.Simple.Single(d => d.Skills.Contains(Skill.Math.Add15));

        for (var i = 0; i < 200; i++)
        {
            var (x, y, _, op) = def.Generator(Rng);
            Assert.Equal(ArithOperator.Addition, op);
            Assert.InRange(x!.Value, 1, 4);
            Assert.InRange(y!.Value, 1, 4);
        }
    }

    [Fact]
    public void Add10NoCarry_NeverProducesASumOfTenOrMore()
    {
        var def = ArithTaskRegistry.Simple.Single(d => d.Skills.Contains(Skill.Math.Add10NoCarry));

        for (var i = 0; i < 200; i++)
        {
            var (x, y, _, op) = def.Generator(Rng);
            Assert.Equal(ArithOperator.Addition, op);
            Assert.True(x!.Value + y!.Value < 10, $"{x}+{y} should be < 10");
        }
    }

    [Fact]
    public void Add10WithCarry_AlwaysProducesASumOfTenOrMore()
    {
        var def = ArithTaskRegistry.Simple.Single(d => d.Skills.Contains(Skill.Math.Add10WithCarry));

        for (var i = 0; i < 200; i++)
        {
            var (x, y, _, op) = def.Generator(Rng);
            Assert.Equal(ArithOperator.Addition, op);
            Assert.True(x!.Value + y!.Value >= 10, $"{x}+{y} should be >= 10");
        }
    }

    [Fact]
    public void Sub10_NeverGoesNegative()
    {
        var def = ArithTaskRegistry.Simple.Single(d => d.Skills.Contains(Skill.Math.Sub10));

        for (var i = 0; i < 200; i++)
        {
            var (x, y, _, op) = def.Generator(Rng);
            Assert.Equal(ArithOperator.Subtraction, op);
            Assert.True(x!.Value - y!.Value >= 0, $"{x}-{y} should be >= 0");
        }
    }

    [Fact]
    public void Sub20_NeverGoesNegative()
    {
        var def = ArithTaskRegistry.Simple.Single(d => d.Skills.Contains(Skill.Math.Sub20));

        for (var i = 0; i < 200; i++)
        {
            var (x, y, _, op) = def.Generator(Rng);
            Assert.Equal(ArithOperator.Subtraction, op);
            Assert.True(x!.Value - y!.Value >= 0, $"{x}-{y} should be >= 0");
        }
    }

    [Fact]
    public void Turbo10_AlwaysLeavesExactlyOneOperandUnknown()
    {
        var def = ArithTaskRegistry.Turbo.Single(d => d.Skills.Contains(Skill.Math.Turbo10));

        for (var i = 0; i < 200; i++)
        {
            var (x, y, z, _) = def.Generator(Rng);
            var knownCount = (x.HasValue ? 1 : 0) + (y.HasValue ? 1 : 0) + (z.HasValue ? 1 : 0);
            Assert.Equal(2, knownCount);
        }
    }
}
