using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Transient view of a model packet and its retained owners.</summary>
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

internal readonly record struct NativeBattleModelPrimitiveRow(int Bucket, int Emission, NativeBattleImmutableSlice<uint> Words, NativeBattleImmutableSlice<int> VertexPayload4, NativeBattleImmutableSlice<BattleSceneScreenPoint>? ProjectedCoordinates, ImmutableArray<NativeBattleGfPacketByteState> PhysicalWriteMask, NativeBattlePilotGeometryReference Pilot);

internal readonly record struct NativeBattleModelEmission(string? PresentationActorId, NativeBattleModelTextureOwner? ModelTextureOwner, NativeBattleSceneCapture? SceneCapture, NativeBattlePilotVisualIdentity PilotIdentity, bool PilotContinuous, bool PilotScreenSpace)
{
    internal NativeBattlePilotModelPose? ModelPose { get; init; }
}

/// <summary>The builder publishes one immutable emission table shared by retained packet and pilot pages.</summary>
internal sealed class NativeBattleModelEmissionStorage
{
    private List<NativeBattleModelEmission>? pending;
    private ImmutableArray<NativeBattleModelEmission> published;

    internal int Add(in NativeBattleModelEmission emission)
    {
        if (!published.IsDefault)
        {
            throw new InvalidOperationException("Published model emission metadata cannot be modified.");
        }
        pending ??= [];
        int index = pending.Count;
        pending.Add(emission);
        return index;
    }

    internal ref readonly NativeBattleModelEmission Read(int index) => ref published.ItemRef(index);

    internal void Freeze()
    {
        if (!published.IsDefault)
        {
            throw new InvalidOperationException("Published model emission metadata cannot be modified.");
        }
        published = pending is null ? [] : [.. pending];
        pending = null;
    }
}

/// <summary>Owns published immutable pages for retained ordering, transaction replay and frame consumers.</summary>
internal sealed class NativeBattleModelPrimitiveBatch
{
    private const int PageSize = 64;
    private readonly ImmutableArray<NativeBattleModelPrimitiveRow[]> pages;
    private readonly NativeBattleModelEmissionStorage? emissions;
    private readonly ImmutableArray<NativeBattleGeometryPrimitive> objects;

    private NativeBattleModelPrimitiveBatch(ImmutableArray<NativeBattleModelPrimitiveRow[]> pages, int count, NativeBattleModelEmissionStorage emissions)
    {
        this.pages = pages;
        this.emissions = emissions;
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
    internal bool UsesRawRows => !pages.IsDefault;

    internal NativeBattleModelPrimitive this[int index]
    {
        get
        {
            if (!objects.IsDefault)
            {
                return objects[index];
            }
            ref readonly NativeBattleModelPrimitiveRow row = ref ReadRow(index);
            ref readonly NativeBattleModelEmission emission = ref emissions!.Read(row.Emission);
            return new(row.Bucket, row.Words, row.VertexPayload4)
            {
                PhysicalWriteMask = row.PhysicalWriteMask,
                ProjectedCoordinates = row.ProjectedCoordinates,
                Pilot = row.Pilot,
                PresentationActorId = emission.PresentationActorId,
                ModelTextureOwner = emission.ModelTextureOwner,
                SceneCapture = emission.SceneCapture
            };
        }
    }

    internal ref readonly NativeBattleModelPrimitiveRow ReadRow(int index) => ref pages[index / PageSize][index % PageSize];
    internal ref readonly NativeBattleModelEmission ReadRawEmission(int emission) => ref emissions!.Read(emission);
    private ref readonly NativeBattleModelEmission ReadEmission(int index) => ref ReadRawEmission(ReadRow(index).Emission);

    internal int ReadBucket(int index) => UsesRawRows ? ReadRow(index).Bucket : objects[index].Bucket;
    internal NativeBattleImmutableSlice<uint> ReadWords(int index) => UsesRawRows ? ReadRow(index).Words : (NativeBattleImmutableSlice<uint>)objects[index].Words;
    internal NativeBattleImmutableSlice<int> ReadVertexPayload4(int index) => UsesRawRows ? ReadRow(index).VertexPayload4 : (NativeBattleImmutableSlice<int>)objects[index].VertexPayload4;
    internal NativeBattleImmutableSlice<BattleSceneScreenPoint>? ReadProjectedCoordinates(int index) => UsesRawRows ? ReadRow(index).ProjectedCoordinates : objects[index].ProjectedCoordinates;
    internal NativeBattlePilotGeometryReference ReadPilot(int index) => UsesRawRows ? ReadRow(index).Pilot : (NativeBattlePilotGeometryReference)objects[index].PilotGeometry;
    internal string? ReadPresentationActorId(int index) => UsesRawRows ? ReadEmission(index).PresentationActorId : objects[index].PresentationActorId;
    internal NativeBattleModelTextureOwner? ReadModelTextureOwner(int index) => UsesRawRows ? ReadEmission(index).ModelTextureOwner : objects[index].ModelTextureOwner;
    internal NativeBattleScreenCoverage? ReadScreenCoverage(int index) => UsesRawRows ? null : objects[index].ScreenCoverage;
    internal NativeBattleSceneCapture? ReadSceneCapture(int index) => UsesRawRows ? ReadEmission(index).SceneCapture : objects[index].SceneCapture;
    internal ImmutableArray<NativeBattleGfPacketByteState>? ReadPhysicalWriteMask(int index) => UsesRawRows ? ReadRow(index).PhysicalWriteMask : objects[index].PhysicalWriteMask;

    public static implicit operator NativeBattleModelPrimitiveBatch(ImmutableArray<NativeBattleGeometryPrimitive> primitives)
    {
        return new(primitives);
    }

    internal sealed class Builder
    {
        private readonly List<NativeBattleModelPrimitiveRow[]> pages = [];
        private readonly NativeBattleModelEmissionStorage emissions = new();
        private int count;
        private bool frozen;
        private NativeBattleModelPilotPage? pilotPage;
        private readonly NativeBattleImmutableSlice<uint>.Builder words = new();
        private readonly NativeBattleImmutableSlice<int>.Builder depths = new();
        private readonly NativeBattleImmutableSlice<BattleSceneScreenPoint>.Builder positions = new();

        internal int BeginEmission(in NativeBattleModelEmission emission) => emissions.Add(emission);

        internal void AddRaw(int bucket, ReadOnlySpan<uint> packet, ReadOnlySpan<int> vertexDepths, ReadOnlySpan<BattleSceneScreenPoint> coordinates,
            ImmutableArray<NativeBattleGfPacketByteState> mask, NativeBattlePilotGeometryReference geometry, int emission)
        {
            RequireOpen();
            if (!geometry.IsEmpty && !geometry.HasModelEmissions(emissions))
            {
                throw new ArgumentException("Raw model rows require pilot geometry from the same emission storage.", nameof(geometry));
            }
            if (count % PageSize == 0)
            {
                pages.Add(new NativeBattleModelPrimitiveRow[PageSize]);
            }
            pages[count / PageSize][count % PageSize] = new(bucket, emission, words.Append(packet), depths.Append(vertexDepths), coordinates.IsEmpty ? null : positions.Append(coordinates), mask, geometry);
            count++;
        }

        internal NativeBattlePilotGeometryReference AddPilot(in NativeBattleModelPilotRow geometry)
        {
            RequireOpen();
            if (pilotPage is null || pilotPage.IsFull)
            {
                pilotPage = new(emissions);
            }
            return pilotPage.Append(geometry);
        }

        private void RequireOpen()
        {
            if (frozen)
            {
                throw new InvalidOperationException("Published model packet pages cannot be modified.");
            }
        }

        internal NativeBattleModelPrimitiveBatch Freeze()
        {
            RequireOpen();
            frozen = true;
            emissions.Freeze();
            words.Freeze();
            depths.Freeze();
            positions.Freeze();
            return new(pages.ToImmutableArray(), count, emissions);
        }
    }
}
