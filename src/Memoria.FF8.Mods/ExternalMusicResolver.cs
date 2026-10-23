using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Memoria.FF8.Mods;

public sealed record ExternalMusicSelection(ModFile File, double OffsetSeconds, int Volume);

/// <summary>Resolves external tracks from the selected native filename, without changing native music identity or routing.</summary>
public sealed class ExternalMusicResolver
{
    private readonly ModCatalogSnapshot catalog;
    private readonly Dictionary<string, MusicConfiguration> configurations = new(StringComparer.Ordinal);

    public ExternalMusicResolver(ModCatalogSnapshot catalog)
    {
        this.catalog = catalog;
        foreach (ModFile file in catalog.FindAll(ModFormat.Junction, "music/config.toml"))
        {
            if (catalog.Packages.Any(package => package.Id == file.PackageId && package.Status == ModEffectiveStatus.Active && package.JunctionMetadata?.UseExternalMusic != false))
            {
                configurations[file.PackageId] = MusicConfiguration.Parse(Encoding.UTF8.GetString(file.ReadBytes()));
            }
        }
    }

    public static IReadOnlyList<ModDiagnostic> Validate(ModCatalogSnapshot catalog)
    {
        List<ModDiagnostic> diagnostics = [];
        foreach (ModFile file in catalog.FindAll(ModFormat.Junction, "music/config.toml"))
        {
            if (!catalog.Packages.Any(package => package.Id == file.PackageId && package.Status == ModEffectiveStatus.Active && package.JunctionMetadata?.UseExternalMusic != false))
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
            if (package.Status != ModEffectiveStatus.Active || package.Format != ModFormat.Junction || package.JunctionMetadata?.UseExternalMusic == false)
            {
                continue;
            }
            MusicConfiguration config = configurations.GetValueOrDefault(package.Id) ?? MusicConfiguration.Empty;
            TrackConfiguration? track = config.Tracks.GetValueOrDefault(name);
            string selectedName = noIntro && track?.NoIntroTrack is string alternate ? alternate : name;
            ModFile? file = catalog.FindAll(ModFormat.Junction, $"music/{selectedName}.ogg").FirstOrDefault(file => file.PackageId == package.Id);
            if (file is null)
            {
                if (selectedName != name && catalog.FindAll(ModFormat.Junction, $"music/{name}.ogg").Any(file => file.PackageId == package.Id))
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
            return new ExternalMusicSelection(file, offset, config.Volume);
        }
        return null;
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
