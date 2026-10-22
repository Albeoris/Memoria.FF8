using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleBadBreath300ResourcesReader
{
    public static NativeBattleBadBreath300Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(0x1047110, 15)) != "mag130-299.tim\0")
        {
            throw new InvalidDataException("Bad Breath requires its original initializer texture resource name.");
        }
        byte[] table = memory.Read(0x1043110, 0x4000);
        ImmutableArray<short> trig = Enumerable.Range(0, 8192).Select(index => BinaryPrimitives.ReadInt16LittleEndian(table.AsSpan(index * 2, 2))).ToImmutableArray();
        byte[] gradient = memory.Read(0x10430F8, 16);
        NativeBattleBadBreath300Resources result = new(new(0x1042304, memory.Read(0x1042304, 0xC7C).ToImmutableArray()), new(0, 0x103F824, 0, 0, memory.Read(0x103F824, 480).ToImmutableArray()), new("FF8_EN:1042F80", memory.Read(0x1042F80, 372).ToImmutableArray()), new("FF8_EN:103DCB0", memory.Read(0x103DCB0, 0x1B74).ToImmutableArray()), memory.Read(0x103FA04, 5248).ToImmutableArray(), memory.Read(0x1040E84, 5248).ToImmutableArray(), trig, Enumerable.Range(0, 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(gradient.AsSpan(index * 4, 4))).ToImmutableArray(), memory.Read(0x1043108, 8).ToImmutableArray(), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x10430F4, 4)));
        ValidatePayloads(result);
        return result;
    }

    public static void ValidatePayloads(NativeBattleBadBreath300Resources resources)
    {
        resources.Validate();
        ValidateMesh(resources.MeshAInitial, 8, 165, [0, 0, 0, 36, 0, 0, 0, 84]);
        ValidateMesh(resources.MeshBInitial, 8, 165, [0, 0, 0, 36, 0, 0, 0, 84]);
        if (Int(resources.Target.Data, 8) != 12)
        {
            throw new InvalidDataException("Bad Breath target ACT shape directory is incomplete.");
        }
        int geometry = 12;
        if (Int(resources.Target.Data, geometry) != 524 || Int(resources.Target.Data, geometry + 4) != 64 || Int(resources.Target.Data, geometry + 8) != 1)
        {
            throw new InvalidDataException("Bad Breath target ACT requires its original single packed vertex bank.");
        }
        ValidateMesh(resources.Target.Data.AsSpan().Slice(geometry, 0x8F8 - geometry).ToArray().ToImmutableArray(), 12, 64, [0, 0, 0, 0, 0, 0, 0, 48]);
        NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(new byte[0x128].ToImmutableArray()).Initialize7016B0(resources.Target, 0x128);
        for (int frame = 0; frame < 26; frame++)
        {
            NativeBattleGfAnimationAdvance next = workspace.Advance701970(resources.Target, 0, (current, node) =>
            {
                if (node.GeometryIndex != 0 || node.Word40 != 0 || node.Word42 != 0)
                {
                    throw new InvalidDataException("Bad Breath target ACT selects an absent original shape or vertex bank.");
                }
                return current;
            });
            if (next.Remaining != 25 - frame)
            {
                throw new InvalidDataException("Bad Breath target ACT changes its original duration.");
            }
            workspace = next.Workspace;
        }
    }

    private static void ValidateMesh(ImmutableArray<byte> mesh, int header, int vertices, int[] expectedCounts)
    {
        int cursor = Int(mesh, 0);
        if (Int(mesh, 4) != vertices || cursor != header + vertices * 8)
        {
            throw new InvalidDataException("Bad Breath packed vertex header leaves its original bank.");
        }
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int count = Int(mesh, cursor);
            cursor = checked(cursor + 4);
            if (count != expectedCounts[group] || count < 0 || cursor > mesh.Length - checked(count * strides[group]))
            {
                throw new InvalidDataException("Bad Breath primitive groups leave their original mesh.");
            }
            for (int face = 0; face < count; face++)
            {
                int corners = group % 2 == 0 ? 3 : 4;
                for (int corner = 0; corner < corners; corner++)
                {
                    int at = checked(cursor + face * strides[group] + 4 + corner * 2);
                    int vertex = BinaryPrimitives.ReadUInt16LittleEndian(mesh.AsSpan().Slice(at, 2)) * 4;
                    if ((vertex & 7) != 0 || vertex > vertices * 8 - 8)
                    {
                        throw new InvalidDataException("Bad Breath face selects an absent packed vertex.");
                    }
                }
            }
            cursor = checked(cursor + count * strides[group]);
        }
        if (cursor != mesh.Length)
        {
            throw new InvalidDataException("Bad Breath primitive groups do not close their complete original payload.");
        }
    }
    private static int Int(ImmutableArray<byte> bytes, int offset)
    {
        if (offset < 0 || offset > bytes.Length - 4)
        {
            throw new InvalidDataException("Bad Breath read leaves its original resource.");
        }
        return BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan().Slice(offset, 4));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleBadBreath300Resources? nativeBadBreath300Resources;
    public NativeBattleBadBreath300Resources ReadNativeBadBreath300Resources() => nativeBadBreath300Resources ??= NativeBattleBadBreath300ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
