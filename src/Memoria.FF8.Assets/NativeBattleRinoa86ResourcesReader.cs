using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleRinoa86ResourcesReader
{
    public static ImmutableArray<NativeBattleEffectFileAsset> ReadFileNames(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        return Enumerable.Range(405, 2).Select(index =>
        {
            uint name = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(checked(0xB84CCCu + (uint)index * 4), 4));
            byte[] bytes = memory.Read(name, 128);
            int length = bytes.AsSpan().IndexOf((byte)0);
            if (length < 1)
            {
                throw new InvalidDataException("Native Rinoa86 requires its actual terminated archive names.");
            }
            return new NativeBattleEffectFileAsset(checked((short)index), Encoding.ASCII.GetString(bytes.AsSpan(0, length)), []);
        }).ToImmutableArray();
    }

    public static NativeBattleRinoa86Resources Read(byte[] executable, ImmutableArray<NativeBattleEffectFileAsset> files)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<byte> animation = Read(0x1565C90, 0x220);
        if (U16(animation, 0) != 2 || U16(animation, 6) != animation.Length)
        {
            throw new InvalidDataException("Native77F250 requires its complete original5099A0 registration.");
        }
        ImmutableArray<byte> script = Read(0x1565EB0, 64);
        ImmutableArray<NativeBattleEffectFileAsset> names = ReadFileNames(executable);
        if (files.IsDefault || files.Length != 2 || U16(script, 0) != 0 || U16(script, 48) != 255)
        {
            throw new InvalidDataException("Native Rinoa86 requires its actual wait/TIM/TIM/end sequence.");
        }
        for (int index = 0; index < 2; index++)
        {
            int row = (index + 1) * 16;
            if (U16(script, row) != 1 || U16(script, row + 2) != names[index].FileId || files[index].FileId != names[index].FileId || files[index].Name != names[index].Name || files[index].Data.IsDefault || files[index].Data.Length != I32(script, row + 8))
            {
                throw new InvalidDataException("Native Rinoa86 archive bytes disagree with their original identities or declared lengths.");
            }
        }
        ImmutableArray<byte> modelBytes = Read(0x1565EF4, 0x4934);
        int[] sections = [0x18, 0x688, 0x3344, 0x4928, 0x4934];
        for (int index = 0; index < sections.Length; index++)
        {
            if (NativeBattleDatDirectory.Section(modelBytes.AsSpan(), index) != sections[index])
            {
                throw new InvalidDataException("Native Rinoa86 requires its complete original owned model directory.");
            }
        }
        NativeBattlePoseSource pose = new(modelBytes.Slice(sections[0], sections[1] - sections[0]), modelBytes.Slice(sections[2], sections[3] - sections[2]));
        pose.Validate();
        ImmutableArray<NativeBattleRawMesh> geometry = NativeBattleRawGeometryReader.Read(modelBytes.Slice(sections[1], sections[2] - sections[1]).ToArray(), pose.BoneCount);
        foreach (NativeBattleRawMesh mesh in geometry)
        {
            mesh.Validate();
        }
        NativeBattleEffectOwnedModelResource model = new(0x1565EF4, modelBytes, pose, geometry);
        ImmutableArray<byte> actor = Read(0x156AA60, 0x1D60);
        for (int section = 0; section < 4; section++)
        {
            _ = NativeBattleDatDirectory.Section(actor.AsSpan(), section);
        }
        const uint tim = 0x170CDCC;
        int first = BinaryPrimitives.ReadInt32LittleEndian(memory.Read(tim + 8, 4));
        int second = BinaryPrimitives.ReadInt32LittleEndian(memory.Read(checked(tim + 8 + (uint)first), 4));
        NativeBattleEffectHeapAllocation sceneTim = new("native-limit86-scene-texture", Read(tim, checked(8 + first + second)));
        uint defaultColor = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xB8B9A8, 4));
        if (geometry.Any(mesh => mesh.Vertices.Length > 350))
        {
            throw new InvalidDataException("Native780130 projected vertices exceed their original2800-byte workspace.");
        }
        return new(new("FF8_EN:1565C90", animation), new(new("FF8_EN:1565EB0", script), files), model, defaultColor, new("FF8_EN:156AA60", actor), sceneTim)
        {
            Programs = [new(0x1706398, Read(0x1706398, 0x928)), new(0x1706CC0, Read(0x1706CC0, 0x2744)), new(0x1709404, Read(0x1709404, 0x39C8))],
            Meshes = ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(0x15618C0, Read(0x15618C0, 0x60)).Add(0x1561920, Read(0x1561920, 0x1FA8)).Add(0x15638C8, Read(0x15638C8, 0x23C8)),
            Sprite = Read(0x156177C, 0x144),
            GroundReferenceZ = unchecked((short)U16(Read(0xB8B7F4, 2), 0)),
            Sounds = ImmutableDictionary<uint, uint>.Empty.Add(0x1561774, unchecked((uint)I32(Read(0x1561774, 4), 0))).Add(0x1561778, unchecked((uint)I32(Read(0x1561778, 4), 0)))
        };

        ImmutableArray<byte> Read(uint address, int length) => NativeBattleEffectStaticResourcesReader.Read(memory, address, length);
    }

    private static ushort U16(ImmutableArray<byte> data, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan().Slice(offset));
    private static int I32(ImmutableArray<byte> data, int offset) => BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan().Slice(offset));
}

public sealed partial class GameAssets
{
    private NativeBattleRinoa86Resources? nativeRinoa86;
    public NativeBattleRinoa86Resources ReadNativeRinoa86Resources()
    {
        if (nativeRinoa86 is null)
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(resources);
            using IFlArchive battle = OpenArchive("battle");
            ImmutableArray<NativeBattleEffectFileAsset> files = NativeBattleRinoa86ResourcesReader.ReadFileNames(executable).Select(file => file with { Data = Read(battle, file.Name).ToImmutableArray() }).ToImmutableArray();
            nativeRinoa86 = NativeBattleRinoa86ResourcesReader.Read(executable, files);
        }
        return nativeRinoa86;
    }
}
