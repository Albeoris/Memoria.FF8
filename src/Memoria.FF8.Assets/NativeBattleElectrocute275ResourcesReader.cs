using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleElectrocute275ResourcesReader
{
    public static NativeBattleElectrocute275Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(0x10E298C, 11)) != "mag125.tim\0")
        {
            throw new InvalidDataException("Electrocute requires its original initializer texture name.");
        }
        ImmutableArray<short> wave = Enumerable.Range(0, 4096).Select(index => BinaryPrimitives.ReadInt16LittleEndian(memory.Read(checked(0x10E098Cu + (uint)index * 2), 2))).ToImmutableArray();
        NativeBattleElectrocute275Resources result = new(new("FF8_EN:10DA574", memory.Read(0x10DA574, 0x1B74).ToImmutableArray()), new(0x10D9B64, memory.Read(0x10D9B64, 0xA10).ToImmutableArray()), Sprite(0x10D9934), Sprite(0x10D9A88), new("FF8_EN:10DC0E8", memory.Read(0x10DC0E8, 0x1E8).ToImmutableArray()), memory.Read(0x10DC98C, 0x4000).ToImmutableArray(), wave, memory.Read(0x10DC2DC, 6).ToImmutableArray(), memory.Read(0x10DC2E4, 0x6A8).ToImmutableArray(), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x10DC2D8, 4)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x10DC2D0, 4)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x10DC2D4, 4)));
        ValidatePayloads(result);
        return result;

        NativeBattleModuleResource Sprite(uint address) => new(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address));
    }

    public static void ValidatePayloads(NativeBattleElectrocute275Resources resources)
    {
        resources.Validate();
        ReadOnlySpan<byte> data = resources.Act.Data.AsSpan();
        int shape = Read(data, 8);
        int animation = Read(data, 4);
        if (Read(data, 0) != 1 || shape != 12 || Read(data, shape) != 12 + 51 * 3 * 8 || Read(data, shape + 4) != 51 || Read(data, shape + 8) != 3)
        {
            throw new InvalidDataException("Electrocute ACT requires its original three-variant shape.");
        }
        int cursor = checked(shape + Read(data, shape));
        for (int group = 0; group < 8; group++)
        {
            int count = Read(data, cursor);
            cursor += 4;
            if (count != (group == 3 ? 32 : 0))
            {
                throw new InvalidDataException("Electrocute ACT requires its actual FT4 primitive group.");
            }
            for (int face = 0; face < count; face++)
            {
                for (int corner = 0; corner < 4; corner++)
                {
                    int index = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(cursor + 4 + corner * 2, 2));
                    if ((index & 1) != 0 || index / 2 >= 51)
                    {
                        throw new InvalidDataException("Electrocute ACT selects an absent original vertex.");
                    }
                }
                cursor = checked(cursor + 24);
            }
        }
        if (cursor != animation || Read(data, animation) != 2 || Read(data, animation + 4) != 32)
        {
            throw new InvalidDataException("Electrocute ACT directory or animation duration is incomplete.");
        }
        NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(new byte[0xC8].ToImmutableArray()).Initialize7016B0(resources.Act, 0xC8);
        for (int frame = 0; frame < 32; frame++)
        {
            NativeBattleGfAnimationAdvance next = workspace.Advance701970(resources.Act, 0, (current, node) =>
            {
                if (node.GeometryIndex != 0 || node.Header != 0x40200 || node.AnimationFlags != 0x33D8 || node.Word40 is < 0 or > 2 || node.Word42 is < 0 or > 2)
                {
                    throw new InvalidDataException("Electrocute ACT selects an unsupported original shape or variant.");
                }
                return current;
            });
            if (next.Remaining != 31 - frame)
            {
                throw new InvalidDataException("Electrocute ACT changes its original programme duration.");
            }
            workspace = next.Workspace;
        }
        ValidatePaths(resources);
        int start = NativeBattleDatDirectory.Section(resources.CasterDat.Data.AsSpan(), 0);
        int end = NativeBattleDatDirectory.Section(resources.CasterDat.Data.AsSpan(), 1);
        if (NativeBattlePoseDecoder.ReadFrameCounts(resources.CasterDat.Data.AsSpan().Slice(start, end - start)).Length != 9)
        {
            throw new InvalidDataException("Electrocute caster binding has an incomplete animation directory.");
        }
    }

    private static void ValidatePaths(NativeBattleElectrocute275Resources resources)
    {
        ImmutableArray<byte> bytes = resources.BonePaths;
        int Word(int index)
        {
            if (index < 0 || index >= bytes.Length / 2)
            {
                throw new InvalidDataException("Electrocute bone path leaves its complete source.");
            }
            return BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan().Slice(index * 2, 2));
        }
        if (Word(0) != 16)
        {
            throw new InvalidDataException("Electrocute requires its original bone path directory.");
        }
        for (int root = 0; root < 16; root++)
        {
            _ = Walk(Word(root + 1), 0);
        }
        int Walk(int index, int depth)
        {
            if (depth >= 32)
            {
                throw new InvalidDataException("Electrocute bone path has excessive nested forks.");
            }
            for (int count = 0; count < bytes.Length / 2; count++)
            {
                int value = Word(index);
                if (value == short.MaxValue)
                {
                    return index + 1;
                }
                if (value < 0)
                {
                    int next = Walk(index + 1, depth + 1);
                    if (next != index - value)
                    {
                        throw new InvalidDataException("Electrocute fork length disagrees with its terminated child path.");
                    }
                    index = next;
                }
                else
                {
                    if (value >= 128 || Word(index + 1) < 0)
                    {
                        throw new InvalidDataException("Electrocute bone path contains an invalid signed bone or vertex selector.");
                    }
                    index += 2;
                }
            }
            throw new InvalidDataException("Electrocute bone path does not terminate inside its source.");
        }
    }

    private static int Read(ReadOnlySpan<byte> data, int offset)
    {
        if (offset < 0 || offset > data.Length - 4)
        {
            throw new InvalidDataException("Electrocute ACT directory leaves its original source.");
        }
        return BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, 4));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleElectrocute275Resources? nativeElectrocute275Resources;
    public NativeBattleElectrocute275Resources ReadNativeElectrocute275Resources() => nativeElectrocute275Resources ??= NativeBattleElectrocute275ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
