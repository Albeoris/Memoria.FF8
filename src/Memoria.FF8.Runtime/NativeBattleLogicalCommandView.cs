namespace Memoria.FF8.Runtime;

/// <summary>Drawing data from one immutable command backing, resolved at the point of consumption.</summary>
public readonly ref struct NativeBattleLogicalCommandView
{
    private readonly ReadOnlySpan<int> vertexPayload;
    private readonly bool hasVertexPayload;

    internal NativeBattleLogicalCommandView(NativeBattleLogicalCommand command)
    {
        Packet = command.Packet;
        WordOffset = command.WordOffset;
        Opcode = command.Opcode;
        Flags20 = command.Flags20;
        ModuleDraw = command.ModuleDraw;
        OrderingBucket = command.OrderingBucket;
        TextureSourceOwner = command.TextureSourceOwner;
        PresentationActorId = command.PresentationActorId;
        ModelTextureOwner = command.ModelTextureOwner;
        ScreenCoverage = command.ScreenCoverage;
        SceneCapture = command.SceneCapture;
        Pilot = command.Pilot;
        WordSpan = command.WordSpan;

        NativeBattleImmutableSlice<int> depths = command.VertexPayloadSlice;
        hasVertexPayload = !depths.IsDefault;
        vertexPayload = depths.AsSpan();
        HasProjectedCoordinates = command.HasProjectedCoordinates;
        ProjectedCoordinateSpan = command.ProjectedCoordinateSpan;
    }

    internal NativeBattleLogicalCommandView(NativeBattlePlacedModelPrimitives placement, int index, int bucket)
    {
        ref readonly NativeBattleModelPrimitiveRow row = ref placement.Batch.ReadRow(index);
        ref readonly NativeBattleModelEmission emission = ref placement.Batch.ReadRawEmission(row.Emission);
        Packet = placement.ReadPacket(index);
        WordOffset = 1;
        WordSpan = row.Words.AsSpan()[1..];
        Opcode = (byte)(WordSpan[0] >> 24);
        Flags20 = row.VertexPayload4.IsEmpty ? (ushort)1 : (ushort)0;
        ModuleDraw = placement.ModuleDraw;
        OrderingBucket = bucket;
        TextureSourceOwner = placement.TextureSourceOwner;
        PresentationActorId = emission.PresentationActorId;
        ModelTextureOwner = emission.ModelTextureOwner;
        ScreenCoverage = null;
        SceneCapture = emission.SceneCapture;
        Pilot = row.Pilot;

        hasVertexPayload = true;
        vertexPayload = row.VertexPayload4.AsSpan();
        HasProjectedCoordinates = row.ProjectedCoordinates.HasValue;
        ProjectedCoordinateSpan = row.ProjectedCoordinates is { } coordinates ? coordinates.AsSpan() : default;
    }

    public NativeBattlePrimitiveReference Packet { get; }
    public int WordOffset { get; }
    public byte Opcode { get; }
    public ushort Flags20 { get; }
    public int? ModuleDraw { get; }
    public int? OrderingBucket { get; }
    public string? TextureSourceOwner { get; }
    public string? PresentationActorId { get; }
    public NativeBattleModelTextureOwner? ModelTextureOwner { get; }
    public NativeBattleScreenCoverage? ScreenCoverage { get; }
    public NativeBattleSceneCapture? SceneCapture { get; }
    public NativeBattlePilotGeometryReference Pilot { get; }
    public ReadOnlySpan<uint> WordSpan { get; }
    public ReadOnlySpan<int> VertexPayloadSpan => hasVertexPayload ? vertexPayload : throw new InvalidDataException("Native command has no original proxy depth fields.");
    public ReadOnlySpan<BattleSceneScreenPoint> ProjectedCoordinateSpan { get; }
    public bool HasProjectedCoordinates { get; }
}
