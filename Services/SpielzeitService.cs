using System.Text.Json;
using Kidz2Learn.Model;
using Microsoft.JSInterop;

namespace Kidz2Learn.Services;

/// <summary>
///     Earns game time (<see cref="Spielzeit" />) from every point the <see cref="ScoreService" />
///     hands out and from solved 💣 tasks, keeps it in localStorage and hands it to the game-time
///     guard on the laptop (<c>k4l-waechter</c> on 127.0.0.1:8768, outside this repo), which
///     freezes and ends the games and hears from a heartbeat whether Kidz4Learn is open. Only
///     devices opened once with <c>?spiele=...</c> use it; their bar is the game-time bar instead
///     of the YouTube one.
/// </summary>
/// <remarks>
///     Deliberately easy to cheat - the minutes field is editable, the console offers
///     <c>k4l.zeit(n)</c>, the guard's address can be typed by hand, localStorage can be edited - and nothing
///     here guards against it. Every way leaves a mismatch instead: telemetry records each credited
///     fill and each payout with the minutes it carried, the handler logs what it received, and the
///     games count their own playtime.
/// </remarks>
public sealed class SpielzeitService : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IJSInProcessRuntime _js;
    private readonly ScoreService _score;
    private readonly TelemetryService _telemetry;
    private Spielzeit _spielzeit = new(Heute);
    private DotNetObjectReference<SpielzeitService>? _ref;

    public SpielzeitService(IJSRuntime js, ScoreService score, TelemetryService telemetry)
    {
        _js = (IJSInProcessRuntime)js;
        _score = score;
        _telemetry = telemetry;
        _score.OnPunkte += HandlePunkte;
    }

    public event Action? OnChange;

    /// <summary>Minutes were just credited (a full bar paid out) - for the bar's animation.</summary>
    public event Action? OnGutschrift;

    /// <summary>This device has games (opened once with <c>?spiele=...</c>): the bar is the game-time bar.</summary>
    public bool Aktiv { get; private set; }

    /// <summary>The guard has frozen the games and waits for minutes (from its answer to the heartbeat).</summary>
    public bool SpielEingefroren { get; private set; }

    public float Punkte => _spielzeit.Punkte;
    public int Ziel => _spielzeit.Ziel;
    public bool MauerWeg => _spielzeit.MauerWeg;
    public bool BrauchtBombe => _spielzeit.BrauchtBombe;
    public int Fuellungen => _spielzeit.Fuellungen;

    public int Minuten
    {
        get => _spielzeit.Minuten;
        set
        {
            _spielzeit.Minuten = Math.Max(0, value);
            Speichern();
        }
    }

    private static DateOnly Heute => DateOnly.FromDateTime(DateTime.Now);

    /// <summary>Loads the stored state and applies the <c>spiele</c> query parameter, if present.</summary>
    public void Init(string? spieleQuery)
    {
        try
        {
            var json = _js.Invoke<string?>("k4l_spielzeitLaden");
            // Each app start begins with an empty bar; only the day's count and credited minutes carry over.
            if (json is not null && JsonSerializer.Deserialize<SpielzeitStand>(json, JsonOptions) is { } stand)
                _spielzeit = new Spielzeit(Heute, stand with { Punkte = 0, MauerWeg = false });
        }
        catch (Exception e) when (e is JsonException or JSException or NotSupportedException)
        {
            Console.WriteLine($"[spielzeit] stored state unreadable, starting over: {e.Message}");
        }

        Aktiv = _js.Invoke<string>("k4l_spiele", spieleQuery ?? "") != "";
        if (Aktiv)
        {
            _ref = DotNetObjectReference.Create(this);
            _js.InvokeVoid("k4l_spielzeitStart", _ref);
        }
    }

    /// <summary>
    ///     A 💣 task was solved - called after its points were added, so they fill the current bar
    ///     at most; a payout always leaves the bar empty.
    /// </summary>
    public void BombeGeloest()
    {
        if (!_spielzeit.MauerWeg)
            _telemetry.RecordSpielzeit(new SpielzeitEintrag(DateTime.Now, "Bombe", Fuellung: _spielzeit.Fuellungen + 1));
        Gutgeschrieben(_spielzeit.BombeGeloest(Heute));
        Speichern();
    }

    /// <summary>
    ///     Hands every unpaid minute to the guard, which thaws frozen games with them; the games
    ///     themselves start as usual. The minutes stay here when the guard doesn't answer.
    /// </summary>
    public async Task<bool> AuszahlenAsync()
    {
        var minuten = Minuten;
        if (!await ((IJSRuntime)_js).InvokeAsync<bool>("k4l_zeitGeben", minuten))
            return false;
        SpielEingefroren = false;
        _telemetry.RecordSpielzeit(new SpielzeitEintrag(DateTime.Now, "Auszahlung", minuten));
        Minuten = 0;
        await _telemetry.FlushAsync();
        return true;
    }

    [JSInvokable]
    public void KonsoleZeit(int minuten)
    {
        Minuten = minuten;
    }

    [JSInvokable]
    public void WaechterStatus(bool eingefroren)
    {
        if (SpielEingefroren == eingefroren)
            return;
        SpielEingefroren = eingefroren;
        OnChange?.Invoke();
    }

    private void HandlePunkte(float punkte)
    {
        Gutgeschrieben(_spielzeit.AddPunkte(punkte, Heute));
        Speichern();
    }

    private void Gutgeschrieben(int fuellungen)
    {
        if (fuellungen > 0)
            OnGutschrift?.Invoke();
        for (var i = fuellungen - 1; i >= 0; i--)
            _telemetry.RecordSpielzeit(new SpielzeitEintrag(DateTime.Now, "Gutschrift", Spielzeit.MinutenProFuellung,
                Fuellung: _spielzeit.Fuellungen - i));
    }

    private void Speichern()
    {
        try
        {
            _js.InvokeVoid("k4l_spielzeitSpeichern", JsonSerializer.Serialize(_spielzeit.Stand, JsonOptions));
        }
        catch (JSException)
        {
        }

        OnChange?.Invoke();
    }

    public void Dispose()
    {
        _score.OnPunkte -= HandlePunkte;
        _ref?.Dispose();
    }
}

/// <summary>A game-time event in the telemetry session: a credited fill, a solved 💣 or a payout.</summary>
/// <param name="Fuellung">The day's fill the event belongs to (1 = first).</param>
public sealed record SpielzeitEintrag(DateTime Time, string Art, int Minuten = 0, int Fuellung = 0);
