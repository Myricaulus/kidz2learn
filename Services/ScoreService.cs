using System.Threading.Tasks;

namespace Kidz2Learn.Services;

public class ScoreService
{
    public event Func<int, Task>? OnScoreChanged;

    private int _score;

    public int Score => _score;

    public void AddPoints(int delta)
    {
        _score = Math.Clamp(_score + delta, 0, 100);
        OnScoreChanged?.Invoke(_score);
    }
}