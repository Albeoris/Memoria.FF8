using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Memoria.FF8.Mods;

public sealed record ExternalMusicSelection(ModFile File, double OffsetSeconds, int Volume)
{
    public ModFile? BiosFile { get; init; }
    public Func<string, string, ModFile>? DependencyResolver { get; init; }

    public ModFile ResolveDependency(string requesterRelativePath, string dependency)
    {
        return DependencyResolver?.Invoke(requesterRelativePath, dependency) ?? throw new InvalidDataException("External music has no package dependency resolver.");
    }
}

/// <summary>Resolves external tracks from the selected native filename, without changing native music identity or routing.</summary>
public sealed class ExternalMusicResolver
{
    private readonly ModCatalogSnapshot catalog;
    private readonly Dictionary<string, MusicConfiguration> configurations = new(StringComparer.Ordinal);

    public ExternalMusicResolver(ModCatalogSnapshot catalog)
    {
        this.catalog = catalog;
        foreach (ModPackage package in catalog.Packages.Where(IsEnabled))
        {
            ModFile? file = FindConfiguration(catalog, package);
            if (file is not null)
            {
                configurations[package.Id] = MusicConfiguration.Parse(Encoding.UTF8.GetString(file.ReadBytes()));
            }
        }
    }

    public static IReadOnlyList<ModDiagnostic> Validate(ModCatalogSnapshot catalog)
    {
        List<ModDiagnostic> diagnostics = [];
        foreach (ModPackage package in catalog.Packages.Where(IsEnabled))
        {
            ModFile? file = FindConfiguration(catalog, package);
            if (file is null)
            {
                continue;
            }
            try
            {
                _ = MusicConfiguration.Parse(Encoding.UTF8.GetString(file.ReadBytes()));
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or NotSupportedException or ArgumentException)
            {
                diagnostics.Add(new ModDiagnostic("external-music-config-invalid", exception.Message, file.PackageId, file.RelativePath));
            }
        }
        return diagnostics;
    }

    private static bool IsEnabled(ModPackage package)
    {
        return package.Status == ModEffectiveStatus.Active && package.Format == ModFormat.Junction && package.JunctionMetadata?.UseExternalMusic != false;
    }

    private static ModFile? FindConfiguration(ModCatalogSnapshot catalog, ModPackage package)
    {
        return catalog.FindAll(ModFormat.Junction, $"{package.JunctionMetadata?.ExternalMusicPath ?? "music"}/config.toml").FirstOrDefault(file => file.PackageId == package.Id);
    }

    public static string TrackName(string nativeFilename)
    {
        string name = Path.GetFileNameWithoutExtension(nativeFilename.Replace('\\', '/'));
        int separator = name.IndexOf('-');
        return ValidateName(separator < 0 ? name : name[(separator + 1)..]).ToLowerInvariant();
    }

    public ExternalMusicSelection? Resolve(string nativeFilename, bool noIntro = false)
    {
        string name = TrackName(nativeFilename);
        foreach (ModPackage package in catalog.Packages)
        {
            if (!IsEnabled(package))
            {
                continue;
            }
            MusicConfiguration config = configurations.GetValueOrDefault(package.Id) ?? MusicConfiguration.Empty;
            TrackConfiguration? track = config.Tracks.GetValueOrDefault(name);
            string selectedName = noIntro && track?.NoIntroTrack is string alternate ? alternate : name;
            string folder = package.JunctionMetadata?.ExternalMusicPath ?? "music";
            string extension = package.JunctionMetadata?.ExternalMusicExtension ?? "ogg";
            ModFile? FindTrack(string trackName)
            {
                return catalog.FindAll(ModFormat.Junction, $"{folder}/{trackName}.{extension}").FirstOrDefault(file => file.PackageId == package.Id);
            }
            ModFile? file = FindTrack(selectedName);
            if (file is null)
            {
                if (selectedName != name && FindTrack(name) is not null)
                {
                    throw new InvalidDataException($"External music alternate track '{selectedName}' is missing from package '{package.Id}'.");
                }
                continue;
            }
            double offset = track?.OffsetSeconds ?? 0;
            if (noIntro && track?.NoIntroTrack is null && track?.IntroSeconds is double intro)
            {
                offset = intro;
            }
            ModFile? bios = null;
            if (package.JunctionMetadata?.PsfBiosPath is string biosPath)
            {
                bios = catalog.FindAll(ModFormat.Junction, biosPath).FirstOrDefault(candidate => candidate.PackageId == package.Id) ?? throw new InvalidDataException($"External music BIOS '{biosPath}' is missing from package '{package.Id}'.");
            }
            return new ExternalMusicSelection(file, offset, config.Volume)
            {
                BiosFile = bios,
                DependencyResolver = (requester, dependency) => ResolveDependency(package, requester, dependency)
            };
        }
        return null;
    }

    private ModFile ResolveDependency(ModPackage package, string requester, string dependency)
    {
        if (string.IsNullOrWhiteSpace(dependency) || dependency.Length > 1024 || dependency.Any(character => char.IsControl(character)) || dependency.Replace('\\', '/').StartsWith('/') || dependency.Contains(':'))
        {
            throw new InvalidDataException("External music dependency must be a bounded package-relative filename.");
        }
        string normalizedRequester;
        string resolved;
        try
        {
            normalizedRequester = ModCatalogSnapshot.NormalizePath(requester);
            string directory = normalizedRequester.Contains('/') ? normalizedRequester[..normalizedRequester.LastIndexOf('/')] + "/" : "";
            resolved = ModCatalogSnapshot.NormalizePath(directory + dependency);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("External music dependency escapes its package.", exception);
        }
        if (!catalog.FindAll(ModFormat.Junction, normalizedRequester).Any(file => file.PackageId == package.Id))
        {
            throw new InvalidDataException($"External music dependency requester '{requester}' is not in package '{package.Id}'.");
        }
        return catalog.FindAll(ModFormat.Junction, resolved).FirstOrDefault(file => file.PackageId == package.Id) ?? throw new InvalidDataException($"External music dependency '{dependency}' requested by '{requester}' is missing from package '{package.Id}'.");
    }

    private static string ValidateName(string name)
    {
        if (!Regex.IsMatch(name, @"^[A-Za-z0-9_-]+$", RegexOptions.CultureInvariant))
        {
            throw new InvalidDataException("External music requires a plain track name.");
        }
        return name;
    }

    private sealed class TrackConfiguration
    {
        public double? OffsetSeconds { get; set; }
        public double? IntroSeconds { get; set; }
        public string? NoIntroTrack { get; set; }
    }

    private sealed class MusicConfiguration
    {
        public static MusicConfiguration Empty { get; } = new();
        public int Volume { get; private set; } = 100;
        public Dictionary<string, TrackConfiguration> Tracks { get; } = new(StringComparer.OrdinalIgnoreCase);

        public static MusicConfiguration Parse(string text)
        {
            MusicConfiguration result = new();
            string section = "";
            HashSet<string> keys = new(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in text.TrimStart('\uFEFF').Split('\n'))
            {
                string line = raw.Split('#', 2)[0].Trim();
                if (line.Length == 0)
                {
                    continue;
                }
                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    section = ValidateName(line[1..^1].Trim());
                    if (!result.Tracks.TryAdd(section, new TrackConfiguration()))
                    {
                        throw new InvalidDataException($"Duplicate external music section '{section}'.");
                    }
                    continue;
                }
                int equals = line.IndexOf('=');
                if (equals < 1)
                {
                    throw new InvalidDataException("Malformed external music configuration assignment.");
                }
                string key = line[..equals].Trim();
                string value = line[(equals + 1)..].Trim();
                if (!keys.Add(section + "/" + key))
                {
                    throw new InvalidDataException($"Duplicate external music setting '{key}'.");
                }
                if (section.Length == 0 && key == "volume" && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int volume) && volume is >= 0 and <= 100)
                {
                    result.Volume = volume;
                    continue;
                }
                if (section.Length > 0)
                {
                    TrackConfiguration track = result.Tracks[section];
                    if (key == "no_intro_track" && value.Length >= 2 && value[0] is '\'' or '"' && value[^1] == value[0])
                    {
                        track.NoIntroTrack = ValidateName(value[1..^1]);
                        continue;
                    }
                    if (key is "intro_seconds" or "offset_seconds" && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) && double.IsFinite(seconds) && seconds >= 0)
                    {
                        if (key == "intro_seconds")
                        {
                            track.IntroSeconds = seconds;
                        }
                        else
                        {
                            track.OffsetSeconds = seconds;
                        }
                        continue;
                    }
                }
                throw new NotSupportedException($"Unsupported external music configuration setting or value: [{section}] {key} = {value}.");
            }
            return result;
        }
    }
}
