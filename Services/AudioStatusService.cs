using Kidz2Learn.Shared;
using Microsoft.JSInterop;

namespace Kidz2Learn.Services;

/// <summary>
///     Plays the app's audio clips and keeps track of whether audio is available at all. The clips
///     come from the app's server, so without a connection they fail; a failed clip (or the
///     browser going offline) makes it ask the server, and while that doesn't answer,
///     <see cref="Available" /> is false and it asks again every <see cref="RetryInterval" />.
///     <c>TaskHost</c> listens: it ends an open listening exercise without scoring and only picks
///     tasks that work without sound until audio is back.
/// </summary>
public sealed class AudioStatusService(IJSRuntime js) : IDisposable
{
    private const string WordElementId = "audioPlayer";
    private const int ProbeTimeoutMs = 4000;
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(10);

    private DotNetObjectReference<AudioStatusService>? _ref;
    private Timer? _retry;
    private Task? _probe;

    public bool Available { get; private set; } = true;

    /// <summary><see cref="Available" /> changed.</summary>
    public event Action? OnChange;

    /// <summary>
    ///     A word clip (<see cref="PlayWordAsync" />) couldn't be played; raised after
    ///     <see cref="Available" /> has been checked, so a handler already sees the new state.
    /// </summary>
    public event Action? OnWordFailed;

    public async Task InitAsync()
    {
        if (_ref is not null)
            return;
        _ref = DotNetObjectReference.Create(this);
        if (!await js.InvokeAsync<bool>("k4l_watchConnection", _ref))
            await OnOffline();
    }

    /// <summary>Plays a word clip on <c>#audioPlayer</c>; false if it couldn't be played.</summary>
    public async Task<bool> PlayWordAsync(string src)
    {
        if (await PlayAsync(WordElementId, src))
            return true;
        OnWordFailed?.Invoke();
        return false;
    }

    /// <summary>Plays a clip on the given shared audio element; false if it couldn't be played.</summary>
    public async Task<bool> PlayAsync(string elementId, string src)
    {
        // null: an interop.js from before the clip reported back - count that as played.
        if (await js.InvokeAsync<bool?>("k4l_playAudioFile", elementId, src) != false)
            return true;
        await ProbeAsync();
        return false;
    }

    [JSInvokable]
    public Task OnOffline()
    {
        SetAvailable(false);
        return Task.CompletedTask;
    }

    [JSInvokable]
    public Task OnOnline()
    {
        return ProbeAsync();
    }

    // One probe at a time: a failed clip during a running probe waits for its answer instead of
    // asking the server again.
    private Task ProbeAsync()
    {
        return _probe ??= RunProbeAsync();
    }

    private async Task RunProbeAsync()
    {
        try
        {
            SetAvailable(await js.InvokeAsync<bool>("k4l_probeServer", AssetManifests.Affirmations, ProbeTimeoutMs));
        }
        finally
        {
            _probe = null;
        }
    }

    private void SetAvailable(bool available)
    {
        _retry?.Dispose();
        _retry = available ? null : new Timer(_ => _ = ProbeAsync(), null, RetryInterval, RetryInterval);
        if (Available == available)
            return;
        Available = available;
        OnChange?.Invoke();
    }

    public void Dispose()
    {
        _retry?.Dispose();
        _ref?.Dispose();
    }
}
