using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Mods;

public static class DemasterSceneLighting
{
    private const string FieldRulesPath = "field_time.txt";
    private const string SceneRulesPath = "scene_light.txt";
    private const string ConfigPath = "demaster.conf";
    private const string NativeDirectory = "scene-lighting/";
    private const string FieldDirectory = "textures/field_bg/";

    public static bool IsInput(string path)
    {
        return path.Equals(ConfigPath, StringComparison.OrdinalIgnoreCase) || path.Equals(FieldRulesPath, StringComparison.OrdinalIgnoreCase)
            || path.Equals(SceneRulesPath, StringComparison.OrdinalIgnoreCase) || FieldName(path) is not null;
    }

    public static bool IsNativeInput(string path)
    {
        return path.StartsWith(NativeDirectory, StringComparison.OrdinalIgnoreCase) && IsInput(path[NativeDirectory.Length..]);
    }

    public static SceneLightingProfile Load(ModCatalogSnapshot catalog, ICollection<ModDiagnostic> diagnostics, string? language = null)
    {
        SceneLightingProfile profile = SceneLightingProfile.Empty;
        var defaultFields = ImmutableDictionary.CreateBuilder<string, SceneFieldLighting>(StringComparer.OrdinalIgnoreCase);
        var localizedFields = ImmutableDictionary.CreateBuilder<string, SceneFieldLighting>(StringComparer.OrdinalIgnoreCase);
        IEnumerable<ModFile> files = catalog.Files.Where(file => file.Format == ModFormat.Demaster && IsInput(file.RelativePath)
            || file.Format == ModFormat.Memoria && IsNativeInput(file.RelativePath));
        foreach (ModFile file in files.OrderByDescending(file => file.Format == ModFormat.Memoria).Reverse())
        {
            string path = file.Format == ModFormat.Memoria ? file.RelativePath[NativeDirectory.Length..] : file.RelativePath;
            void Warn(int line, string message)
            {
                diagnostics.Add(new("SceneLighting", $"Line {line}: {message}", file.PackageId, file.RelativePath));
            }
            try
            {
                if (ReferenceEquals(profile, SceneLightingProfile.Empty))
                {
                    profile = new() { ClockEnabled = true, SceneLightsEnabled = true, GradeEnabled = true };
                }
                string content = Encoding.UTF8.GetString(file.ReadBytes());
                if (FieldName(path) is { } field)
                {
                    string owner = Path.GetFileName(Path.GetDirectoryName(path))!;
                    if (field.Equals(owner, StringComparison.OrdinalIgnoreCase))
                    {
                        defaultFields[owner] = ReadField(content, Warn);
                    }
                    else if (language is not null && field.Equals($"{owner}_{language}", StringComparison.OrdinalIgnoreCase))
                    {
                        localizedFields[owner] = ReadField(content, Warn);
                    }
                    continue;
                }
                profile = path.ToLowerInvariant() switch
                {
                    ConfigPath => ReadConfiguration(profile, content, Warn),
                    FieldRulesPath => profile with { FieldRules = ReadFieldRules(content, Warn) },
                    SceneRulesPath => ReadSceneRules(profile, content, Warn),
                    _ => profile
                };
            }
            catch (IOException error)
            {
                diagnostics.Add(new("SceneLighting", error.Message, file.PackageId, file.RelativePath));
            }
            catch (UnauthorizedAccessException error)
            {
                diagnostics.Add(new("SceneLighting", error.Message, file.PackageId, file.RelativePath));
            }
        }
        return ReferenceEquals(profile, SceneLightingProfile.Empty) ? profile : profile with { Fields = defaultFields.ToImmutable().SetItems(localizedFields) };
    }

    private static string? FieldName(string path)
    {
        const string suffix = "_pbr.txt";
        return path.StartsWith(FieldDirectory, StringComparison.OrdinalIgnoreCase) && path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? Path.GetFileName(path)[..^suffix.Length] : null;
    }

    private static SceneLightingProfile ReadConfiguration(SceneLightingProfile profile, string content, Action<int, string> warn)
    {
        Dictionary<string, (int Line, string Value)> settings = new(StringComparer.OrdinalIgnoreCase);
        foreach (InputSection section in Sections(content, (_, _) => { }).Where(section => section.Name.Length == 0))
        {
            foreach (InputEntry entry in section.Entries)
            {
                settings[entry.Key] = (entry.Line, entry.Value);
            }
        }
        double Number(string key, double fallback)
        {
            if (!settings.TryGetValue(key, out var entry))
            {
                return fallback;
            }
            if (TryNumber(entry.Value, out double value))
            {
                return value;
            }
            warn(entry.Line, $"Invalid {key}: {entry.Value}");
            return fallback;
        }
        int Integer(string key, int fallback)
        {
            if (!settings.TryGetValue(key, out var entry))
            {
                return fallback;
            }
            if (int.TryParse(entry.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                return value;
            }
            warn(entry.Line, $"Invalid {key}: {entry.Value}");
            return fallback;
        }
        int lunarFrom = Integer("LUT_LUNAR_FROM", 0);
        int lunarTo = Integer("LUT_LUNAR_TO", ushort.MaxValue);
        return profile with
        {
            ClockEnabled = Integer("DAY_NIGHT", 1) != 0,
            SceneLightsEnabled = Integer("SCENE_LIGHT", 1) != 0,
            GradeEnabled = Integer("LUT", 1) != 0,
            GradeStrength = (float)Math.Clamp(Number("LUT_STRENGTH", 1), 0, 1),
            IndoorGradeStrength = (float)Math.Clamp(Number("LUT_INSIDE", 0.5), 0, 1),
            LunarGrade = lunarFrom > 0 ? new(lunarFrom, lunarTo) : null,
            Cycle = new(Number("DAY_NIGHT_DAY", 480), Number("DAY_NIGHT_NIGHT", 240), Number("DAY_NIGHT_FADE", 40), Number("DAY_NIGHT_BATTLE", 3), Number("DAY_NIGHT_EVENING", 0.9))
        };
    }

    private static SceneFieldLighting ReadField(string content, Action<int, string> warn)
    {
        SceneFieldLighting field = default;
        foreach (InputEntry entry in Sections(content, (_, _) => { }, ";#").SelectMany(section => section.Entries))
        {
            if (entry.Key == "time" && TryTime(entry.Value) is SceneTime time)
            {
                field = field with { Time = time };
            }
            else if (entry.Key == "sky" && (TryBool(entry.Value) ?? ParseBooleanWord(entry.Value)) is bool sky)
            {
                field = field with { Sky = sky };
            }
            else if (entry.Key is "time" or "sky")
            {
                warn(entry.Line, $"Invalid {entry.Key}: {entry.Value}");
            }
        }
        return field;
    }

    private static ImmutableArray<SceneFieldTimeRule> ReadFieldRules(string content, Action<int, string> warn)
    {
        ImmutableArray<SceneFieldTimeRule>.Builder rules = ImmutableArray.CreateBuilder<SceneFieldTimeRule>();
        foreach (InputSection section in Sections(content, warn, ";#"))
        {
            if (!section.Name.StartsWith("rule", StringComparison.Ordinal))
            {
                if (section.Name.Length > 0 || section.Entries.Count > 0)
                {
                    warn(section.Line, "Expected a rule section.");
                }
                continue;
            }
            List<string> included = [];
            List<string> excluded = [];
            SceneTime? time = null;
            SceneNumberRange? moment = null;
            foreach (InputEntry entry in section.Entries)
            {
                switch (entry.Key)
                {
                    case "field":
                        foreach (string pattern in Words(entry.Value.Replace(',', ' ')))
                        {
                            if (pattern.StartsWith('!') && pattern.Length > 1)
                            {
                                excluded.Add(pattern[1..]);
                            }
                            else if (pattern != "!")
                            {
                                included.Add(pattern);
                            }
                        }
                        break;
                    case "time" when TryTime(entry.Value) is SceneTime parsedTime:
                        time = parsedTime;
                        break;
                    case "moment" when TryFieldMoment(entry.Value) is SceneNumberRange parsed:
                        moment = parsed;
                        break;
                    default:
                        warn(entry.Line, $"Invalid {entry.Key}: {entry.Value}");
                        break;
                }
            }
            if (included.Count == 0 || time is null)
            {
                warn(section.Line, "A field time rule needs a field selector and a time.");
                continue;
            }
            rules.Add(new(included, excluded, moment, time.Value));
        }
        return rules.ToImmutable();
    }

    private static SceneLightingProfile ReadSceneRules(SceneLightingProfile profile, string content, Action<int, string> warn)
    {
        SceneLightLook battle = new();
        SceneLightLook world = new() { Sun = 0.25f };
        ImmutableArray<SceneLookRule>.Builder rules = ImmutableArray.CreateBuilder<SceneLookRule>();
        foreach (InputSection section in Sections(content, warn))
        {
            bool rule = section.Name.StartsWith("rule ", StringComparison.Ordinal) || section.Name == "rule";
            if (!rule && section.Name is not ("battle" or "world"))
            {
                if (section.Name.Length > 0)
                {
                    warn(section.Line, $"Unknown section: {section.Name}");
                }
                continue;
            }
            SceneLookOverride look = new();
            bool inBattle = true;
            bool inWorld = true;
            SceneNumberRange? moment = null;
            ImmutableArray<SceneNumberRange> stages = [];
            int lightLimit = section.Name == "battle" ? 4 : 64;
            foreach (InputEntry entry in section.Entries)
            {
                string value = entry.Value.ToLowerInvariant();
                switch (entry.Key)
                {
                    case "where" when rule && value is ("battle" or "world" or "both"):
                        inBattle = value != "world";
                        inWorld = value != "battle";
                        continue;
                    case "stage" when rule:
                        ImmutableArray<SceneNumberRange> selected = ReadRanges(entry.Value);
                        if (!selected.IsEmpty)
                        {
                            stages = selected;
                            continue;
                        }
                        break;
                    case "moment" when rule && TryRange(entry.Value, int.MaxValue) is SceneNumberRange range:
                        moment = range;
                        continue;
                    case "light" when TryLight(entry.Value) is SceneLightPoint point:
                        if (look.Lights.Length < lightLimit)
                        {
                            look = look with { Lights = look.Lights.Add(point) };
                        }
                        else
                        {
                            warn(entry.Line, $"This section supports up to {lightLimit} lights.");
                        }
                        continue;
                }
                if (ReadLook(look, entry.Key, entry.Value) is SceneLookOverride changed)
                {
                    look = changed;
                }
                else
                {
                    warn(entry.Line, $"Invalid {entry.Key}: {entry.Value}");
                }
            }
            if (rule)
            {
                rules.Add(new(inBattle, inWorld, stages, moment, look));
            }
            else if (section.Name == "battle")
            {
                battle = look.Apply(battle, lightLimit);
            }
            else
            {
                world = look.Apply(world, lightLimit);
            }
        }
        return profile with { Battle = battle, World = world, SceneRules = rules.ToImmutable() };
    }

    private static SceneLookOverride? ReadLook(SceneLookOverride look, string key, string value)
    {
        switch (key)
        {
            case "time" when TryTime(value) is SceneTime time:
                return look with { Time = time };
            case "night":
                return value.ToLowerInvariant() switch
                {
                    "auto" => look with { Night = SceneNightMode.Auto },
                    "0" => look with { Night = SceneNightMode.Never },
                    "1" => look with { Night = SceneNightMode.Always },
                    _ => null
                };
            case "smooth" when TryBool(value) is bool smooth:
                return look with { Smooth = smooth };
            case "day_tint" or "night_tint" when TryVector(Words(value), 4) is Vector3 tint:
                return key == "day_tint" ? look with { DayTint = tint } : look with { NightTint = tint };
        }
        if (!TryNumber(value, out double numeric))
        {
            return null;
        }
        float number = (float)numeric;
        return key switch
        {
            "night_level" when numeric is >= 0 and <= 1 => look with { NightLevel = number },
            "ao" when numeric is >= 0 and <= 1 => look with { Ao = number },
            "sun" when numeric is >= 0 and <= 1 => look with { Sun = number },
            "fighter_sun" when numeric is >= 0 and <= 1 => look with { FighterSun = number },
            "shadow" when numeric is >= 0 and <= 1 => look with { Shadow = number },
            "glow" when numeric is >= 0 and <= 4 => look with { Glow = number },
            "relief" when numeric is >= 0 and <= 4 => look with { Relief = number },
            "specular" when numeric is >= 0 and <= 4 => look with { Specular = number },
            "parallax" when numeric is >= 0 and <= 64 => look with { Parallax = number },
            "sun_azimuth" when numeric is >= -360 and <= 360 => look with { SunAzimuth = number },
            "sun_height" when numeric is >= 5 and <= 90 => look with { SunHeight = number },
            _ => null
        };
    }

    private static SceneLightPoint? TryLight(string value)
    {
        string[] words = Words(value);
        if (words.Length is not (7 or 8) || !TryNumber(words[0], out double x) || !TryNumber(words[1], out double y) || !TryNumber(words[2], out double z)
            || TryVector(words.AsSpan(3, 3), 64) is not Vector3 color || !TryNumber(words[6], out double range) || range <= 0)
        {
            return null;
        }
        SceneLightAvailability? when = words.Length == 7 ? SceneLightAvailability.Always : words[7].ToLowerInvariant() switch
        {
            "always" => SceneLightAvailability.Always,
            "day" => SceneLightAvailability.Day,
            "night" => SceneLightAvailability.Night,
            _ => null
        };
        return when is null ? null : new(new((float)x, (float)y, (float)z), color, (float)range, when.Value);
    }

    private static Vector3? TryVector(ReadOnlySpan<string> words, float max)
    {
        if (words.Length != 3 || !TryNumber(words[0], out double red) || !TryNumber(words[1], out double green) || !TryNumber(words[2], out double blue) || red < 0 || green < 0 || blue < 0)
        {
            return null;
        }
        return new((float)Math.Min(red, max), (float)Math.Min(green, max), (float)Math.Min(blue, max));
    }

    private static bool TryNumber(string value, out double number)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && double.IsFinite(number);
    }

    private static SceneTime? TryTime(string value) => value.ToLowerInvariant() switch
    {
        "cycle" => SceneTime.Cycle,
        "day" => SceneTime.Day,
        "night" => SceneTime.Night,
        _ => null
    };

    private static bool? TryBool(string value) => value.ToLowerInvariant() switch
    {
        "1" or "on" or "yes" => true,
        "0" or "off" or "no" => false,
        _ => null
    };

    private static bool? ParseBooleanWord(string value)
    {
        return bool.TryParse(value, out bool result) ? result : null;
    }

    private static SceneNumberRange? TryFieldMoment(string value)
    {
        string[] parts = Words(value.Replace('-', ' '));
        if (parts.Length is not (1 or 2) || !ushort.TryParse(parts[0], CultureInfo.InvariantCulture, out ushort first))
        {
            return null;
        }
        if (parts.Length == 1)
        {
            return new(first, first);
        }
        return ushort.TryParse(parts[1], CultureInfo.InvariantCulture, out ushort last) && last >= first ? new(first, last) : null;
    }

    private static ImmutableArray<SceneNumberRange> ReadRanges(string value)
    {
        ImmutableArray<SceneNumberRange>.Builder ranges = ImmutableArray.CreateBuilder<SceneNumberRange>();
        foreach (string part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (TryRange(part, int.MaxValue) is not SceneNumberRange range)
            {
                return [];
            }
            ranges.Add(range);
        }
        return ranges.ToImmutable();
    }

    private static SceneNumberRange? TryRange(string value, int max)
    {
        string[] parts = value.Split('-', StringSplitOptions.TrimEntries);
        if (parts.Length is not (1 or 2) || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int first) || first < 0 || first > max)
        {
            return null;
        }
        if (parts.Length == 1)
        {
            return new(first, first);
        }
        return int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int last) && last >= first && last <= max
            ? new(first, last) : null;
    }

    private static string[] Words(string text) => text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    private sealed record InputEntry(int Line, string Key, string Value);
    private sealed record InputSection(int Line, string Name, List<InputEntry> Entries);

    private static IEnumerable<InputSection> Sections(string content, Action<int, string> warn, string commentMarkers = ";")
    {
        using StringReader reader = new(content.TrimStart('\uFEFF'));
        InputSection section = new(1, "", []);
        int line = 0;
        while (reader.ReadLine() is string raw)
        {
            line++;
            int comment = raw.AsSpan().IndexOfAny(commentMarkers);
            string text = raw[..(comment < 0 ? raw.Length : comment)].Trim();
            if (text.Length == 0)
            {
                continue;
            }
            if (text[0] == '[')
            {
                yield return section;
                int close = text.IndexOf(']');
                section = new(line, text[1..(close < 0 ? text.Length : close)].Trim().ToLowerInvariant(), []);
                continue;
            }
            int equals = text.IndexOf('=');
            if (equals <= 0)
            {
                warn(line, "Expected key = value.");
                continue;
            }
            section.Entries.Add(new(line, text[..equals].Trim().ToLowerInvariant(), text[(equals + 1)..].Trim()));
        }
        yield return section;
    }
}
