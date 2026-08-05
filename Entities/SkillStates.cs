using System.Text.Json.Serialization;
using Kidz2Learn.Model;
using Kidz2Learn.Shared;
using Tavenem.DataStorage;

namespace Kidz2Learn.Entities;

public sealed record SkillAttempt
{
    public bool Correct {get;set;}
    public int NeededTimeMs {get;set;}
    public float EffectiveDifficulty {get;set;}
    public AttemptFailReason FailReason {get;set;}
}

public enum AttemptFailReason
{
    Unknown,
    Guessed, // Just hit any button. Frustration?
    ThinkingError, // A single specific task failes continously, but others from same category are stable. Utilize specificTaskHistory to determine.
    SkillCeiling, // Missing Knowledge "Schwäche bei Übergang"
    SimpleError, // Maybe missclicked? Maybe distrubed? Maybe wrongly read?
    MissingConcept // Might be the same as SkillCeiling
}

public sealed class SkillState : IIdItem
{
    // 20 was originally sized for a *specific task* history (Kompetenzniveau, e.g. per "5+6") -
    // too small once shared across every task that trains a skill, since many different tasks
    // feed the same SkillState. Bumped to 50 (SkillMigrationHelper v2) so it stays a meaningfully
    // "recent" but not too noisy window.
    public readonly static int AttemptHistorySize = 50;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty; // skillId

    public float Mastery { get; set; }
    public int Attempts { get; set; }
    public int Success { get; set; }
    public int SuccessRow { get; set; }
    public int SuccessFail { get; set; }
    public float EffectiveDifficulty {get;set;}
    public DateTime LastAttempt {get;set;}

    [JsonConverter(typeof(RingBufferJsonConverter<SkillAttempt>))]
    public RingBuffer<SkillAttempt> AttemptsHistory {get;set;} = new(AttemptHistorySize);


   public bool Equals(IIdItem? other)
    {
        return Id == other?.Id;
    }

    public string DisplayName => $"{Id}:{Mastery:2}";

    /// <summary>
    ///     "Forgetful" accuracy over just <see cref="AttemptsHistory"/> (the last <see cref="AttemptHistorySize"/>
    ///     attempts for this skill, oldest ones fall out automatically) instead of the lifetime
    ///     <see cref="Attempts"/>/<see cref="Success"/> totals - old mistakes stop counting once
    ///     enough new attempts have happened. Same "at least 5 attempts" floor as
    ///     <see cref="Kompetenzniveau.GetProzentValue"/> uses for the analogous per-task metric, so
    ///     both read the same "not enough data yet" as null/"--%" instead of a misleading 100%.
    /// </summary>
    public float? RecentAccuracy
    {
        get
        {
            if (AttemptsHistory.Count < 5)
                return null;

            var correct = 0;
            for (var i = 0; i < AttemptsHistory.Count; i++)
                if (AttemptsHistory[i].Correct)
                    correct++;

            return (float)correct / AttemptsHistory.Count;
        }
    }
}

public sealed class SkillMeta : IIdItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "meta";

    public int SchemaVersion { get; set; } = 1;
    public bool Initialized { get; set; }

    public bool Equals(IIdItem? other)
    {
        return Id == other?.Id;
    }
}