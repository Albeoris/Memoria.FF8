using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleRenzokukenSpecial161ResourcesReader
{
    public static NativeBattleSearchSpriteResource ReadTargetSprite(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        return new(0xDA8D28, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, 0xDA8D28));
    }

    public static NativeBattleRenzokuken160Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        BattleNativeRenzokukenResource actor = NativeBattleRenzokukenResourceReader.Read(executable).Single(resource => resource.MagicId == 161);
        ImmutableArray<NativeBattleSearchSpriteResource> sprites = new uint[] { 0xDA8ED4, 0xDA91A4, 0xDA9080 }.Select(address => new NativeBattleSearchSpriteResource(address, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address))).ToImmutableArray();
        ImmutableArray<NativeBattleStaticMeshResource> meshes = new uint[] { 0xDA9474, 0xDA9E5C, 0xDAA12C, 0xE9AD28 }.Select(address => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address)).ToImmutableArray();
        ImmutableArray<uint> colors = Enumerable.Range(0, 24).Select(index => U32(0xDAA668u + 4u * (uint)index)).ToImmutableArray();
        ImmutableArray<int> lead = Enumerable.Range(0, 6).Select(index => unchecked((int)U32(0xDAA6D8u + 4u * (uint)index))).ToImmutableArray();
        return new(new("FF8_EN:E9AEC0", NativeBattleEffectStaticResourcesReader.Read(memory, 0xE9AEC0, 0x454)), actor, sprites, colors, lead, meshes, U32(0xE9AD24), U32(0xB8B7D8)) { ImpactAxis = Enumerable.Range(0, 3).Select(index => unchecked((int)U32(0xDAA6C8u + 4u * (uint)index))).ToImmutableArray(), StreakAgeOffsets = Enumerable.Range(0, 8).Select(index => U16(0xE9AEB0u + 2u * (uint)index)).ToImmutableArray(), StreakPaletteOffsets = Enumerable.Range(0, 8).Select(index => U16(0xE9AEA0u + 2u * (uint)index)).ToImmutableArray(), StreakReferencePoint = new(unchecked((short)U16(0xB8B7F0)), unchecked((short)U16(0xB8B7F2)), unchecked((short)U16(0xB8B7F4))) };

        uint U32(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
        ushort U16(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(memory.Read(address, 2));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleRenzokuken160Resources? nativeRenzokukenSpecial161Resources;
    public NativeBattleSearchSpriteResource ReadNativeRenzokukenSpecial161TargetSprite() => NativeBattleRenzokukenSpecial161ResourcesReader.ReadTargetSprite(GameDataResources.ReadNativeExecutable(this.resources));
    public NativeBattleRenzokuken160Resources ReadNativeRenzokukenSpecial161Resources() => nativeRenzokukenSpecial161Resources ??= NativeBattleRenzokukenSpecial161ResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
