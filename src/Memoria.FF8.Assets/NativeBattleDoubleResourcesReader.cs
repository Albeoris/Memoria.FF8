using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleDoubleResourcesReader
{
    public static NativeBattleDoubleResources Read(byte[] executable)
    {
        const uint animationAddress = 0x1641188;
        ImmutableArray<byte> header = NativeBattleEffectStaticResourcesReader.Read(executable, animationAddress, 8);
        ushort length = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan().Slice(6));
        if (length != 0x630 || BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan()) != 2 || animationAddress + length != 0x16417B8)
        {
            throw new InvalidDataException("Native Double animation must end at its original initializer filename.");
        }
        NativeBattleEffectHeapAllocation animation = new("FF8_EN:1641188", NativeBattleEffectStaticResourcesReader.Read(executable, animationAddress, length));
        ImmutableArray<NativeBattleModuleResource> sprites = new uint[] { 0x163E930, 0x163EA90, 0x163EB6C }.Select(address => NativeBattleEffectStaticResourcesReader.ReadSprite(executable, address)).ToImmutableArray();
        ImmutableArray<NativeBattleStaticMeshResource> meshes = new uint[] { 0x163EB94, 0x163EDF4, 0x163F014, 0x163F224, 0x163FFD0 }.Select(address => NativeBattleEffectStaticResourcesReader.ReadMesh(executable, address)).ToImmutableArray();
        uint sound = BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffectStaticResourcesReader.Read(executable, 0x163E92C, 4).AsSpan());
        return new(animation, sprites, meshes, ReadShape(0x163FB00), ReadShape(0x163FD68), sound);

        ImmutableArray<byte> ReadShape(uint address)
        {
            ImmutableArray<byte> directory = NativeBattleEffectStaticResourcesReader.Read(executable, address, 8);
            int count = BinaryPrimitives.ReadInt32LittleEndian(directory.AsSpan().Slice(4));
            if (count is < 0 or > 4096)
            {
                throw new InvalidDataException("Native Double morph shape exceeds its bounded original vertex count.");
            }
            return NativeBattleEffectStaticResourcesReader.Read(executable, address, checked(8 + count * 8));
        }
    }
}

public sealed partial class GameAssets
{
    private NativeBattleDoubleResources? nativeDoubleResources;

    public NativeBattleDoubleResources ReadNativeDoubleResources()
    {
        return nativeDoubleResources ??= NativeBattleDoubleResourcesReader.Read(File.ReadAllBytes(Path.Combine(GameDirectory, "FF8_EN.exe")));
    }
}
