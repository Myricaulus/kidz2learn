using Kidz2Learn.Model;
using Kidz2Learn.Model.Tasks;
using Kidz2Learn.Model.Tasks.TaskDefs;
using Xunit;

namespace Kidz2Learn.Tests;

public class AdaptiveTaskGeneratorTests
{
    [Fact]
    public async Task ChooseTaskAsync_WithSimpleSkills_NeverPicksTheTurboTask()
    {
        // Regression test: ArithmeticChallenge used to pass a "simple" category string that never
        // actually filtered anything (see TECH_DEBT.md #1), so it could draw the Turbo10 task -
        // whose generator can leave x or y null - and crash on `number1N!.Value`.
        var generator = new AdaptiveTaskGenerator(new FakeSkillMasteryStore(), new Random(1));

        for (var i = 0; i < 300; i++)
        {
            var task = await generator.ChooseTaskAsync<ArithTaskDefinition>(ArithTaskRegistry.SimpleSkills);
            Assert.DoesNotContain(Skill.Math.Turbo10, task.Task.Skills);
        }
    }

    [Fact]
    public async Task ChooseTaskAsync_WithoutSkillFilter_DrawsFromTheWholeDomainEventually()
    {
        var generator = new AdaptiveTaskGenerator(new FakeSkillMasteryStore(), new Random(1));

        var seenSkills = new HashSet<string>();
        for (var i = 0; i < 300; i++)
        {
            var task = await generator.ChooseTaskAsync<ArithTaskDefinition>();
            seenSkills.UnionWith(task.Task.Skills);
        }

        Assert.Contains(Skill.Math.Turbo10, seenSkills);
        Assert.Contains(Skill.Math.Add15, seenSkills);
    }

    [Fact]
    public async Task ChooseTaskAsync_PrefersTheLessMasteredOfTwoEquallyDifficultSkills()
    {
        // add_10_with_carry and sub_10 are both DifficultyLevel 3, so difficulty alone can't
        // explain a skew - only the mastery weighting can.
        var store = new FakeSkillMasteryStore(new Dictionary<string, float>
        {
            [Skill.Math.Add10WithCarry] = 0.0f,
            [Skill.Math.Sub10] = 1.0f
        });
        var generator = new AdaptiveTaskGenerator(store, new Random(7));

        var addCount = 0;
        var subCount = 0;
        for (var i = 0; i < 400; i++)
        {
            var task = await generator.ChooseTaskAsync<ArithTaskDefinition>(
                [Skill.Math.Add10WithCarry, Skill.Math.Sub10]);
            if (task.Task.Skills.Contains(Skill.Math.Add10WithCarry)) addCount++;
            else subCount++;
        }

        Assert.True(addCount > subCount,
            $"Expected the unmastered skill to be picked more often (add={addCount}, sub={subCount})");
    }

    [Fact]
    public async Task ChooseTaskAsync_UnknownSkillFilter_Throws()
    {
        var generator = new AdaptiveTaskGenerator(new FakeSkillMasteryStore(), new Random(1));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            generator.ChooseTaskAsync<ArithTaskDefinition>(["not-a-real-skill"]));
    }

    [Fact]
    public async Task LearningTask_Success_AdjustsEveryDeclaredSkillOfTheChosenTask()
    {
        var store = new FakeSkillMasteryStore();
        var generator = new AdaptiveTaskGenerator(store, new Random(1));

        var task = await generator.ChooseTaskAsync<ArithTaskDefinition>([Skill.Math.Turbo10]);
        await task.Success(new Kompetenzniveau());

        Assert.Equal(task.Task.Skills, store.AdjustCalls.Select(c => c.SkillId));
        Assert.All(store.AdjustCalls, c => Assert.True(c.Success));
    }

    [Fact]
    public async Task LearningTask_Fail_AdjustsEveryDeclaredSkillOfTheChosenTask()
    {
        var store = new FakeSkillMasteryStore();
        var generator = new AdaptiveTaskGenerator(store, new Random(1));

        var task = await generator.ChooseTaskAsync<ArithTaskDefinition>([Skill.Math.Turbo10]);
        await task.Fail(new Kompetenzniveau());

        Assert.Equal(task.Task.Skills, store.AdjustCalls.Select(c => c.SkillId));
        Assert.All(store.AdjustCalls, c => Assert.False(c.Success));
    }
}

public class ChooseAnyAsyncTests
{
    [Fact]
    public async Task ChooseAnyAsync_WithoutSkillFilter_DrawsFromBothDomains()
    {
        var generator = new AdaptiveTaskGenerator(new FakeSkillMasteryStore(), new Random(1));

        var seenSkills = new HashSet<string>();
        for (var i = 0; i < 400; i++)
        {
            var chosen = await generator.ChooseAnyAsync();
            seenSkills.UnionWith(chosen.Skills);
        }

        Assert.Contains(Skill.Math.Add15, seenSkills);
        Assert.Contains(Skill.ReadPrecise, seenSkills);
    }

    [Fact]
    public async Task ChooseAnyAsync_WithSkillFilter_OnlyReturnsTasksMatchingThoseSkills()
    {
        var generator = new AdaptiveTaskGenerator(new FakeSkillMasteryStore(), new Random(3));
        var allowed = new[] { Skill.Math.Add15, Skill.ReadPrecise };

        for (var i = 0; i < 200; i++)
        {
            var chosen = await generator.ChooseAnyAsync(allowed);
            Assert.Contains(chosen.Skills, s => allowed.Contains(s));
        }
    }

    [Fact]
    public async Task ChooseAnyAsync_NeverReturnsATaskWithAnUnregisteredView()
    {
        // "arith-turbo" (Skill.Math.Turbo10) has no TaskPresentationRegistry entry yet -
        // TurboArithChallenge is a standalone page, not on TaskHost (see TASK_PRESENTATION_REDESIGN.md,
        // "Event-artige Session-Bausteine im Mixer"). ChooseAnyAsync must not hand it back, or
        // TaskHost's TaskPresentationRegistry.Resolve would throw.
        var generator = new AdaptiveTaskGenerator(new FakeSkillMasteryStore(), new Random(7));

        for (var i = 0; i < 400; i++)
        {
            var chosen = await generator.ChooseAnyAsync();
            Assert.True(TaskPresentationRegistry.IsRegistered(chosen.View));
        }
    }

    [Fact]
    public async Task ChooseAnyAsync_UnknownSkillFilter_Throws()
    {
        var generator = new AdaptiveTaskGenerator(new FakeSkillMasteryStore(), new Random(1));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            generator.ChooseAnyAsync(["not-a-real-skill"]));
    }

    [Fact]
    public async Task ChooseAnyAsync_PayloadAndViewMatchTheChosenDefinitionsDomain()
    {
        var generator = new AdaptiveTaskGenerator(new FakeSkillMasteryStore(), new Random(5));

        var arith = await generator.ChooseAnyAsync([Skill.Math.Add15]);
        Assert.Equal("arith-numpad", arith.View);
        var (x, y, _, _) = ((int?, int?, int?, ArithOperator))arith.Payload;
        Assert.NotNull(x);
        Assert.NotNull(y);

        var silben = await generator.ChooseAnyAsync([Skill.ReadPrecise]);
        Assert.Equal("silben-multiple-choice", silben.View);
        var (correct, options) = ((string, string[]))silben.Payload;
        Assert.NotEmpty(correct);
        Assert.NotEmpty(options);
    }

    [Fact]
    public async Task ChooseAnyAsync_Success_AdjustsEveryDeclaredSkillOfTheChosenTask()
    {
        var store = new FakeSkillMasteryStore();
        var generator = new AdaptiveTaskGenerator(store, new Random(1));

        var chosen = await generator.ChooseAnyAsync([Skill.Math.Add15]);
        await chosen.Success(new Kompetenzniveau());

        Assert.Equal(chosen.Skills, store.AdjustCalls.Select(c => c.SkillId));
        Assert.All(store.AdjustCalls, c => Assert.True(c.Success));
    }

    [Fact]
    public async Task ChooseAnyAsync_Fail_AdjustsEveryDeclaredSkillOfTheChosenTask()
    {
        var store = new FakeSkillMasteryStore();
        var generator = new AdaptiveTaskGenerator(store, new Random(1));

        var chosen = await generator.ChooseAnyAsync([Skill.Math.Add15]);
        await chosen.Fail(new Kompetenzniveau());

        Assert.Equal(chosen.Skills, store.AdjustCalls.Select(c => c.SkillId));
        Assert.All(store.AdjustCalls, c => Assert.False(c.Success));
    }
}

public class ArithTaskRegistrySkillGroupTests
{
    [Fact]
    public void SimpleSkills_ExcludesTurboSkill()
    {
        Assert.DoesNotContain(Skill.Math.Turbo10, ArithTaskRegistry.SimpleSkills);
    }

    [Fact]
    public void SimpleSkills_ContainsEverySkillDeclaredBySimpleTasks()
    {
        var expected = ArithTaskRegistry.Simple.SelectMany(d => d.Skills).ToHashSet();

        Assert.Equal(expected, ArithTaskRegistry.SimpleSkills);
    }
}
