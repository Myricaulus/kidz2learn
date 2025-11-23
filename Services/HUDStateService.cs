public class HUDStateService
{
    public event Action? OnChange;

    public int ComboCount { get; private set; }
    public bool ShowTimer { get; private set; }
    public int Difficulty { get; private set; }

    public void SetCombo(int v) { ComboCount = v; OnChange?.Invoke(); }
    public void IncrementCombo() { ComboCount++; OnChange?.Invoke(); }
    public void SetTimer(bool v) { ShowTimer = v; OnChange?.Invoke(); }
    public void SetDifficulty(int v) { Difficulty = v; OnChange?.Invoke(); }

    public void ResetAll()
    {
        ComboCount = 0;
        ShowTimer = false;
        Difficulty = 0;
        OnChange?.Invoke();
    }
}