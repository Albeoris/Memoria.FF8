using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleRenzokuken159ResourcesReader
{
    public static NativeBattleRenzokuken159Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        BattleNativeRenzokukenResource actor = NativeBattleRenzokukenResourceReader.Read(executable).Single(resource => resource.MagicId == 159);
        ImmutableArray<NativeBattleSearchSpriteResource> sprites = new uint[] { 0xDAC2D0, 0xDAC5A0, 0xDAC47C }.Select(address => new NativeBattleSearchSpriteResource(address, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address))).ToImmutableArray();
        ImmutableArray<NativeBattleStaticMeshResource> meshes = new uint[] { 0xDAC858, 0xDAD240, 0xDAD510, 0xEA32D0, 0xDAD928, 0xDAD9C8, 0xDAE9B8 }.Select(address => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address)).ToImmutableArray();
        ImmutableArray<uint> colors = Enumerable.Range(0, 24).Select(index => U32(0xDAFAC4u + 4u * (uint)index)).ToImmutableArray();
        ImmutableArray<int> lead = Enumerable.Range(0, 4).Select(index => unchecked((int)U32(0xDAFB38u + 4u * (uint)index))).ToImmutableArray();
        return new(new("FF8_EN:EA3468", NativeBattleEffectStaticResourcesReader.Read(memory, 0xEA3468, 0x5B0)), actor, sprites, colors, lead, meshes, U32(0xEA32CC), U32(0xB8B7D8))
        {
            ContactSpread = Enumerable.Range(0, 4).Select(index => unchecked((int)U32(0xDAFB68u + 4u * (uint)index))).ToImmutableArray(),
            ImpactAxis = Enumerable.Range(0, 3).Select(index => unchecked((int)U32(0xDAFB28u + 4u * (uint)index))).ToImmutableArray(),
            StreakAgeOffsets = Enumerable.Range(0, 8).Select(index => U16(0xEA3458u + 2u * (uint)index)).ToImmutableArray(),
            StreakPaletteOffsets = Enumerable.Range(0, 8).Select(index => U16(0xEA3448u + 2u * (uint)index)).ToImmutableArray(),
            StreakReferencePoint = new(unchecked((short)U16(0xB8B7F0)), unchecked((short)U16(0xB8B7F2)), unchecked((short)U16(0xB8B7F4)))
        };

        uint U32(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
        ushort U16(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(memory.Read(address, 2));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleRenzokuken159Resources? nativeRenzokuken159Resources;
    public NativeBattleRenzokuken159Resources ReadNativeRenzokuken159Resources() => nativeRenzokuken159Resources ??= NativeBattleRenzokuken159ResourcesReader.Read(GameDataResources.ReadNativeExecutable(this.resources));
}
