using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleShivaResourcesReader
{
    public static NativeBattleShivaResources Read(IGameNativeMemorySource memory, Func<short, string, ImmutableArray<byte>> readBattleFile)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(readBattleFile);
        ImmutableArray<NativeBattleEffectFileAsset>.Builder files = ImmutableArray.CreateBuilder<NativeBattleEffectFileAsset>(6);
        for (short id = 543; id <= 548; id++)
        {
            uint address = U32(checked(0xB84CCCu + 4u * (uint)id));
            byte[] nameBytes = memory.Read(address, 128);
            int end = nameBytes.AsSpan().IndexOf((byte)0);
            if (end < 1)
            {
                throw new InvalidDataException("Native Shiva battle-file directory lacks its bounded original filename.");
            }
            string name = Encoding.ASCII.GetString(nameBytes.AsSpan(0, end));
            files.Add(new(id, name, readBattleFile(id, name)));
        }
        ImmutableArray<byte> vibrationBytes = Read(0xD32344, 0x144);
        NativeVibrationBank vibration = NativeVibrationBankReader.ReadTable(vibrationBytes.ToArray(), memory.SourceHash);
        ImmutableArray<NativeBattleEffectFileAsset> loadedFiles = files.MoveToImmutable();
        NativeBattleShivaResources result = new(
            new("FF8_EN:D32604", Read(0xD32604, 0x13E)),
            vibration,
            U32(0xE9AD20),
            [U32(0xD32488), U32(0xD3248C), U32(0xD32490), U32(0xD32494)],
            loadedFiles,
            new("FF8_EN:D20FC4", Read(0xD20FC4, 0x250)),
            NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xD215C4u),
            new("FF8_EN:D324B8", Read(0xD324B8, 64)))
        {
            BurstMeshD3138C = NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xD3138Cu),
            ShardMeshD2F1C4 = NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xD2F1C4u),
            FirstSpriteD209F4 = NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0xD209F4),
            SecondSpriteD20C7C = NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0xD20C7C),
            ParticleSpriteD20AD0 = NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0xD20AD0),
            Model544 = ReadModel544(loadedFiles[1].Data),
            TextureValuesD3250C = Enumerable.Range(0, 6).Select(index => unchecked((int)U32(checked(0xD3250Cu + (uint)(index * 4))))).ToImmutableArray(),
            ClippingVertexSelectorsD32524 = Enumerable.Range(0, 56).Select(index => unchecked((int)U32(checked(0xD32524u + (uint)(index * 4))))).ToImmutableArray(),
            ModelParticleSpriteD208B0 = NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0xD208B0),
            AnchorMeshD31A74 = NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xD31A74u),
            RingMeshD3048C = NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xD3048Cu),
            RingMeshD309A4 = NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xD309A4u),
            ChildMeshD21B9C = NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xD21B9Cu),
            ScrollMeshD2A50C = NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xD2A50Cu),
            SpawnRowsD324F8 = new("FF8_EN:D324F8", Read(0xD324F8, 16)),
            FinalMeshD257CC = NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xD257CCu),
            FinalMeshD27E6C = NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xD27E6Cu),
            LateParticleSpriteD20E10 = NativeBattleEffectStaticResourcesReader.ReadSprite(memory, 0xD20E10),
            SecondShatterMeshD2BA3C = NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xD2BA3Cu),
            SecondShatterMeshD2D600 = NativeBattleEffectStaticResourcesReader.ReadMesh(memory, 0xD2D600u)
        };
        result.Validate();
        return result;

        ImmutableArray<byte> Read(uint address, int length) => NativeBattleEffectStaticResourcesReader.Read(memory, address, length);
        uint U32(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
    }

    private static NativeBattleEffectOwnedModelResource ReadModel544(ImmutableArray<byte> bytes)
    {
        int skeleton = NativeBattleDatDirectory.Section(bytes.AsSpan(), 0);
        int geometryOffset = NativeBattleDatDirectory.Section(bytes.AsSpan(), 1);
        int animations = NativeBattleDatDirectory.Section(bytes.AsSpan(), 2);
        int end = NativeBattleDatDirectory.Section(bytes.AsSpan(), 3);
        NativeBattlePoseSource pose = new(bytes.Slice(skeleton, geometryOffset - skeleton), bytes.Slice(animations, end - animations));
        pose.Validate();
        ImmutableArray<NativeBattleRawMesh> geometry = NativeBattleRawGeometryReader.Read(bytes.Slice(geometryOffset, animations - geometryOffset).ToArray(), pose.BoneCount);
        foreach (NativeBattleRawMesh mesh in geometry)
        {
            mesh.Validate();
        }
        return new(0x20DFAB8, bytes, pose, geometry);
    }
}

public sealed partial class GameAssets
{
    private NativeBattleShivaResources? nativeShivaResources;

    public NativeBattleShivaResources ReadNativeShivaResources()
    {
        if (nativeShivaResources is null)
        {
            using IFlArchive battle = OpenArchive("battle");
            nativeShivaResources = NativeBattleShivaResourcesReader.Read(ReadNativeMemory(), (_, name) => Read(battle, name).ToImmutableArray());
        }
        return nativeShivaResources;
    }
}
