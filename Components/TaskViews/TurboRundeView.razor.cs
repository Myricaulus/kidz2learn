using Kidz2Learn.Model;
using Kidz2Learn.Model.Tasks;
using Kidz2Learn.Model.Tasks.TaskDefs;
using Kidz2Learn.Services;
using Microsoft.AspNetCore.Components;
using Tavenem.Blazor.IndexedDB;

namespace Kidz2Learn.Components.TaskViews;

/// <summary>
///     "turbo-runde" view: the mixer's short Turbo round - as many Turbo tasks up to 10 as fit into
///     <see cref="EventLaunchOptions.TargetDuration" />, started by the child with "Los!", the whole
///     round scored as one attempt at the end (<see cref="TurboRunde" />). Every right answer earns
///     points even if the round as a whole doesn't count as solved for mastery.
/// </summary>
public partial class TurboRundeView : ComponentBase, ITaskView, IDisposable
{
    private const string LogId = "turbo-runde";
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
    private readonly List<string> _verlauf = [];
    private CancellationTokenSource? _uhr;
    private IChosenTask? _loadedForTask;

    private Phase _phase;
    private int _sekunden;
    private DateTime _start;
    private DateTime _ende;
    private (int? x, int? y, int? z, ArithOperator op) _aufgabe;
    private string _eingabe = string.Empty;
    private ElementReference _eingabeRef;
    private bool _fokussieren;
    private int _richtig;
    private int _falsch;

    // Bumped on every answer; keys the task so its flash animation restarts.
    private int _blitzCount;
    private string _blitz = string.Empty;

    private double RestProzent =>
        Math.Clamp((_ende - DateTime.Now).TotalMilliseconds / (_ende - _start).TotalMilliseconds * 100, 0, 100);

    private string?[] Teile =>
    [
        _aufgabe.x?.ToString(), _aufgabe.op == ArithOperator.Addition ? "+" : "-", _aufgabe.y?.ToString(), "=",
        _aufgabe.z?.ToString()
    ];

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
        _verlauf.Clear();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_fokussieren || _phase != Phase.Laeuft)
            return;
        _fokussieren = false;
        await _eingabeRef.FocusAsync();
    }

    private void Start()
    {
        Session.BeginTask();
        _start = DateTime.Now;
        _ende = _start.AddSeconds(_sekunden);
        _phase = Phase.Laeuft;
        NaechsteAufgabe();

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
                await InvokeAsync(StateHasChanged);
            }

            await InvokeAsync(Beenden);
        }
        catch (TaskCanceledException)
        {
        }
    }

    private void NaechsteAufgabe()
    {
        _aufgabe = ArithTaskRegistry.TurboAufgabe(_rng);
        _eingabe = string.Empty;
        _fokussieren = true;
    }

    private void Pruefen()
    {
        if (_phase != Phase.Laeuft || !int.TryParse(_eingabe, out var wert))
            return;

        var richtig = wert == TurboRunde.Loesung(_aufgabe);
        if (richtig)
            _richtig++;
        else
            _falsch++;
        _verlauf.Add(TurboRunde.Text(_aufgabe, wert) + (richtig ? "✓" : "✗"));
        _blitz = richtig ? "turbo-richtig" : "turbo-falsch";
        _blitzCount++;
        NaechsteAufgabe();
    }

    private async Task Beenden()
    {
        if (_phase != Phase.Laeuft)
            return;
        _phase = Phase.Ende;
        StateHasChanged();

        var db = AufgabenDb["ArithmetikAufgaben"] ?? throw new InvalidOperationException("ArithmetikAufgaben store missing");
        var log = await db.GetItemAsync<ArithemticLog>(LogId) ?? new ArithemticLog { Id = LogId };
        var detail = $"{_sekunden} s: {_richtig} richtig, {_falsch} falsch | {string.Join(" ", _verlauf)}";

        if (TurboRunde.Geschafft(_richtig, _falsch))
        {
            log.Kompetenz.AddRichtig();
            await Session.RecordSuccess(ChosenTask, log.Kompetenz, 2 * _richtig, 2 * _richtig, detail);
        }
        else
        {
            log.Kompetenz.AddFalsch();
            await Session.RecordFailure(ChosenTask, log.Kompetenz, _richtig, 0, detail);
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
