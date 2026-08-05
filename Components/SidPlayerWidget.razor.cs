using System.Net;
using System.Net.Http.Json;
using Kidz2Learn.Services;
using Microsoft.AspNetCore.Components;

namespace Kidz2Learn.Components;

public partial class SidPlayerWidget : ComponentBase
{
    private bool _isStarted;
    private List<string>? _sidFiles;
    [Inject] private HttpClient Http { get; set; } = null!;
    [Inject] public SidWidgetService Player { get; set; } = null!;
    public string SidTitle { get; set; } = string.Empty;

    private bool _isPaused;
    private Random _random = new();
    private double _volume;


    protected override async Task OnInitializedAsync()
    {
        Player.OnVolumeChanged += ApplyVolume;
        await Player.SetVolume(0.4); // seeds SidWidgetService's base volume, cascades via ApplyVolume below
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

    // Reacts to SidWidgetService.OnVolumeChanged (manual slider moves *and* a challenge page's
    // Duck()/Restore()) by pushing the effective volume into the actual JS SID player + this
    // widget's own slider position. Manual slider drags themselves go through Player.SetVolume
    // (see the .razor markup), which loops back here - single source of truth either way.
    private async Task ApplyVolume(double volume)
    {
        _volume = volume;
        await SidPlayer.SetVolume(volume / 4);
        StateHasChanged();
    }

    public void Dispose()
    {
        Player.OnVolumeChanged -= ApplyVolume;
    }
}