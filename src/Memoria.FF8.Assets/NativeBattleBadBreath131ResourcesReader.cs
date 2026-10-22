using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleBadBreath131ResourcesReader
{
    public static NativeBattleBadBreath131Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(0x1047110, 15)) != "mag130-299.tim\0")
        {
            throw new InvalidDataException("Bad Breath requires its original initializer texture resource name.");
        }
        byte[] table = memory.Read(0x121C990, 0x4000);
        ImmutableArray<short> trig = Enumerable.Range(0, 8192).Select(index => BinaryPrimitives.ReadInt16LittleEndian(table.AsSpan(index * 2, 2))).ToImmutableArray();
        byte[] gradient = memory.Read(0x121C97C, 16);
        NativeBattleBadBreath131Resources result = new(new(0x121BB58, memory.Read(0x121BB58, 0xC7C).ToImmutableArray()), new(0, 0x1219078, 0, 0, memory.Read(0x1219078, 480).ToImmutableArray()), new("FF8_EN:121C7D4", memory.Read(0x121C7D4, 420).ToImmutableArray()), memory.Read(0x1219258, 5248).ToImmutableArray(), memory.Read(0x121A6D8, 5248).ToImmutableArray(), trig, Enumerable.Range(0, 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(gradient.AsSpan(index * 4, 4))).ToImmutableArray(), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x121C98C, 4)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x121C978, 4)));
        ValidatePayloads(result);
        return result;
    }

    public static void ValidatePayloads(NativeBattleBadBreath131Resources resources)
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
        for (int frame = 0; frame < 34; frame++)
        {
            NativeBattleGfAnimationAdvance next = workspace.Advance701970(resources.Target, 0, (current, node) =>
            {
                if (node.GeometryIndex != 0 || node.Word40 != 0 || node.Word42 != 0)
                {
                    throw new InvalidDataException("Bad Breath target ACT selects an absent original shape or vertex bank.");
                }
                return current;
            });
            if (next.Remaining != 33 - frame)
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
    private NativeBattleBadBreath131Resources? nativeBadBreath131Resources;
    public NativeBattleBadBreath131Resources ReadNativeBadBreath131Resources() => nativeBadBreath131Resources ??= NativeBattleBadBreath131ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
