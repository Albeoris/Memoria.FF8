using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleMicroMissiles299ResourcesReader
{
    public static NativeBattleMicroMissiles299Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(0x104E320, 15)) != "mag283-298.tim\0")
        {
            throw new InvalidDataException("Micro Missiles requires its original initializer resource name.");
        }
        NativeBattleMicroMissiles299Resources result = new(Source(0x10490A8, 0x2594), Source(0x104B63C, 0x12D4), Source(0x104C910, 0x1658), Sprite(0, 0x1048C94, 0x144), Sprite(1, 0x1048DD8, 0x144), Sprite(2, 0x1048F1C, 0x18C), new("FF8_EN:104DF68", memory.Read(0x104DF68, 916).ToImmutableArray()), new("FF8_EN:1047120", memory.Read(0x1047120, 0x1B74).ToImmutableArray()), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x104E2FC, 4)));
        ValidatePayloads(result);
        return result;
        NativeBattleGfAnimationSource Source(uint address, int length) => new(address, memory.Read(address, length).ToImmutableArray());
        NativeBattleModuleResource Sprite(byte id, uint address, int length) => new(id, address, 0, 0, memory.Read(address, length).ToImmutableArray());
    }

    public static void ValidatePayloads(NativeBattleMicroMissiles299Resources resources)
    {
        resources.Validate();
        ValidateAct(resources.Missile, 0x7D8, 45);
        ValidateAct(resources.Impact, 0xA0, 12);
        ValidateAct(resources.WideImpact, 0x114, 16);
    }

    private static void ValidateAct(NativeBattleGfAnimationSource source, int workspaceSize, int duration)
    {
        int shapes = Read(0);
        int animation = Read(4);
        int expected = checked(8 + shapes * 4);
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int shape = 0; shape < shapes; shape++)
        {
            int start = Read(8 + shape * 4);
            int end = shape + 1 == shapes ? animation : Read(12 + shape * 4);
            int vertices = Read(start + 4);
            int variants = Read(start + 8);
            if (start != expected || end <= start || end > animation || vertices is < 1 or > 64 || variants is < 1 or > 3 || Read(start) != 12 + vertices * variants * 8)
            {
                throw new InvalidDataException("Micro Missiles ACT shape leaves its original vertex and morph owners.");
            }
            int cursor = checked(start + Read(start));
            for (int group = 0; group < 8; group++)
            {
                int count = Read(cursor);
                cursor = checked(cursor + 4);
                if (count is < 0 or > 4096 || cursor > end - checked(count * strides[group]))
                {
                    throw new InvalidDataException("Micro Missiles ACT primitive group leaves its original shape.");
                }
                for (int face = 0; face < count; face++)
                {
                    int corners = group % 2 == 0 ? 3 : 4;
                    for (int corner = 0; corner < corners; corner++)
                    {
                        int at = checked(cursor + face * strides[group] + 4 + corner * 2);
                        int vertex = BinaryPrimitives.ReadUInt16LittleEndian(source.Data.AsSpan().Slice(at, 2)) * 4;
                        if ((vertex & 7) != 0 || vertex > vertices * 8 - 8)
                        {
                            throw new InvalidDataException("Micro Missiles ACT face selects an absent packed vertex.");
                        }
                    }
                }
                cursor = checked(cursor + count * strides[group]);
            }
            if (cursor != end)
            {
                throw new InvalidDataException("Micro Missiles ACT groups do not close their original shape.");
            }
            expected = end;
        }
        NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(new byte[workspaceSize].ToImmutableArray()).Initialize7016B0(source, workspaceSize);
        for (int frame = 0; frame < duration; frame++)
        {
            NativeBattleGfAnimationAdvance step = workspace.Advance701970(source, 0, (current, node) =>
            {
                if ((uint)node.GeometryIndex >= shapes)
                {
                    throw new InvalidDataException("Micro Missiles ACT programme selects an absent shape.");
                }
                return current;
            });
            if (step.Remaining != duration - frame - 1)
            {
                throw new InvalidDataException("Micro Missiles ACT programme changes its original duration.");
            }
            workspace = step.Workspace;
        }
        int Read(int offset)
        {
            if (offset < 0 || offset > source.Data.Length - 4)
            {
                throw new InvalidDataException("Micro Missiles ACT read leaves its original immutable source.");
            }
            return BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(offset, 4));
        }
    }
}

public sealed partial class GameAssets
{
    private NativeBattleMicroMissiles299Resources? nativeMicroMissiles299Resources;
    public NativeBattleMicroMissiles299Resources ReadNativeMicroMissiles299Resources() => nativeMicroMissiles299Resources ??= NativeBattleMicroMissiles299ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
