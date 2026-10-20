using Kidz2Learn.Shared.Extensions;
using System.Net;
using System.Net.Http.Json;
using Kidz2Learn.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Kidz2Learn.Components;

/// <param name="Seconds">How long the tune runs before it loops (HVSC song length), null if unknown.</param>
public sealed record SidFile(string File, double? Seconds);

/// <summary>
///     Background SID music: a random tune at start, and at the end of each tune (its HVSC length
///     from <c>sidfiles.json</c>, <see cref="DefaultSeconds" /> if unknown) a random other one.
/// </summary>
public partial class SidPlayerWidget : ComponentBase, IDisposable
{
    private const double DefaultSeconds = 180;

    private bool _isStarted;
    private List<SidFile>? _sidFiles;
    private SidFile? _current;
    private DotNetObjectReference<SidPlayerWidget>? _selfRef;
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
        _sidFiles = await Http.GetFreshJsonAsync<List<SidFile>>("sids/sidfiles.json");
        await TogglePlay();
    }
    
    [JSInvokable]
    public async Task OnSidEnded()
    {
        await InvokeAsync(NextTitle);
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
            var andere = _sidFiles?.Where(s => s != _current).ToList();
            if (andere is { Count: 0 })
                andere = _sidFiles;

            if (andere is { Count: > 0 })
            {
                _current = andere[_random.Next(andere.Count)];
                SidTitle = _current.File;
                _selfRef ??= DotNetObjectReference.Create(this);
                await SidPlayer.OnEnd(_selfRef, _current.Seconds ?? DefaultSeconds);
                await SidPlayer.LoadStart($"sids/{_current.File}", 0);
                _isStarted = true;
                StateHasChanged();
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
        _selfRef?.Dispose();
    }
}