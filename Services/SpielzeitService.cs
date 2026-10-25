using System.Text.Json;
using Kidz2Learn.Model;
using Microsoft.JSInterop;

namespace Kidz2Learn.Services;

/// <summary>
///     Earns game time (<see cref="Spielzeit" />) from every point the <see cref="ScoreService" />
///     hands out and from solved 💣 tasks, keeps it in localStorage and hands it to a game on the
///     laptop through a <c>k4l-spiel://&lt;spiel&gt;?min=&lt;minuten&gt;</c> link (the handler there
///     starts, freezes and ends the game). Only devices opened once with
///     <c>?spiele=steam,heroic,prism</c> show game buttons.
/// </summary>
/// <remarks>
///     Deliberately easy to cheat - the minutes field is editable, the console offers
///     <c>k4l.zeit(n)</c>, the link can be typed by hand, localStorage can be edited - and nothing
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

    /// <summary>Games this device has buttons for (<c>steam</c>, <c>heroic</c>, <c>prism</c>).</summary>
    public IReadOnlyList<string> Spiele { get; private set; } = [];

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
            if (json is not null)
                _spielzeit = new Spielzeit(Heute, JsonSerializer.Deserialize<SpielzeitStand>(json, JsonOptions));
        }
        catch (Exception e) when (e is JsonException or JSException or NotSupportedException)
        {
            Console.WriteLine($"[spielzeit] stored state unreadable, starting over: {e.Message}");
        }

        Spiele = _js.Invoke<string>("k4l_spiele", spieleQuery ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (Spiele.Count > 0)
        {
            _ref = DotNetObjectReference.Create(this);
            _js.InvokeVoid("k4l_spielzeitKonsole", _ref);
        }
    }

    /// <summary>A 💣 task was solved - called before its points are added, so they carry over.</summary>
    public void BombeGeloest()
    {
        var mauerWarDa = !_spielzeit.MauerWeg;
        Gutgeschrieben(_spielzeit.BombeGeloest(Heute));
        if (mauerWarDa)
            _telemetry.RecordSpielzeit(new SpielzeitEintrag(DateTime.Now, "Bombe", Fuellung: _spielzeit.Fuellungen + 1));
        Speichern();
    }

    /// <summary>Hands every unpaid minute to <paramref name="spiel" />'s handler on this laptop.</summary>
    public async Task AuszahlenAsync(string spiel)
    {
        var minuten = Minuten;
        _telemetry.RecordSpielzeit(new SpielzeitEintrag(DateTime.Now, "Auszahlung", minuten, Spiel: spiel));
        Minuten = 0;
        await _telemetry.FlushAsync();
        _js.InvokeVoid("k4l_oeffneSpiel", $"k4l-spiel://{Uri.EscapeDataString(spiel)}?min={minuten}");
    }

    [JSInvokable]
    public void KonsoleZeit(int minuten)
    {
        Minuten = minuten;
    }

    private void HandlePunkte(float punkte)
    {
        Gutgeschrieben(_spielzeit.AddPunkte(punkte, Heute));
        Speichern();
    }

    private void Gutgeschrieben(int fuellungen)
    {
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
public sealed record SpielzeitEintrag(DateTime Time, string Art, int Minuten = 0, int Fuellung = 0, string? Spiel = null);
