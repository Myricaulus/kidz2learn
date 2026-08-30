using System.Text.Json.Serialization;
using Tavenem.DataStorage;

namespace Kidz2Learn.Entities;

/// <summary>
///     Per-syllable rating row for the Silbenhammer game mode, stored in IndexedDB store
///     "SilbenHammerRatings". <see cref="Id" /> is the normalized syllable text (see
///     Model.SilbenHammerSyllableKey.Normalize), not a word - every syllable actually read during a
///     Silbenhammer round updates its own row here, independent of the coarse
///     <see cref="SkillState" />/<see cref="Kidz2Learn.Model.ISkillMasteryStore" /> mastery.
/// </summary>
public sealed class SilbenHammerSyllableRating : IIdItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty; // normalized syllable text

    public int CleanStreak { get; set; }

    public bool Equals(IIdItem? other)
    {
        return Id == other?.Id;
    }
}
