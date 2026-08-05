using Kidz2Learn.Model.Tasks;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Kidz2Learn.Tests;

public class TaskPresentationRegistryTests
{
    private sealed class FakeTaskView : ITaskView
    {
        public IChosenTask Task { get; set; } = null!;
        public EventCallback OnNext { get; set; }
    }

    private sealed class NotATaskView;

    [Fact]
    public void Register_ThenResolve_ReturnsTheRegisteredType()
    {
        var view = $"test-view-{Guid.NewGuid()}";

        TaskPresentationRegistry.Register(view, typeof(FakeTaskView));

        Assert.Equal(typeof(FakeTaskView), TaskPresentationRegistry.Resolve(view));
    }

    [Fact]
    public void Resolve_UnknownView_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            TaskPresentationRegistry.Resolve($"no-such-view-{Guid.NewGuid()}"));
    }

    [Fact]
    public void Register_TypeNotImplementingITaskView_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            TaskPresentationRegistry.Register($"bad-view-{Guid.NewGuid()}", typeof(NotATaskView)));
    }
}
