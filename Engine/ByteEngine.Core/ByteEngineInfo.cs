using System.Reflection;

namespace ByteEngine.Core;

/// <summary>
/// The running engine version. The value comes from the build's
/// <c>AssemblyInformationalVersion</c>, which is generated from
/// <c>ByteEngineVersion</c> in the repository root <c>Directory.Build.props</c>.
/// Do not hard-code a version here; add a second source only if the build
/// system cannot supply one.
/// </summary>
public static class ByteEngineInfo
{
    public static string Version { get; } =
        ResolveVersion();

    private static string ResolveVersion()
    {
        Assembly assembly =
            typeof(ByteEngineInfo).Assembly;

        string? informational =
            assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            int metadata = informational.IndexOf('+');
            return metadata >= 0
                ? informational[..metadata]
                : informational;
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
