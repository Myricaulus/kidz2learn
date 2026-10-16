using Kidz2Learn.Model;

namespace Kidz2Learn.Services;

public class HudStateService
{
    public event Action? OnChange;

    public int ComboCount { get; private set; }
    public bool ShowTimer { get; private set; }
    /// <summary>The current TaskHost task's difficulty; null while no task is on screen.</summary>
    public Difficulty? Difficulty { get; private set; }

    public void SetCombo(int v) { ComboCount = v; OnChange?.Invoke(); }
    public void IncrementCombo() { ComboCount++; OnChange?.Invoke(); }
    public void SetTimer(bool v) { ShowTimer = v; OnChange?.Invoke(); }
    public void SetDifficulty(Difficulty? v) { Difficulty = v; OnChange?.Invoke(); }

    public void ResetAll()
    {
        ComboCount = 0;
        ShowTimer = false;
        Difficulty = null;
        OnChange?.Invoke();
    }
}