using Kidz2Learn.Entities;
using Kidz2Learn.Shared.Extensions;
using Tavenem.Blazor.IndexedDB;

namespace Kidz2Learn.Model;

public static class Skill
{
    public const string ReadSyllables = "read_syllables";
    public const string ReadPrecise = "read_precise";
    public const string TurboRead = "read_turbo";

    public static class Math
    {
        public const string Add15 = "add_1_5";
        public const string Add10NoCarry = "add_10_no_carry";
        public const string Add10WithCarry = "add_10_with_carry";
        public const string Sub10 = "sub_10";
        public const string Add20 = "add_20";
        public const string Sub20 = "sub_20";
        public const string Turbo10 = "turbo_10";
        public const string Turbo20 = "turbo_20";
    }
}

public sealed class SkillDefinition
{
    public required string Id { get; init; } // PK: skillId "add_1_5"
    public required string Domain { get; init; } // TaskDomain.Math, TaskDomain.Reading
    public required int Difficulty { get; init; } // Relative to other Domains
    public string? Category { get; init; } // "arithmetic", "geometry", "spelling"
    public required string DisplayName { get; init; } // "Addition bis 5"
}

public static class TaskDomain
{
    public const string Math = "math";
    public const string Reading = "reading";
}

public static class SkillRegistry
{
    private static readonly Dictionary<string, SkillDefinition> Skills = new()
    {
        {
            Skill.Math.Add15,
            new SkillDefinition
            {
                Id = Skill.Math.Add15, Domain = TaskDomain.Math, Category = "arithmetic", Difficulty = 1,
                DisplayName = "Addition bis 5"
            }
        },
        {
            Skill.Math.Add10NoCarry,
            new SkillDefinition
            {
                Id = Skill.Math.Add10NoCarry, Domain = TaskDomain.Math, Category = "arithmetic", Difficulty = 2,
                DisplayName = "Addition bis 10 ohne Übergang"
            }
        },
        {
            Skill.Math.Add10WithCarry,
            new SkillDefinition
            {
                Id = Skill.Math.Add10WithCarry, Domain = TaskDomain.Math, Category = "arithmetic", Difficulty = 3,
                DisplayName = "Addition mit Übergang"
            }
        },
        {
            Skill.Math.Add20,
            new SkillDefinition
            {
                Id = Skill.Math.Add20, Domain = TaskDomain.Math, Category = "arithmetic", Difficulty = 4,
                DisplayName = "Addition bis 20"
            }
        },
        {
            Skill.Math.Sub10,
            new SkillDefinition
            {
                Id = Skill.Math.Sub10, Domain = TaskDomain.Math, Category = "arithmetic", Difficulty = 3,
                DisplayName = "Subtraktion bis 10"
            }
        },
        {
            Skill.Math.Sub20,
            new SkillDefinition
            {
                Id = Skill.Math.Sub20, Domain = TaskDomain.Math, Category = "arithmetic", Difficulty = 4,
                DisplayName = "Subtraktion bis 20"
            }
        },
        {
            Skill.Math.Turbo10,
            new SkillDefinition
            {
                Id = Skill.Math.Turbo10, Domain = TaskDomain.Math, Category = "arithmetic", Difficulty = 4,
                DisplayName = "Turbo Rechnen bis 10"
            }
        },
        {
            Skill.ReadSyllables,
            new SkillDefinition
            {
                Id = Skill.ReadSyllables, Domain = TaskDomain.Reading, Category = "phonetics", Difficulty = 1,
                DisplayName = "Silben lesen"
            }
        },
        {
            Skill.ReadPrecise,
            new SkillDefinition
            {
                Id = Skill.ReadPrecise, Domain = TaskDomain.Reading, Category = "phonetics", Difficulty = 2,
                DisplayName = "Präzise lesen"
            }
        }
    };

    public static IReadOnlyDictionary<string, SkillDefinition> All => Skills;

    public static SkillDefinition Get(string id)
    {
        return Skills.TryGetValue(id, out var v)
            ? v
            : new SkillDefinition { Id = id, Domain = "unkown", Difficulty = 1, DisplayName = "unknown" };
    }

    public static IEnumerable<SkillDefinition> ByDomain(string domain)
    {
        return Skills.Values.Where(s => s.Domain == domain);
    }

    public static IEnumerable<SkillDefinition> ByCategory(string category)
    {
        return Skills.Values.Where(s => s.Category == category);
    }
}

public record SkillView(SkillDefinition Definition, SkillState State);

public sealed class SkillMasteryStore(IndexedDb skillDb)
{
    private readonly IndexedDbStore _store = skillDb["SkillStates"]
                                             ?? throw new InvalidOperationException("SkillStates DB failed to init");

    public async Task Adjust(string skillId, Difficulty difficulty, int timeMs, Kompetenzniveau specificTaskHistory,
        bool success)
    {
        float delta;
        if (success)
            delta = difficulty switch
            {
                Difficulty.Normal => 0.03f,
                Difficulty.Hard => 0.06f,
                Difficulty.Extreme => 0.1f,
                _ => 0.03f
            };
        else
            delta = difficulty switch
            {
                Difficulty.Normal => -0.05f,
                Difficulty.Hard => -0.02f,
                Difficulty.Extreme => -0.01f,
                _ => -0.05f
            };
        var s = await _store.GetItemAsync<SkillState>(skillId) ?? new SkillState
            { Id = skillId, EffectiveDifficulty = SkillRegistry.All[skillId].Difficulty };
        s.Attempts++;
        if (success)
        {
            s.Success++;
            s.SuccessRow++;
            s.SuccessFail = 0;
        }
        else
        {
            s.SuccessRow = 0;
            s.SuccessFail++;
        }

        s.AttemptsHistory.Add(new SkillAttempt
        {
            Correct = success,
            EffectiveDifficulty = s.EffectiveDifficulty,
            NeededTimeMs = timeMs,
            FailReason = AttemptFailReason.Unknown // TODO: try to guess AttemptFailReason
        });

        // specificTaskHistory contains the history of last 20 attempts about this specific Task, like "5+6" or "5+7"
        specificTaskHistory.CountFalsch(); // return how many of the last 20 were failed
        specificTaskHistory.CountRichtig(); // return how many of the last 20 were successful
        specificTaskHistory
            .CountLastFalschRow(); // return 0 if last attempt was a success, otherwise how many of the last attempts were wrong in a row
        specificTaskHistory
            .CountLastRichtigRow(); // return 0 if last attempt has failed, otherwise how many of the last attempts were correct in a row

        float SkillRowFactor(SkillState s)
        {
            if (s.SuccessRow > 0)
                return 1f + 0.15f * (float)Math.Log2(1 + s.SuccessRow);

            if (s.SuccessFail > 0)
                return 1f / (1f + 0.2f * s.SuccessFail);

            return 1f;
        }

        float TaskRowFactor(Kompetenzniveau taskHistory)
        {
            var good = taskHistory.CountLastRichtigRow();
            var bad = taskHistory.CountLastFalschRow();

            if (good > 0)
                return 1f + 0.2f * (float)Math.Sqrt(good);

            if (bad > 0)
                return 1f / (1f + 0.3f * bad);

            return 1f;
        }

        float TimeFactor(int timeMs, float effectiveDifficulty)
        {
            // Erwartete Zeit steigt mit Schwierigkeit
            var expectedMs = 1500f + effectiveDifficulty * 2000f;

            var ratio = expectedMs / Math.Max(timeMs, 5000);

            return Math.Clamp(ratio, 0.5f, 1.8f);
        }
        // TODO: Compute new "effectiveDifficulty" dependend on past perfomance, to adjust probabilty of showing tasks for this skill in future. 
        // higher difficulty => less probability. This should mainly reduce frustration.

        // TODO: Compute Mastery Delta dependend on past perfomance, utilizing Bayesian Knowledge Tracing. 
        // This is the main key value for selecting a fitting Task for this or following skills


        var finaldelta = delta *
                         SkillRowFactor(s) *
                         TaskRowFactor(specificTaskHistory) *
                         TimeFactor(timeMs, s.EffectiveDifficulty);
        s.LastAttempt = DateTime.Now;
        s.Mastery = Math.Clamp(s.Mastery + finaldelta, 0, 1);
        Console.WriteLine($"{skillId}|A:{s.Attempts}|S:{s.Success}|M:{s.Mastery}|Δ:{delta}");
        await _store.StoreItemAsync(s);
    }

    private async Task<IReadOnlyDictionary<string, SkillState>> Snapshot()
    {
        return await _store.ToDictionaryAsync<SkillState>();
    }

    public async Task<IEnumerable<SkillView>> GetSkillViewEnumerableAsync()
    {
        // Alle SkillStates aus der DB holen
        var statesById = await Snapshot();

        // Alle Skills in SkillRegistry einbeziehen
        return SkillRegistry.All.Values
            .Select(def => new SkillView(def, statesById.GetValueOrDefault(def.Id) ?? new SkillState()));
    }
}