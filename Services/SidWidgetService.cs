using System.Threading.Tasks;

namespace Kidz2Learn.Services;

public class SidWidgetService
{
    public event Func<double, Task>? OnVolumeChanged;

    // The user's own choice (SidPlayerWidget's slider). Kept separate from _duckedVolume so a
    // challenge page temporarily lowering the volume can restore *this* afterwards instead of
    // blowing it away with a hardcoded value - see TECH_DEBT.md, "Musiklautstärke" finding.
    private double _baseVolume = 1.0;

    // Set while a challenge page needs the music quieter (e.g. a listening exercise); null means
    // "no active duck", i.e. Volume falls back to _baseVolume.
    private double? _duckedVolume;

    public double Volume => _duckedVolume ?? _baseVolume;

    /// <summary>Explicit user choice (the widget's volume slider) - cancels any active duck.</summary>
    public async Task SetVolume(double value)
    {
        _baseVolume = Math.Clamp(value, 0.0, 1.0);
        _duckedVolume = null;
        await Notify();
    }

    /// <summary>Temporarily lowers the volume without losing the user's chosen base volume - pair with <see cref="Restore" />.</summary>
    public async Task Duck(double value)
    {
        _duckedVolume = Math.Clamp(value, 0.0, 1.0);
        await Notify();
    }

    /// <summary>Undoes a previous <see cref="Duck" />, returning to the user's chosen base volume.</summary>
    public async Task Restore()
    {
        _duckedVolume = null;
        await Notify();
    }

    private async Task Notify()
    {
        if (OnVolumeChanged != null)
            await OnVolumeChanged.Invoke(Volume);
    }
}
