using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Kidz2Learn.Shared.Extensions;

public static class HttpClientExtensions
{
    /// <summary>
    ///     Like <c>GetFromJsonAsync</c>, but has the browser revalidate its cached copy first. The
    ///     manifests (<c>sidfiles.json</c>, <c>affirmations.json</c>) are regenerated on every build
    ///     under the same URL, and a heuristically cached old one no longer matches the new app.
    /// </summary>
    public static async Task<T?> GetFreshJsonAsync<T>(this HttpClient http, string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.SetBrowserRequestCache(BrowserRequestCache.NoCache);
        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>();
    }
}
