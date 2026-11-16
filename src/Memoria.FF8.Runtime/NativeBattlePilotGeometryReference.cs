using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime;

/// <summary>A small retained reference to immutable model corners or richer authored effect geometry.</summary>
public readonly struct NativeBattlePilotGeometryReference
{
    private readonly object? owner;
    private readonly int index;

    public NativeBattlePilotGeometryReference(NativeBattlePilotVisualGeometry? geometry)
    {
        owner = geometry;
        index = 0;
    }

    internal NativeBattlePilotGeometryReference(NativeBattleModelPilotPage page, int index)
    {
        owner = page;
        this.index = index;
    }

    public bool IsEmpty => owner is null;
    internal bool IsModel => owner is NativeBattleModelPilotPage;
    internal NativeBattlePilotVisualGeometry? AuthoredGeometry => Rich;
    private NativeBattlePilotVisualGeometry? Rich => owner as NativeBattlePilotVisualGeometry;
    private ref readonly NativeBattleModelPilotGeometry Model => ref ((NativeBattleModelPilotPage)owner!).Read(index);
    public NativeBattlePilotVisualIdentity Identity => owner is NativeBattleModelPilotPage ? Model.Identity : Rich?.Identity ?? default;
    public byte CornerCount => owner is NativeBattleModelPilotPage ? Model.CornerCount : Rich?.CornerCount ?? 0;
    public bool Continuous => owner is NativeBattleModelPilotPage ? Model.Continuous : Rich?.Continuous ?? false;
    public bool ScreenSpace => owner is NativeBattleModelPilotPage ? Model.ScreenSpace : Rich?.ScreenSpace ?? false;
    public NativeBattleForeignSurface? ForeignSurface => Rich?.ForeignSurface;
    public NativeBattlePilotSpriteGeometry? Sprite => Rich?.Sprite;
    public Vector4? SourceDepths => Rich?.SourceDepths;
    public bool UseSourceDepths => Rich?.UseSourceDepths ?? false;
    public ImmutableArray<Vector2> ScreenOffsets => Rich?.ScreenOffsets ?? [];
    public float ScreenScale => Rich?.ScreenScale ?? 1;
    public Vector4? ProjectionScreenBounds => Rich?.ProjectionScreenBounds;
    public ushort? FocalLengthOverride => Rich?.FocalLengthOverride;

    public Vector3 Corner(int corner)
    {
        if (owner is NativeBattleModelPilotPage)
        {
            return Model.Corner(corner);
        }
        return (Rich ?? throw new InvalidOperationException("An absent pilot reference has no corners.")).Corner(corner);
    }

    public float SampleDepth(float cameraDepth)
    {
        return Rich is { } geometry ? geometry.SampleDepth(cameraDepth) : (cameraDepth + 0f) / 1f * 1f;
    }

    /// <summary>Compatibility materialization; rendering and ordering use this reference directly.</summary>
    public NativeBattlePilotVisualGeometry? ToGeometry()
    {
        if (owner is not NativeBattleModelPilotPage)
        {
            return Rich;
        }
        ref readonly NativeBattleModelPilotGeometry model = ref Model;
        return new(model.Identity, model.A, model.B, model.C, model.D, model.CornerCount, model.Continuous) { ScreenSpace = model.ScreenSpace };
    }

    public static implicit operator NativeBattlePilotGeometryReference(NativeBattlePilotVisualGeometry? geometry)
    {
        return new(geometry);
    }
}

internal readonly record struct NativeBattleModelPilotGeometry(NativeBattlePilotVisualIdentity Identity, Vector3 A, Vector3 B, Vector3 C, Vector3 D, byte CornerCount, bool Continuous, bool ScreenSpace)
{
    internal Vector3 Corner(int corner)
    {
        return corner switch
        {
            0 => A,
            1 => B,
            2 => C,
            3 when CornerCount == 4 => D,
            _ => throw new ArgumentOutOfRangeException(nameof(corner))
        };
    }
}

/// <summary>Only the unpublished model builder can append; retained references never expose writable storage.</summary>
internal sealed class NativeBattleModelPilotPage
{
    internal const int Capacity = 64;
    private readonly NativeBattleModelPilotGeometry[] values = new NativeBattleModelPilotGeometry[Capacity];
    private int count;

    internal bool IsFull => count == Capacity;

    internal NativeBattlePilotGeometryReference Append(in NativeBattleModelPilotGeometry value)
    {
        if (IsFull)
        {
            throw new InvalidOperationException("A completed pilot geometry page cannot accept another polygon.");
        }
        int index = count++;
        values[index] = value;
        return new(this, index);
    }

    internal ref readonly NativeBattleModelPilotGeometry Read(int index)
    {
        if ((uint)index >= (uint)count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        return ref values[index];
    }
}
