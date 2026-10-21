using System.Globalization;
using System.Reflection;

namespace Kidz2Learn.Shared;

/// <summary>
///     The build line on the welcome screen: the version from Kidz2Learn.csproj, the commit the SDK
///     appends to the informational version ("1.0.0+3ad36b8a..."), and the UTC build day the csproj
///     stores as assembly metadata "BuildDate".
/// </summary>
public static class AppVersion
{
    public static string Text { get; } = Format(
        typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
        typeof(AppVersion).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "BuildDate")?.Value);

    /// <summary>"Version 1.0.0 (3ad36b8) vom 21.10.2026"; the commit and the day are left out when unknown.</summary>
    public static string Format(string? informationalVersion, string? buildDate)
    {
        var teile = (informationalVersion ?? "?").Split('+', 2);
        var text = $"Version {teile[0]}";
        if (teile.Length == 2 && teile[1].Length > 0)
            text += $" ({teile[1][..Math.Min(7, teile[1].Length)]})";
        if (DateOnly.TryParseExact(buildDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var tag))
            text += $" vom {tag.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}";
        return text;
    }
}
