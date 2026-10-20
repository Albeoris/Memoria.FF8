using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleRinoa89ResourcesReader
{
    public static ImmutableArray<NativeBattleEffectFileAsset> ReadFileNames(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        return new short[] { 758, 759, 760, 761, 762, 763, 764, 360, 361, 362 }.Select(id =>
        {
            uint name = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(checked(0xB84CCCu + (uint)id * 4), 4));
            byte[] bytes = memory.Read(name, 128);
            int length = bytes.AsSpan().IndexOf((byte)0);
            if (length <= 0)
            {
                throw new InvalidDataException("Native Wishing Star archive name is not terminated.");
            }
            return new NativeBattleEffectFileAsset(id, Encoding.ASCII.GetString(bytes.AsSpan(0, length)), []);
        }).ToImmutableArray();
    }

    public static NativeBattleRinoa89Resources Read(byte[] executable, ImmutableArray<NativeBattleEffectFileAsset> files)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<NativeBattleEffectFileAsset> names = ReadFileNames(executable);
        int[] sizes = [33312, 33312, 30848, 33312, 33824, 33824, 26176, 33824, 33824, 33824];
        if (files.IsDefault || files.Length != sizes.Length)
        {
            throw new InvalidDataException("Native Wishing Star requires its seven scene files and three return textures.");
        }
        for (int index = 0; index < files.Length; index++)
        {
            if (files[index].FileId != names[index].FileId || files[index].Name != names[index].Name || files[index].Data.IsDefault || files[index].Data.Length != sizes[index])
            {
                throw new InvalidDataException("Native Wishing Star archive bytes changed their original identities or extents.");
            }
        }
        if (memory.ReadLoader(0x2556584, 160).Any(value => value != 0))
        {
            throw new InvalidDataException("Native Wishing Star process globals require their original loader-zero owner.");
        }
        ImmutableArray<byte> body = Read(0x14913FC, 16692);
        int[] directory = [24, 1672, 13124, 16680, 16692];
        for (int index = 0; index < directory.Length; index++)
        {
            if (NativeBattleDatDirectory.Section(body.AsSpan(), index) != directory[index])
            {
                throw new InvalidDataException("Native Wishing Star requires its complete original model directory.");
            }
        }
        NativeBattlePoseSource pose = new(body.Slice(24, 1648), body.Slice(13124, 3556));
        pose.Validate();
        ImmutableArray<NativeBattleRawMesh> geometry = NativeBattleRawGeometryReader.Read(body.Slice(1672, 11452).ToArray(), pose.BoneCount);
        foreach (NativeBattleRawMesh mesh in geometry)
        {
            mesh.Validate();
        }
        if (pose.BoneCount != 34 || geometry.Max(mesh => mesh.Vertices.Length) != 302)
        {
            throw new InvalidDataException("Native Wishing Star requires its actual34-bone302-vertex body.");
        }
        NativeBattleRinoa89Resources result = new(new("native-limit89-static", Read(NativeBattleRinoa89Resources.StaticAddress, checked((int)(NativeBattleRinoa89Resources.StaticEnd - NativeBattleRinoa89Resources.StaticAddress)))), [Tim(0x14AF700, 33824), Tim(0x14B7B20, 101408)])
        {
            Body = new(0x14913FC, body, pose, geometry),
            Files = files,
            Sounds = new uint[] { 0x14913F0, 0x14913F4, 0x14913F8 }.ToImmutableDictionary(address => address, address => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4)))
        };
        result.Validate();
        return result;

        ImmutableArray<byte> Read(uint address, int length) => NativeBattleEffectStaticResourcesReader.Read(memory, address, length);
        NativeBattleEffectHeapAllocation Tim(uint address, int expected)
        {
            int first = BinaryPrimitives.ReadInt32LittleEndian(memory.Read(address + 8, 4));
            int second = BinaryPrimitives.ReadInt32LittleEndian(memory.Read(checked(address + 8 + (uint)first), 4));
            int length = checked(8 + first + second);
            if (length != expected)
            {
                throw new InvalidDataException("Native Wishing Star entry TIM changed its complete original extent.");
            }
            return new("native-limit89-entry-texture", Read(address, length));
        }
    }
}

public sealed partial class GameAssets
{
    private NativeBattleRinoa89Resources? nativeRinoa89;

    public NativeBattleRinoa89Resources ReadNativeRinoa89Resources()
    {
        if (nativeRinoa89 is null)
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(resources);
            using IFlArchive battle = OpenArchive("battle");
            ImmutableArray<NativeBattleEffectFileAsset> files = NativeBattleRinoa89ResourcesReader.ReadFileNames(executable).Select(file => file with { Data = Read(battle, file.Name).ToImmutableArray() }).ToImmutableArray();
            nativeRinoa89 = NativeBattleRinoa89ResourcesReader.Read(executable, files);
        }
        return nativeRinoa89;
    }
}
