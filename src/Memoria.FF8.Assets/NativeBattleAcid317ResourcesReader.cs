using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleAcid317ResourcesReader
{
    public static NativeBattleAcid317Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(0x1005AD8, 11)) != "mag127.tim\0")
        {
            throw new InvalidDataException("Acid requires its original initializer texture name.");
        }
        NativeBattleAcid317Resources result = new(new("FF8_EN:FFE6B0", memory.Read(0xFFE6B0, 0x1B74).ToImmutableArray()), new(0x1000224, memory.Read(0x1000224, 0x1178).ToImmutableArray()), Sprite(0x100139C), Sprite(0x10014FC), Sprite(0x10016A8), new("FF8_EN:10017A0", memory.Read(0x10017A0, 0x328).ToImmutableArray()), memory.Read(0x1001ACC, 0x4000).ToImmutableArray(), memory.Read(0x1005AD0, 8).ToImmutableArray(), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x1001AC8, 4)));
        ValidatePayloads(result);
        return result;

        NativeBattleModuleResource Sprite(uint address) => new(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address));
    }

    public static void ValidatePayloads(NativeBattleAcid317Resources resources)
    {
        resources.Validate();
        ReadOnlySpan<byte> data = resources.TargetAct.Data.AsSpan();
        int shape = Read(data, 8);
        int animation = Read(data, 4);
        if (Read(data, 0) != 1 || shape != 12 || Read(data, shape) != 12 + 64 * 2 * 8 || Read(data, shape + 4) != 64 || Read(data, shape + 8) != 2)
        {
            throw new InvalidDataException("Acid ACT requires its original two-variant shape.");
        }
        int cursor = checked(shape + Read(data, shape));
        for (int group = 0; group < 8; group++)
        {
            int count = Read(data, cursor);
            cursor += 4;
            if (count != (group == 7 ? 48 : 0))
            {
                throw new InvalidDataException("Acid ACT requires its actual GT4 primitive group.");
            }
            for (int face = 0; face < count; face++)
            {
                for (int corner = 0; corner < 4; corner++)
                {
                    int index = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(cursor + 4 + corner * 2, 2));
                    if ((index & 1) != 0 || index / 2 >= 64)
                    {
                        throw new InvalidDataException("Acid ACT selects an absent original vertex.");
                    }
                }
                cursor = checked(cursor + 36);
            }
        }
        if (cursor != animation || Read(data, animation) != 6 || Read(data, animation + 4) != 20)
        {
            throw new InvalidDataException("Acid ACT directory or animation duration is incomplete.");
        }
        NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(new byte[0x248].ToImmutableArray()).Initialize7016B0(resources.TargetAct, 0x248);
        for (int frame = 0; frame < 20; frame++)
        {
            NativeBattleGfAnimationAdvance next = workspace.Advance701970(resources.TargetAct, 0, (current, node) =>
            {
                if (node.GeometryIndex != 0 || node.Header != 0x1000 || node.AnimationFlags != 0x33D8 || node.Word40 is < 0 or > 1 || node.Word42 is < 0 or > 1)
                {
                    throw new InvalidDataException("Acid ACT selects an unsupported original shape or variant.");
                }
                return current;
            });
            if (next.Remaining != 19 - frame)
            {
                throw new InvalidDataException("Acid ACT changes its original programme duration.");
            }
            workspace = next.Workspace;
        }
        int start = NativeBattleDatDirectory.Section(resources.CasterDat.Data.AsSpan(), 0);
        int end = NativeBattleDatDirectory.Section(resources.CasterDat.Data.AsSpan(), 1);
        if (NativeBattlePoseDecoder.ReadFrameCounts(resources.CasterDat.Data.AsSpan().Slice(start, end - start)).Length != 9)
        {
            throw new InvalidDataException("Acid caster binding has an incomplete animation directory.");
        }
    }

    private static int Read(ReadOnlySpan<byte> data, int offset)
    {
        if (offset < 0 || offset > data.Length - 4)
        {
            throw new InvalidDataException("Acid ACT directory leaves its original source.");
        }
        return BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, 4));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleAcid317Resources? nativeAcid317Resources;
    public NativeBattleAcid317Resources ReadNativeAcid317Resources() => nativeAcid317Resources ??= NativeBattleAcid317ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
