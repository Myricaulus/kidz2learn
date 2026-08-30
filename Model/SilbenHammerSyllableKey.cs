using System.Text;

namespace Kidz2Learn.Model;

/// <summary>
///     Normalizes a syllable's text into the exact string used as its IndexedDB/rating-store key
///     and its index key everywhere else in Silbenhammer - must be applied consistently, since
///     IndexedDB keys are exact strings ("Son" vs "son" would otherwise be two different rows).
///     Deliberately preserves umlauts/ß (no diacritic stripping) - "ä" and "a" are different sounds.
/// </summary>
public static class SilbenHammerSyllableKey
{
    public static string Normalize(string syllable)
    {
        return syllable.Trim().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }
}
