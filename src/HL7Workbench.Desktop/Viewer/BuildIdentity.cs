using System.Reflection;
using System.Text.RegularExpressions;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Displays only bounded version/commit metadata, never arbitrary build strings or paths.</summary>
internal sealed record BuildIdentity(string Version, string Build)
{
    internal static BuildIdentity Current => FromMetadata(typeof(BuildIdentity).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    internal static BuildIdentity FromMetadata(string? metadata)
    {
        // SDK builds append the actual Git revision. A partial token, URL, branch
        // name or custom metadata is not reliable evidence of a source commit.
        var parts = (metadata ?? "").Split('+');
        var version = Regex.IsMatch(parts[0], @"\A\d{1,5}\.\d{1,5}\.\d{1,5}(?:-[A-Za-z0-9.-]{1,48})?\z")
            ? parts[0] : "unavailable";
        var build = parts.Length == 2 && Regex.IsMatch(parts[1], @"\A(?:[0-9a-fA-F]{40}|[0-9a-fA-F]{64})\z")
            ? parts[1][..8].ToLowerInvariant() : "unavailable";
        return new(version, build);
    }
}
