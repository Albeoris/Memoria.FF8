using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Retained model packet values share immutable pages instead of one intermediate object per polygon.</summary>
internal readonly record struct NativeBattleModelPrimitive(int Bucket, NativeBattleImmutableSlice<uint> Words, NativeBattleImmutableSlice<int> VertexPayload4)
{
    internal ImmutableArray<NativeBattleGfPacketByteState>? PhysicalWriteMask { get; init; }
    internal NativeBattleImmutableSlice<BattleSceneScreenPoint>? ProjectedCoordinates { get; init; }
    internal NativeBattlePilotGeometryReference Pilot { get; init; }
    internal NativeBattlePilotVisualGeometry? PilotGeometry => Pilot.ToGeometry();
    internal string? PresentationActorId { get; init; }
    internal NativeBattleModelTextureOwner? ModelTextureOwner { get; init; }
    internal NativeBattleScreenCoverage? ScreenCoverage { get; init; }
    internal NativeBattleSceneCapture? SceneCapture { get; init; }

    public static implicit operator NativeBattleModelPrimitive(NativeBattleGeometryPrimitive primitive)
    {
        ArgumentNullException.ThrowIfNull(primitive);
        return new(primitive.Bucket, primitive.Words, primitive.VertexPayload4)
        {
            PhysicalWriteMask = primitive.PhysicalWriteMask,
            ProjectedCoordinates = primitive.ProjectedCoordinates,
            Pilot = primitive.PilotGeometry,
            PresentationActorId = primitive.PresentationActorId,
            ModelTextureOwner = primitive.ModelTextureOwner,
            ScreenCoverage = primitive.ScreenCoverage,
            SceneCapture = primitive.SceneCapture
        };
    }
}

/// <summary>Owns published immutable pages for retained ordering, transaction replay and frame consumers.</summary>
internal sealed class NativeBattleModelPrimitiveBatch
{
    private const int PageSize = 64;
    private readonly ImmutableArray<NativeBattleModelPrimitive[]> pages;
    private readonly ImmutableArray<NativeBattleGeometryPrimitive> objects;

    private NativeBattleModelPrimitiveBatch(ImmutableArray<NativeBattleModelPrimitive[]> pages, int count)
    {
        this.pages = pages;
        Count = count;
    }

    private NativeBattleModelPrimitiveBatch(ImmutableArray<NativeBattleGeometryPrimitive> objects)
    {
        this.objects = objects;
        Count = objects.IsDefault ? 0 : objects.Length;
    }

    internal int Count { get; }
    internal bool IsDefault => pages.IsDefault && objects.IsDefault;
    internal bool IsEmpty => Count == 0;
    internal NativeBattleModelPrimitive this[int index] => !objects.IsDefault ? objects[index] : pages[index / PageSize][index % PageSize];

    public static implicit operator NativeBattleModelPrimitiveBatch(ImmutableArray<NativeBattleGeometryPrimitive> primitives)
    {
        return new(primitives);
    }

    internal sealed class Builder
    {
        private readonly List<NativeBattleModelPrimitive[]> pages = [];
        private int count;
        private bool frozen;
        private NativeBattleModelPilotPage? pilotPage;
        private readonly NativeBattleImmutableSlice<uint>.Builder words = new();
        private readonly NativeBattleImmutableSlice<int>.Builder depths = new();
        private readonly NativeBattleImmutableSlice<BattleSceneScreenPoint>.Builder positions = new();

        internal void AddRaw(int bucket, ReadOnlySpan<uint> packet, ReadOnlySpan<int> vertexDepths, ReadOnlySpan<BattleSceneScreenPoint> coordinates,
            ImmutableArray<NativeBattleGfPacketByteState> mask, NativeBattlePilotGeometryReference geometry, string? actorId, NativeBattleModelTextureOwner? material)
        {
            NativeBattleSceneCapture? sceneCapture = NativeBattleSceneCapture.Current;
            Add(new(bucket, words.Append(packet), depths.Append(vertexDepths))
            {
                PhysicalWriteMask = mask,
                ProjectedCoordinates = coordinates.IsEmpty ? null : positions.Append(coordinates),
                Pilot = geometry,
                SceneCapture = sceneCapture,
                PresentationActorId = actorId,
                ModelTextureOwner = sceneCapture is null && geometry.ForeignSurface is null ? material : null
            });
        }

        internal NativeBattlePilotGeometryReference AddPilot(in NativeBattleModelPilotGeometry geometry)
        {
            if (frozen)
            {
                throw new InvalidOperationException("Published pilot geometry pages cannot be modified.");
            }
            if (pilotPage is null || pilotPage.IsFull)
            {
                pilotPage = new();
            }
            return pilotPage.Append(geometry);
        }

        internal void Add(NativeBattleModelPrimitive primitive)
        {
            if (frozen)
            {
                throw new InvalidOperationException("Published model packet pages cannot be modified.");
            }
            if (count % PageSize == 0)
            {
                pages.Add(new NativeBattleModelPrimitive[PageSize]);
            }
            pages[count / PageSize][count % PageSize] = primitive;
            count++;
        }

        internal NativeBattleModelPrimitiveBatch Freeze()
        {
            frozen = true;
            words.Freeze();
            depths.Freeze();
            positions.Freeze();
            return new(pages.ToImmutableArray(), count);
        }
    }
}
