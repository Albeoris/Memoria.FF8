using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleUltima149ResourcesReader
{
    public static NativeBattleUltima149Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<byte> vibrationBytes = memory.Read(0xDC9ABC, 0x116).ToImmutableArray();
        NativeVibrationBank vibration = new(memory.SourceHash, Enumerable.Range(0, 64).Select(index => BinaryPrimitives.ReadInt32LittleEndian(vibrationBytes.AsSpan().Slice(index * 4))).ToImmutableArray(), vibrationBytes);
        vibration.Validate();
        NativeBattleModuleResource Sprite(uint address) => NativeBattleEffectStaticResourcesReader.ReadSprite(memory, address);
        NativeBattleStaticMeshResource Mesh(uint address) => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address);
        uint Word(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
        return new(new("FF8_EN:DC2D84", memory.Read(0xDC2D84, 0x3A8).ToImmutableArray()), Sprite(0xDC333C), Sprite(0xDC312C), Sprite(0xDC35B8), Mesh(0xDC8BC4), Mesh(0xDC8804), Mesh(0xDC7F54), Mesh(0xDC3784), Mesh(0xDC5254), Mesh(0xDC6EA4), memory.Read(0xDC7B94, 960).ToImmutableArray(), Enumerable.Range(0, 16).Select(index => unchecked((int)Word(0xDC9BDCu + (uint)index * 4))).ToImmutableArray(), vibration, new(BinaryPrimitives.ReadInt16LittleEndian(memory.Read(0xB8B7F0, 2)), BinaryPrimitives.ReadInt16LittleEndian(memory.Read(0xB8B7F2, 2)), BinaryPrimitives.ReadInt16LittleEndian(memory.Read(0xB8B7F4, 2))), Word(0xDC9BD0), Word(0xDC9BD4));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleUltima149Resources? nativeUltima149Resources;
    public NativeBattleUltima149Resources ReadNativeUltima149Resources() => nativeUltima149Resources ??= NativeBattleUltima149ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
