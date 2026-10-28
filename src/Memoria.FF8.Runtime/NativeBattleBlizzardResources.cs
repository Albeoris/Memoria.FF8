using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBlizzardResources(NativeBattleEffectHeapAllocation Animation, NativeBattleModuleResource Sprite5, NativeBattleModuleResource Sprite16, NativeBattleStaticMeshResource PrimaryMesh, NativeBattleStaticMeshResource PartnerMesh, NativeBattleStaticMeshResource BurstMesh, uint Sound);

public sealed record NativeBattleBlizzardFaces([property: JsonRequired] ImmutableArray<NativeBattleEffectMemoryRegion> KnownRanges)
{
    public const uint Address = 0x24BB968;
    public const int SliceLength = 0xA00;

    public byte[] Read(int sequence) => NativeKnownByteRanges.Read(KnownRanges, Offset(sequence), SliceLength, 3 * SliceLength);
    public NativeBattleBlizzardFaces Write(int sequence, ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != SliceLength)
        {
            throw new InvalidDataException("Native61B190 requires its complete80x32-byte current face slice.");
        }
        return new(NativeKnownByteRanges.Write(KnownRanges, Offset(sequence), bytes, 3 * SliceLength));
    }

    private static int Offset(int sequence)
    {
        if ((uint)sequence >= 3)
        {
            throw new NotSupportedException("Native Blizzard face sequence aliases its live physical main/child banks beyond24BD768; that alias is not owned.");
        }
        return sequence * SliceLength;
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleBlizzardFaces? BlizzardFaces
    {
        get => effectState.BlizzardFaces;
        init => effectState = effectState with { BlizzardFaces = value };
    }
}
