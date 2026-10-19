using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleShot192ResourcesReader
{
    public static NativeBattleShot192Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        const uint directory = 0xD155E8;
        byte[] header = memory.Read(directory, 20);
        if (U32(header, 0) != 3)
        {
            throw new InvalidDataException("Native Scatter Shot requires its original three-section actor override.");
        }
        int length = checked((int)U32(header, 16));
        ImmutableArray<byte> actor = NativeBattleEffectStaticResourcesReader.Read(memory, directory, length);
        for (int section = 0; section < 4; section++)
        {
            _ = NativeBattleDatDirectory.Section(actor.AsSpan(), section);
        }
        ImmutableArray<NativeBattleSearchSpriteResource> sprites = new uint[] { 0xD16B58, 0xD16CA4, 0xD16C30, 0xD16EC8, 0xD17074 }.Select(address => new NativeBattleSearchSpriteResource(address, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address))).ToImmutableArray();
        ImmutableArray<NativeBattleStaticMeshResource> meshes = new uint[] { 0xD17220, 0xD17768, 0xD17B58, 0xD17DC0 }.Select(address => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address)).ToImmutableArray();
        uint[] addresses = Enumerable.Range(0, 5).Select(index => U32(memory.Read(0xD18680u + (uint)index * 4, 4), 0)).ToArray();
        byte[] axis = memory.Read(0xD18670, 12);
        return new(new("FF8_EN:D155E8", actor), sprites, meshes, [new(unchecked((short)(int)U32(axis, 0)), unchecked((short)(int)U32(axis, 4)), unchecked((short)(int)U32(axis, 8)))], addresses.ToImmutableArray(), U32(memory.Read(0xD18668, 4), 0));
    }

    private static uint U32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
}

public sealed partial class GameAssets
{
    private NativeBattleShot192Resources? nativeShot192Resources;
    public NativeBattleShot192Resources ReadNativeShot192Resources() => nativeShot192Resources ??= NativeBattleShot192ResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
