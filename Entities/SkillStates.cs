using System.Text.Json.Serialization;
using Tavenem.DataStorage;

namespace Kidz2Learn.Entities;

public sealed class SkillState : IIdItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty; // skillId

    public float Mastery { get; set; }
    public int Attempts { get; set; }
    public int Success { get; set; }

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