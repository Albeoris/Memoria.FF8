using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleRenzokuken332ResourcesReader
{
    public static NativeBattleRenzokuken160Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        BattleNativeRenzokukenResource actor = NativeBattleRenzokukenResourceReader.Read(executable).Single(resource => resource.MagicId == 332);
        ImmutableArray<NativeBattleSearchSpriteResource> sprites = new uint[] { 0xCC3648, 0xCC3918, 0xCC37F4 }.Select(address => new NativeBattleSearchSpriteResource(address, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address))).ToImmutableArray();
        ImmutableArray<NativeBattleStaticMeshResource> meshes = new uint[] { 0xCC3BD0, 0xCC45B8, 0xCC4838, 0xE7D4A4, 0xCC54E0 }.Select(address => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address)).ToImmutableArray();
        ImmutableArray<uint> colors = Enumerable.Range(0, 24).Select(index => U32(0xCC660Cu + 4u * (uint)index)).ToImmutableArray();
        ImmutableArray<int> lead = Enumerable.Range(0, 7).Select(index => unchecked((int)U32(0xCC6680u + 4u * (uint)index))).ToImmutableArray();
        return new(new("FF8_EN:E7D63C", NativeBattleEffectStaticResourcesReader.Read(memory, 0xE7D63C, 0xD2C)), actor, sprites, colors, lead, meshes, U32(0xE7D4A0), U32(0xB8B7D8)) { ImpactAxis = Enumerable.Range(0, 3).Select(index => unchecked((int)U32(0xCC6670u + 4u * (uint)index))).ToImmutableArray(), StreakAgeOffsets = Enumerable.Range(0, 8).Select(index => U16(0xE7D62Cu + 2u * (uint)index)).ToImmutableArray(), StreakPaletteOffsets = Enumerable.Range(0, 8).Select(index => U16(0xE7D61Cu + 2u * (uint)index)).ToImmutableArray(), ContactOffsets = Enumerable.Range(0, 7).Select(index => new NativeBattleVector(unchecked((short)U16(0xCC66B0u + 8u * (uint)index)), unchecked((short)U16(0xCC66B0u + 8u * (uint)index + 2)), unchecked((short)U16(0xCC66B0u + 8u * (uint)index + 4)))).ToImmutableArray(), StreakReferencePoint = new(unchecked((short)U16(0xB8B7F0)), unchecked((short)U16(0xB8B7F2)), unchecked((short)U16(0xB8B7F4))) };

        uint U32(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
        ushort U16(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(memory.Read(address, 2));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleRenzokuken160Resources? nativeRenzokuken332Resources;
    public NativeBattleRenzokuken160Resources ReadNativeRenzokuken332Resources() => nativeRenzokuken332Resources ??= NativeBattleRenzokuken332ResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
