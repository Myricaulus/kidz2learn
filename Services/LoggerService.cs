using Microsoft.AspNetCore.Components;

public class LoggerService
{
    public int gesamtAnzahl;
    public float erfolgreich;
    public event Action<RenderFragment>? OnLogAppended;

    public void Log(RenderFragment fragment)
    {
        OnLogAppended?.Invoke(fragment);
    }
}