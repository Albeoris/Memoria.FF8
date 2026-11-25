using System.Security.Cryptography;
using System.Text;
using Memoria.FF8.Mods;
using NuGet.Versioning;

namespace Memoria.Launcher;

internal sealed record OfficialModNotice(OfficialModComponent Component, ModPackageInstallResult? Installation, string? Compatibility, string? AcknowledgementKey, bool Reviewed)
{
    public string Describe()
    {
        OfficialModRelease? latest = Component.Latest;
        string installed = Installation switch
        {
            { RestartRequired: true } => $"{Installation.Version} staged · restart required",
            { Installed: true, Version.Length: 0 } => "Installed legacy copy · version unknown",
            { Installed: true } => $"Installed {Installation.Version}",
            { Error: not null } => $"Installed copy needs attention · {Installation.Error}",
            _ => "Not installed"
        };
        string available = latest is null ? "No release listed" : $"Latest {latest.Version} · {FormatSize(latest.Artifact.Size)}";
        string target = Compatibility ?? (latest is null ? "Supports the selected game edition and language" : "Compatible with the selected game and installed engine");
        return $"{Component.Name}\n{installed} · {available}\n{target}\n{Component.Description}" + (string.IsNullOrWhiteSpace(latest?.ReleaseNotes) ? "" : $"\n{latest.ReleaseNotes}");
    }

    private static string FormatSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes:N0} bytes",
            < 1048576 => $"{bytes / 1024d:N1} KiB",
            _ => $"{bytes / 1048576d:N1} MiB"
        };
    }
}

internal sealed record OfficialModNotifications(string? Error, string ProfileStateDirectory, IReadOnlyList<OfficialModNotice> Notices)
{
    public string Summary
    {
        get
        {
            List<string> summaries = [];
            if (Error is not null)
            {
                summaries.Add("Could not check official mods — showing saved information.");
            }
            OfficialModNotice[] pending = Notices.Where(notice => notice is { AcknowledgementKey: not null, Reviewed: false }).ToArray();
            int updates = pending.Count(notice => notice.Installation is not null && notice.Compatibility is null);
            int added = pending.Count(notice => notice.Installation is null && notice.Compatibility is null);
            if (updates > 0)
            {
                summaries.Add(updates == 1 ? "1 mod update available" : $"{updates} mod updates available");
            }
            if (added > 0)
            {
                summaries.Add(added == 1 ? "1 new official mod available" : $"{added} new official mods available");
            }
            foreach (var blocked in pending.Where(notice => notice.Compatibility is not null).GroupBy(notice => (HasInstallation: notice.Installation is not null, Reason: notice.Compatibility!)))
            {
                int count = blocked.Count();
                string name = blocked.Key.HasInstallation ? "mod update" : "new official mod";
                string subject = $"{count} {name}{(count == 1 ? "" : "s")}";
                summaries.Add(blocked.Key.Reason.StartsWith("Requires Memoria ", StringComparison.Ordinal)
                    ? $"{subject} {(count == 1 ? "requires" : "require")} {blocked.Key.Reason["Requires ".Length..].TrimEnd('.')}"
                    : $"{subject} · {blocked.Key.Reason}");
            }
            if (summaries.Count != 0)
            {
                return string.Join(" · ", summaries);
            }
            return Notices.Any(notice => notice.Reviewed) ? "Official mod changes reviewed" : "Official mods are up to date";
        }
    }

    public static OfficialModNotifications Create(OfficialCatalogSnapshot catalog, string profileStateDirectory, string? engineVersion, string edition, string language, IReadOnlyDictionary<string, ModPackageInstallResult> installed)
    {
        IEnumerable<OfficialModComponent> localOnly = installed.Values.Select(installation => installation.Official).OfType<OfficialModComponent>().Select(component => component with { Latest = null });
        var installations = installed.Values.ToLookup(installation => installation.Official?.CatalogId);
        OfficialModNotice[] notices = catalog.Components.Concat(localOnly).Where(component => !component.IsLanguageDependency && component.LanguageOffers?.Count is not > 0).DistinctBy(component => component.CatalogId).Select(component =>
        {
            ModPackageInstallResult? installation = installations[component.CatalogId].FirstOrDefault();
            string? compatibility = OfficialModCatalog.CompatibilityError(component, engineVersion, edition, language);
            string? key = null;
            if (component.Latest is { } latest && OfficialModCatalog.TargetMatches(component.Target, edition, language))
            {
                if (installation is null)
                {
                    key = Digest(OfficialModCatalog.SourceId, component.Id, "new");
                }
                else if (NuGetVersion.TryParse(installation.Version, out NuGetVersion? current) && NuGetVersion.TryParse(latest.Version, out NuGetVersion? candidate) && VersionComparer.VersionRelease.Compare(candidate, current) > 0)
                {
                    key = Digest(OfficialModCatalog.SourceId, component.Id, "update", candidate.ToNormalizedString().ToLowerInvariant());
                }
            }
            return new OfficialModNotice(component, installation, compatibility, key, key is not null && File.Exists(Path.Combine(profileStateDirectory, key)));
        }).OrderBy(notice => notice.Component.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
        return new(catalog.Error, profileStateDirectory, Array.AsReadOnly(notices));
    }

    public static string StateDirectory(string configuration, string modsRoot, string gameDirectory, string edition, string language)
    {
        string profile = Digest(IdentityPath(modsRoot), IdentityPath(gameDirectory), edition, language);
        return Path.Combine(configuration, "official-mod-notifications", profile);
    }

    public OfficialModNotifications Acknowledge()
    {
        Directory.CreateDirectory(ProfileStateDirectory);
        foreach (OfficialModNotice notice in Notices.Where(notice => notice is { AcknowledgementKey: not null, Reviewed: false }))
        {
            using FileStream marker = new(Path.Combine(ProfileStateDirectory, notice.AcknowledgementKey!), FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
        }
        return this with { Notices = Array.AsReadOnly(Notices.Select(notice => notice with { Reviewed = notice.AcknowledgementKey is not null }).ToArray()) };
    }

    private static string IdentityPath(string path)
    {
        string normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        return OperatingSystem.IsWindows() ? normalized.ToUpperInvariant() : normalized;
    }

    private static string Digest(params string[] values) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\0', values))));
}
