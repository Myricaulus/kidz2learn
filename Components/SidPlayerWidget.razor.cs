using System.Net;
using System.Net.Http.Json;
using Kidz2Learn.Services;
using Microsoft.AspNetCore.Components;

namespace Kidz2Learn.Components;

public partial class SidPlayerWidget : ComponentBase
{
    private bool _isStarted = false;
    private List<string>? _sidFiles;
    [Inject] private HttpClient Http { get; set; } = null!;
    [Inject] public SidWidgetService Player { get; set; } = default!;
    public string SidTitle { get; set; } = string.Empty;

    private bool _isPaused;
    private Random _random = new();
    private double _volume;


    protected override async Task OnInitializedAsync()
    {
        Player.OnVolumeChanged += SetVolume;
        await SetVolume(0.4);
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


    public async Task TogglePlay()
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
        _volume = volume;
        await SidPlayer.SetVolume(volume / 4);
        StateHasChanged();
    }

    public void Dispose()
    {
        Player.OnVolumeChanged -= SetVolume;
    }
}