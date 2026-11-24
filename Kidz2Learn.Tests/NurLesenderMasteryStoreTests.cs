using Kidz2Learn.Model;
using Xunit;

namespace Kidz2Learn.Tests;

public class NurLesenderMasteryStoreTests
{
    [Fact]
    public async Task Adjust_LeavesTheInnerStoreUntouched_AndReportsNoChange()
    {
        var inner = new FakeSkillMasteryStore(new Dictionary<string, float> { [Skill.ReadAbcSort] = 0.6f });
        var store = new NurLesenderMasteryStore(inner);

        var change = await store.Adjust(Skill.ReadAbcSort, Difficulty.Normal, 1000, new Kompetenzniveau(), true, 1f);

        Assert.Empty(inner.AdjustCalls);
        Assert.Equal(new MasteryChange(0.6f, 0.6f), change);
    }

    [Fact]
    public async Task Reading_GoesThroughToTheInnerStore()
    {
        var inner = new FakeSkillMasteryStore(new Dictionary<string, float> { [Skill.ReadAbcSort] = 0.6f });

        var views = await new NurLesenderMasteryStore(inner).GetSkillViewEnumerableAsync();

        Assert.Equal(0.6f, views.Single(v => v.State.Id == Skill.ReadAbcSort).State.Mastery);
    }
}
