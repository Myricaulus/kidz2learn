using System.Diagnostics;
using Kidz2Learn.Model;
using Kidz2Learn.Model.Lesen;
using Kidz2Learn.Model.Tasks;
using Kidz2Learn.Pages.SilbenChallenge;
using Kidz2Learn.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Tavenem.Blazor.IndexedDB;

namespace Kidz2Learn.Components.TaskViews;

/// <summary>
///     "abc-sortieren" view: the child drags the word cards into ABC order (or taps two cards to
///     swap them) and hands the order in with "Prüfen". Two tries: the first wrong one only says
///     so, with no hint; the second shows for <see cref="AufloesungMs" /> which cards are in the
///     wrong place and which letters decide the order, then moves on. Every check is scored and
///     observed (<see cref="AbcSortieren.Beobachtung" />).
/// </summary>
public partial class AbcSortierenView : ComponentBase, ITaskView, IAsyncDisposable
{
    private const int CorrectDelayMs = 1400;
    private const int AufloesungMs = 5000;

    private enum Phase
    {
        Sortieren,
        Geloest,
        Aufloesung
    }

    [Parameter] public IChosenTask ChosenTask { get; set; } = null!;
    [Parameter] public EventCallback OnNext { get; set; }

    [Inject(Key = "AufgabenDB")] private IndexedDb AufgabenDb { get; set; } = null!;
    [Inject] private LoggerService Logger { get; set; } = null!;
    [Inject] private TaskSessionController Session { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;

    private IChosenTask? _loadedForTask;
    private AbcAufgabe _aufgabe = new(1, 0, 0, []);
    private List<string> _reihenfolge = [];
    private ElementReference _liste;
    private IJSObjectReference? _sortierListe;
    private DotNetObjectReference<AbcSortierenView>? _selfRef;
    private CancellationTokenSource _timer = new();
    private readonly Stopwatch _seitAnzeige = new();
    private Phase _phase;
    private int _versuch;
    private int? _ausgewaehlt;
    private int _zuege;
    private int? _ersterZugMs;
    private bool _isProcessing;
    private string? _feedbackText;
    private int _correctCount;
    private int _wrongCount;

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(ChosenTask, _loadedForTask))
            return;
        _loadedForTask = ChosenTask;

        _timer.Cancel();
        _timer.Dispose();
        _timer = new CancellationTokenSource();
        _aufgabe = (AbcAufgabe)ChosenTask.Payload;
        _reihenfolge = [.. _aufgabe.Woerter];
        _phase = Phase.Sortieren;
        _versuch = 1;
        _ausgewaehlt = null;
        _zuege = 0;
        _ersterZugMs = null;
        _isProcessing = false;
        _feedbackText = null;
        _seitAnzeige.Restart();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;
        _selfRef = DotNetObjectReference.Create(this);
        _sortierListe = await JS.InvokeAsync<IJSObjectReference>("k4l_sortierListe", _liste, _selfRef);
    }

    private string KartenClass(string wort, int index)
    {
        if (_phase == Phase.Aufloesung)
            return _aufgabe.Richtig[index] == wort ? "abc-richtig" : "abc-falsch";
        if (_phase == Phase.Geloest)
            return "abc-richtig";
        return _ausgewaehlt == index ? "abc-ausgewaehlt" : string.Empty;
    }

    [JSInvokable]
    public void Verschoben(int von, int nach)
    {
        if (_phase != Phase.Sortieren || von == nach || von < 0 || nach < 0 ||
            von >= _reihenfolge.Count || nach >= _reihenfolge.Count)
            return;
        var wort = _reihenfolge[von];
        _reihenfolge.RemoveAt(von);
        _reihenfolge.Insert(nach, wort);
        _ausgewaehlt = null;
        NoteZug();
        StateHasChanged();
    }

    /// <summary>Tap one card, then another, and the two swap places; tapping the same card again lets go.</summary>
    [JSInvokable]
    public void Angetippt(int index)
    {
        if (_phase != Phase.Sortieren || index < 0 || index >= _reihenfolge.Count)
            return;
        if (_ausgewaehlt is not { } erste)
            _ausgewaehlt = index;
        else
        {
            _ausgewaehlt = null;
            if (erste != index)
            {
                (_reihenfolge[erste], _reihenfolge[index]) = (_reihenfolge[index], _reihenfolge[erste]);
                NoteZug();
            }
        }
        StateHasChanged();
    }

    private void NoteZug()
    {
        _zuege++;
        _ersterZugMs ??= (int)_seitAnzeige.ElapsedMilliseconds;
    }

    private async Task Pruefen()
    {
        if (_phase != Phase.Sortieren || _isProcessing)
            return;
        _isProcessing = true;
        _ausgewaehlt = null;

        var richtig = _aufgabe.IstRichtig(_reihenfolge);
        var antwort = _reihenfolge.ToList();
        var daten = AbcSortieren.Beobachtung(_aufgabe, antwort, _zuege, _ersterZugMs);
        var detail = $"[{string.Join(", ", _aufgabe.Woerter)}] → [{string.Join(", ", antwort)}]";
        _zuege = 0;

        var db = AufgabenDb["LeseAufgaben"] ?? throw new InvalidOperationException("LeseAufgaben store missing");
        var id = SilbenLog.GenId(string.Join("-", _aufgabe.Richtig), Skill.ReadAbcSort);
        var log = await db.GetItemAsync<SilbenLog>(id) ?? new SilbenLog { Id = id };
        log.Wort = $"ABC: {string.Join(" < ", _aufgabe.Richtig)}";

        if (richtig)
        {
            _phase = Phase.Geloest;
            _correctCount++;
            _feedbackText = "Richtig!";
            log.Falsch = _versuch - 1;
            log.Kompetenz.AddRichtig();
            var points = AbcSortieren.Punkte(_aufgabe.Woerter.Count, _versuch);
            await Session.RecordSuccess(ChosenTask, log.Kompetenz, points, points, detail, daten);
            WeiterNach(CorrectDelayMs);
        }
        else
        {
            _wrongCount++;
            log.Falsch = _versuch;
            log.Kompetenz.AddFalsch();
            await Session.RecordFailure(ChosenTask, log.Kompetenz, 0, 0, detail, daten);
            if (_versuch == 1)
            {
                _versuch = 2;
                _feedbackText = "Falsch, letzter Versuch";
            }
            else
            {
                _phase = Phase.Aufloesung;
                _feedbackText = "Leider falsch. Achte auf die blauen Buchstaben!";
                WeiterNach(AufloesungMs);
            }
        }

        if (!PlayMode.Koop)
            await db.StoreItemAsync(log);
        Logger.Log(log.ToRenderFragment());
        _isProcessing = false;
        StateHasChanged();
    }

    private void WeiterNach(int ms)
    {
        var token = _timer.Token;
        _ = Task.Delay(ms, token).ContinueWith(async t =>
        {
            if (!t.IsCanceled)
                await InvokeAsync(() => OnNext.InvokeAsync());
        });
    }

    public async ValueTask DisposeAsync()
    {
        _timer.Cancel();
        _timer.Dispose();
        if (_sortierListe is not null)
        {
            try
            {
                await _sortierListe.InvokeVoidAsync("dispose");
                await _sortierListe.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Page gone - nothing left to detach.
            }
        }
        _selfRef?.Dispose();
    }
}
