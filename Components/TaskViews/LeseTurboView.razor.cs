using Kidz2Learn.Entities;
using Kidz2Learn.Model;
using Kidz2Learn.Model.Lesen;
using Kidz2Learn.Model.Tasks;
using Kidz2Learn.Model.Tasks.TaskDefs;
using Kidz2Learn.Pages.SilbenChallenge;
using Kidz2Learn.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Tavenem.Blazor.IndexedDB;

namespace Kidz2Learn.Components.TaskViews;

/// <summary>
///     "lese-turbo" view: one word at a time, "echtes Wort oder Quatschwort?" (buttons or ←/→) for
///     <see cref="EventLaunchOptions.TargetDuration" /> after a "Los!", the whole round scored as one
///     attempt at the end (<see cref="LeseTurbo" />). No feedback on speed during the round, only a
///     short red flash on a wrong answer, so the reading keeps its rhythm; a word left for
///     <see cref="LeseTurbo.ZeitProWort" /> counts as wrong. Every right answer earns points even if
///     the round as a whole doesn't count as solved for mastery.
/// </summary>
public partial class LeseTurboView : ComponentBase, ITaskView, IDisposable
{
    private const string LogId = "lese-turbo";
    private const int TickMs = 200;

    private enum Phase
    {
        Start,
        Laeuft,
        Ende
    }

    [Parameter] public IChosenTask ChosenTask { get; set; } = null!;
    [Parameter] public EventCallback OnNext { get; set; }

    [Inject(Key = "AufgabenDB")] private IndexedDb AufgabenDb { get; set; } = null!;
    [Inject] private TaskSessionController Session { get; set; } = null!;

    private readonly Random _rng = new();
    private readonly HashSet<string> _gezeigt = [];
    private readonly List<string> _verlauf = [];
    private readonly List<BeobachtungsItem> _items = [];
    private CancellationTokenSource? _uhr;
    private IChosenTask? _loadedForTask;

    private Phase _phase;
    private int _sekunden;
    private DateTime _start;
    private DateTime _ende;
    private LeseTurboWort _wort = new(string.Empty, true);
    private DateTime _wortStart;
    private ElementReference _feldRef;
    private bool _fokussieren;
    private int _richtig;
    private int _falsch;

    // Bumped on every word; keys the word so its flash animation restarts.
    private int _wortCount;
    private string _blitz = string.Empty;

    private double RestProzent =>
        Math.Clamp((_ende - DateTime.Now).TotalMilliseconds / (_ende - _start).TotalMilliseconds * 100, 0, 100);

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(ChosenTask, _loadedForTask))
            return;
        _loadedForTask = ChosenTask;

        var launch = (EventLaunchOptions)ChosenTask.Payload;
        _sekunden = (int)(launch.TargetDuration ?? TimeSpan.FromSeconds(25)).TotalSeconds;
        _phase = Phase.Start;
        _richtig = 0;
        _falsch = 0;
        _gezeigt.Clear();
        _verlauf.Clear();
        _items.Clear();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_fokussieren || _phase != Phase.Laeuft)
            return;
        _fokussieren = false;
        await _feldRef.FocusAsync();
    }

    private void Start()
    {
        Session.BeginTask();
        _start = DateTime.Now;
        _ende = _start.AddSeconds(_sekunden);
        _phase = Phase.Laeuft;
        _fokussieren = true;
        NaechstesWort(string.Empty);

        _uhr = new CancellationTokenSource();
        _ = Uhr(_uhr.Token);
    }

    private async Task Uhr(CancellationToken token)
    {
        try
        {
            while (DateTime.Now < _ende)
            {
                await Task.Delay(TickMs, token);
                await InvokeAsync(() =>
                {
                    if (_phase == Phase.Laeuft && DateTime.Now - _wortStart > LeseTurbo.ZeitProWort)
                        Werten(null);
                    StateHasChanged();
                });
            }

            await InvokeAsync(Beenden);
        }
        catch (TaskCanceledException)
        {
        }
    }

    private void NaechstesWort(string blitz)
    {
        _wort = LeseTurbo.Naechstes(_rng, _gezeigt);
        _wortStart = DateTime.Now;
        _blitz = blitz;
        _wortCount++;
    }

    private void Taste(KeyboardEventArgs e)
    {
        if (e.Key == "ArrowLeft")
            Antwort(true);
        else if (e.Key == "ArrowRight")
            Antwort(false);
    }

    private void Antwort(bool echt)
    {
        if (_phase == Phase.Laeuft)
            Werten(echt);
    }

    /// <param name="echt">The child's answer, or null when the word timed out.</param>
    private void Werten(bool? echt)
    {
        var richtig = echt == _wort.Echt;
        if (richtig)
            _richtig++;
        else
            _falsch++;

        var sekunden = (DateTime.Now - _wortStart).TotalSeconds;
        _items.Add(new BeobachtungsItem(_wort.Text, _wort.Echt, echt is null ? "zeit" : richtig ? "richtig" : "falsch",
            (int)(sekunden * 1000)));
        _verlauf.Add($"{_wort.Text}{(_wort.Echt ? "" : "*")}{(echt is null ? "⏱" : richtig ? "✓" : "✗")}{sekunden:0.0}");
        NaechstesWort(richtig ? string.Empty : "leseturbo-falsch");
    }

    private async Task Beenden()
    {
        if (_phase != Phase.Laeuft)
            return;
        _phase = Phase.Ende;
        StateHasChanged();

        var db = AufgabenDb["LeseAufgaben"] ?? throw new InvalidOperationException("LeseAufgaben store missing");
        var id = SilbenLog.GenId(LogId, Skill.TurboRead);
        var log = await db.GetItemAsync<SilbenLog>(id) ?? new SilbenLog { Id = id };
        log.Wort = LogId;
        log.Falsch = _falsch;
        // Nonsense words are marked with *, timeouts with ⏱, then the seconds it took.
        var detail = $"{_sekunden} s: {_richtig} richtig, {_falsch} falsch | {string.Join(" ", _verlauf)}";
        var daten = new BeobachtungsDaten(Items: [.. _items]);

        if (LeseTurbo.Geschafft(_richtig, _falsch))
        {
            log.Kompetenz.AddRichtig();
            await Session.RecordSuccess(ChosenTask, log.Kompetenz, _richtig, _richtig, detail, daten);
        }
        else
        {
            log.Kompetenz.AddFalsch();
            await Session.RecordFailure(ChosenTask, log.Kompetenz, _richtig / 2, 0, detail, daten);
        }

        await db.StoreItemAsync(log);
    }

    private async Task Weiter()
    {
        await OnNext.InvokeAsync();
    }

    public void Dispose()
    {
        _uhr?.Cancel();
        _uhr?.Dispose();
    }
}
