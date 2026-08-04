using System.Net.Http.Json;
using Microsoft.JSInterop;

namespace Kidz2Learn.Services;

/// <summary>
/// Plays a random success/failure sound after a task is answered, shared across all challenge
/// pages via the persistently-mounted #affirmationPlayer element in MainLayout.razor. The file
/// list comes from wwwroot/audio/affirmations/affirmations.json (regenerated from the "erfolg"/
/// "misserfolg" folders by generate_affirmations_json.ps1); until sound files actually exist
/// there, every category is empty and PlayAsync silently does nothing.
/// </summary>
public class AffirmationService(HttpClient http, IJSRuntime js)
{
    private const string ManifestUrl = "audio/affirmations/affirmations.json";
    private const string ElementId = "affirmationPlayer";

    private readonly Random _rng = new();
    private Task<Dictionary<string, List<string>>?>? _manifestTask;

    private Task<Dictionary<string, List<string>>?> ManifestAsync()
    {
        return _manifestTask ??= LoadManifestAsync();
    }

    private async Task<Dictionary<string, List<string>>?> LoadManifestAsync()
    {
        try
        {
            return await http.GetFromJsonAsync<Dictionary<string, List<string>>>(ManifestUrl);
        }
        catch
        {
            // No manifest yet (or it's malformed) - treat exactly like "no sounds available".
            return null;
        }
    }

    public Task PlayAsync(bool success)
    {
        return PlayAsync(success ? "erfolg" : "misserfolg");
    }

    public Task PlayErfolgAsync()
    {
        return PlayAsync("erfolg");
    }

    public Task PlayMisserfolgAsync()
    {
        return PlayAsync("misserfolg");
    }

    private async Task PlayAsync(string category)
    {
        var manifest = await ManifestAsync();
        if (manifest is null || !manifest.TryGetValue(category, out var files) || files.Count == 0)
            return;

        var file = files[_rng.Next(files.Count)];
        await js.InvokeVoidAsync("k4l_playAudioFile", ElementId, $"audio/affirmations/{category}/{file}");
    }
}
