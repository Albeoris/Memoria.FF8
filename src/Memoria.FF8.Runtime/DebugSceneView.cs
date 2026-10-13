namespace Memoria.FF8.Runtime;

public enum DebugSceneTarget
{
    Clear,
    Actor,
    Background,
    Dialogue
}

/// <summary>Presentation-only research command; never an ordinary player action.</summary>
public sealed record DebugSceneCommand(DebugSceneTarget Target, int? ActorId = null, int? ModuleId = null, bool? Visible = null, float? X = null, float? Y = null, float? Z = null, int? AnimationId = null, float? Frame = null, bool? Playing = null, float? FramesPerSecond = null);
public sealed record DebugActorView(bool? Visible = null, float? X = null, float? Y = null, float? Z = null, int? AnimationId = null, float? Frame = null, bool Playing = false, float FramesPerSecond = 30);
public sealed record DebugSceneView(int FieldId)
{
    public bool DialogueVisible { get; init; } = true;
    public bool BackgroundVisible { get; init; } = true;
    public HashSet<int> HiddenBackgroundModules { get; init; } = [];
    public Dictionary<int, DebugActorView> Actors { get; init; } = [];

    public void Validate()
    {
        if (FieldId < 0 || HiddenBackgroundModules.Any(module => module is < 0 or > 255) || Actors.Any(pair => pair.Key < 0 || pair.Value.AnimationId < 0 || pair.Value.Frame < 0 || !Finite(pair.Value.X) || !Finite(pair.Value.Y) || !Finite(pair.Value.Z) || !Finite(pair.Value.Frame) || !float.IsFinite(pair.Value.FramesPerSecond) || pair.Value.FramesPerSecond is <= 0 or > 240))
        {
            throw new InvalidDataException("Invalid debug presentation override.");
        }
    }

    private static bool Finite(float? value)
    {
        return value is null || float.IsFinite(value.Value);
    }
}
