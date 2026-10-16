using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Kidz2Learn.Services;

/// <summary>One answered attempt, as sent to the telemetry receiver.</summary>
public sealed record TelemetryEntry(
    DateTime Time,
    string View,
    IReadOnlyList<string> Skills,
    string? Detail,
    bool Correct,
    int Points,
    long DurationMs);

/// <summary>
///     Opt-in per-device session log for the parent's own machines. A device is opted in only by
///     opening the app once with <c>?telemetrie=&lt;url&gt;</c>, which <c>k4l_telemetry*</c> in
///     <c>interop.js</c> keeps in localStorage; <c>?telemetrie=off</c> opts out again. On every
///     other device this service never sends anything and never changes app behavior. Local dev runs
///     opt in via the <c>Telemetrie</c> key of the gitignored <c>wwwroot/appsettings.Development.json</c>
///     instead (see <c>MainLayout.razor</c>); <c>App</c> in each session tells them apart from the
///     kids' sessions.
/// </summary>
/// <remarks>
///     Each send carries the whole session so far under one session id, so the receiver just
///     overwrites one file per session and a lost send is healed by the next one. Unsent sessions
///     stay pending in localStorage and are retried on the next send (also from later app starts).
/// </remarks>
public sealed class TelemetryService(IJSRuntime js, ScoreService score, NavigationManager navigation)
{
    private const int TaskSendTimeoutMs = 3000;
    private const int FinalSendTimeoutMs = 4000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _sessionId = $"{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..6]}";
    private readonly DateTime _started = DateTime.Now;
    private readonly List<TelemetryEntry> _entries = [];
    private bool? _enabled;

    /// <summary>Applies the <c>telemetrie</c> query parameter, if present, and retries pending sessions.</summary>
    public async Task InitAsync(string? queryValue)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(queryValue))
                await js.InvokeVoidAsync("k4l_telemetryConfigure", queryValue);
            _enabled = await js.InvokeAsync<bool>("k4l_telemetryEnabled");
            if (_enabled == true)
                await js.InvokeAsync<bool>("k4l_telemetrySendPending", TaskSendTimeoutMs);
        }
        catch (JSException)
        {
            _enabled = false;
        }
    }

    public bool IsEnabled => _enabled == true;

    public void Record(TelemetryEntry entry)
    {
        if (!IsEnabled)
            return;
        _entries.Add(entry);
        _ = SendAsync(TaskSendTimeoutMs);
    }

    /// <summary>
    ///     Sends the session and reports whether it arrived. Always true on devices that aren't opted
    ///     in, so callers can gate on the result without knowing about opt-in.
    /// </summary>
    public async Task<bool> FlushAsync()
    {
        return !IsEnabled || await SendAsync(FinalSendTimeoutMs);
    }

    private async Task<bool> SendAsync(int timeoutMs)
    {
        var payload = new
        {
            SessionId = _sessionId,
            App = navigation.BaseUri,
            Started = _started,
            Sent = DateTime.Now,
            TotalScore = score.TotalScore,
            Entries = _entries
        };
        try
        {
            return await js.InvokeAsync<bool>("k4l_telemetrySend", _sessionId,
                JsonSerializer.Serialize(payload, JsonOptions), timeoutMs);
        }
        catch (JSException)
        {
            return false;
        }
    }
}
