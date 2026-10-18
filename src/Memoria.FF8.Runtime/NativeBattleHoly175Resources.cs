using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleHoly175SpriteResource(uint Address, ImmutableArray<byte> Bytes);
public sealed record NativeBattleHoly175Resources(NativeBattleEffectHeapAllocation Animation, NativeBattleEffectHeapAllocation Tim, ImmutableArray<NativeBattleHoly175SpriteResource> ProjectedSprites, ImmutableArray<NativeBattleStaticMeshResource> Meshes, NativeBattleEffectHeapAllocation SoundDescriptor)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEffectHeapAllocation? CaptureDirectory { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEffectHeapAllocation? CaptureBlackPackets { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEffectHeapAllocation? DelayTable { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEffectHeapAllocation? RayRows { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEffectHeapAllocation? WindowColors { get; init; }

    [JsonIgnore] public uint Sound => BinaryPrimitives.ReadUInt32LittleEndian(SoundDescriptor.Data.AsSpan());

    public NativeBattleHoly175SpriteResource Sprite(uint address)
    {
        NativeBattleHoly175SpriteResource[] matches = ProjectedSprites.Where(sprite => sprite.Address == address).ToArray();
        return matches.Length == 1 ? matches[0] : throw new NotSupportedException("Native Holy requires its exact original projected sprite owner.");
    }

    public NativeBattleStaticMeshResource Mesh(uint address)
    {
        NativeBattleStaticMeshResource[] matches = Meshes.Where(mesh => mesh.Address == address).ToArray();
        return matches.Length == 1 ? matches[0] : throw new NotSupportedException("Native Holy requires its exact original mesh owner.");
    }
}
