using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleRinoa87ResourcesReader
{
    public static ImmutableArray<NativeBattleEffectFileAsset> ReadFileNames(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        return Enumerable.Range(407, 3).Select(index =>
        {
            uint name = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(checked(0xB84CCCu + (uint)index * 4), 4));
            byte[] bytes = memory.Read(name, 128);
            int length = bytes.AsSpan().IndexOf((byte)0);
            if (length < 1)
            {
                throw new InvalidDataException("Native Rinoa87 requires its actual terminated archive names.");
            }
            return new NativeBattleEffectFileAsset(checked((short)index), Encoding.ASCII.GetString(bytes.AsSpan(0, length)), []);
        }).ToImmutableArray();
    }

    public static NativeBattleRinoa87Resources Read(byte[] executable, ImmutableArray<NativeBattleEffectFileAsset> files)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<byte> script = Read(0x155B190, 80);
        ImmutableArray<NativeBattleEffectFileAsset> names = ReadFileNames(executable);
        if (files.IsDefault || files.Length != 3 || U16(script, 0) != 0 || U16(script, 64) != 255)
        {
            throw new InvalidDataException("Native Rinoa87 requires its actual wait/data/TIM/TIM/end sequence.");
        }
        for (int index = 0; index < 3; index++)
        {
            int row = (index + 1) * 16;
            int opcode = index == 0 ? 3 : 1;
            if (U16(script, row) != opcode || U16(script, row + 2) != names[index].FileId || files[index].FileId != names[index].FileId || files[index].Name != names[index].Name || files[index].Data.IsDefault || files[index].Data.Length != I32(script, row + 8))
            {
                throw new InvalidDataException("Native Rinoa87 archive bytes disagree with original identities or declared lengths.");
            }
        }
        ImmutableArray<byte> modelBytes = Read(0x155B1E4, 0x4F10);
        int[] sections = [0x18, 0x688, 0x3344, 0x4F04, 0x4F10];
        for (int index = 0; index < sections.Length; index++)
        {
            if (NativeBattleDatDirectory.Section(modelBytes.AsSpan(), index) != sections[index])
            {
                throw new InvalidDataException("Native Rinoa87 requires its complete original owned model directory.");
            }
        }
        NativeBattlePoseSource pose = new(modelBytes.Slice(sections[0], sections[1] - sections[0]), modelBytes.Slice(sections[2], sections[3] - sections[2]));
        pose.Validate();
        ImmutableArray<NativeBattleRawMesh> geometry = NativeBattleRawGeometryReader.Read(modelBytes.Slice(sections[1], sections[2] - sections[1]).ToArray(), pose.BoneCount);
        foreach (NativeBattleRawMesh mesh in geometry)
        {
            mesh.Validate();
        }
        if (geometry.Any(mesh => mesh.Vertices.Length > 320))
        {
            throw new InvalidDataException("Native775330 projected vertices exceed their original2560-byte workspace.");
        }
        NativeBattleEffectOwnedModelResource model = new(0x155B1E4, modelBytes, pose, geometry);
        ImmutableArray<byte> actor = Read(0x1560550, 0x11F4);
        for (int index = 0; index < 4; index++)
        {
            if (NativeBattleDatDirectory.Section(actor.AsSpan(), index) != new[] { 0x14, 0x103C, 0x104C, 0x11F4 }[index])
            {
                throw new InvalidDataException("Native776010 requires its original three-section actor override.");
            }
        }
        const uint tim = 0x16FDF78;
        int first = BinaryPrimitives.ReadInt32LittleEndian(memory.Read(tim + 8, 4));
        int second = BinaryPrimitives.ReadInt32LittleEndian(memory.Read(checked(tim + 8 + (uint)first), 4));
        NativeBattleEffectHeapAllocation sceneTim = new("native-limit87-scene-texture", Read(tim, checked(8 + first + second)));
        NativeBattleRinoa87NodeResources nodes = new(Read(NativeBattleRinoa87NodeResources.Address, NativeBattleRinoa87NodeResources.Length));
        nodes.Validate();
        NativeBattleRinoa87Resources result = new(new(new("FF8_EN:155B190", script), files), model, BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0xB8B9A8, 4)), new("FF8_EN:1560550", actor), sceneTim, NativeBattleRinoa87CameraResourcesReader.Read(executable), nodes, new(0x16FD248, Read(0x16FD248, 0xD30)))
        {
            Sprite = NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, 0x1557D34),
            Meshes = new uint[] { 0x1557FBC, 0x155AA5C, 0x155ADF4, 0x155B12C }.ToImmutableDictionary(address => address, address => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address).Bytes),
            TargetCurve = Words(0x1561744, 16),
            FirstImpactCounts = Words(0x1560538, 6),
            SecondImpactCounts = Words(0x1560544, 6),
            Sounds = new uint[] { 0x1557D2C, 0x1557D30 }.ToImmutableDictionary(address => address, address => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4)))
        };
        if (result.TargetCurve[^1] != 4096)
        {
            throw new InvalidDataException("Native77E260 requires its original bounded4096 target-motion curve.");
        }
        return result;

        ImmutableArray<byte> Read(uint address, int length) => NativeBattleEffectStaticResourcesReader.Read(memory, address, length);
        ImmutableArray<short> Words(uint address, int count) => Enumerable.Range(0, count).Select(index => BinaryPrimitives.ReadInt16LittleEndian(memory.Read(checked(address + (uint)(index * 2)), 2))).ToImmutableArray();
    }

    private static ushort U16(ImmutableArray<byte> data, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan().Slice(offset));
    private static int I32(ImmutableArray<byte> data, int offset) => BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan().Slice(offset));
}

public sealed partial class GameAssets
{
    private NativeBattleRinoa87Resources? nativeRinoa87;
    public NativeBattleRinoa87Resources ReadNativeRinoa87Resources()
    {
        if (nativeRinoa87 is null)
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(resources);
            using IFlArchive battle = OpenArchive("battle");
            ImmutableArray<NativeBattleEffectFileAsset> files = NativeBattleRinoa87ResourcesReader.ReadFileNames(executable).Select(file => file with { Data = Read(battle, file.Name).ToImmutableArray() }).ToImmutableArray();
            nativeRinoa87 = NativeBattleRinoa87ResourcesReader.Read(executable, files);
        }
        return nativeRinoa87;
    }
}
