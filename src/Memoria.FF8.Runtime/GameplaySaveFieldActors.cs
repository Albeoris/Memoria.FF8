using System.Numerics;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Logical field actors and consumed action state; current model definitions are reconstructed separately.</summary>
public sealed record GameplaySaveFieldActors([property: JsonRequired] int Version, [property: JsonRequired] int FieldId, [property: JsonRequired] GameplaySaveFieldActor[] Actors, [property: JsonRequired] int[] RegisteredActors, [property: JsonRequired] Dictionary<int, NativeModelPartResourceState> ModelParts)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<int, Dictionary<int, GameplaySaveModelPartOffset>>? ModelPartOffsets { get; init; }

    public GameplaySaveFieldActors Detach()
    {
        Validate();
        return GameplaySaveCodec.Detach(this);
    }

    public void Validate()
    {
        if (Version != 1 || FieldId is < 72 or > ushort.MaxValue || Actors is null || RegisteredActors is null || RegisteredActors.Length > 32 || RegisteredActors.Any(id => id < 0 || id >= Actors.Length) || RegisteredActors.Distinct().Count() != RegisteredActors.Length || ModelParts is null || Actors.Any(actor => actor is null) || Actors.Select(actor => actor.ObjectId).Distinct().Count() != Actors.Length || ModelParts.Any(pair => pair.Key is < 0 or > 255 || pair.Value?.Translations is null || pair.Value.Translations.Any(entry => entry.Key < 0 || entry.Value is not { Mode: 4, Duration: not 0 })))
        {
            throw new InvalidDataException("Gameplay field actors require a supported field, distinct logical owners and settled model-part translations.");
        }
        foreach (GameplaySaveFieldActor actor in Actors)
        {
            actor.Validate();
        }
        if (ModelPartOffsets is not null)
        {
            if (ModelPartOffsets.Any(pair => pair.Key is < 0 or > 255 || ModelParts.ContainsKey(pair.Key) || pair.Value is null || pair.Value.Any(part => part.Key < 0 || part.Value is null)))
            {
                throw new InvalidDataException("Gameplay model-part offsets have invalid or duplicate resource owners.");
            }
            foreach (GameplaySaveModelPartOffset offset in ModelPartOffsets.Values.SelectMany(parts => parts.Values))
            {
                offset.Validate();
            }
        }
    }
}

public sealed record GameplaySaveFieldActor([property: JsonRequired] int ObjectId, [property: JsonRequired] int Label, [property: JsonRequired] GameplaySaveActorIdentity Identity, [property: JsonRequired] GameplaySaveActorPose Pose, [property: JsonRequired] GameplaySaveActorControl Control, [property: JsonRequired] GameplaySaveActorAppearance Appearance, [property: JsonRequired] GameplaySaveActorAnimation Animation, [property: JsonRequired] GameplaySaveActorAnimation? AnimationBackup, [property: JsonRequired] GameplaySaveActorFace Face, [property: JsonRequired] GameplaySaveActorLine Line, [property: JsonRequired] GameplaySaveActorFootsteps Footsteps, [property: JsonRequired] GameplaySaveActorOffset? Offset)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GameplaySaveActorTurn? Turn { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GameplaySaveActorMovement? Movement { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GameplaySaveActorJump? Jump { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GameplaySaveActorLadderMode3? LadderMode3 { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GameplaySaveActorLadder? Ladder { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool PushContactPending { get; init; }

    [JsonRequired]
    public GameplaySaveActorLadderAnimations? LadderAnimations { get; init; }

    public void Validate()
    {
        Turn?.Validate();
        Movement?.Validate();
        Jump?.Validate();
        LadderMode3?.Validate();
        Ladder?.Validate();
        if (Line?.ContactVersion is < 0 or > 2)
        {
            throw new InvalidDataException("Gameplay line contact has an invalid logical schema version.");
        }
        if (ObjectId < 0 || Label < 0 || Identity is null || Pose is null || Control is null || Appearance is null || Animation is null || Face is null || Line is null || Footsteps is null || Identity.ModelId is < -1 or > 255 && Identity.ModelId != ushort.MaxValue || Identity.CharacterId is < -2 or > 255 || !Finite(Pose.Position) || !float.IsFinite(Pose.Direction) || Pose.Direction is < 0 or >= 256 || !Finite(Pose.DisplayOffset) || Pose.Triangle is < 0 or > ushort.MaxValue || Pose.FixedPosition is { } raw && (Pose.Position.ToVector3() != new Vector3(raw.X / 4096f, raw.Y / 4096f, raw.Z / 4096f) || (raw.X >> 12) is < short.MinValue or > short.MaxValue || (raw.Y >> 12) is < short.MinValue or > short.MaxValue || (raw.Z >> 12) is < short.MinValue or > short.MaxValue) || Control.Completion is > 2 && (Ladder is null || Control.Completion > 6) || Control.MotionDirection is < 0 or > 255 || Control.FacingHold is < 0 or > 255 || (Control.MotionDirection is null) != (Control.FacingHold is null) || (Control.AutoAnimation is null) != (Control.DetachedFollow is null) || Appearance.PolygonColor is { } color && (!Finite(color) || color.X is < 0 or > 255 || color.Y is < 0 or > 255 || color.Z is < 0 or > 255 || color.X != MathF.Truncate(color.X) || color.Y != MathF.Truncate(color.Y) || color.Z != MathF.Truncate(color.Z)) || Appearance.ShadowRadii is { Length: not 8 } || Appearance.EyeMode is not (null or 0 or 0x80000 or 0x100000 or 0x200000) || (Line.Start is null) != (Line.End is null) || Line.Start is { } start && !Finite(start) || Line.End is { } end && !Finite(end) || (Footsteps.First is null) != (Footsteps.Second is null) || Offset is { Mode: > 3 } || Offset is { } offset && (offset.Counter > offset.Duration || offset.Mode == 0 && offset.Counter != 0 || offset.Mode == 3 && offset.Counter != offset.Duration || Pose.DisplayOffset != new GameplaySaveActorVector(offset.X, offset.Y, offset.Z)))
        {
            throw new InvalidDataException("Gameplay actor values are malformed or describe an unsupported active transition.");
        }
        if (Face.Returning && Face.TargetPoint is not null || Face.Duration is < 0 or > ushort.MaxValue || Face.Progress < 0 || Face.Progress > Face.Duration || new[] { Face.FirstAngle, Face.SecondAngle, Face.Elapsed, Face.PendingBaseline, Face.PitchTarget, Face.YawTarget }.Any(value => value is < short.MinValue or > short.MaxValue) || !Finite(Face.Limits) || new[] { Face.Limits.X, Face.Limits.Y, Face.Limits.Z }.Any(value => value is < 0 or > 255 || value != MathF.Truncate(value)) || Face.TargetPoint is { } target && (!Finite(target) || new[] { target.X, target.Y, target.Z }.Any(value => value != MathF.Truncate(value) || value is < short.MinValue or > short.MaxValue)) || Face.FirstControlRotation is { } rotation && !Finite(rotation))
        {
            throw new InvalidDataException("Gameplay actor face words or limits are invalid.");
        }
        Animation.Validate();
        AnimationBackup?.Validate();
    }

    private static bool Finite(Vector3 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    }
}

public sealed record GameplaySaveActorIdentity([property: JsonRequired] int ModelId, [property: JsonRequired] int CharacterId, [property: JsonRequired] bool Active);
public sealed record GameplaySaveActorPose([property: JsonRequired] GameplaySaveActorVector Position, [property: JsonRequired] GameplaySaveActorPosition? FixedPosition, [property: JsonRequired] int Triangle, [property: JsonRequired] float Direction, [property: JsonRequired] GameplaySaveActorVector DisplayOffset, [property: JsonRequired] short RootTranslationZ);
public sealed record GameplaySaveActorControl([property: JsonRequired] bool TalkEnabled, [property: JsonRequired] bool PushEnabled, [property: JsonRequired] bool CollisionEnabled, [property: JsonRequired] bool? SavedCollisionDisabled, [property: JsonRequired] int TalkRadius, [property: JsonRequired] int PushRadius, [property: JsonRequired] int MovementSpeed, [property: JsonRequired] short? MovementBaseSpeed, [property: JsonRequired] short? AccelerationTarget, [property: JsonRequired] byte DirectionLimit, [property: JsonRequired] int MovementFlags, [property: JsonRequired] bool IgnoresWalkmesh, [property: JsonRequired] ushort? Completion, [property: JsonRequired] ushort? LandingTriangle, [property: JsonRequired] short? TurnAccumulator, [property: JsonRequired] byte? GroundContact, [property: JsonRequired] byte? TurnMode, [property: JsonRequired] int? MotionDirection, [property: JsonRequired] int? FacingHold, [property: JsonRequired] bool? AutoAnimation, [property: JsonRequired] bool? DetachedFollow, [property: JsonRequired] bool CardChallenge);
public sealed record GameplaySaveActorAppearance([property: JsonRequired] bool Visible, [property: JsonRequired] bool AdditionalControlDisabled, [property: JsonRequired] bool PrimitiveTransparency, [property: JsonRequired] byte? RendererMode, [property: JsonRequired] byte? BlinkParameter, [property: JsonRequired] uint? EyeMode, [property: JsonRequired] GameplaySaveActorVector? PolygonColor, [property: JsonRequired] int GetaParameter, [property: JsonRequired] byte ShadeLevel, [property: JsonRequired] byte[]? ShadowRadii);

[Flags]
public enum GameplaySaveActorAnimationFlags
{
    Complete = 0x0800,
    Frozen = 0x1000,
    Repeating = 0x2000,
    ReturnIdle = 0x4000,
    Hold = 0x8000
}

public sealed record GameplaySaveActorAnimation([property: JsonRequired] byte Selector, [property: JsonRequired] ushort? Frame, [property: JsonRequired] ushort Increment, [property: JsonRequired] ushort First, [property: JsonRequired] GameplaySaveActorAnimationFlags? Flags, [property: JsonRequired] int Idle, [property: JsonRequired] int Walk, [property: JsonRequired] int Run, [property: JsonRequired] int IdleFirst, [property: JsonRequired] int IdleLast, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ushort? End = null)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool UseResourceLastPose { get; init; }

    public void Validate()
    {
        if (UseResourceLastPose && (End is not null || Frame is null || (Flags & (GameplaySaveActorAnimationFlags.Complete | GameplaySaveActorAnimationFlags.Frozen)) != (GameplaySaveActorAnimationFlags.Complete | GameplaySaveActorAnimationFlags.Frozen)))
        {
            throw new InvalidDataException("Resource-relative final animation pose requires known frozen completion and no explicit range end.");
        }
        if (Flags is { } flags && ((int)flags & ~0xF800) != 0 || Idle is < 0 or > 255 || Walk is < 0 or > 255 || Run is < 0 or > 255 || IdleFirst < 0 || IdleLast < IdleFirst || End is ushort end && (First > end || Frame > end))
        {
            throw new InvalidDataException("Gameplay actor animation requires valid selectors and logical playback flags.");
        }
    }
}

public sealed record GameplaySaveActorFace([property: JsonRequired] int FirstAngle, [property: JsonRequired] int SecondAngle, [property: JsonRequired] int Elapsed, [property: JsonRequired] GameplaySaveActorVector Limits, [property: JsonRequired] int PendingBaseline, [property: JsonRequired] int PitchTarget, [property: JsonRequired] int YawTarget, [property: JsonRequired] int Duration, [property: JsonRequired] int Progress, [property: JsonRequired] GameplaySaveActorVector? TargetPoint, [property: JsonRequired] GameplaySaveActorVector? FirstControlRotation)
{
    /// <summary>Nontracking override-release mode, used by both literal angular targets and a consumed return-to-baseline operation.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Returning { get; init; }
}
public sealed record GameplaySaveActorLine([property: JsonRequired] GameplaySaveActorVector? Start, [property: JsonRequired] GameplaySaveActorVector? End, [property: JsonRequired] bool Enabled, [property: JsonRequired] bool DoorEnabled, [property: JsonRequired] bool Contact, [property: JsonRequired] bool DoorContact, [property: JsonRequired] byte? FacingDirection, [property: JsonRequired] bool? FacingContact)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ContactVersion { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool CrossingPending { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool EntryPending { get; init; }
}
public sealed record GameplaySaveActorFootsteps([property: JsonRequired] bool Enabled, [property: JsonRequired] bool? Special, [property: JsonRequired] byte? First, [property: JsonRequired] byte? Second);
public sealed record GameplaySaveActorOffset([property: JsonRequired] short X, [property: JsonRequired] short Y, [property: JsonRequired] short Z, [property: JsonRequired] short StartX, [property: JsonRequired] short StartY, [property: JsonRequired] short StartZ, [property: JsonRequired] short TargetX, [property: JsonRequired] short TargetY, [property: JsonRequired] short TargetZ, [property: JsonRequired] ushort Duration, [property: JsonRequired] ushort Counter, [property: JsonRequired] byte Mode);

public sealed record GameplaySaveActorPosition([property: JsonRequired] int X, [property: JsonRequired] int Y, [property: JsonRequired] int Z);

public readonly record struct GameplaySaveActorVector([property: JsonRequired] float X, [property: JsonRequired] float Y, [property: JsonRequired] float Z)
{
    public Vector3 ToVector3()
    {
        return new(X, Y, Z);
    }

    public static implicit operator GameplaySaveActorVector(Vector3 value)
    {
        return new(value.X, value.Y, value.Z);
    }

    public static implicit operator Vector3(GameplaySaveActorVector value)
    {
        return value.ToVector3();
    }
}

public sealed record GameplaySaveActorLadderAnimations([property: JsonRequired] byte Start, [property: JsonRequired] byte Loop, [property: JsonRequired] byte End);
