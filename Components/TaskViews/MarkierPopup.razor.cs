using Kidz2Learn.Pages.SilbenChallenge;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Kidz2Learn.Components.TaskViews;

/// <summary>
///     "Mark your mistake" correction popup, extracted verbatim out of the old monolithic
///     SilbenChallenge.razor(.cs) - self-contained word-diff correction UI (marks, letter
///     substitutions, gaps, hints), previously ~15 fields and a dozen handlers living directly on
///     the challenge page. Split out purely for file size/readability (SilbenChallenge.razor was
///     ~650 lines); still Silben-specific, not a generic reusable mechanism - see
///     TASK_PRESENTATION_REDESIGN.md's resolved "offene Frage" on this.
/// </summary>
public partial class MarkierPopup : ComponentBase
{
    [Parameter, EditorRequired] public string CorrectWord { get; set; } = string.Empty;
    [Parameter, EditorRequired] public string WrongWord { get; set; } = string.Empty;

    /// <summary>Raised once the child has correctly identified every required mark/substitution/gap.</summary>
    [Parameter] public EventCallback OnResolved { get; set; }

    [Inject] private IJSRuntime Js { get; set; } = null!;

    private HashSet<int> _requiredMarks = [];
    private Dictionary<int, char> _requiredSubstitutions = [];
    private Dictionary<int, char> _requiredGaps = [];
    private HashSet<int> _markedIndices = [];
    private Dictionary<int, char> _letterCorrections = [];
    private Dictionary<int, char> _insertedLetters = [];
    private int? _openGapIndex;
    private int? _openLetterIndex;
    private bool _needsGapFocus;
    private bool _needsLetterFocus;
    private ElementReference _gapInputRef;
    private ElementReference _letterInputRef;
    private int _failedPopupAttempts;
    private bool _shakeWrongBox;
    private bool _hoverArmed;

    protected override void OnInitialized()
    {
        var requirements = WordDiff.BuildRequirements(CorrectWord, WrongWord);
        _requiredMarks = requirements.RequiredMarks;
        _requiredSubstitutions = requirements.RequiredSubstitutions;
        _requiredGaps = requirements.RequiredGaps;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_needsGapFocus)
        {
            _needsGapFocus = false;
            await _gapInputRef.FocusAsync();
        }

        if (_needsLetterFocus)
        {
            _needsLetterFocus = false;
            await Js.InvokeVoidAsync("k4l_focusAndSelect", _letterInputRef);
        }
    }

    private void OnCorrectBoxEnter()
    {
        _hoverArmed = true;
    }

    private void OpenLetterEdit(int index)
    {
        _openLetterIndex = index;
        _needsLetterFocus = true;
    }

    private void CloseLetterEdit()
    {
        _openLetterIndex = null;
    }

    private void OnLetterInput(int index, ChangeEventArgs e)
    {
        var text = e.Value?.ToString() ?? "";
        if (text.Length > 0)
        {
            _letterCorrections[index] = text[^1];
            _markedIndices.Remove(index);
            _openLetterIndex = null;
        }
        else
        {
            _letterCorrections.Remove(index);
        }
    }

    private void OnLetterKeyDown(KeyboardEventArgs e, int index)
    {
        if (e.Key is "Enter" or "Escape" or "Tab")
            _openLetterIndex = null;
    }

    private void MarkLetterAsExtra(int index)
    {
        _markedIndices.Add(index);
        _letterCorrections.Remove(index);
        _openLetterIndex = null;
    }

    private void OpenGap(int gapIndex)
    {
        _openGapIndex = gapIndex;
        _needsGapFocus = true;
    }

    private void CloseGap()
    {
        _openGapIndex = null;
    }

    private void OnGapInput(int gapIndex, ChangeEventArgs e)
    {
        var text = e.Value?.ToString() ?? "";
        if (text.Length > 0)
            _insertedLetters[gapIndex] = text[^1];
        else
            _insertedLetters.Remove(gapIndex);
    }

    private void OnGapKeyDown(KeyboardEventArgs e, int gapIndex)
    {
        if (e.Key is "Enter" or "Escape" or "Tab")
            _openGapIndex = null;
    }

    private void RemoveGapLetter(int gapIndex)
    {
        _insertedLetters.Remove(gapIndex);
    }

    private async Task OnWeiterClicked()
    {
        var marksOk = _markedIndices.SetEquals(_requiredMarks);
        var substitutionsOk = _requiredSubstitutions.Count == _letterCorrections.Count &&
                               _requiredSubstitutions.All(kv =>
                                   _letterCorrections.TryGetValue(kv.Key, out var typed) &&
                                   char.ToLowerInvariant(typed) == char.ToLowerInvariant(kv.Value));
        var gapsOk = _requiredGaps.Count == _insertedLetters.Count &&
                     _requiredGaps.All(kv =>
                         _insertedLetters.TryGetValue(kv.Key, out var typed) &&
                         char.ToLowerInvariant(typed) == char.ToLowerInvariant(kv.Value));

        if (marksOk && substitutionsOk && gapsOk)
        {
            await OnResolved.InvokeAsync();
            return;
        }

        _failedPopupAttempts++;
        _shakeWrongBox = true;
        StateHasChanged();
        await Task.Delay(500);
        _shakeWrongBox = false;
        StateHasChanged();
    }

    private void ResetMarkierPopup()
    {
        _markedIndices = [];
        _letterCorrections = [];
        _insertedLetters = [];
        _openGapIndex = null;
        _openLetterIndex = null;
    }

    private void GiveHint()
    {
        foreach (var mark in _requiredMarks)
            if (!_markedIndices.Contains(mark))
            {
                _markedIndices.Add(mark);
                return;
            }

        foreach (var marked in _markedIndices)
            if (!_requiredMarks.Contains(marked))
            {
                _markedIndices.Remove(marked);
                return;
            }

        foreach (var (index, expected) in _requiredSubstitutions)
            if (!_letterCorrections.TryGetValue(index, out var typed) ||
                char.ToLowerInvariant(typed) != char.ToLowerInvariant(expected))
            {
                _letterCorrections[index] = expected;
                _markedIndices.Remove(index);
                return;
            }

        foreach (var (gapIndex, expected) in _requiredGaps)
            if (!_insertedLetters.TryGetValue(gapIndex, out var typed) ||
                char.ToLowerInvariant(typed) != char.ToLowerInvariant(expected))
            {
                _insertedLetters[gapIndex] = expected;
                return;
            }
    }

    private string WrongWordRowStyle
    {
        get
        {
            const int comfortableLength = 8;
            var length = WrongWord.Length;
            var scale = length <= comfortableLength ? 1.0 : Math.Max(0.6, (double)comfortableLength / length);
            return $"--k4l-scale:{scale.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        }
    }
}
