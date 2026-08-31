using System.Net;
using System.Text;
using Kidz2Learn.Model;
using Kidz2Learn.Model.Tasks;
using Kidz2Learn.Model.Tasks.TaskDefs;
using Kidz2Learn.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Tavenem.Blazor.IndexedDB;

namespace Kidz2Learn.Components.TaskViews;

/// <summary>
///     "silben-hammer" view: a Silbenhammer "burst" is one fresh target-syllable word plus a few
///     same-syllable follow-ups (<see cref="SilbenHammerSelectorOptions.FollowUpRounds" />),
///     picked internally via <see cref="SilbenHammerSelector" /> and only handed back to
///     <c>TaskHost</c> (via <see cref="OnNext" />) once the whole burst is done - see
///     Model/SilbenHammerSelector.cs and the Silbenhammer plan doc for the full design.
/// </summary>
public partial class SilbenHammerView : ComponentBase, ITaskView
{
    [Parameter] public IChosenTask ChosenTask { get; set; } = null!;
    [Parameter] public EventCallback OnNext { get; set; }

    [Inject(Key = "AufgabenDB")] private IndexedDb AufgabenDb { get; set; } = null!;
    [Inject] private IJSRuntime Js { get; set; } = null!;
    [Inject] private TaskSessionController Session { get; set; } = null!;
    [Inject] private ScoreService Score { get; set; } = null!;
    [Inject] private HudStateService Hud { get; set; } = null!;
    [Inject] private LoggerService Logger { get; set; } = null!;

    private readonly Random _rng = new();

    // Guards the burst-reset below against firing again on incidental re-renders that don't carry
    // a new ChosenTask - same pattern as every other TaskView (see SilbenMultipleChoiceView.razor.cs).
    private IChosenTask? _loadedForTask;

    private SilbenHammerSelector? _selector;
    private ISilbenHammerRatingStore? _ratingStore;
    private int _burstSize;
    private int _wordsCompletedThisBurst;

    private SilbenHammerWordEntry? _currentWord;
    private int _syllableIndex;
    private bool _struggledThisSyllable;
    private bool _wordHadAnyStruggle;
    private bool _showWordButtons;
    private bool _isAnimating;
    private bool _wordCompletionAnimating;
    private int _mergeIndex = -1;

    private int _animKey;
    private string _animClass = "";

    // Whether the whole word (not just the current syllable) is visible for context, per
    // ChosenTask.Difficulty: Normal shows it for the whole word, Hard for a brief peek, Extreme
    // never. _wordGeneration guards the Hard case's delayed hide against a fast child already
    // being on a *different* word by the time the 3s peek would end.
    private bool _showWordContext;
    private int _wordGeneration;

    private bool _showHelp;

    // Only the very first burst of the whole page visit shows the "Lade Wort" placeholder -
    // every later burst quietly resolves the next word in the background while the just-finished
    // word stays on screen, then swaps once ready. Guards against the IndexedDB rating-store
    // round trip in PickNextWordAsync ever flashing that placeholder mid-session (the catalog/
    // index themselves are compile-time data - see SilbenHammerWords.g.cs - so no fetch to guard).
    private bool _hasLoadedOnce;

    protected override async Task OnParametersSetAsync()
    {
        if (ReferenceEquals(ChosenTask, _loadedForTask))
            return;
        _loadedForTask = ChosenTask;

        _wordsCompletedThisBurst = 0;
        _syllableIndex = 0;
        _struggledThisSyllable = false;
        _wordHadAnyStruggle = false;
        _showWordButtons = false;
        _wordCompletionAnimating = false;
        _animClass = "";
        if (!_hasLoadedOnce)
            _currentWord = null;

        var launch = (EventLaunchOptions)ChosenTask.Payload;
        _burstSize = ResolveRoundBudget(launch, ChosenTask.Difficulty);
        var options = new SilbenHammerSelectorOptions { FollowUpRounds = _burstSize - 1 };

        var ratingStore = new SilbenHammerRatingStore(AufgabenDb);
        _ratingStore = ratingStore;
        _selector = new SilbenHammerSelector(SilbenHammerWords.Index, ratingStore, _rng, options);

        var next = await _selector.PickNextWordAsync();
        if (next is null)
        {
            await OnNext.InvokeAsync();
            return;
        }

        _currentWord = next;
        _hasLoadedOnce = true;
        _ = UpdateWordContextVisibilityAsync();
    }

    private void OnStruggle()
    {
        if (_isAnimating || _currentWord is null)
            return;

        _struggledThisSyllable = true;
        _wordHadAnyStruggle = true;
        _animKey++;
        _animClass = "shl-wobble";
    }

    private async Task OnCorrect()
    {
        if (_isAnimating || _currentWord is null)
            return;
        _isAnimating = true;

        var syllable = _currentWord.Syllables[_syllableIndex];
        var normalized = SilbenHammerSyllableKey.Normalize(syllable);
        var clean = !_struggledThisSyllable;
        var streak = clean
            ? await _ratingStore!.RecordCleanAsync(normalized)
            : await _ratingStore!.RecordStruggledAsync(normalized);
        Logger.Log(BuildSyllableLogEntry(_currentWord.Word, syllable, clean, SilbenHammerScoring.ComputeScore(streak)));

        _animKey++;
        _animClass = "shl-glow";
        StateHasChanged();
        await Js.InvokeVoidAsync("k4l_playHammerClang");
        await Task.Delay(450);

        var isLastSyllable = _syllableIndex == _currentWord.Syllables.Length - 1;
        if (isLastSyllable)
        {
            if (_currentWord.Syllables.Length == 1)
                // No syllable-linking to check for a single-syllable word - Button B already did
                // double duty as both "syllable correct" and "word complete".
                await CompleteWordAsync();
            else
                _showWordButtons = true;
        }
        else
        {
            _syllableIndex++;
            _struggledThisSyllable = false;
        }

        _isAnimating = false;
    }

    private void OnRestartWord()
    {
        if (_isAnimating)
            return;

        // Pure navigation - no rating/store side effects, per the confirmed design.
        _syllableIndex = 0;
        _struggledThisSyllable = false;
        _showWordButtons = false;
        _animClass = "";
    }

    private async Task OnWordDone()
    {
        if (_isAnimating)
            return;
        _isAnimating = true;
        await CompleteWordAsync();
        _isAnimating = false;
    }

    private async Task CompleteWordAsync()
    {
        _wordCompletionAnimating = true;
        _mergeIndex = -1;
        StateHasChanged();

        for (var i = 0; i < _currentWord!.Syllables.Length; i++)
        {
            _mergeIndex = i;
            StateHasChanged();
            await Js.InvokeVoidAsync("k4l_playHammerClang");
            // Pause on each syllable long enough to read it again before the next one hits.
            await Task.Delay(650);
        }

        // Hold the finished word on screen for a couple of seconds before moving on.
        await Task.Delay(2000);

        var (basePoints, bonusPoints) = ResolveWordPoints(clean: !_wordHadAnyStruggle, ChosenTask.Difficulty);
        Score.AddPoints(basePoints, bonusPoints);
        Hud.IncrementCombo();

        // Computed before the increment below, so it reads as "how many more after this one".
        var burstRemaining = _burstSize - _wordsCompletedThisBurst - 1;
        Logger.Log(BuildWordDoneLogEntry(_currentWord.Word, _selector!.TargetSyllable,
            _selector.RemainingWordsForTargetSyllable(), burstRemaining));

        _wordCompletionAnimating = false;
        _wordsCompletedThisBurst++;

        if (_wordsCompletedThisBurst >= _burstSize)
        {
            // One RecordSuccess per completed burst (not per syllable/word) - keeps the coarse
            // Skill.SilbenHammer mastery meaningful for the mixer without flooding its streak/time
            // model with many small events. See Model/SilbenHammerSelector.cs remarks.
            await Session.RecordSuccess(ChosenTask, new Kompetenzniveau(), 10, 10);
            await OnNext.InvokeAsync();
            return;
        }

        await LoadNextWordInBurstAsync();
    }

    private async Task LoadNextWordInBurstAsync()
    {
        _currentWord = await _selector!.PickNextWordAsync();
        _syllableIndex = 0;
        _struggledThisSyllable = false;
        _wordHadAnyStruggle = false;
        _showWordButtons = false;
        _animClass = "";

        if (_currentWord is null)
        {
            await OnNext.InvokeAsync();
            return;
        }

        _ = UpdateWordContextVisibilityAsync();
        StateHasChanged();
    }

    private void OnToggleHelp()
    {
        _showHelp = !_showHelp;
    }

    private async Task UpdateWordContextVisibilityAsync()
    {
        _wordGeneration++;
        var myGeneration = _wordGeneration;

        switch (ChosenTask.Difficulty)
        {
            case Difficulty.Normal:
                _showWordContext = true;
                break;
            case Difficulty.Hard:
                _showWordContext = true;
                StateHasChanged();
                await Task.Delay(3000);
                if (myGeneration == _wordGeneration)
                    _showWordContext = false;
                break;
            default:
                _showWordContext = false;
                break;
        }

        StateHasChanged();
    }

    // Difficulty (Normal/Hard/Extreme) is the chooser's own mastery-weighting verdict, separate
    // from the event's static RoundBudget config (EventTaskRegistry) - a harder pick means the
    // learner is doing well on Skill.SilbenHammer overall, so drill a bit longer per burst.
    private static int ResolveRoundBudget(EventLaunchOptions launch, Difficulty difficulty)
    {
        var baseBudget = launch.RoundBudget ?? 4;
        return difficulty switch
        {
            Difficulty.Hard => baseBudget + 1,
            Difficulty.Extreme => baseBudget + 2,
            _ => baseBudget
        };
    }

    // Points per completed word - deliberately small and struggle-sensitive, not scaled by
    // syllable count, so a handful of long words in one burst can't dominate the 0-100 session
    // score bar (MainLayout's Biber-Fortschritt/YouTube-reward threshold) on their own. A clean
    // word pays roughly what one correct multiple-choice answer does elsewhere in the app; one
    // with any struggled syllable pays less. Difficulty is the mixer's own mastery-weighting
    // verdict (see ResolveRoundBudget) - a harder pick is worth a bit more.
    //
    // TODO: let the mastery/difficulty system determine points directly instead of these fixed
    // constants, since it already knows the task's effective difficulty relative to the player -
    // needs a broader change to how LearningTask/TaskSessionController award points across every
    // task type, not just this one, so left as fixed numbers for now.
    private static (int Base, int Bonus) ResolveWordPoints(bool clean, Difficulty difficulty)
    {
        var (basePoints, bonusPoints) = clean ? (5, 6) : (2, 2);
        var multiplier = difficulty switch
        {
            Difficulty.Hard => 1.25,
            Difficulty.Extreme => 1.5,
            _ => 1.0
        };
        return ((int)Math.Round(basePoints * multiplier), (int)Math.Round(bonusPoints * multiplier));
    }

    // Per-syllable entry in the LiveLogger (Components/LiveLogger.razor) - one per "richtig"
    // press, same idea as SilbenLog.ToRenderFragment() for the multiple-choice views, so the
    // mentor can see which syllable was just judged and how its rating moved.
    private static RenderFragment BuildSyllableLogEntry(string word, string syllable, bool clean, int newScore)
    {
        return builder =>
        {
            var i = 0;
            builder.OpenElement(i++, "div");
            builder.AddAttribute(i++, "class", "log-entry silben-hammer-log");
            builder.AddContent(i++, "🔨 ");
            builder.OpenElement(i++, "b");
            builder.AddContent(i++, syllable);
            builder.CloseElement(); // </b>
            builder.AddContent(i++, $" (aus \"{word}\") ");
            builder.OpenElement(i++, "span");
            builder.AddAttribute(i++, "style", $"color: {(clean ? "#3aa757" : "#e0872a")}");
            builder.AddContent(i++, clean ? "sauber ✓" : "gestolpert");
            builder.CloseElement(); // </span>
            builder.AddContent(i, $" → Wertung: {newScore}");
            builder.CloseElement(); // </div>
        };
    }

    // One entry per completed word (Button D / the single-syllable shortcut) - "was man geschafft
    // hat", separate from the per-syllable entries above so both are visible in the log. Also
    // surfaces the burst/selector internals (remaining budget, other unused words that still
    // share the target syllable) for debugging the "does the burst actually stay on one syllable"
    // behavior, not just the player-facing outcome.
    private static RenderFragment BuildWordDoneLogEntry(
        string word, string? targetSyllable, IReadOnlyList<string> remainingCandidates, int burstRemaining)
    {
        const int maxCandidatesShown = 8;

        return builder =>
        {
            var i = 0;
            builder.OpenElement(i++, "div");
            builder.AddAttribute(i++, "class", "log-entry silben-hammer-log silben-hammer-log-done");
            builder.AddContent(i++, "🏁 Wort geschafft: ");
            builder.OpenElement(i++, "b");
            builder.AddContent(i++, word);
            builder.CloseElement(); // </b>

            if (targetSyllable is null)
            {
                builder.CloseElement(); // </div>
                return;
            }

            builder.AddContent(i++, $" (Übungs-Silbe: {targetSyllable}, noch {burstRemaining} im Burst)");

            builder.OpenElement(i++, "div");
            builder.AddAttribute(i++, "style", "font-size:0.85em; color:#777;");
            if (remainingCandidates.Count == 0)
            {
                builder.AddContent(i, $"Keine weiteren Wörter mit \"{targetSyllable}\" übrig.");
            }
            else
            {
                var shown = string.Join(", ", remainingCandidates.Take(maxCandidatesShown));
                var extra = remainingCandidates.Count - maxCandidatesShown;
                var suffix = extra > 0 ? $" (+{extra} weitere)" : "";
                builder.AddContent(i, $"Übrige Wörter mit \"{targetSyllable}\": {shown}{suffix}");
            }
            builder.CloseElement(); // </div>

            builder.CloseElement(); // </div>
        };
    }

    // Whole-word context line (shown per ChosenTask.Difficulty, see UpdateWordContextVisibilityAsync)
    // - the syllable currently being read stands out, the rest of the word is muted so it reads
    // as background context, not competing with the big current-syllable display below it.
    private static string BuildWordContextHtml(SilbenHammerWordEntry word, int currentIndex)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < word.Syllables.Length; i++)
        {
            var cls = i == currentIndex ? "shl-context-current" : "shl-context-other";
            var encoded = WebUtility.HtmlEncode(word.Syllables[i]);
            sb.Append($"<span class=\"{cls}\">{encoded}</span>");
        }

        return sb.ToString();
    }

    // --i is only consumed by .shl-wobble's per-letter stagger (see the <style> block) - the
    // "richtig" hammer glow (.shl-glow) deliberately ignores it and hits every letter of the
    // syllable at once, since the syllable was read as one unit, not spelled out.
    private static string BuildSyllableHtml(string syllable)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < syllable.Length; i++)
        {
            var encoded = WebUtility.HtmlEncode(syllable[i].ToString());
            sb.Append($"<span class=\"shl-letter\" style=\"--i:{i}\">{encoded}</span>");
        }

        return sb.ToString();
    }
}
