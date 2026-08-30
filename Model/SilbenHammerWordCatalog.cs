using System.Net.Http.Json;
using System.Text.Json;

namespace Kidz2Learn.Model;

/// <summary>
///     Loads and caches wwwroot/data/silben-hammer-words.json - and the <see cref="SilbenHammerSyllableIndex" />
///     built from it - for the lifetime of the page. Scoped (like
///     <see cref="Kidz2Learn.Services.AffirmationService" />), so both the (large) catalog fetch
///     and the syllable-index build happen once, not on every Silbenhammer burst: rebuilding the
///     index from ~6000 words per burst used to be the actual cause of the multi-second "Lade
///     Wort" stall between words (see remarks on <see cref="SilbenHammerSyllableIndex.Build" />).
///     <see cref="SilbenHammerSelector" /> is built on top of the index this returns.
/// </summary>
public sealed class SilbenHammerWordCatalog(HttpClient http)
{
    private const string ManifestUrl = "data/silben-hammer-words.json";

    // Explicit (not the implicit no-args overload) so deserialization doesn't depend on whatever
    // System.Net.Http.Json's default casing behavior happens to be - the generated JSON uses
    // lowercase keys ("word"/"syllables"/"tier"), the C# record uses PascalCase properties.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private Task<IReadOnlyList<SilbenHammerWordEntry>>? _loadTask;
    private Task<SilbenHammerSyllableIndex>? _indexTask;

    public Task<IReadOnlyList<SilbenHammerWordEntry>> GetWordsAsync()
    {
        return _loadTask ??= LoadAsync();
    }

    public Task<SilbenHammerSyllableIndex> GetSyllableIndexAsync()
    {
        return _indexTask ??= BuildIndexAsync();
    }

    private async Task<SilbenHammerSyllableIndex> BuildIndexAsync()
    {
        var words = await GetWordsAsync();
        return SilbenHammerSyllableIndex.Build(words);
    }

    private async Task<IReadOnlyList<SilbenHammerWordEntry>> LoadAsync()
    {
        var words = await http.GetFromJsonAsync<List<SilbenHammerWordEntry>>(ManifestUrl, JsonOptions);
        return words ?? [];
    }
}
