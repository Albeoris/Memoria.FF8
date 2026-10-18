using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleMoogle96ResourcesReader
{
    public static ImmutableArray<NativeBattleEffectFileAsset> ReadFileNames(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<NativeBattleEffectFileAsset>.Builder files = ImmutableArray.CreateBuilder<NativeBattleEffectFileAsset>(4);
        for (short id = 438; id <= 441; id++)
        {
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(checked(0xB84CCCu + 4u * (uint)id), 4));
            byte[] bytes = memory.Read(pointer, 128);
            int end = bytes.AsSpan().IndexOf((byte)0);
            if (end < 1)
            {
                throw new InvalidDataException("Native Moogle file directory requires its original terminated name.");
            }
            files.Add(new(id, Encoding.ASCII.GetString(bytes.AsSpan(0, end)), []));
        }
        return files.MoveToImmutable();
    }

    public static NativeBattleMoogle96Resources Read(byte[] executable, ImmutableArray<NativeBattleEffectFileAsset> files)
    {
        NativeExecutableMemory memory = new(executable);
        ImmutableArray<byte> bytes = Read(0x152BC04, 0x7060);
        int[] sections = [0x18, 0x5C8, 0x4F20, 0x7048, 0x7060];
        if (U32(bytes, 0) != 4)
        {
            throw new InvalidDataException("Native Moogle model requires its original four-section source.");
        }
        for (int index = 0; index < sections.Length; index++)
        {
            if (NativeBattleDatDirectory.Section(bytes.AsSpan(), index) != sections[index])
            {
                throw new InvalidDataException("Native Moogle model section disagrees with its complete original extent.");
            }
        }
        NativeBattlePoseSource pose = new(bytes.Slice(sections[0], sections[1] - sections[0]), bytes.Slice(sections[2], sections[3] - sections[2]));
        pose.Validate();
        ImmutableArray<NativeBattleRawMesh> geometry = NativeBattleRawGeometryReader.Read(bytes.Slice(sections[1], sections[2] - sections[1]).ToArray(), pose.BoneCount);
        foreach (NativeBattleRawMesh mesh in geometry)
        {
            mesh.Validate();
        }
        NativeBattleEffectOwnedModelResource model = new(0x152BC04, bytes, pose, geometry);
        NativeBattleEffectHeapAllocation script = Allocation(0x152BBA0, 0x60);
        if (U32(script.Data, 0) != 0 || BinaryPrimitives.ReadUInt16LittleEndian(script.Data.AsSpan().Slice(0x50)) != 0xFF)
        {
            throw new InvalidDataException("Native Moogle IO script requires its original WAIT and terminal records.");
        }
        ImmutableArray<NativeBattleEffectFileAsset> names = ReadFileNames(executable);
        if (files.IsDefault || files.Length != 4)
        {
            throw new InvalidDataException("Native Moogle requires all four external file owners.");
        }
        for (int index = 0; index < 4; index++)
        {
            int offset = (index + 1) * 16;
            int length = index < 2 ? 0x8220 : 0x4220;
            if (BinaryPrimitives.ReadUInt16LittleEndian(script.Data.AsSpan().Slice(offset)) != 1 || BinaryPrimitives.ReadUInt16LittleEndian(script.Data.AsSpan().Slice(offset + 2)) != 438 + index || U32(script.Data, offset + 4) != 0 || U32(script.Data, offset + 8) != length || U32(script.Data, offset + 12) != 0)
            {
                throw new InvalidDataException("Native Moogle IO source requires its original file records and cold destination fields.");
            }
            if (files[index].FileId != names[index].FileId || files[index].Name != names[index].Name || files[index].Data.IsDefault || files[index].Data.Length != length)
            {
                throw new InvalidDataException("Native Moogle external file identity or extent disagrees with its original script.");
            }
        }
        NativeBattleEffectHeapAllocation first = Allocation(0x169227C, 0x4B6C);
        NativeBattleEffectHeapAllocation second = Allocation(0x1696DE8, 0x33D4);
        CheckNodes(first.Data, [0, 1, 4, 5, 6, 7, 8, 9, 10, 11], 0x3DFC, 2, 4);
        CheckNodes(second.Data, [0, 1, 2, 3, 4, 5, 6, 7], 0x2914, 4, 2);
        NativeBattleGfAnimationSource program = new(0x169A1BC, Read(0x169A1BC, 0x9DF4));
        if (U32(program.Data, 0) != 21 || U32(program.Data, 4) != 0x7C34 || U32(program.Data, 0x7C34) != 29 || U32(program.Data, 0x7C38) != 135)
        {
            throw new InvalidDataException("Native Moogle program requires its original geometry and animation directories.");
        }
        for (int index = 0; index < 21; index++)
        {
            int offset = checked((int)U32(program.Data, 8 + index * 4));
            uint count = U32(program.Data, offset + 4);
            uint shapes = U32(program.Data, offset + 8);
            if (count is < 1 or > 160 || shapes < 1 || U32(program.Data, offset) != 12 + count * shapes * 8)
            {
                throw new InvalidDataException("Native Moogle geometry requires its original twelve-byte complete shape header.");
            }
        }
        _ = new NativeBattleGfAnimationWorkspace(new byte[0x488].ToImmutableArray()).Initialize7016B0(program, 0x488);
        ImmutableArray<byte> vibrationBytes = Read(0x1692140, 0x13C);
        if (U32(vibrationBytes, 0) != 0 || U32(vibrationBytes, 4) != 0x100)
        {
            throw new InvalidDataException("Native Moogle vibration requires its reached null and program1 directory entries.");
        }
        NativeVibrationBank vibration = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, [0, 0x100], vibrationBytes);
        vibration.Validate();
        return new(model, Sound(0xB8B9A8), NativeBattleMoogleCameraResourcesReader.Read(executable), new(0x1692140, vibration), script, first, second, program, [Allocation(0x1532FC8, 0x10), Allocation(0x1532FD8, 0x10), Allocation(0x1532FE8, 0x18)], files, Sound(0x152BB94), Sound(0x152BB98));

        ImmutableArray<byte> Read(uint address, int length) => NativeBattleEffectStaticResourcesReader.Read(memory, address, length);
        NativeBattleEffectHeapAllocation Allocation(uint address, int length) => new($"FF8_EN:{address:X}", Read(address, length));
        uint Sound(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(address, 4));
    }

    private static void CheckNodes(ImmutableArray<byte> bytes, int[] indices, int firstDefinition, int sprites, int meshes)
    {
        if (U32(bytes, 0) != 0)
        {
            throw new InvalidDataException("Native Moogle neutral resource requires its original cold fixup flag.");
        }
        for (int index = 0; index < 16; index++)
        {
            int ordinal = Array.IndexOf(indices, index);
            uint expected = ordinal < 0 ? 0 : (uint)(firstDefinition + ordinal * 0x158);
            if (U32(bytes, 4 + index * 4) != expected)
            {
                throw new InvalidDataException("Native Moogle neutral definitions disagree with their complete directory.");
            }
        }
        for (int table = 1; table < 4; table++)
        {
            int populated = 0;
            for (int index = 0; index < 16; index++)
            {
                uint offset = U32(bytes, 4 + table * 64 + index * 4);
                if (offset != 0)
                {
                    if (offset < 0x104 || offset >= bytes.Length)
                    {
                        throw new InvalidDataException("Native Moogle neutral source pointer leaves its complete bank.");
                    }
                    populated++;
                }
            }
            if (populated != (table == 1 ? sprites : table == 2 ? meshes : 0))
            {
                throw new InvalidDataException("Native Moogle neutral resource has a different source-table envelope.");
            }
        }
    }

    private static uint U32(ImmutableArray<byte> bytes, int offset)
    {
        if (offset < 0 || offset > bytes.Length - 4)
        {
            throw new InvalidDataException("Native Moogle resource read leaves its complete original owner.");
        }
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleMoogle96Resources? nativeMoogle96Resources;
    public NativeBattleMoogle96Resources ReadNativeMoogle96Resources()
    {
        if (nativeMoogle96Resources is null)
        {
            byte[] executable = GameDataResources.ReadNativeExecutable(this.resources);
            using IFlArchive battle = OpenArchive("battle");
            ImmutableArray<NativeBattleEffectFileAsset> files = NativeBattleMoogle96ResourcesReader.ReadFileNames(executable).Select(file => file with { Data = Read(battle, file.Name).ToImmutableArray() }).ToImmutableArray();
            nativeMoogle96Resources = NativeBattleMoogle96ResourcesReader.Read(executable, files);
        }
        return nativeMoogle96Resources;
    }
}
