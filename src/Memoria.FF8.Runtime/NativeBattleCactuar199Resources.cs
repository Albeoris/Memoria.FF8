using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCactuar199SpriteResource(uint Address, ImmutableArray<byte> Bytes);
public sealed record NativeBattleCactuar199VibrationResource(uint Address, NativeVibrationBank Bank);
public sealed record NativeBattleCactuar199Resources(NativeBattleEffectHeapAllocation InitializerHeapTim, NativeBattleEffectOwnedModelResource Model, NativeBattleEffectHeapAllocation EntryModelCopySeed, NativeBattleEffectHeapAllocation ModelTim, NativeBattleCactuar199SpriteResource Sprite, NativeBattleStaticMeshResource MeshA, NativeBattleStaticMeshResource MeshB, NativeBattleStaticMeshResource MeshC, NativeBattleCactuar199VibrationResource Vibration, NativeBattleEffectHeapAllocation CameraProgram, NativeBattleEffectHeapAllocation RandomTape, NativeBattleEffectHeapAllocation AlignmentAxis, NativeBattleEffectHeapAllocation NeedleAlignmentAxis, NativeBattleEffectHeapAllocation EarlyColors, NativeBattleEffectHeapAllocation LateColors, NativeBattleEffectHeapAllocation Sounds, NativeBattleEffectHeapAllocation Voices)
{
    [JsonIgnore] public uint Sound1 => U32(Sounds.Data, 0);
    [JsonIgnore] public uint Sound2 => U32(Sounds.Data, 4);
    [JsonIgnore] public uint Voice1 => U32(Voices.Data, 0);
    [JsonIgnore] public uint Voice2 => U32(Voices.Data, 4);
    private static uint U32(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));
}
