using System.Reflection;

namespace Relay.Agent;

/// <summary>Single source of truth for the agent's version — read from the assembly, which the
/// csproj &lt;Version&gt; sets. Bump the version in one place (the csproj) and everything follows.</summary>
public static class AppInfo
{
    /// <summary>Marketing version, e.g. "0.3.0".</summary>
    public static string Version { get; } = ReadVersion();

    private static string ReadVersion()
    {
        var info = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        // Strip any "+<git-sha>" build metadata that the SDK appends.
        if (!string.IsNullOrEmpty(info))
        {
            var plus = info.IndexOf('+');
            return plus >= 0 ? info[..plus] : info;
        }
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        return v is null ? "0.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
    }
}
