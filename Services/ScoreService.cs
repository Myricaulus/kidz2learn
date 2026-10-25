using System;
using System.Threading;
using System.Threading.Tasks;
using Kidz2Learn.Model;

namespace Kidz2Learn.Services;

public class ScoreService : IDisposable
{
    public event Action<float, float>? OnScoreChanged;

    /// <summary>Every point change as handed out, after the Koop factor and before the bar's 0..100 clamp.</summary>
    public event Action<float>? OnPunkte;

    private float _baseScore;
    private float _bonusScore;

    private readonly Timer _bonusDecayTimer;

    private float _decayFactor = 0.03f; 
    private const float MinDecay = 0.1f; 
    private const float MaxDecay = 0.50f; 
    private const float MinBonusThreshold = 0.1f;

    public float BaseScore => _baseScore;
    public float BonusScore => _bonusScore;
    public float TotalScore => Math.Clamp(BaseScore + BonusScore, 0, 100);
    private DateTime? _sessionStart;
    private float _targetSeconds = 300f;
    private int _targetTasks = 20;
    private float TargetTimePerTask => _targetSeconds / _targetTasks;
    private int _completedTasks;

    public ScoreService()
    {
        _bonusDecayTimer = new Timer(_ =>
        {
            if (_bonusScore <= MinBonusThreshold)
            {
                if (_bonusScore != 0)
                {
                    _bonusScore = 0;
                    NotifyChanged();
                }
                return;
            }

            _bonusScore -= Math.Clamp(
                _bonusScore * _decayFactor,
                MinDecay,
                MaxDecay
            );          

            NotifyChanged();
        }, null, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));
    }

    public void AddPoints(int delta, int bonus)
    {
        _completedTasks++;
        if(_sessionStart != null)
        {
            var elapsed = (DateTime.Now - _sessionStart!)?.TotalSeconds!;
            var relSpeed = (float)elapsed /TargetTimePerTask; //TargetTimePerTask = TargetSeconds / TargetTasks;
            // berechne gewünschte Werte (Target) basierend auf Spieler
            var targetSeconds = _targetSeconds * relSpeed;

            // jetzt sanft 20% in Richtung Ziel bewegen
            _targetSeconds += (targetSeconds - _targetSeconds) * 0.2f;
            _decayFactor *= relSpeed;
            Console.WriteLine($"Elapsed:{elapsed} |RelSpe: {relSpeed} |TarSpe: {_targetSeconds} |DecFac: {_decayFactor} |TargTime: {TargetTimePerTask}");
        }
        _sessionStart = DateTime.Now;
        
        // Playing together, the two need twice the points to reach YouTube.
        var factor = PlayMode.Koop ? 0.5f : 1f;
        _baseScore = Math.Clamp(_baseScore + delta * factor, 0, 100);
        _bonusScore = Math.Max(0, _bonusScore + bonus * factor);
        OnPunkte?.Invoke((delta + bonus) * factor);
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        OnScoreChanged?.Invoke(BaseScore, BonusScore);
    }

    public void Dispose()
    {
        _bonusDecayTimer.Dispose();
    }
}
