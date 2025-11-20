using System.Threading.Tasks;

namespace Kidz2Learn.Services;

public class SidWidgetService
{
    public event Func<double, Task>? OnVolumeChanged;

    private double _volume = 1.0;

    public double Volume => _volume;

    public async Task SetVolume(double value)
    {
        _volume = Math.Clamp(value, 0.0, 1.0);
        if (OnVolumeChanged != null)
        {
            await OnVolumeChanged.Invoke(_volume);
        }
    }
}