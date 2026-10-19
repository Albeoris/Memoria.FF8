using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleShot197ResourcesReader
{
    public static NativeBattleShot197Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        const uint directory = 0xCFB348;
        byte[] header = memory.Read(directory, 20);
        if (U32(header, 0) != 3)
        {
            throw new InvalidDataException("Native Armor Shot requires its original three-section actor override.");
        }
        int length = checked((int)U32(header, 16));
        ImmutableArray<byte> actor = NativeBattleEffectStaticResourcesReader.Read(memory, directory, length);
        for (int section = 0; section < 4; section++)
        {
            _ = NativeBattleDatDirectory.Section(actor.AsSpan(), section);
        }
        ImmutableArray<NativeBattleSearchSpriteResource> sprites = new uint[] { 0xCFD714, 0xCFDA74, 0xCFDC20 }.Select(address => new NativeBattleSearchSpriteResource(address, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address))).ToImmutableArray();
        ImmutableArray<NativeBattleStaticMeshResource> meshes = new uint[] { 0xCFDD18, 0xCFEDE8, 0xCFF2F8, 0xD012B0, 0xD02180, 0xD02570, 0xD03298, 0xD03940, 0xD04B88 }.Select(address => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address)).ToImmutableArray();
        uint[] addresses = Enumerable.Range(0, 5).Select(index => U32(memory.Read(0xD05268u + (uint)index * 4, 4), 0)).ToArray();
        byte[] axis = memory.Read(0xD05238, 12);
        byte[] secondaryAxis = memory.Read(0xD05248, 12);
        byte[] tertiaryAxis = memory.Read(0xD05258, 12);
        return new(new("FF8_EN:CFB348", actor), sprites, meshes, [new(unchecked((short)(int)U32(axis, 0)), unchecked((short)(int)U32(axis, 4)), unchecked((short)(int)U32(axis, 8))), new(unchecked((short)(int)U32(secondaryAxis, 0)), unchecked((short)(int)U32(secondaryAxis, 4)), unchecked((short)(int)U32(secondaryAxis, 8))), new(unchecked((short)(int)U32(tertiaryAxis, 0)), unchecked((short)(int)U32(tertiaryAxis, 4)), unchecked((short)(int)U32(tertiaryAxis, 8)))], addresses.ToImmutableArray(), U32(memory.Read(0xD05230, 4), 0));
    }

    private static uint U32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
}

public sealed partial class GameAssets
{
    private NativeBattleShot197Resources? nativeShot197Resources;
    public NativeBattleShot197Resources ReadNativeShot197Resources() => nativeShot197Resources ??= NativeBattleShot197ResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
