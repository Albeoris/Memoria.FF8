using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleNoMercy200ResourcesReader
{
    public static ImmutableArray<NativeBattleEffectFileAsset> ReadTextureNames(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        return new short[] { 562, 563, 564 }.Select(id =>
        {
            uint address = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xB84CCCu + (uint)id * 4, 4));
            byte[] text = memory.Read(address, 128);
            int end = text.AsSpan().IndexOf((byte)0);
            if (end <= 0)
            {
                throw new InvalidDataException("Native No Mercy texture name is not terminated.");
            }
            return new NativeBattleEffectFileAsset(id, Encoding.ASCII.GetString(text.AsSpan(0, end)), []);
        }).ToImmutableArray();
    }

    public static NativeBattleNoMercy200Resources Read(byte[] executable, ImmutableArray<NativeBattleEffectFileAsset> files)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<NativeBattleNoMercy200ColdRegion> cold = new (uint Address, int Length)[] { (0x24F7A68, 0x12F8), (0x24F8D90, 0xC00), (0x24FBCC8, 0xA0), (0xE33D70, 28) }.Select(range => new NativeBattleNoMercy200ColdRegion(range.Address, memory.ReadLoader(range.Address, range.Length).ToImmutableArray())).ToImmutableArray();
        ImmutableArray<NativeBattleStaticMeshResource> meshes = new uint[] { 0xE2E15C, 0xE2F8DC, 0xE2FFB4, 0xE3102C, 0xE316B4, 0xE3239C, 0xE3260C, 0xE328BC, 0xE33074 }.Select(address => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address)).ToImmutableArray();
        ImmutableArray<NativeBattleSearchSpriteResource> sprites = new uint[] { 0xE3382C, 0xE339D8, 0xE33B84, 0xE2DB40, 0xE2DC84, 0xE2DFE4 }.Select(address => new NativeBattleSearchSpriteResource(address, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address))).ToImmutableArray();
        byte[] header = memory.Read(0xF57D60, 12);
        if (BinaryPrimitives.ReadUInt32LittleEndian(header) != 16 || BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4)) != 9 || BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8)) != 1548)
        {
            throw new InvalidDataException("Native No Mercy requires its original paletted entry TIM.");
        }
        int pixels = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xF58374, 4)));
        if (pixels != 131084)
        {
            throw new InvalidDataException("Native No Mercy entry TIM has an unexpected pixel extent.");
        }
        NativeBattleNoMercy200Resources result = new(new("FF8_EN:E2B518", memory.Read(NativeBattleNoMercy200Resources.StaticAddress, NativeBattleNoMercy200Resources.StaticLength).ToImmutableArray()), cold, new("FF8_EN:F57D60", memory.Read(0xF57D60, checked(8 + 1548 + pixels)).ToImmutableArray()), files, meshes, sprites, new(0xE33D8C, memory.Read(0xE33D8C, 0x148).ToImmutableArray()));
        result.Validate();
        return result;
    }
}

public sealed partial class GameAssets
{
    private NativeBattleNoMercy200Resources? nativeNoMercy200Resources;

    public NativeBattleNoMercy200Resources ReadNativeNoMercy200Resources()
    {
        if (nativeNoMercy200Resources is null)
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(resources);
            ImmutableArray<NativeBattleEffectFileAsset> names = NativeBattleNoMercy200ResourcesReader.ReadTextureNames(executable);
            using IFlArchive battle = OpenArchive("battle");
            nativeNoMercy200Resources = NativeBattleNoMercy200ResourcesReader.Read(executable, names.Select(name => name with { Data = Read(battle, name.Name).ToImmutableArray() }).ToImmutableArray());
        }
        return nativeNoMercy200Resources;
    }
}
