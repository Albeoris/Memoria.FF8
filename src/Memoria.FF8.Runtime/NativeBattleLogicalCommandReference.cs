using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>One executed command backed by authored data or a placed immutable raw model row.</summary>
[JsonConverter(typeof(NativeBattleLogicalCommandReferenceJsonConverter))]
public readonly record struct NativeBattleLogicalCommandReference
{
    private readonly object? owner;
    private readonly int row;
    private readonly int bucket;

    private NativeBattleLogicalCommandReference(NativeBattleLogicalCommand command)
    {
        owner = command;
        row = 0;
        bucket = 0;
    }

    internal NativeBattleLogicalCommandReference(NativeBattlePlacedModelPrimitives placement, int row, int bucket)
    {
        owner = placement;
        this.row = row;
        this.bucket = bucket;
    }

    private NativeBattleLogicalCommand Authored => owner as NativeBattleLogicalCommand ?? throw new InvalidOperationException("Native command reference has no authored backing.");
    public bool IsDefault => owner is null;
    internal bool HasModelPlacement => owner is NativeBattlePlacedModelPrimitives;
    public NativeBattlePrimitiveReference Packet => owner is NativeBattlePlacedModelPrimitives model ? model.ReadPacket(row) : Authored.Packet;
    public int WordOffset => owner is NativeBattlePlacedModelPrimitives ? 1 : Authored.WordOffset;
    public byte Opcode => owner is NativeBattlePlacedModelPrimitives model ? (byte)(model.Batch.ReadWords(row)[1] >> 24) : Authored.Opcode;
    public ushort Flags20 => owner is NativeBattlePlacedModelPrimitives model ? model.ReadFlags(row) : Authored.Flags20;
    public int? ModuleDraw => owner is NativeBattlePlacedModelPrimitives model ? model.ModuleDraw : Authored.ModuleDraw;
    public int? OrderingBucket => owner is NativeBattlePlacedModelPrimitives ? bucket : Authored.OrderingBucket;
    public string? TextureSourceOwner => owner is NativeBattlePlacedModelPrimitives model ? model.TextureSourceOwner : Authored.TextureSourceOwner;
    public string? PresentationActorId => owner is NativeBattlePlacedModelPrimitives model ? model.Batch.ReadPresentationActorId(row) : Authored.PresentationActorId;
    public NativeBattleModelTextureOwner? ModelTextureOwner => owner is NativeBattlePlacedModelPrimitives model ? model.Batch.ReadModelTextureOwner(row) : Authored.ModelTextureOwner;
    public NativeBattleScreenCoverage? ScreenCoverage => owner is NativeBattlePlacedModelPrimitives model ? model.Batch.ReadScreenCoverage(row) : Authored.ScreenCoverage;
    public NativeBattleSceneCapture? SceneCapture => owner is NativeBattlePlacedModelPrimitives model ? model.Batch.ReadSceneCapture(row) : Authored.SceneCapture;
    public NativeBattlePilotGeometryReference Pilot => owner is NativeBattlePlacedModelPrimitives model ? model.Batch.ReadPilot(row) : Authored.Pilot;
    public NativeBattlePilotVisualGeometry? PilotGeometry => Pilot.ToGeometry();

    public ReadOnlySpan<uint> WordSpan => owner is NativeBattlePlacedModelPrimitives model ? model.Batch.ReadWords(row).AsSpan()[1..] : Authored.WordSpan;
    public ImmutableArray<uint> Words => owner is NativeBattlePlacedModelPrimitives model ? ReadModelCommandWords(model).ToImmutableArray() : Authored.Words;
    public ReadOnlySpan<int> VertexPayloadSpan => owner is NativeBattlePlacedModelPrimitives model ? model.Batch.ReadVertexPayload4(row).AsSpan() : Authored.VertexPayloadSpan;
    public ImmutableArray<int> VertexPayload4 => owner is NativeBattlePlacedModelPrimitives model ? model.Batch.ReadVertexPayload4(row).ToImmutableArray() : Authored.VertexPayload4;
    internal NativeBattleImmutableSlice<BattleSceneScreenPoint>? ProjectedCoordinateSlice => owner is NativeBattlePlacedModelPrimitives model ? model.Batch.ReadProjectedCoordinates(row) : Authored.ProjectedCoordinateSlice;
    public ImmutableArray<BattleSceneScreenPoint>? ProjectedCoordinates => ProjectedCoordinateSlice?.ToImmutableArray();
    public ReadOnlySpan<BattleSceneScreenPoint> ProjectedCoordinateSpan => ProjectedCoordinateSlice is { } slice ? slice.AsSpan() : default;
    public bool HasProjectedCoordinates => ProjectedCoordinateSlice.HasValue;

    public NativeBattleLogicalCommandView Resolve()
    {
        return owner switch
        {
            NativeBattlePlacedModelPrimitives placement => new(placement, row, bucket),
            NativeBattleLogicalCommand command => new(command),
            _ => throw new InvalidOperationException("Native command reference has no authored backing.")
        };
    }

    private NativeBattleImmutableSlice<uint> ReadModelCommandWords(NativeBattlePlacedModelPrimitives model)
    {
        NativeBattleImmutableSlice<uint> words = model.Batch.ReadWords(row);
        return words.Slice(1, words.Length - 1);
    }

    public NativeBattleLogicalCommand ToCommand()
    {
        if (owner is not NativeBattlePlacedModelPrimitives model)
        {
            return Authored;
        }
        NativeBattleImmutableSlice<uint> words = model.Batch.ReadWords(row);
        return new(model.ReadPacket(row), 1, (byte)(words[1] >> 24), words, words.Length - 1, model.ReadFlags(row), model.Batch.ReadVertexPayload4(row), model.ModuleDraw)
        {
            OrderingBucket = bucket,
            ProjectedCoordinateSlice = model.Batch.ReadProjectedCoordinates(row),
            TextureSourceOwner = model.TextureSourceOwner,
            ModelTextureOwner = model.Batch.ReadModelTextureOwner(row),
            Pilot = model.Batch.ReadPilot(row),
            PresentationActorId = model.Batch.ReadPresentationActorId(row),
            ScreenCoverage = model.Batch.ReadScreenCoverage(row),
            SceneCapture = model.Batch.ReadSceneCapture(row)
        };
    }

    public static implicit operator NativeBattleLogicalCommandReference(NativeBattleLogicalCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return new(command);
    }

    internal bool HasSameOwner(NativeBattleLogicalCommandReference other)
    {
        return ReferenceEquals(owner, other.owner) && row == other.row && bucket == other.bucket;
    }

    internal NativeBattleLogicalCommandReference MapBacking(Func<object?, object?> map)
    {
        return owner switch
        {
            NativeBattleLogicalCommand command => (NativeBattleLogicalCommand)map(command)!,
            NativeBattlePlacedModelPrimitives placement => new((NativeBattlePlacedModelPrimitives)map(placement)!, row, bucket),
            _ => this
        };
    }
}

/// <summary>Final packet placement for a batch of immutable raw model rows.</summary>
internal sealed class NativeBattlePlacedModelPrimitives
{
    private readonly ImmutableArray<NativeBattlePrimitiveReference> packets;

    internal NativeBattlePlacedModelPrimitives(NativeBattleModelPrimitiveBatch batch, ImmutableArray<NativeBattlePrimitiveReference> packets, int? moduleDraw, string? textureSourceOwner)
    {
        if (!batch.UsesRawRows || packets.IsDefault || packets.Length != batch.Count)
        {
            throw new InvalidDataException("Native model placement requires an immutable raw batch with one final packet reference per row.");
        }
        Batch = batch;
        this.packets = packets;
        ModuleDraw = moduleDraw;
        TextureSourceOwner = textureSourceOwner;
    }

    internal NativeBattleModelPrimitiveBatch Batch { get; }
    internal int? ModuleDraw { get; }
    internal string? TextureSourceOwner { get; }
    internal NativeBattlePrimitiveReference ReadPacket(int row) => packets[row];
    internal ushort ReadFlags(int row) => Batch.ReadVertexPayload4(row).IsEmpty ? (ushort)1 : (ushort)0;
}

internal sealed class NativeBattleLogicalCommandReferenceJsonConverter : JsonConverter<NativeBattleLogicalCommandReference>
{
    public override NativeBattleLogicalCommandReference Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize<NativeBattleLogicalCommand>(ref reader, options) ?? throw new JsonException("Native command requires its authored packet.");
    }

    public override void Write(Utf8JsonWriter writer, NativeBattleLogicalCommandReference value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value.ToCommand(), options);
    }
}
