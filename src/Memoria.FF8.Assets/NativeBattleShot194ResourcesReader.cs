using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleShot194ResourcesReader
{
    public static NativeBattleShot194Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        const uint directory = 0xD0BD78;
        byte[] header = memory.Read(directory, 20);
        if (U32(header, 0) != 3)
        {
            throw new InvalidDataException("Native Flame Shot requires its original three-section actor override.");
        }
        int length = checked((int)U32(header, 16));
        ImmutableArray<byte> actor = NativeBattleEffectStaticResourcesReader.Read(memory, directory, length);
        for (int section = 0; section < 4; section++)
        {
            _ = NativeBattleDatDirectory.Section(actor.AsSpan(), section);
        }
        ImmutableArray<NativeBattleSearchSpriteResource> sprites = new uint[] { 0xD0D938, 0xD0DB5C, 0xD0DC34, 0xD0DCA8 }.Select(address => new NativeBattleSearchSpriteResource(address, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address))).ToImmutableArray();
        ImmutableArray<NativeBattleStaticMeshResource> meshes = new uint[] { 0xD0DCE4, 0xD0FB0C, 0xD106DC, 0xD11B54, 0xD1283C }.Select(address => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address)).ToImmutableArray();
        uint[] addresses = Enumerable.Range(0, 5).Select(index => U32(memory.Read(0xD12E80u + (uint)index * 4, 4), 0)).ToArray();
        byte[] axis = memory.Read(0xD12E70, 12);
        return new(new("FF8_EN:D0BD78", actor), sprites, meshes, [new(unchecked((short)(int)U32(axis, 0)), unchecked((short)(int)U32(axis, 4)), unchecked((short)(int)U32(axis, 8)))], addresses.ToImmutableArray(), U32(memory.Read(0xD12E64, 4), 0), Enumerable.Range(0, 8).Select(index => U32(memory.Read(0xD12E94u + (uint)index * 4, 4), 0)).ToImmutableArray());
    }

    private static uint U32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
}

public sealed partial class GameAssets
{
    private NativeBattleShot194Resources? nativeShot194Resources;
    public NativeBattleShot194Resources ReadNativeShot194Resources() => nativeShot194Resources ??= NativeBattleShot194ResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
