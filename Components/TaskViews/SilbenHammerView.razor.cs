using System.Net;
using System.Text;
using Kidz2Learn.Model;
using Kidz2Learn.Model.Tasks;
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
    [Inject] private SilbenHammerWordCatalog Catalog { get; set; } = null!;

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
    private bool _showWordButtons;
    private bool _isAnimating;
    private bool _wordCompletionAnimating;
    private int _mergeIndex = -1;

    private int _animKey;
    private string _animClass = "";

    // Only the very first burst of the whole page visit shows the "Lade Wort" placeholder -
    // every later burst quietly resolves the next word in the background while the just-finished
    // word stays on screen, then swaps once ready. Matters a lot in practice: fetching the
    // syllable index is now cached (see SilbenHammerWordCatalog), so this is no longer masking a
    // multi-second stall, but even a fast fetch would otherwise flash the placeholder every burst.
    private bool _hasLoadedOnce;

    protected override async Task OnParametersSetAsync()
    {
        if (ReferenceEquals(ChosenTask, _loadedForTask))
            return;
        _loadedForTask = ChosenTask;

        _wordsCompletedThisBurst = 0;
        _syllableIndex = 0;
        _struggledThisSyllable = false;
        _showWordButtons = false;
        _wordCompletionAnimating = false;
        _animClass = "";
        if (!_hasLoadedOnce)
            _currentWord = null;

        var index = await Catalog.GetSyllableIndexAsync();
        var ratingStore = new SilbenHammerRatingStore(AufgabenDb);
        var options = new SilbenHammerSelectorOptions();
        _ratingStore = ratingStore;
        _burstSize = 1 + options.FollowUpRounds;
        _selector = new SilbenHammerSelector(index, ratingStore, _rng, options);

        var next = await _selector.PickNextWordAsync();
        if (next is null)
        {
            await OnNext.InvokeAsync();
            return;
        }

        _currentWord = next;
        _hasLoadedOnce = true;
    }

    private void OnStruggle()
    {
        if (_isAnimating || _currentWord is null)
            return;

        _struggledThisSyllable = true;
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
        if (_struggledThisSyllable)
            await _ratingStore!.RecordStruggledAsync(normalized);
        else
            await _ratingStore!.RecordCleanAsync(normalized);

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
            // Pause on each syllable long enough to read it again before the next one hits -
            // this used to be 160ms, which just blurred the whole word past in one flash.
            await Task.Delay(650);
        }

        // Hold the finished word on screen for a couple of seconds before moving on.
        await Task.Delay(2000);

        Score.AddPoints(5 * _currentWord.Syllables.Length, 8);
        Hud.IncrementCombo();

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
        _showWordButtons = false;
        _animClass = "";

        if (_currentWord is null)
        {
            await OnNext.InvokeAsync();
            return;
        }

        StateHasChanged();
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
