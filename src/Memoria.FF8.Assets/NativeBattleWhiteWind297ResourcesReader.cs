using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleWhiteWind297ResourcesReader
{
    public static NativeBattleWhiteWind297Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(0x105BA98, 11)) != "mag296.tim\0")
        {
            throw new InvalidDataException("Blue Magic White Wind requires its original texture resource name.");
        }
        NativeBattleWhiteWind297Resources result = new(Source(0x105AEFC, 0xA30), Source(0x105A494, 0xA68), Source(0x10583FC, 0x2098), new("FF8_EN:105B92C", memory.Read(0x105B92C, 0x168).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x105BA94, 4)), new("FF8_EN:1056888", memory.Read(0x1056888, 0x1B74).ToImmutableArray()));
        ValidatePayloads(result);
        return result;

        NativeBattleGfAnimationSource Source(uint address, int length) => new(address, memory.Read(address, length).ToImmutableArray());
    }

    public static void ValidatePayloads(NativeBattleWhiteWind297Resources resources)
    {
        resources.Validate();
        ValidateAct(resources.Caster, 0x188, 22);
        ValidateAct(resources.Sweep, 0x194, 26);
        ValidateAct(resources.Target, 0x278, 26);
    }

    private static void ValidateAct(NativeBattleGfAnimationSource source, int workspaceBytes, int duration)
    {
        int shapes = Read(0);
        int animation = Read(4);
        int expected = 8 + shapes * 4;
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int index = 0; index < shapes; index++)
        {
            int start = Read(8 + index * 4);
            int end = index + 1 == shapes ? animation : Read(12 + index * 4);
            if (start != expected || end <= start || end > animation)
            {
                throw new InvalidDataException("White Wind ACT shape leaves its complete original directory.");
            }
            int vertices = Read(start + 4);
            int variants = Read(start + 8);
            int faces = Read(start);
            if (vertices is < 1 or > 120 || variants is < 1 or > 2 || faces != 12 + vertices * variants * 8)
            {
                throw new InvalidDataException("White Wind ACT shape has an invalid twelve-byte vertex header.");
            }
            int cursor = checked(start + faces);
            for (int group = 0; group < 8; group++)
            {
                int count = Read(cursor);
                cursor = checked(cursor + 4);
                if (count is < 0 or > 4096 || cursor > end - checked(count * strides[group]))
                {
                    throw new InvalidDataException("White Wind ACT primitive group leaves its original shape.");
                }
                for (int face = 0; face < count; face++)
                {
                    int corners = group % 2 == 0 ? 3 : 4;
                    for (int corner = 0; corner < corners; corner++)
                    {
                        int offset = checked(cursor + face * strides[group] + 4 + corner * 2);
                        int vertex = BinaryPrimitives.ReadUInt16LittleEndian(source.Data.AsSpan().Slice(offset, 2)) * 4;
                        if ((vertex & 7) != 0 || vertex > vertices * 8 - 8)
                        {
                            throw new InvalidDataException("White Wind ACT face selects an absent packed vertex.");
                        }
                    }
                }
                cursor = checked(cursor + count * strides[group]);
            }
            if (cursor != end)
            {
                throw new InvalidDataException("White Wind ACT groups do not close their original shape.");
            }
            expected = end;
        }
        NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(new byte[workspaceBytes].ToImmutableArray()).Initialize7016B0(source, workspaceBytes);
        for (int frame = 0; frame < duration; frame++)
        {
            NativeBattleGfAnimationAdvance next = workspace.Advance701970(source, 0, (current, node) =>
            {
                if ((uint)node.GeometryIndex >= shapes)
                {
                    throw new InvalidDataException("White Wind ACT node selects an absent original shape.");
                }
                return current;
            });
            if (next.Remaining != duration - frame - 1)
            {
                throw new InvalidDataException("White Wind ACT programme changes its original duration.");
            }
            workspace = next.Workspace;
        }

        int Read(int offset)
        {
            if (offset < 0 || offset > source.Data.Length - 4)
            {
                throw new InvalidDataException("White Wind ACT read leaves its original source.");
            }
            return BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(offset, 4));
        }
    }
}

public sealed partial class GameAssets
{
    private NativeBattleWhiteWind297Resources? nativeWhiteWind297Resources;

    public NativeBattleWhiteWind297Resources ReadNativeWhiteWind297Resources() => nativeWhiteWind297Resources ??= NativeBattleWhiteWind297ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
