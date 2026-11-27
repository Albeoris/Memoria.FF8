using System.Collections.Frozen;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Mods;

/// <summary>Published scene color lookups. Place entries precede global entries of the same phase.</summary>
public sealed class SceneColorGradeMods
{
    private static readonly string[] PhaseNames = ["day", "dusk", "night", "dawn"];
    public static SceneColorGradeMods Empty { get; } = new(new Dictionary<string, ColorLookupTable>().ToFrozenDictionary(StringComparer.OrdinalIgnoreCase), []);
    private readonly FrozenDictionary<string, ColorLookupTable> tables;

    private SceneColorGradeMods(FrozenDictionary<string, ColorLookupTable> tables, IReadOnlyCollection<string> packageIds)
    {
        this.tables = tables;
        PackageIds = packageIds;
    }

    public IReadOnlyCollection<string> PackageIds { get; }

    public static SceneColorGradeMods Load(ModCatalogSnapshot catalog, ICollection<ModDiagnostic> diagnostics)
    {
        Dictionary<string, ColorLookupTable> tables = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> owners = new(StringComparer.OrdinalIgnoreCase);
        foreach (ModFile file in catalog.Files.Where(file => file.Format is ModFormat.Memoria or ModFormat.Demaster).OrderBy(file => file.Format != ModFormat.Memoria))
        {
            string directory = file.Format == ModFormat.Memoria ? "scene-lighting/lut/" : "lut/";
            if (!file.RelativePath.StartsWith(directory, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            string key = file.RelativePath[directory.Length..];
            string phase = Path.GetFileNameWithoutExtension(key);
            string normal = phase.StartsWith("lunar_", StringComparison.OrdinalIgnoreCase) ? phase[6..] : phase;
            if (!IsLookupScope(key) || !PhaseNames.Contains(normal, StringComparer.OrdinalIgnoreCase) || !key.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new("SceneLighting.UnsupportedLookup", "Expected a PNG strip named day, dusk, night or dawn, optionally prefixed with lunar_.", file.PackageId, file.RelativePath));
                continue;
            }
            if (tables.ContainsKey(key))
            {
                continue;
            }
            try
            {
                ColorLookupTable table = ColorLookupTable.FromStrip(TextureImageDecoder.Decode(file.ReadBytes(), file.RelativePath).Image);
                tables.Add(key, table);
                owners.Add(file.PackageId);
            }
            catch (Exception error) when (error is IOException or InvalidDataException or NotSupportedException or UnauthorizedAccessException)
            {
                diagnostics.Add(new("SceneLighting.InvalidLookup", error.Message, file.PackageId, file.RelativePath));
            }
        }
        return new(tables.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase), owners.ToArray());
    }

    public SceneColorGrade Resolve(string place, SceneLightingState? state)
    {
        if (tables.Count == 0 || state is null || state.GradeStrength <= 0)
        {
            return default;
        }
        (string first, string second, float mix) = state.GradeTime switch
        {
            SceneTime.Day => ("day", "day", 0),
            SceneTime.Night => ("night", "night", 0),
            _ => ResolveCycle(state.Cycle, state.ClockSeconds)
        };
        ColorLookupTable? a = Table(first);
        ColorLookupTable? b = mix > 0 ? Table(second) : null;
        if (a is not null && b is not null && a.Size != b.Size)
        {
            a = mix < 0.5f ? a : b;
            b = null;
            mix = 0;
        }
        return new(a, b, mix, state.GradeStrength);

        ColorLookupTable? Table(string phase)
        {
            ColorLookupTable? result = state.LunarGrade ? Find("lunar_" + phase) ?? Find(phase) : Find(phase);
            return result?.IsIdentity == true ? null : result;
        }

        ColorLookupTable? Find(string phase)
        {
            string name = phase + ".png";
            return tables.GetValueOrDefault(place + "/" + name) ?? tables.GetValueOrDefault(name);
        }
    }

    private static bool IsLookupScope(string path)
    {
        string[] parts = path.ToLowerInvariant().Split('/');
        return parts.Length == 1 || parts is ["world", _] || parts is ["field", _, _] ||
            parts is ["battle", var stage, _] && stage.Length == 8 && stage.StartsWith("a0stg", StringComparison.Ordinal) && stage.AsSpan(5).IndexOfAnyExceptInRange('0', '9') < 0;
    }

    private static (string First, string Second, float Mix) ResolveCycle(SceneLightingCycle cycle, double seconds)
    {
        double time = cycle.Wrap(seconds);
        double fade = 1.5 * cycle.Fade;
        double dusk = Math.Max(0, Math.Min(cycle.Day * cycle.Evening, cycle.Day - cycle.Fade / 2));
        double middleOfNight = cycle.Day + cycle.Night / 2;
        double nightStart = Math.Min(cycle.Day + fade, middleOfNight);
        double nightEnd = Math.Max(cycle.Length - fade, middleOfNight);
        double dawnEnd = Math.Min(Math.Max(fade, 0.1 * cycle.Day), dusk / 2);
        if (time < dawnEnd)
        {
            return ("dawn", "day", Smooth(time, 0, dawnEnd));
        }
        if (time < dusk)
        {
            return ("day", "day", 0);
        }
        if (time < cycle.Day)
        {
            return ("day", "dusk", Smooth(time, dusk, cycle.Day));
        }
        if (time < nightStart)
        {
            return ("dusk", "night", Smooth(time, cycle.Day, nightStart));
        }
        if (time < nightEnd)
        {
            return ("night", "night", 0);
        }
        return ("night", "dawn", Smooth(time, nightEnd, cycle.Length));
    }

    private static float Smooth(double position, double start, double end)
    {
        double t = Math.Clamp((position - start) / (end - start), 0, 1);
        return (float)(t * t * (3 - 2 * t));
    }
}
