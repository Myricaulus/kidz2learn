using System.Text.Json.Serialization;
using Kidz2Learn.Entities;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Tavenem.Blazor.IndexedDB;
using Tavenem.DataStorage;
using System.Linq;

namespace Kidz2Learn.Model;

public sealed class SkillMigrationHelper
{
    // BUMP THIS WHENEVER YOU CHANGE SKILL SCHEMA OR MIGRATION LOGIC
    private const int CurrentSchemaVersion = 1;
    private readonly IndexedDb _db;
    private readonly IndexedDbStore _metaStore;
    private readonly IndexedDbStore _skillStore;

    public SkillMigrationHelper(IndexedDb db)
    {
        _db = db;
        _metaStore = _db["SkillMeta"]??throw new Exception("SkillMeta db failed");
        _skillStore = _db["SkillStates"]??throw new Exception("SkillStates db failed");;
    }

    public async Task InitAsync()
    {
        var meta = await _metaStore.GetItemAsync<SkillMeta>("meta");
        if (meta is { Initialized: true })
            return;

        // Initialisiere alle Skills mit 0
        foreach (var skill in SkillRegistry.All)
        {
            await _skillStore.StoreItemAsync(new SkillState
            {
                Id = skill.Value.Id,
                Mastery = 0,
                Attempts = 0,
                Success = 0
            });
        }

        await _metaStore.StoreItemAsync(new SkillMeta { Initialized = true, SchemaVersion = CurrentSchemaVersion });
    }

    public async Task EnsureSchemaAsync()
    {
        var meta = await _metaStore.GetItemAsync<SkillMeta>("meta");

        if (meta == null)
        {
            await MigrateLegacyAsync();
            await _metaStore.StoreItemAsync(new SkillMeta { Initialized = true, SchemaVersion = CurrentSchemaVersion });
        } else if (meta.SchemaVersion < CurrentSchemaVersion)
        {
            await MigrateAsync(meta.SchemaVersion, CurrentSchemaVersion, _metaStore);
        }
    }

    private async Task MigrateAsync(int schemaVersion, int currentSchemaVersion, IndexedDbStore metaStore)
    {
        await Task.Delay(100);
        throw new NotImplementedException();
    }

    private async Task MigrateLegacyAsync()
    {
        var arithDb = _db["ArithmetikAufgaben"]??throw new Exception("ArithmetikAufgaben db failed");

        List<ArithemticLog> logs = [];
        
        var legacyStats = await arithDb.GetItemAsync<ArithemticLogStats>("0") ?? new ArithemticLogStats();
        await arithDb.RemoveItemAsync("0");

        await foreach (var log in arithDb.GetAllAsync<ArithemticLog>())
            logs.Add(log);

        await arithDb.StoreItemAsync(legacyStats);
            
        if(logs.Count == 0)
        {
            await InitAsync();
            return;
        }
        
        // Aggregation pro Skill
        var stats = new Dictionary<string, (int attempts, int success, float prozent, int count)>();

        foreach (var log in logs)
        {
            var skillIds = SkillInference.FromLegacyLog(log);

            foreach (var skill in skillIds)
            {
                if (!stats.ContainsKey(skill))
                    stats[skill] = (0, 0, 0, 0);

                var current = stats[skill];
                
                stats[skill] = (
                    current.attempts + log.Kompetenz.Versuche, // Summe über logs
                    current.success + log.Kompetenz.Richtig, // Summe über logs
                    current.prozent + log.Kompetenz.GetProzentValue(), // Durchschnitt über logs
                    ++current.count
                );
            }
        }

        // In SkillState schreiben
        foreach (var (skillId, value) in stats)
        {
            await _skillStore.StoreItemAsync(new SkillState
            {
                Id = skillId,
                Attempts = value.attempts,
                Success = value.success,
                Mastery = value.count == 0 ? 0 : value.prozent / value.count
            });
        }
    }
}

public static class SkillInference
{
    public static IEnumerable<string> FromLegacyLog(ArithemticLog log)
    {
        var isAdd = log.Id.Contains('+');
        var valuelist = log.Id.Split(isAdd?'+':'-');
        var a = int.Parse(valuelist[0]);
        var b = int.Parse(valuelist[1]);

        if (isAdd)
        {
            var sum = a + b;

            if (a < 5 && b < 5)
                yield return "add_1_5";

            else if (sum < 10)
                yield return "add_10_no_carry";

            else if (sum >= 10 && a < 10 && b < 10)
                yield return "add_10_with_carry";

            else if (sum <= 20)
                yield return "add_20";
            else 
                yield return "add_100";
        }
        else
        {
            var diff = a - b;

            if (a < 10 && b < 10)
                yield return "sub_10";
            else if (a < 20)
                yield return "sub_20";
            else 
                yield return "sub_100";
        }
    }
}
