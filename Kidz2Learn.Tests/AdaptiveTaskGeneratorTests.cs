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
        // "arith-turbo" (the 3-minute TurboArithChallenge page's definition) has no
        // TaskPresentationRegistry entry - the mixer runs Turbo10 as "turbo-runde" instead.
        // ChooseAnyAsync must not hand it back, or TaskHost's TaskPresentationRegistry.Resolve would throw.
        var generator = new AdaptiveTaskGenerator(new FakeSkillMasteryStore(), new Random(7));

        for (var i = 0; i < 400; i++)
        {
            var chosen = await generator.ChooseAnyAsync();
            Assert.True(TaskPresentationRegistry.IsRegistered(chosen.View));
        }
    }

    [Fact]
    public async Task ChooseAnyAsync_CountsEveryGroupOnce_HoweverManyDefinitionsItHas()
    {
        // read_story_math has five definitions, read_wh_question and read_riddle two each. Per
        // group, only its easiest level counts (2 vs. 1 vs. 1 -> inverted weights 1 : 2 : 2), so the
        // word problems come up a fifth of the time instead of five ninths.
        var generator = new AdaptiveTaskGenerator(new FakeSkillMasteryStore(), new Random(11));
        var counts = new Dictionary<string, int>();
        const int runs = 3000;

        for (var i = 0; i < runs; i++)
        {
            var chosen = await generator.ChooseAnyAsync([Skill.ReadStoryMath, Skill.ReadWhQuestion, Skill.ReadRiddle]);
            counts[chosen.Skills[0]] = counts.GetValueOrDefault(chosen.Skills[0]) + 1;
        }

        Assert.Equal(3, counts.Count);
        Assert.InRange(counts[Skill.ReadStoryMath], runs * 15 / 100, runs * 25 / 100);
        Assert.InRange(counts[Skill.ReadWhQuestion], runs * 35 / 100, runs * 45 / 100);
        Assert.InRange(counts[Skill.ReadRiddle], runs * 35 / 100, runs * 45 / 100);
    }

    [Fact]
    public async Task ChooseAnyAsync_WithinAGroup_PicksEveryUnlockedDefinition_AndRatesDifficultyAgainstTheWholePool()
    {
        var store = new FakeSkillMasteryStore(new Dictionary<string, float> { [Skill.ReadStoryMath] = 0.3f });
        var generator = new AdaptiveTaskGenerator(store, new Random(13));
        var levels = new HashSet<(int, Difficulty, int)>();

        for (var i = 0; i < 600; i++)
        {
            var chosen = await generator.ChooseAnyAsync([Skill.ReadWhQuestion]);
            if (chosen.Skills[0] == Skill.ReadStoryMath)
                levels.Add((chosen.Level, chosen.Difficulty, chosen.LevelsAboveEasiest));
        }

        Assert.Equal([(2, Difficulty.Hard, 1), (3, Difficulty.Extreme, 2)], levels.Order());
    }

    [Fact]
    public async Task ChooseAnyAsync_KeepsHarderLevelsLocked_UntilTheGroupsSkillIsFarEnough()
    {
        var generator = new AdaptiveTaskGenerator(new FakeSkillMasteryStore(), new Random(17));

        for (var i = 0; i < 300; i++)
            Assert.Equal(1, (await generator.ChooseAnyAsync([Skill.ReadNouns])).Level);
    }

    [Theory]
    [InlineData(0.2f, 1)]
    [InlineData(0.3f, 2)]
    [InlineData(0.6f, 3)]
    [InlineData(0.75f, 4)]
    [InlineData(0.9f, 5)]
    public async Task ChooseAnyAsync_UnlocksOneLevelPerThreshold(float mastery, int hardest)
    {
        var store = new FakeSkillMasteryStore(new Dictionary<string, float> { [Skill.ReadNouns] = mastery });
        var generator = new AdaptiveTaskGenerator(store, new Random(19));
        var levels = new HashSet<int>();

        for (var i = 0; i < 1500; i++)
            levels.Add((await generator.ChooseAnyAsync([Skill.ReadNouns])).Level);

        Assert.Equal(Enumerable.Range(1, hardest), levels.Order());
    }

    [Fact]
    public async Task ChooseAnyAsync_LeavesExcludedTasksOut_UnlessNothingElseIsLeft()
    {
        var generator = new AdaptiveTaskGenerator(new FakeSkillMasteryStore(), new Random(23));

        for (var i = 0; i < 200; i++)
        {
            var chosen = await generator.ChooseAnyAsync([Skill.ReadNouns, Skill.ReadRiddle],
                d => d.Skills[0] == Skill.ReadNouns);
            Assert.Equal(Skill.ReadRiddle, chosen.Skills[0]);
        }

        var nurNomen = await generator.ChooseAnyAsync([Skill.ReadNouns], _ => true);
        Assert.Equal(Skill.ReadNouns, nurNomen.Skills[0]);
    }

    [Fact]
    public async Task TryChooseAnyAsync_WithoutSound_NeverPicksAListeningExercise()
    {
        var generator = new AdaptiveTaskGenerator(new FakeSkillMasteryStore(), new Random(29));
        var seenSkills = new HashSet<string>();

        for (var i = 0; i < 300; i++)
        {
            var chosen = await generator.TryChooseAnyAsync([Skill.ReadSyllables, Skill.ReadPrecise, Skill.GraphemPhonem],
                playable: d => !d.BrauchtTon);
            Assert.NotNull(chosen);
            Assert.False(chosen.Definition.BrauchtTon);
            seenSkills.UnionWith(chosen.Skills);
        }

        Assert.Equal([Skill.GraphemPhonem], seenSkills);
    }

    [Fact]
    public async Task TryChooseAnyAsync_ReturnsNull_WhenNothingOfThePoolIsPlayable()
    {
        var generator = new AdaptiveTaskGenerator(new FakeSkillMasteryStore(), new Random(1));

        Assert.Null(await generator.TryChooseAnyAsync([Skill.ReadSyllables, Skill.ReadPrecise],
            exclude: _ => true, playable: d => !d.BrauchtTon));
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
        var changes = await chosen.Success(new Kompetenzniveau(), 1234);

        Assert.Equal(chosen.Skills, store.AdjustCalls.Select(c => c.SkillId));
        Assert.Equal(chosen.Skills, changes.Keys);
        Assert.All(store.AdjustCalls, c => Assert.True(c.Success));
    }

    [Fact]
    public async Task ChooseAnyAsync_Fail_AdjustsEveryDeclaredSkillOfTheChosenTask()
    {
        var store = new FakeSkillMasteryStore();
        var generator = new AdaptiveTaskGenerator(store, new Random(1));

        var chosen = await generator.ChooseAnyAsync([Skill.Math.Add15]);
        var changes = await chosen.Fail(new Kompetenzniveau(), 1234);

        Assert.Equal(chosen.Skills, store.AdjustCalls.Select(c => c.SkillId));
        Assert.Equal(chosen.Skills, changes.Keys);
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
