using System.Globalization;
using Memoria.FF8.Platform;

namespace Memoria.FF8.Mods;

public sealed record GameLanguageOffer(string Id, string DisplayTag, string Title, GameLanguageProfile? Profile, ModPackage? InstalledPackage, RemoteModEntry? Release, string? Option, string? Requirement)
{
    public bool IsOriginal => Id == "original";
    public bool IsInstalled => IsOriginal || InstalledPackage is not null;
    public bool CanSelect => Profile is not null && Requirement is null && (IsInstalled || Release is not null);
    public string Source => IsOriginal ? "Installed game" : Release?.SourceId == RemoteModCatalogAdapter.SourceId || InstalledPackage?.Format == ModFormat.Junction ? "Junction" : Release?.SourceId == OfficialModCatalog.SourceId || InstalledPackage?.Official is not null ? "Memoria" : "Installed mod";
    public string? Version { get; init; }
    public long? DownloadBytes { get; init; }
    public IReadOnlyList<GameLanguageDownloadMember> DownloadMembers { get; init; } = [];
    public bool RequiresAcquisition { get; init; }
    public bool CanUpdate { get; init; }
    public string? PublishedSize => Release?.PublishedSize;
    internal LanguageOfferMetadata? Metadata { get; init; }

    public GameLanguageChoice ToChoice(string? packageId)
    {
        return new(DisplayTag, Id, Option, Profile ?? throw new InvalidOperationException("This language has no installed native base."), packageId);
    }
}

public sealed record GameLanguageDownloadMember(string Name, string Version, long? Bytes, bool IsInstalled);

public sealed record GameLanguageGroup(string DisplayTag, string EnglishName, string SelfName, IReadOnlyList<GameLanguageOffer> Offers)
{
    public string Label => EnglishName.Equals(SelfName, StringComparison.OrdinalIgnoreCase) ? EnglishName : $"{EnglishName} ({SelfName})";
}

public sealed class GameLanguageInventory
{
    private GameLanguageInventory(IReadOnlyList<GameLanguageGroup> groups)
    {
        Groups = groups;
    }

    public IReadOnlyList<GameLanguageGroup> Groups { get; }

    public static GameLanguageInventory Create(IReadOnlyList<GameLanguageProfile> profiles, GameLanguageProfile currentProfile, IEnumerable<ModPackage> installed, IEnumerable<RemoteModEntry> junction, IEnumerable<RemoteModEntry> languageIndex)
    {
        RemoteModEntry[] releases = junction.Concat(languageIndex).Where(LanguagePackages.IsLanguage).DistinctBy(entry => (entry.SourceId, entry.Id)).ToArray();
        ModPackage[] installedPackages = installed.ToArray();
        ModPackage[] packages = installedPackages.Where(package => LanguagePackages.IsLanguage(package, releases) && !LanguagePackages.IsDependency(package)).ToArray();
        List<GameLanguageOffer> offers = [];
        foreach (GameLanguageProfile profile in profiles.OrderByDescending(profile => profile == currentProfile))
        {
            if (LanguagePackages.DisplayTag(profile.ResourceLanguage) is string tag)
            {
                offers.Add(new("original", tag, "Original", profile, null, null, null, null));
            }
        }
        foreach (ModPackage package in packages)
        {
            RemoteModEntry? release = releases.FirstOrDefault(entry => MatchesPackage(entry, package));
            IReadOnlyList<LanguageOfferMetadata> options = package.Format == ModFormat.Junction
                ? LanguagePackages.JunctionOffers(release?.Version == package.JunctionMetadata?.Version ? release : null, package.JunctionMetadata)
                : LanguagePackages.NativeOffers(package);
            foreach (LanguageOfferMetadata metadata in options)
            {
                LanguageOfferMetadata? published = release?.LanguageOffers.FirstOrDefault(offer => offer.Option == metadata.Option && LanguagePackages.DisplayTag(offer.Locale) == LanguagePackages.DisplayTag(metadata.Locale));
                AddOffer(offers, published is null ? metadata : metadata with { Label = published.Label }, profiles, currentProfile, package, release, installedPackages);
            }
        }
        foreach (RemoteModEntry entry in releases)
        {
            ModPackage? package = packages.FirstOrDefault(package => MatchesPackage(entry, package));
            IReadOnlyList<LanguageOfferMetadata> options = entry.SourceId == RemoteModCatalogAdapter.SourceId ? LanguagePackages.JunctionOffers(entry, package?.JunctionMetadata) : entry.LanguageOffers;
            foreach (LanguageOfferMetadata metadata in options)
            {
                string id = ProviderIdentity(package, entry);
                if (offers.Any(offer => offer.Id == id && offer.DisplayTag == LanguagePackages.DisplayTag(metadata.Locale) && offer.Option == OptionIdentity(metadata)))
                {
                    continue;
                }
                AddOffer(offers, metadata, profiles, currentProfile, null, entry, installedPackages);
            }
        }
        GameLanguageGroup[] groups = offers.GroupBy(offer => offer.DisplayTag, StringComparer.OrdinalIgnoreCase).Select(group =>
        {
            CultureInfo culture = CultureInfo.GetCultureInfo(group.Key);
            string selfName = culture.NativeName;
            selfName = selfName.Length == 0 ? selfName : char.ToUpper(selfName[0], culture) + selfName[1..];
            return new GameLanguageGroup(group.Key, culture.EnglishName, selfName, Array.AsReadOnly(group.OrderByDescending(offer => offer.IsOriginal).ThenByDescending(offer => offer.IsInstalled).ThenBy(offer => offer.Title, StringComparer.OrdinalIgnoreCase).ToArray()));
        }).OrderBy(group => group.EnglishName, StringComparer.OrdinalIgnoreCase).ToArray();
        return new(Array.AsReadOnly(groups));
    }

    public GameLanguageGroup? OfferForDisplayCulture(CultureInfo displayCulture, string effectiveDisplayTag)
    {
        string? requestedScript = Script(displayCulture.Name);
        for (CultureInfo culture = displayCulture; culture.Name.Length != 0; culture = culture.Parent)
        {
            string? displayTag = LanguagePackages.DisplayTag(culture.Name);
            requestedScript ??= Script(culture.Name);
            if (requestedScript is not null && Script(culture.Name) is null && culture.IsNeutralCulture)
            {
                break;
            }
            if (displayTag == LanguagePackages.DisplayTag(effectiveDisplayTag))
            {
                return null;
            }
            GameLanguageGroup? group = Groups.FirstOrDefault(candidate => candidate.DisplayTag == displayTag && candidate.Offers.Any(offer => offer.CanSelect));
            if (group is not null)
            {
                return group;
            }
        }
        return null;
    }

    private static string? Script(string tag)
    {
        return tag.Split('-').Skip(1).FirstOrDefault(part => part.Length == 4 && part.All(char.IsAsciiLetter));
    }

    private static void AddOffer(List<GameLanguageOffer> offers, LanguageOfferMetadata metadata, IReadOnlyList<GameLanguageProfile> profiles, GameLanguageProfile currentProfile, ModPackage? package, RemoteModEntry? entry, IReadOnlyList<ModPackage> installed)
    {
        if (LanguagePackages.DisplayTag(metadata.Locale) is not string displayTag)
        {
            return;
        }
        GameLanguageProfile? profile = metadata.NativeVariants.Any(variant => variant.Matches(currentProfile)) && profiles.Contains(currentProfile)
            ? currentProfile
            : profiles.FirstOrDefault(available => metadata.NativeVariants.Any(variant => variant.Matches(available)));
        string? requirement = null;
        if (profile is null)
        {
            requirement = metadata.NativeVariants.Count > 0
                ? "Requires " + string.Join(" or ", metadata.NativeVariants.Select(variant => variant.Edition + " " + CultureInfo.GetCultureInfo(LanguagePackages.DisplayTag(variant.Language)!).EnglishName)) + " game data."
                : "This provider has no supported native base.";
        }
        else if (package is null && entry?.Official?.Latest is { } latest)
        {
            requirement = OfficialModCatalog.EngineError(latest.EngineVersionRange, EngineBuildMetadata.CompatibilityVersion);
        }
        else if (entry?.SourceId == RemoteModCatalogAdapter.SourceId && package is null && !LanguagePackages.IsReviewedJunctionOffer(entry, metadata))
        {
            requirement = "The download has no verified mapping to a package language option.";
        }
        if (entry?.SourceId == RemoteModCatalogAdapter.SourceId && profile is not null && profile.ResourceLanguage != "en")
        {
            requirement = "Junction packages currently require Classic English game data.";
            profile = null;
        }
        if (package?.JunctionMetadata is { } manifest && (metadata.Value is not null || metadata.Option is not null) && LanguagePackages.JunctionOption(manifest, metadata) is null)
        {
            requirement = "The installed release does not declare this language option.";
            profile = null;
        }
        string? version = package?.JunctionMetadata?.Version ?? package?.Official?.Latest?.Version ?? (package is null ? entry?.Version : null);
        bool canUpdate = package is not null && version is not null && entry?.CompareVersion(version) is > 0 && (package.Official is not null || package.Format == ModFormat.Junction);
        bool managesDependencies = package is null || package.Descriptor?.Dependencies.Count > 0;
        List<GameLanguageDownloadMember> members = [];
        bool requiresAcquisition = package is null;
        IReadOnlyList<OfficialModComponent> dependencies = managesDependencies || canUpdate ? entry?.RequiredLanguagePackages ?? [] : [];
        IReadOnlyList<ModPackage> installedDependencies = [];
        if (package is not null && profile is not null)
        {
            try
            {
                installedDependencies = LanguagePackages.ResolveInstalledDependencies(package, installed, profile);
            }
            catch (InvalidDataException exception)
            {
                if (dependencies.Count == 0)
                {
                    requirement ??= exception.Message;
                }
                else
                {
                    requiresAcquisition = true;
                }
            }
        }
        if (entry is not null)
        {
            members.Add(new(entry.Name, entry.Version, entry.Official?.Latest?.Artifact.Size ?? LanguagePackages.JunctionDownloadBytes(entry), package is not null && !canUpdate));
            foreach (OfficialModComponent dependency in dependencies)
            {
                OfficialModRelease release = dependency.Latest!;
                bool installedRelease = installed.Any(candidate => candidate.Official?.Id == dependency.Id && candidate.Official.Latest?.Version == release.Version && candidate.OfficialAvailable);
                members.Add(new(dependency.Name, release.Version, release.Artifact.Size, installedRelease));
                canUpdate |= package is not null && !installedRelease && !requiresAcquisition;
            }
        }
        else if (package is not null)
        {
            members.Add(new(package.Name, version ?? "", null, true));
            members.AddRange(installedDependencies.Select(dependency => new GameLanguageDownloadMember(dependency.Name, dependency.Official?.Latest?.Version ?? "", null, true)));
        }
        long? bytes = members.Where(member => !member.IsInstalled).All(member => member.Bytes.HasValue)
            ? members.Where(member => !member.IsInstalled).Sum(member => member.Bytes!.Value) : null;
        offers.Add(new(ProviderIdentity(package, entry), displayTag, metadata.Label, profile, package, entry, OptionIdentity(metadata), requirement)
        {
            Metadata = metadata,
            Version = version,
            DownloadBytes = bytes is > 0 ? bytes : null,
            DownloadMembers = members.AsReadOnly(),
            RequiresAcquisition = requiresAcquisition,
            CanUpdate = canUpdate
        });
    }

    private static string? OptionIdentity(LanguageOfferMetadata metadata)
    {
        return metadata.Option ?? metadata.Value?.ToString(CultureInfo.InvariantCulture);
    }

    private static bool MatchesPackage(RemoteModEntry entry, ModPackage package)
    {
        return entry.SourceId == RemoteModCatalogAdapter.SourceId ? entry.Id == package.JunctionMetadata?.Id
            : entry.SourceId == OfficialModCatalog.SourceId && entry.Official?.Id is string id && (id == package.Descriptor?.Id || id == package.Official?.Id);
    }

    internal static string ProviderIdentity(ModPackage? package, RemoteModEntry? entry)
    {
        if (package?.JunctionMetadata?.Id is not null || entry?.SourceId == RemoteModCatalogAdapter.SourceId && entry.Id != Guid.Empty)
        {
            return RemoteModCatalogAdapter.SourceId + "/" + (package?.JunctionMetadata?.Id ?? entry!.Id).ToString("D");
        }
        string? nativeId = package?.Descriptor?.Id ?? package?.Official?.Id ?? entry?.Official?.Id;
        return nativeId is null ? "installed/" + package!.Id : OfficialModCatalog.SourceId + "/" + nativeId;
    }
}
