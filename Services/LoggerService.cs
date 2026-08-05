using Microsoft.AspNetCore.Components;

namespace Kidz2Learn.Services;

public class LoggerService
{
    public event Action<RenderFragment>? OnLogAppended;

    public void Log(RenderFragment fragment)
    {
        OnLogAppended?.Invoke(fragment);
    }
}