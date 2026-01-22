using System.Collections.ObjectModel;
using Kidz2Learn.Entities;
using Microsoft.AspNetCore.Components;
using Tavenem.Blazor.IndexedDB;

namespace Kidz2Learn.Model;

public static class Skill
{
    public static class Math
    {
        public const string add_1_5 = "add_1_5";
        public const string add_10_no_carry = "add_10_no_carry";
        public const string add_10_with_carry = "add_10_with_carry";
        public const string sub_10 = "sub_10";    
        public const string add_20 = "add_20";
        public const string sub_20 = "sub_20";
    }
    public const string read_syllables = "read_syllables";
}

public sealed class SkillDefinition
{
    public required string Id { get; init; }           // PK: "add_1_5"
    public required string Domain { get; init; }       // "math", "reading"
    public required int Difficulty {get; init;} // Relative to other Domains
    public string? Category { get; init; }     // "arithmetic", "geometry", "spelling"
    public required string DisplayName { get; init; }  // "Addition bis 5"
}

public static class SkillRegistry
{
    private static readonly Dictionary<string,SkillDefinition> _skills = new()
    {
        {Skill.Math.add_1_5, new()  { Id=Skill.Math.add_1_5, Domain="math", Category="arithmetic", Difficulty=1,DisplayName="Addition bis 5" }},
        {Skill.Math.add_10_no_carry, new() { Id=Skill.Math.add_10_no_carry, Domain="math", Category="arithmetic", Difficulty=2,DisplayName="Addition bis 10 ohne Übergang" }},
        {Skill.Math.add_10_with_carry, new() { Id=Skill.Math.add_10_with_carry, Domain="math", Category="arithmetic", Difficulty=3,DisplayName="Addition mit Übergang" }},
        {Skill.Math.add_20, new() { Id=Skill.Math.add_20, Domain="math", Category="arithmetic", Difficulty=4,DisplayName="Addition bis 20" }},
        {Skill.Math.sub_10, new() { Id=Skill.Math.sub_10, Domain="math", Category="arithmetic", Difficulty=3,DisplayName="Subtraktion bis 10" }},
        {Skill.Math.sub_20, new() { Id=Skill.Math.sub_20, Domain="math", Category="arithmetic", Difficulty=4,DisplayName="Subtraktion bis 20" }},
        {Skill.read_syllables, new() { Id=Skill.read_syllables, Domain="reading", Category="phonetics", Difficulty=1,DisplayName="Silben lesen" }},
    };

    public static SkillDefinition Get(string id)
        => _skills.TryGetValue(id,out var v)?v:new(){Id=id, Domain="unkown", Difficulty=1, DisplayName="unknown"};

    public static IEnumerable<SkillDefinition> ByDomain(string domain)
        => _skills.Values.Where(s => s.Domain == domain);

    public static IEnumerable<SkillDefinition> ByCategory(string category)
        => _skills.Values.Where(s => s.Category == category);

    public static IReadOnlyDictionary<string,SkillDefinition> All => _skills;
}


public sealed class SkillMasteryStore(IndexedDb skillDb)
{
    private readonly Dictionary<string, SkillState> _states = new();
    private readonly IndexedDbStore _store = skillDb["SkillStates"]
            ?? throw new InvalidOperationException("SkillStates DB failed to init");

    public async Task LoadAsync()
    {
        var all = _store.GetAllAsync<SkillState>();
        if(all is not null)
            await foreach (var s in all)
                _states[s.Id] = s;
    }

    public float Get(string id)
        => _states.TryGetValue(id, out var s) ? s.Mastery : 0;

    public void Adjust(string id, Difficulty difficulty, bool success)
    {
        float delta;
        if(success)
        {
            delta = difficulty switch
            {
                Difficulty.Normal => 0.03f,
                Difficulty.Hard => 0.06f,
                Difficulty.Extreme => 0.1f,
                _ => 0.03f
            };


        } else {
            delta = difficulty switch
            {
                Difficulty.Normal => -0.05f,
                Difficulty.Hard => -0.04f,
                Difficulty.Extreme => -0.03f,
                _ => -0.05f
            };
        }
        if (!_states.TryGetValue(id, out var s))
        {
            s = new SkillState { Id = id };
            _states[id] = s;
        }

        s.Mastery = Math.Clamp(s.Mastery + delta, 0, 1);
        s.Attempts++;
        if (success) s.Success++;
        _store.StoreItemAsync(s);
    }

    internal IReadOnlyDictionary<string, SkillState> Snapshot() => _states;

}
