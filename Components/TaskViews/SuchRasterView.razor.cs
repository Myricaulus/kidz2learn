using Kidz2Learn.Entities;
using Kidz2Learn.Model;
using Kidz2Learn.Model.Lesen;
using Kidz2Learn.Model.Tasks;
using Kidz2Learn.Pages.SilbenChallenge;
using Kidz2Learn.Services;
using Microsoft.AspNetCore.Components;
using Tavenem.Blazor.IndexedDB;

namespace Kidz2Learn.Components.TaskViews;

/// <summary>
///     "such-raster" view (Nomen finden, Spiegelbuchstaben): the child taps every matching tile and
///     hands the grid in with "Fertig!". Only that first hand-in is scored
///     (<see cref="SuchRasterWertung" />). A clean grid moves on by itself. Otherwise the mistakes
///     light up for <see cref="BlinkMs" /> and the child has to find and fix them: "Weiter" stays
///     locked until everything is right or <see cref="SperreMs" /> have passed. Pressing it while
///     mistakes are left marks them for good, and the child still fixes them before going on.
/// </summary>
public partial class SuchRasterView : ComponentBase, ITaskView, IDisposable
{
    private const int CorrectDelayMs = 1400;
    private const int BlinkMs = 1500;
    private const int SperreMs = 10_000;

    private enum Phase
    {
        Suchen,
        Korrigieren,
        Hilfe,
        Geloest
    }

    [Parameter] public IChosenTask ChosenTask { get; set; } = null!;
    [Parameter] public EventCallback OnNext { get; set; }

    [Inject(Key = "AufgabenDB")] private IndexedDb AufgabenDb { get; set; } = null!;
    [Inject] private LoggerService Logger { get; set; } = null!;
    [Inject] private TaskSessionController Session { get; set; } = null!;

    private IChosenTask? _loadedForTask;
    private SuchRaster _raster = new(string.Empty, 4, [], 0, string.Empty);
    private readonly HashSet<int> _marked = [];
    private CancellationTokenSource _timers = new();
    private Phase _phase;
    private bool _blinken;
    private bool _sperreVorbei;
    private bool _ohneFehler;
    private string? _feedbackText;
    private int _correctCount;
    private int _wrongCount;

    private bool KnopfFrei => _phase == Phase.Geloest || (_phase == Phase.Korrigieren && _sperreVorbei);

    private bool AllesRichtig => _raster.Felder.Select((f, i) => f.Treffer == _marked.Contains(i)).All(x => x);

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(ChosenTask, _loadedForTask))
            return;
        _loadedForTask = ChosenTask;

        _timers.Cancel();
        _timers.Dispose();
        _timers = new CancellationTokenSource();
        _raster = (SuchRaster)ChosenTask.Payload;
        _marked.Clear();
        _phase = Phase.Suchen;
        _blinken = false;
        _sperreVorbei = false;
        _ohneFehler = false;
        _feedbackText = null;
    }

    private string FeldClass(RasterFeld feld, int index)
    {
        var marked = _marked.Contains(index);
        var zeigeFehler = _phase is Phase.Hilfe or Phase.Geloest || (_phase == Phase.Korrigieren && _blinken);
        if (!zeigeFehler)
            return marked ? "raster-markiert" : string.Empty;
        return (marked, feld.Treffer) switch
        {
            (true, true) => "raster-richtig",
            (true, false) => "raster-falsch",
            (false, true) => "raster-vergessen",
            _ => string.Empty
        };
    }

    private void Toggle(int index)
    {
        if (_phase == Phase.Geloest)
            return;
        if (!_marked.Remove(index))
            _marked.Add(index);

        if (_phase != Phase.Suchen && AllesRichtig)
        {
            _phase = Phase.Geloest;
            _feedbackText = "Jetzt stimmt alles!";
        }
    }

    private async Task Abgeben()
    {
        if (_phase != Phase.Suchen)
            return;

        var falschMarkiert = _marked.Count(i => !_raster.Felder[i].Treffer);
        var vergessen = _raster.Felder.Where((f, i) => f.Treffer && !_marked.Contains(i)).Count();
        var fehler = falschMarkiert + vergessen;
        _ohneFehler = fehler == 0;

        if (_ohneFehler)
        {
            _phase = Phase.Geloest;
            _feedbackText = "Super, alle gefunden!";
        }
        else
        {
            _phase = Phase.Korrigieren;
            _blinken = true;
            _feedbackText = fehler == 1 ? "Ein Fehler - findest du ihn?" : $"{fehler} Fehler - findest du sie?";
            _ = RunTimers(_timers.Token);
        }

        await Record(fehler, falschMarkiert, vergessen);
        StateHasChanged();

        if (_ohneFehler)
            _ = Task.Delay(CorrectDelayMs)
                .ContinueWith(async _ => await InvokeAsync(() => OnNext.InvokeAsync()));
    }

    private async Task RunTimers(CancellationToken token)
    {
        try
        {
            await Task.Delay(BlinkMs, token);
            _blinken = false;
            await InvokeAsync(StateHasChanged);
            await Task.Delay(SperreMs - BlinkMs, token);
            _sperreVorbei = true;
            await InvokeAsync(StateHasChanged);
        }
        catch (TaskCanceledException)
        {
            // Next task or view gone - nothing left to show.
        }
    }

    private async Task Weiter()
    {
        if (!KnopfFrei)
            return;
        if (_phase == Phase.Geloest)
        {
            await OnNext.InvokeAsync();
            return;
        }

        _phase = Phase.Hilfe;
        _feedbackText = "Rot ist falsch, gestrichelt fehlt noch - verbessere es!";
    }

    private async Task Record(int fehler, int falschMarkiert, int vergessen)
    {
        var db = AufgabenDb["LeseAufgaben"] ?? throw new InvalidOperationException("LeseAufgaben store missing");
        var skill = ChosenTask.Skills.First();
        var id = SilbenLog.GenId(_raster.LogKey, skill);
        var log = await db.GetItemAsync<SilbenLog>(id) ?? new SilbenLog { Id = id };
        log.Wort = $"Suchraster {_raster.LogKey}";
        log.Falsch = fehler;
        var markiert = string.Join(", ", _marked.Order().Select(i => _raster.Felder[i].Text));
        var detail = $"{_raster.Ziel}: [{string.Join(", ", _raster.Felder.Select(f => f.Text))}] → [{markiert}] " +
                     $"({falschMarkiert} falsch, {vergessen} vergessen)";

        var fehlerItems = _raster.Felder
            .Select((f, i) => (f, marked: _marked.Contains(i)))
            .Where(x => x.f.Treffer != x.marked)
            .Select(x => new BeobachtungsItem(x.f.Text, null, x.marked ? "falsch-markiert" : "vergessen", 0))
            .ToList();
        var werte = new Dictionary<string, string>
        {
            ["felder"] = _raster.Felder.Count.ToString(),
            ["spalten"] = _raster.Spalten.ToString(),
            ["treffer"] = _raster.AnzahlTreffer.ToString()
        };
        if (_raster.Eng is { } eng)
            werte["abstand"] = eng ? "eng" : "weit";
        var daten = new BeobachtungsDaten(_raster.Ziel, Werte: werte, Items: fehlerItems);

        var points = SuchRasterWertung.Punkte(_raster.BasisPunkte, fehler);
        if (SuchRasterWertung.Geschafft(fehler))
        {
            _correctCount++;
            log.Kompetenz.AddRichtig();
            await Session.RecordSuccess(ChosenTask, log.Kompetenz, points, points, detail, daten);
        }
        else
        {
            _wrongCount++;
            log.Kompetenz.AddFalsch();
            // Below zero only the base score shrinks; the bonus stays untouched.
            await Session.RecordFailure(ChosenTask, log.Kompetenz, points, 0, detail, daten);
        }

        await db.StoreItemAsync(log);
        Logger.Log(log.ToRenderFragment());
    }

    public void Dispose()
    {
        _timers.Cancel();
        _timers.Dispose();
    }
}
