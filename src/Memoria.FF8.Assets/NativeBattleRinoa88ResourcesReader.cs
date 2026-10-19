using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleRinoa88ResourcesReader
{
    public static ImmutableArray<NativeBattleEffectFileAsset> ReadFileNames(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        return Enumerable.Range(410, 6).Select(index =>
        {
            uint name = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(checked(0xB84CCCu + (uint)index * 4), 4));
            byte[] bytes = memory.Read(name, 128);
            int length = bytes.AsSpan().IndexOf((byte)0);
            if (length < 1)
            {
                throw new InvalidDataException("Native Rinoa88 requires its actual terminated archive names.");
            }
            return new NativeBattleEffectFileAsset(checked((short)index), Encoding.ASCII.GetString(bytes.AsSpan(0, length)), []);
        }).ToImmutableArray();
    }

    public static NativeBattleRinoa88Resources Read(byte[] executable, ImmutableArray<NativeBattleEffectFileAsset> files)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<byte> script = Read(0x154F898, 128);
        ImmutableArray<NativeBattleEffectFileAsset> names = ReadFileNames(executable);
        if (files.IsDefault || files.Length != 6 || U16(script, 0) != 0 || U16(script, 112) != 255)
        {
            throw new InvalidDataException("Native Rinoa88 requires its actual wait/data/data/data/data/TIM/TIM/end sequence.");
        }
        int[] opcodes = [2, 4, 4, 3, 1, 1];
        int[] lengths = [8356, 10912, 7984, 12944, 33824, 33824];
        for (int index = 0; index < files.Length; index++)
        {
            int row = (index + 1) * 16;
            if (U16(script, row) != opcodes[index] || U16(script, row + 2) != names[index].FileId || files[index].FileId != names[index].FileId || files[index].Name != names[index].Name || files[index].Data.IsDefault || files[index].Data.Length != lengths[index] || I32(script, row + 8) != lengths[index])
            {
                throw new InvalidDataException("Native Rinoa88 archive bytes disagree with their original file identities and declared lengths.");
            }
        }
        NativeBattleEffectOwnedModelResource primary = Model(0x154F91C, 23840, [24, 1672, 13124, 23828, 23840]);
        NativeBattleEffectOwnedModelResource secondary = Model(0x155563C, 1828, [24, 136, 816, 1828, 1828]);
        if (primary.Geometry.Max(mesh => mesh.Vertices.Length) != 302 || secondary.Geometry.Max(mesh => mesh.Vertices.Length) != 20)
        {
            throw new InvalidDataException("Native7719A0 requires the original302/20-vertex model owners.");
        }
        new NativeBattleRinoa88NodeResources(files[0].Data).Validate();
        ImmutableArray<byte> actor = Read(0x15560C0, 7148);
        for (int index = 0; index < 4; index++)
        {
            if (NativeBattleDatDirectory.Section(actor.AsSpan(), index) != new[] { 20, 6472, 6500, 7148 }[index])
            {
                throw new InvalidDataException("Native772AB0 requires its complete original three-section caster override.");
            }
        }
        ImmutableDictionary<uint, NativeBattleEffectHeapAllocation> textures = new uint[] { 0x16F3000, 0x16FB420 }.ToImmutableDictionary(address => address, Tim);
        return new(new(new("native-limit88-io-script", script), files), primary, secondary, BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xB8B9A8, 4)), new("native-limit88-actor", actor), textures, NativeBattleRinoa88CameraResourcesReader.Read(executable), new(0x16F1F8C, Read(0x16F1F8C, 0x1074)), new(files[0].Data))
        {
            Sprites = new uint[] { 0x154ECEC, 0x154EE30 }.ToImmutableDictionary(address => address, address => NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address)),
            Meshes = new uint[] { 0x154EF74, 0x154F404 }.ToImmutableDictionary(address => address, address => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address).Bytes),
            ScreenColors = Enumerable.Range(0, 27).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(checked(0x1557CACu + (uint)index * 4), 4))).ToImmutableArray(),
            Sounds = new uint[] { 0x154ECE4, 0x154ECE8 }.ToImmutableDictionary(address => address, address => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4)))
        };

        NativeBattleEffectOwnedModelResource Model(uint address, int length, int[] sections)
        {
            ImmutableArray<byte> data = Read(address, length);
            for (int index = 0; index < sections.Length; index++)
            {
                if (NativeBattleDatDirectory.Section(data.AsSpan(), index) != sections[index])
                {
                    throw new InvalidDataException("Native Rinoa88 requires each complete original owned model directory.");
                }
            }
            NativeBattlePoseSource pose = new(data.Slice(sections[0], sections[1] - sections[0]), data.Slice(sections[2], sections[3] - sections[2]));
            pose.Validate();
            ImmutableArray<NativeBattleRawMesh> geometry = NativeBattleRawGeometryReader.Read(data.Slice(sections[1], sections[2] - sections[1]).ToArray(), pose.BoneCount);
            foreach (NativeBattleRawMesh mesh in geometry)
            {
                mesh.Validate();
            }
            return new(address, data, pose, geometry);
        }
        NativeBattleEffectHeapAllocation Tim(uint address)
        {
            int first = BinaryPrimitives.ReadInt32LittleEndian(memory.Read(address + 8, 4));
            int second = BinaryPrimitives.ReadInt32LittleEndian(memory.Read(checked(address + 8 + (uint)first), 4));
            return new("native-limit88-scene-texture", Read(address, checked(8 + first + second)));
        }
        ImmutableArray<byte> Read(uint address, int length) => NativeBattleEffectStaticResourcesReader.Read(memory, address, length);
    }

    private static ushort U16(ImmutableArray<byte> data, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan().Slice(offset));
    private static int I32(ImmutableArray<byte> data, int offset) => BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan().Slice(offset));
}

public sealed partial class GameAssets
{
    private NativeBattleRinoa88Resources? nativeRinoa88;

    public NativeBattleRinoa88Resources ReadNativeRinoa88Resources()
    {
        if (nativeRinoa88 is null)
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(resources);
            using IFlArchive battle = OpenArchive("battle");
            ImmutableArray<NativeBattleEffectFileAsset> files = NativeBattleRinoa88ResourcesReader.ReadFileNames(executable).Select(file => file with { Data = Read(battle, file.Name).ToImmutableArray() }).ToImmutableArray();
            nativeRinoa88 = NativeBattleRinoa88ResourcesReader.Read(executable, files);
        }
        return nativeRinoa88;
    }
}
