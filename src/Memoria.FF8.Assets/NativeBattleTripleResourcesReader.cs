using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleTripleResourcesReader
{
    public static NativeBattleTripleResources Read(byte[] executable)
    {
        const uint animationAddress = 0x1629288;
        ImmutableArray<byte> header = NativeBattleEffectStaticResourcesReader.Read(executable, animationAddress, 8);
        ushort length = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan().Slice(6));
        if (length != 0x98C || BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan()) != 2 || animationAddress + length != 0x1629C14)
        {
            throw new InvalidDataException("Native Triple animation must end at its original two curve tables.");
        }
        NativeBattleEffectHeapAllocation animation = new("FF8_EN:1629288", NativeBattleEffectStaticResourcesReader.Read(executable, animationAddress, length));
        ImmutableArray<NativeBattleModuleResource> sprites = new uint[] { 0x16269DC, 0x1626B3C, 0x1626C18, 0x1626CC8 }.Select(address => NativeBattleEffectStaticResourcesReader.ReadSprite(executable, address)).ToImmutableArray();
        ImmutableArray<NativeBattleStaticMeshResource> meshes = new uint[] { 0x1626CF0, 0x1626F00, 0x1627160, 0x1627380, 0x1627970, 0x1628218 }.Select(address => NativeBattleEffectStaticResourcesReader.ReadMesh(executable, address)).ToImmutableArray();
        uint sound = BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectStaticResourcesReader.Read(executable, 0x16269D8, 4).AsSpan());
        return new(animation, sprites, meshes, ReadShape(0x16276E8), ReadShape(0x1629000), ReadCurve(0x1629C14), ReadCurve(0x1629C2C), sound);

        ImmutableArray<byte> ReadShape(uint address)
        {
            ImmutableArray<byte> directory = NativeBattleEffectStaticResourcesReader.Read(executable, address, 8);
            int count = BinaryPrimitives.ReadInt32LittleEndian(directory.AsSpan().Slice(4));
            if (count is < 0 or > 4096)
            {
                throw new InvalidDataException("Native Triple morph shape exceeds its bounded original vertex count.");
            }
            return NativeBattleEffectStaticResourcesReader.Read(executable, address, checked(8 + count * 8));
        }

        ImmutableArray<ushort> ReadCurve(uint address)
        {
            ImmutableArray<byte> bytes = NativeBattleEffectStaticResourcesReader.Read(executable, address, 24);
            ImmutableArray<ushort>.Builder words = ImmutableArray.CreateBuilder<ushort>(12);
            for (int index = 0; index < 12; index++)
            {
                words.Add(BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(index * 2)));
            }
            return words.MoveToImmutable();
        }
    }
}

public sealed partial class GameAssets
{
    private NativeBattleTripleResources? nativeTripleResources;

    public NativeBattleTripleResources ReadNativeTripleResources()
    {
        return nativeTripleResources ??= NativeBattleTripleResourcesReader.Read(File.ReadAllBytes(Path.Combine(GameDirectory, "FF8_EN.exe")));
    }
}
