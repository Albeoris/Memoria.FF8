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

    internal NativeBattlePilotGeometryReference(NativeBattlePilotModelPose.Surface surface, int index)
    {
        owner = surface;
        this.index = index;
    }

    public bool IsEmpty => owner is null;
    internal bool IsModel => owner is NativeBattleModelPilotPage;
    internal bool HasModelEmissions(NativeBattleModelEmissionStorage emissions) => owner is NativeBattleModelPilotPage page && ReferenceEquals(page.Emissions, emissions);
    internal NativeBattlePilotVisualGeometry? AuthoredGeometry => Rich;
    private NativeBattlePilotVisualGeometry? Rich => owner as NativeBattlePilotVisualGeometry;
    private NativeBattlePilotModelPose.Surface? Surface => owner as NativeBattlePilotModelPose.Surface;
    private ref readonly NativeBattleModelPilotRow Model => ref ((NativeBattleModelPilotPage)owner!).Read(index);
    private ref readonly NativeBattleModelEmission Emission => ref ((NativeBattleModelPilotPage)owner!).ReadEmission(Model.Emission);
    public NativeBattlePilotVisualIdentity Identity => owner is NativeBattleModelPilotPage ? Emission.PilotIdentity with { Primitive = Model.Primitive } : Surface is { } surface ? surface.Identity with { Primitive = unchecked(surface.Identity.Primitive * 65537 + index) } : Rich?.Identity ?? default;
    public byte CornerCount => owner is NativeBattleModelPilotPage ? Model.CornerCount : Surface is { } surface ? (byte)(surface.Polygons[index].Quad ? 4 : 3) : Rich?.CornerCount ?? 0;
    public bool Continuous => owner is NativeBattleModelPilotPage ? Emission.PilotContinuous : Surface?.Continuous ?? Rich?.Continuous ?? false;
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotModelPose? ModelPose => owner is NativeBattleModelPilotPage ? Emission.ModelPose : Rich?.ModelPose;
    public bool ScreenSpace => owner is NativeBattleModelPilotPage ? Emission.PilotScreenSpace : Rich?.ScreenSpace ?? false;
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
        if (Surface is { } surface)
        {
            return surface.Corner(index, corner);
        }
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
        if (Surface is not null)
        {
            return new(Identity, Corner(0), Corner(1), Corner(2), CornerCount == 4 ? Corner(3) : default, CornerCount, Continuous);
        }
        if (owner is not NativeBattleModelPilotPage)
        {
            return Rich;
        }
        ref readonly NativeBattleModelPilotRow model = ref Model;
        ref readonly NativeBattleModelEmission emission = ref Emission;
        return new(emission.PilotIdentity with { Primitive = model.Primitive }, model.A, model.B, model.C, model.D, model.CornerCount, emission.PilotContinuous) { ScreenSpace = emission.PilotScreenSpace, ModelPose = emission.ModelPose };
    }

    public static implicit operator NativeBattlePilotGeometryReference(NativeBattlePilotVisualGeometry? geometry)
    {
        return new(geometry);
    }
}

internal readonly record struct NativeBattleModelPilotRow(Vector3 A, Vector3 B, Vector3 C, Vector3 D, int Primitive, int Emission, byte CornerCount)
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
    private const int Capacity = 64;
    private readonly NativeBattleModelPilotRow[] values = new NativeBattleModelPilotRow[Capacity];
    private int count;

    internal NativeBattleModelPilotPage(NativeBattleModelEmissionStorage emissions)
    {
        Emissions = emissions;
    }

    internal NativeBattleModelEmissionStorage Emissions { get; }
    internal bool IsFull => count == Capacity;

    internal NativeBattlePilotGeometryReference Append(in NativeBattleModelPilotRow value)
    {
        if (IsFull)
        {
            throw new InvalidOperationException("A completed pilot geometry page cannot accept another polygon.");
        }
        int index = count++;
        values[index] = value;
        return new(this, index);
    }

    internal ref readonly NativeBattleModelPilotRow Read(int index)
    {
        if ((uint)index >= (uint)count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        return ref values[index];
    }

    internal ref readonly NativeBattleModelEmission ReadEmission(int index) => ref Emissions.Read(index);
}
