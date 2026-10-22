using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleLaserEye71ResourcesReader
{
    public static NativeBattleLaserEye71Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(0x1591C30, 11)) != "mag070.tim\0")
        {
            throw new InvalidDataException("Laser Eye requires its original initializer resource name.");
        }
        ImmutableArray<NativeBattleStaticMeshResource> meshes = new uint[] { 0x158F688, 0x158FDA0, 0x158FE48 }.Select(address => NativeBattleEffectStaticResourcesReader.ReadMesh(memory, address)).ToImmutableArray();
        ImmutableArray<NativeBattleLaserEye71EmissionRow> emissions = Enumerable.Range(0, 19).Select(index =>
        {
            byte[] row = memory.Read(checked(0x1591B98u + (uint)index * 8), 8);
            return new NativeBattleLaserEye71EmissionRow(Word(row, 0), Word(row, 2), Word(row, 4), Word(row, 6));
        }).ToImmutableArray();
        NativeBattleLaserEye71Resources result = new(new("FF8_EN:158F534", memory.Read(0x158F534, 340).ToImmutableArray()), new(memory.Read(NativeBattleLaserEye71Profile.SourceAddress, NativeBattleLaserEye71Profile.SourceLength).ToImmutableArray()) { ResourceAddress = NativeBattleLaserEye71Profile.SourceAddress }, new("FF8_EN:1590024", memory.Read(0x1590024, 0x1B74).ToImmutableArray()), meshes, new(0, 0x158FF60, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, 0x158FF60)), emissions, BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x158F530, 4)));
        ValidatePayloads(result);
        return result;
    }

    public static void ValidatePayloads(NativeBattleLaserEye71Resources resources)
    {
        resources.Validate();
        byte[] bytes = resources.Executable.Data.ToArray();
        int[] counts = new int[4];
        for (int table = 0; table < 4; table++)
        {
            for (int slot = 0; slot < 16; slot++)
            {
                int offset = Dword(bytes, 4 + table * 64 + slot * 4);
                if (offset == 0)
                {
                    continue;
                }
                counts[table]++;
                if (table == 0)
                {
                    Require(bytes, offset, 0x158);
                    if (bytes[offset + 0x16] is not (0 or 4 or 5) || bytes[offset + 0x1C] is not (0 or 2))
                    {
                        throw new InvalidDataException("Laser Eye source selects an unsupported node layout.");
                    }
                    for (int field = 0xC8; field < 0x148; field += 4)
                    {
                        Require(bytes, Dword(bytes, offset + field), 80);
                    }
                }
                else if (table == 1)
                {
                    Require(bytes, offset, 12);
                    int frames = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 8, 2));
                    Require(bytes, offset, checked(12 + 2 * frames));
                    for (int phase = 0; phase < frames; phase++)
                    {
                        int frame = checked(offset + BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 10 + 2 * phase, 2)));
                        int quads = Dword(bytes, frame) & int.MaxValue;
                        Require(bytes, frame, checked(4 + 20 * quads));
                    }
                }
                else if (table == 2)
                {
                    ValidateMesh(bytes, offset, bytes.Length, false);
                }
                else
                {
                    int length = Dword(bytes, offset);
                    int vertices = Dword(bytes, offset + 4);
                    if (vertices <= 0 || length != checked(8 + 8 * vertices))
                    {
                        throw new InvalidDataException("Laser Eye morph source has an incomplete vertex boundary.");
                    }
                    Require(bytes, offset, length);
                }
            }
        }
        if (!counts.SequenceEqual(new[] { 12, 6, 7, 5 }))
        {
            throw new InvalidDataException("Laser Eye executable directories are incomplete.");
        }
        foreach (NativeBattleStaticMeshResource mesh in resources.Meshes)
        {
            ValidateMesh(mesh.Bytes.ToArray(), 0, mesh.Bytes.Length, true);
        }
        int begin = NativeBattleDatDirectory.Section(resources.CasterDat.Data.AsSpan(), 0);
        int end = NativeBattleDatDirectory.Section(resources.CasterDat.Data.AsSpan(), 1);
        if (NativeBattlePoseDecoder.ReadFrameCounts(resources.CasterDat.Data.AsSpan().Slice(begin, end - begin)).Length != 9)
        {
            throw new InvalidDataException("Laser Eye caster binding has an incomplete animation directory.");
        }
    }

    private static void ValidateMesh(byte[] bytes, int offset, int end, bool exactEnd)
    {
        int vertices = Dword(bytes, offset + 4);
        int relative = Dword(bytes, offset);
        if (vertices <= 0 || vertices > 4096 || relative != checked(8 + vertices * 8))
        {
            throw new InvalidDataException("Laser Eye static shape has an invalid packed vertex directory.");
        }
        int cursor = checked(offset + relative);
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < strides.Length; group++)
        {
            int count = Dword(bytes, cursor);
            cursor += 4;
            if (count < 0 || count > 4096 || count > 0 && group is not (0 or 2 or 3))
            {
                throw new InvalidDataException("Laser Eye shape has an invalid primitive directory.");
            }
            Require(bytes, cursor, checked(count * strides[group]));
            for (int face = 0; face < count; face++)
            {
                for (int corner = 0; corner < ((group & 1) == 0 ? 3 : 4); corner++)
                {
                    int vertex = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(cursor + face * strides[group] + 4 + corner * 2, 2));
                    if ((vertex & 1) != 0 || vertex / 2 >= vertices)
                    {
                        throw new InvalidDataException("Laser Eye polygon selects an absent packed vertex.");
                    }
                }
            }
            cursor = checked(cursor + count * strides[group]);
        }
        if (cursor > end || exactEnd && cursor != end)
        {
            throw new InvalidDataException("Laser Eye shape has an incomplete primitive boundary.");
        }
    }

    private static int Dword(byte[] bytes, int offset)
    {
        Require(bytes, offset, 4);
        return BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));
    }

    private static void Require(byte[] bytes, int offset, int count)
    {
        if (offset < 0 || count < 0 || offset > bytes.Length - count)
        {
            throw new InvalidDataException("Laser Eye typed source leaves its complete immutable resource.");
        }
    }

    private static short Word(byte[] bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset, 2));
}

public sealed partial class GameAssets
{
    private NativeBattleLaserEye71Resources? nativeLaserEye71Resources;
    public NativeBattleLaserEye71Resources ReadNativeLaserEye71Resources() => nativeLaserEye71Resources ??= NativeBattleLaserEye71ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
