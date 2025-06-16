using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;

namespace Kidz2Learn.Components;

public partial class SidPlayerWidget : ComponentBase
{
    private bool _isStarted = false;
    private List<string>? _sidFiles;
    [Inject] private HttpClient Http { get; set; } = null!;
    public string SidTitle { get; set; } = string.Empty;

    private bool _isPaused;
    private Random _random = new();

    protected override async Task OnInitializedAsync()
    {
        await SetVolume(0.25);
        _sidFiles = await Http.GetFromJsonAsync<List<string>>("sids/sidfiles.json");
        await TogglePlay();
    }
    
    private async Task NextTitle()
    {
        await SidPlayer.Stop();
        _isStarted=false;
        _isPaused=false;
        await TogglePlay();
    }


    private async Task TogglePlay()
    {
        if (!_isStarted)
        {
            // Testweise einfach ein hartcodiertes SID File laden
            var sid = _sidFiles?[_random.Next(_sidFiles.Count)];
            
            if (sid is not null)
            {
                SidTitle = sid;
                await SidPlayer.LoadStart($"sids/{sid}", 0);
                _isStarted = true;
            }
        }
        else
        {
            if (!_isPaused)
                await SidPlayer.Pause();
            else
                await SidPlayer.PlayCont();
            _isPaused = !_isPaused;
        }
    }

    private async Task SetVolume(double volume)
    {
        await SidPlayer.SetVolume(volume);
    }
}