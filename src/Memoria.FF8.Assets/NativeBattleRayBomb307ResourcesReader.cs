using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleRayBomb307ResourcesReader
{
    public static NativeBattleRayBomb307Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(0xCE4158, 11)) != "mag009.tim\0")
        {
            throw new InvalidDataException("Ray-Bomb requires its original initializer texture name.");
        }
        uint[] meshes = [0xCE190C, 0xCE2CE4, 0xCE2F9C, 0xCE3254, 0xCE350C, 0xCE3BB4];
        uint[] sprites = [0xCE13E0, 0xCE158C, 0xCE15B4, 0xCE1760];
        NativeBattleRayBomb307Resources result = new(new("FF8_EN:CDF86C", memory.Read(0xCDF86C, 0x1B74).ToImmutableArray()), new("FF8_EN:CDF6D8", memory.Read(0xCDF6D8, 404).ToImmutableArray()), meshes.Select(address => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address)).ToImmutableArray(), Enumerable.Range(0, 3).Select(index => Dword(0xCE4128u + (uint)index * 4)).ToImmutableArray(), sprites.Select(address => new NativeBattleModuleResource(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address))).ToImmutableArray(), Enumerable.Range(0, 10).Select(index => unchecked((int)Dword(0xCE40D8u + (uint)index * 4))).ToImmutableArray(), Enumerable.Range(0, 10).Select(index => unchecked((int)Dword(0xCE4100u + (uint)index * 4))).ToImmutableArray(), Anchors(0xCE4138), Anchors(0xCE4148), [new(0xCE40CC, Dword(0xCE40CC)), new(0xCE40D0, Dword(0xCE40D0))]);
        ValidatePayloads(result);
        return result;

        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
        ImmutableArray<NativeBattleModuleAnchor> Anchors(uint address) => Enumerable.Range(0, 2).Select(index =>
        {
            byte[] bytes = memory.Read(address + (uint)index * 8, 8);
            return new NativeBattleModuleAnchor(Word(bytes, 0), Word(bytes, 2), Word(bytes, 4), Word(bytes, 6));
        }).ToImmutableArray();
    }
    public static void ValidatePayloads(NativeBattleRayBomb307Resources resources)
    {
        resources.Validate();
        int[] vertices = [144, 12, 12, 12, 55, 50];
        int[][] groups = [[0, 0, 0, 0, 0, 0, 0, 108], [0, 0, 0, 0, 0, 0, 20, 0], [0, 0, 0, 0, 0, 0, 20, 0], [0, 0, 0, 0, 0, 0, 20, 0], [0, 0, 0, 0, 18, 36, 0, 0], [0, 0, 0, 0, 0, 36, 0, 0]];
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int meshIndex = 0; meshIndex < resources.Meshes.Length; meshIndex++)
        {
            ReadOnlySpan<byte> bytes = resources.Meshes[meshIndex].Bytes.AsSpan();
            int cursor = BinaryPrimitives.ReadInt32LittleEndian(bytes);
            if (BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(4)) != vertices[meshIndex] || cursor != 8 + vertices[meshIndex] * 8)
            {
                throw new InvalidDataException("Ray-Bomb static shape has an incomplete packed vertex directory.");
            }
            for (int group = 0; group < 8; group++)
            {
                int count = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(cursor, 4));
                cursor += 4;
                if (count != groups[meshIndex][group])
                {
                    throw new InvalidDataException("Ray-Bomb static shape changes its original primitive groups.");
                }
                for (int face = 0; face < count; face++)
                {
                    int corners = (group & 1) == 0 ? 3 : 4;
                    for (int corner = 0; corner < corners; corner++)
                    {
                        int index = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(cursor + 4 + corner * 2, 2));
                        if ((index & 1) != 0 || index * 4 + 8 > vertices[meshIndex] * 8)
                        {
                            throw new InvalidDataException("Ray-Bomb polygon selects an absent original packed vertex.");
                        }
                    }
                    cursor += strides[group];
                }
            }
            if (cursor != bytes.Length)
            {
                throw new InvalidDataException("Ray-Bomb static shape has an incomplete primitive boundary.");
            }
        }
        int start = NativeBattleDatDirectory.Section(resources.CasterDat.Data.AsSpan(), 0);
        int end = NativeBattleDatDirectory.Section(resources.CasterDat.Data.AsSpan(), 1);
        if (NativeBattlePoseDecoder.ReadFrameCounts(resources.CasterDat.Data.AsSpan().Slice(start, end - start)).Length != 9)
        {
            throw new InvalidDataException("Ray-Bomb caster binding has an incomplete animation directory.");
        }
    }
    private static short Word(byte[] bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset, 2));
}

public sealed partial class GameAssets
{
    private NativeBattleRayBomb307Resources? nativeRayBomb307Resources;
    public NativeBattleRayBomb307Resources ReadNativeRayBomb307Resources() => nativeRayBomb307Resources ??= NativeBattleRayBomb307ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
