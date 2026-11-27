using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime;

public enum SceneTime { Cycle, Day, Night }
public enum SceneNightMode { Auto, Never, Always }
public enum SceneLightAvailability { Always, Night, Day }

public readonly record struct SceneFieldLighting(SceneTime Time, bool Sky);
public readonly record struct SceneClockSnapshot(double Seconds, bool Paused, SceneFieldLighting? BattleField = null)
{
    public void Validate()
    {
        if (!double.IsFinite(Seconds) || Seconds < 0 || BattleField is { } field && !Enum.IsDefined(field.Time))
        {
            throw new InvalidDataException("Invalid scene lighting clock.");
        }
    }
}
public readonly record struct SceneNumberRange(int First, int Last)
{
    public bool Contains(int value) => value >= First && value <= Last;
}

public interface ISceneLightingProfileSource
{
    SceneLightingProfile SceneLightingProfile { get; }
}

public sealed record SceneLightPoint(Vector3 Position, Vector3 Color, float Range, SceneLightAvailability When);

public sealed record SceneLightLook
{
    public SceneTime Time { get; init; }
    public SceneNightMode Night { get; init; }
    public Vector3 DayTint { get; init; } = Vector3.One;
    public Vector3 NightTint { get; init; } = new(0.55f, 0.65f, 1);
    public float NightLevel { get; init; } = 0.3f;
    public float Ao { get; init; } = 0.5f;
    public float Glow { get; init; } = 1;
    public float Sun { get; init; } = 0.35f;
    public float FighterSun { get; init; } = 0.5f;
    public float Shadow { get; init; } = 0.5f;
    public float Relief { get; init; } = 1;
    public float Specular { get; init; } = 0.5f;
    public float Parallax { get; init; } = 4;
    public float SunAzimuth { get; init; } = 30;
    public float SunHeight { get; init; } = 60;
    public bool Smooth { get; init; } = true;
    public ImmutableArray<SceneLightPoint> Lights { get; init; } = [];
}

public sealed record SceneLookOverride
{
    public SceneTime? Time { get; init; }
    public SceneNightMode? Night { get; init; }
    public Vector3? DayTint { get; init; }
    public Vector3? NightTint { get; init; }
    public float? NightLevel { get; init; }
    public float? Ao { get; init; }
    public float? Glow { get; init; }
    public float? Sun { get; init; }
    public float? FighterSun { get; init; }
    public float? Shadow { get; init; }
    public float? Relief { get; init; }
    public float? Specular { get; init; }
    public float? Parallax { get; init; }
    public float? SunAzimuth { get; init; }
    public float? SunHeight { get; init; }
    public bool? Smooth { get; init; }
    public ImmutableArray<SceneLightPoint> Lights { get; init; } = [];

    public SceneLightLook Apply(SceneLightLook look, int lightLimit)
    {
        return look with
        {
            Time = Time ?? look.Time,
            Night = Night ?? look.Night,
            DayTint = DayTint ?? look.DayTint,
            NightTint = NightTint ?? look.NightTint,
            NightLevel = NightLevel ?? look.NightLevel,
            Ao = Ao ?? look.Ao,
            Glow = Glow ?? look.Glow,
            Sun = Sun ?? look.Sun,
            FighterSun = FighterSun ?? look.FighterSun,
            Shadow = Shadow ?? look.Shadow,
            Relief = Relief ?? look.Relief,
            Specular = Specular ?? look.Specular,
            Parallax = Parallax ?? look.Parallax,
            SunAzimuth = SunAzimuth ?? look.SunAzimuth,
            SunHeight = SunHeight ?? look.SunHeight,
            Smooth = Smooth ?? look.Smooth,
            Lights = Lights.IsEmpty ? look.Lights : look.Lights.AddRange(Lights.Take(Math.Max(0, lightLimit - look.Lights.Length)))
        };
    }
}

public sealed record SceneLookRule(bool Battle, bool World, ImmutableArray<SceneNumberRange> Stages, SceneNumberRange? Moment, SceneLookOverride Look)
{
    public bool Matches(bool battle, int stage, int moment)
    {
        return (battle ? Battle : World) && (Stages.IsEmpty || battle && Stages.Any(range => range.Contains(stage))) && (Moment?.Contains(moment) ?? true);
    }
}

public sealed class SceneFieldTimeRule
{
    private readonly string[] fields;
    private readonly string[] excludedFields;
    private readonly SceneNumberRange? moment;

    public SceneFieldTimeRule(IEnumerable<string> fields, IEnumerable<string> excludedFields, SceneNumberRange? moment, SceneTime time)
    {
        this.fields = fields.Select(pattern => pattern.ToLowerInvariant()).ToArray();
        this.excludedFields = excludedFields.Select(pattern => pattern.ToLowerInvariant()).ToArray();
        this.moment = moment;
        Time = time;
    }

    public SceneTime Time { get; }

    public bool Matches(string field, int storyMoment)
    {
        string name = field.ToLowerInvariant();
        return (moment?.Contains(storyMoment) ?? true) && fields.Any(pattern => NameMatches(pattern, name)) && !excludedFields.Any(pattern => NameMatches(pattern, name));
    }

    private static bool NameMatches(string pattern, string name)
    {
        int next = 0;
        int index = 0;
        int star = -1;
        int matched = 0;
        while (index < name.Length)
        {
            if (next < pattern.Length && pattern[next] == '*')
            {
                star = next++;
                matched = index;
            }
            else if (next < pattern.Length && pattern[next] == name[index])
            {
                next++;
                index++;
            }
            else if (star >= 0)
            {
                next = star + 1;
                index = ++matched;
            }
            else
            {
                return false;
            }
        }
        return pattern.AsSpan(next).Trim('*').IsEmpty;
    }
}

public sealed record SceneLightingProfile
{
    public static SceneLightingProfile Empty { get; } = new();

    public SceneLightingCycle Cycle { get; init; } = new();
    public bool ClockEnabled { get; init; }
    public bool ClockPerSave { get; init; } = true;
    public bool SceneLightsEnabled { get; init; }
    public bool GradeEnabled { get; init; }
    public float GradeStrength { get; init; } = 1;
    public float IndoorGradeStrength { get; init; } = 0.5f;
    public SceneNumberRange? LunarGrade { get; init; }
    public ImmutableDictionary<string, SceneFieldLighting> Fields { get; init; } = ImmutableDictionary.Create<string, SceneFieldLighting>(StringComparer.OrdinalIgnoreCase);
    public ImmutableArray<SceneFieldTimeRule> FieldRules { get; init; } = [];
    public SceneLightLook Battle { get; init; } = new();
    public SceneLightLook World { get; init; } = new() { Sun = 0.25f };
    public ImmutableArray<SceneLookRule> SceneRules { get; init; } = [];

    public SceneFieldLighting ResolveField(string field, int moment)
    {
        SceneFieldLighting place = Fields.GetValueOrDefault(field);
        if (ClockEnabled)
        {
            foreach (SceneFieldTimeRule rule in FieldRules)
            {
                if (rule.Matches(field, moment))
                {
                    place = place with { Time = rule.Time };
                }
            }
        }
        return place;
    }

    public SceneLightingState ResolveFieldState(SceneFieldLighting field, int moment, double seconds)
    {
        SceneTime time = ClockEnabled ? field.Time : SceneTime.Day;
        return ResolveState(time, moment, seconds, !field.Sky, null);
    }

    public SceneLightingState ResolveScene(bool battle, int stage, int moment, SceneFieldLighting origin, bool fromWorld, double seconds)
    {
        bool fixedPlace = battle && !fromWorld && origin.Time != SceneTime.Cycle;
        bool outdoors = !battle || fromWorld || origin.Sky;
        SceneLightLook? look = null;
        if (SceneLightsEnabled)
        {
            look = battle ? Battle : World;
            foreach (SceneLookRule rule in SceneRules)
            {
                if (rule.Matches(battle, stage, moment))
                {
                    look = rule.Look.Apply(look, battle ? 4 : 64);
                }
            }
        }
        SceneTime time = look?.Time ?? SceneTime.Cycle;
        if (time == SceneTime.Cycle && (!ClockEnabled || fixedPlace || look?.Night == SceneNightMode.Never || !outdoors && look?.Night != SceneNightMode.Always))
        {
            time = SceneTime.Day;
        }
        return ResolveState(time, moment, seconds, !outdoors, look);
    }

    private SceneLightingState ResolveState(SceneTime time, int moment, double seconds, bool indoors, SceneLightLook? look)
    {
        float night = time == SceneTime.Cycle ? (float)Cycle.NightAmount(seconds) : time == SceneTime.Night ? 1 : 0;
        float evening = time == SceneTime.Cycle ? (float)Cycle.EveningAmount(seconds) : night;
        Vector3 tint = look is null ? Vector3.One : Vector3.Lerp(look.DayTint, look.NightTint * look.NightLevel, night);
        float grade = GradeEnabled ? Math.Clamp(GradeStrength * (indoors ? IndoorGradeStrength : 1), 0, 1) : 0;
        return new(time, night, evening, tint, look, Cycle, seconds, grade, ClockEnabled ? time : SceneTime.Day, LunarGrade?.Contains(moment) ?? false);
    }
}

public sealed record SceneLightingState(SceneTime Time, float NightAmount, float EveningAmount, Vector3 Tint, SceneLightLook? Look, SceneLightingCycle Cycle, double ClockSeconds, float GradeStrength, SceneTime GradeTime, bool LunarGrade);
