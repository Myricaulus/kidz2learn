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
    public readonly static int AttemptHistorySize = 20;

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