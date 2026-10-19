using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleRenzokuken333ResourcesReader
{
    public static NativeBattleRenzokuken160Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        BattleNativeRenzokukenResource actor = NativeBattleRenzokukenResourceReader.Read(executable).Single(resource => resource.MagicId == 333);
        ImmutableArray<NativeBattleSearchSpriteResource> sprites = new uint[] { 0xCC0410, 0xCC06E0, 0xCC05BC }.Select(address => new NativeBattleSearchSpriteResource(address, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address))).ToImmutableArray();
        ImmutableArray<NativeBattleStaticMeshResource> meshes = new uint[] { 0xCC0998, 0xCC1380, 0xCC1600, 0xE7A3AC, 0xCC2B38 }.Select(address => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address)).ToImmutableArray();
        ImmutableArray<uint> colors = Enumerable.Range(0, 24).Select(index => U32(0xCC3524u + 4u * (uint)index)).ToImmutableArray();
        ImmutableArray<int> lead = Enumerable.Range(0, 8).Select(index => unchecked((int)U32(0xCC3598u + 4u * (uint)index))).ToImmutableArray();
        return new(new("FF8_EN:E7A544", NativeBattleEffectStaticResourcesReader.Read(memory, 0xE7A544, 0xB7C)), actor, sprites, colors, lead, meshes, U32(0xE7A3A8), U32(0xB8B7D8)) { ImpactAxis = Enumerable.Range(0, 3).Select(index => unchecked((int)U32(0xCC3588u + 4u * (uint)index))).ToImmutableArray(), StreakAgeOffsets = Enumerable.Range(0, 8).Select(index => U16(0xE7A534u + 2u * (uint)index)).ToImmutableArray(), StreakPaletteOffsets = Enumerable.Range(0, 8).Select(index => U16(0xE7A524u + 2u * (uint)index)).ToImmutableArray(), ContactOffsets = Enumerable.Range(0, 8).Select(index => new NativeBattleVector(unchecked((short)U16(0xCC35C8u + 8u * (uint)index)), unchecked((short)U16(0xCC35C8u + 8u * (uint)index + 2)), unchecked((short)U16(0xCC35C8u + 8u * (uint)index + 4)))).ToImmutableArray(), StreakReferencePoint = new(unchecked((short)U16(0xB8B7F0)), unchecked((short)U16(0xB8B7F2)), unchecked((short)U16(0xB8B7F4))) };

        uint U32(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
        ushort U16(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(memory.Read(address, 2));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleRenzokuken160Resources? nativeRenzokuken333Resources;
    public NativeBattleRenzokuken160Resources ReadNativeRenzokuken333Resources() => nativeRenzokuken333Resources ??= NativeBattleRenzokuken333ResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
