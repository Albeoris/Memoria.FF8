using System.Numerics;
using System.Text.Json.Serialization;

namespace Memoria.FF7.Formats;

public sealed record Ff7BattleSearchScopeBasis(Ff7BattleBraverPoint Right, Ff7BattleBraverPoint Up, Ff7BattleBraverPoint Forward)
{
    [JsonIgnore]
    public Matrix4x4 Matrix => new(Right.X / 4096f, Right.Y / 4096f, Right.Z / 4096f, 0, Up.X / 4096f, Up.Y / 4096f, Up.Z / 4096f, 0, Forward.X / 4096f, Forward.Y / 4096f, Forward.Z / 4096f, 0, 0, 0, 0, 1);
}

public sealed record Ff7BattleSearchScopeMesh(int Model, BattleRotationWords Rotation, int LongitudinalScale, int TransverseScale, short Transparency);
public sealed record Ff7BattleSearchScopeSprite(int StartedAt, Ff7BattleBraverPoint Position);
public sealed record Ff7BattleSearchScopeState(ushort Targets, Ff7BattleBraverPoint Source, Ff7BattleBraverPoint TargetCenter, Ff7BattleSearchScopeBasis Basis, short InitialScaleCounter = 0, int Frame = -1)
{
    public short ScaleCounter { get; init; } = InitialScaleCounter;
    public IReadOnlyList<Ff7BattleSearchScopeMesh> Meshes { get; init; } = [];
    public IReadOnlyList<Ff7BattleSearchScopeSprite> Sprites { get; init; } = [];
    public Ff7BattlePrimaryState? Primary { get; init; }
    public bool Completed => Frame >= 51;
}

public sealed record Ff7BattleSearchScopeUpdate(Ff7BattleSearchScopeState State, ushort ImpactTargets);

public static class Ff7BattleSearchScopeEffect
{
    public const int FirstSpriteFrames = 8;
    public const int SecondSpriteFrames = 12;

    public static Ff7BattleSearchScopeState Start(ushort targets, Vector3 source, Vector3 targetCenter, short initialScaleCounter = 0)
    {
        Ff7BattleSearchScopeState state = CreateState(targets, source, targetCenter, initialScaleCounter);
        return state with { Primary = Ff7BattlePrimaryEffects.ScheduleSearchScope(new() { SearchScopeCounter = initialScaleCounter }, targets, source, targetCenter) };
    }

    internal static Ff7BattleSearchScopeState CreateState(ushort targets, Vector3 source, Vector3 targetCenter, short initialScaleCounter = 0)
    {
        if (targets == 0 || (targets & ~0x03F7) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targets));
        }
        Ff7BattleBraverPoint from = Ff7BattleBraverPoint.At(source);
        Ff7BattleBraverPoint to = Ff7BattleBraverPoint.At(targetCenter);
        return new(targets, from, to, AimFromDirection(Difference(from, to)), initialScaleCounter);
    }

    internal static Ff7BattleSearchScopeBasis AimFromDirection(Ff7BattleBraverPoint direction)
    {
        Ff7BattleBraverPoint forward = Normalize(direction.Position);
        Ff7BattleBraverPoint right = Normalize(new(forward.Z, 0, -forward.X));
        Vector3 cross = Vector3.Cross(forward.Position / 4096, right.Position / 4096);
        Ff7BattleBraverPoint up = Normalize(new((int)((double)cross.X * 4096), (int)((double)cross.Y * 4096), (int)((double)cross.Z * 4096)));
        return new(right, up, forward);
    }

    public static Ff7BattleSearchScopeUpdate Advance(Ff7BattleSearchScopeState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Completed)
        {
            return new(state, 0);
        }
        Ff7BattlePrimaryState previous = state.Primary ?? Ff7BattlePrimaryEffects.ImportSearchScope(new() { Tick = state.Frame, SearchScopeCounter = state.ScaleCounter }, state);
        Ff7BattlePrimaryState primary = Ff7BattlePrimaryEffects.Advance(previous);
        return new(Ff7BattlePrimaryEffects.ProjectSearchScope(primary, state, retainPrimary: true), primary.ImpactTargets);
    }

    public static byte Alpha(short transparency) => Ff7BattleStageFade.Alpha(transparency);

    private static Ff7BattleBraverPoint Difference(Ff7BattleBraverPoint source, Ff7BattleBraverPoint target) => new((short)(target.X - source.X), (short)(target.Y - source.Y), (short)(target.Z - source.Z));

    private static Ff7BattleBraverPoint Normalize(Vector3 point)
    {
        float length = (float)Math.Sqrt((float)((double)point.X * point.X + (double)point.Y * point.Y + (double)point.Z * point.Z));
        if (length <= 0)
        {
            return new(0, 0, 0);
        }
        float reciprocal = 4096f / length;
        return new((short)(int)((double)point.X * reciprocal), (short)(int)((double)point.Y * reciprocal), (short)(int)((double)point.Z * reciprocal));
    }

    internal static int Sine(int angle) => (int)(Math.Sin(angle * 0.087890625f * (Math.PI / 180)) * 4096);
}
