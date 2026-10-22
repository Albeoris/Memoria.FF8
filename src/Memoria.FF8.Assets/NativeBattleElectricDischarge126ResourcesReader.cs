using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleElectricDischarge126ResourcesReader
{
    public static NativeBattleElectricDischarge126Resources Read(byte[] executable)
    {
        NativeExecutableMemory memory = new(executable);
        if (Encoding.ASCII.GetString(memory.Read(0x10E298C, 11)) != "mag125.tim\0")
        {
            throw new InvalidDataException("Electric Discharge requires its original initializer texture name.");
        }
        ImmutableArray<short> wave = Enumerable.Range(0, 4096).Select(index => BinaryPrimitives.ReadInt16LittleEndian(memory.Read(checked(0x1246A0Cu + (uint)index * 2), 2))).ToImmutableArray();
        NativeBattleElectricDischarge126Resources result = new(new(0x12417D8, memory.Read(0x12417D8, 0xA10).ToImmutableArray()), Sprite(0x12415A8), Sprite(0x12416FC), new("FF8_EN:12421E8", memory.Read(0x12421E8, 0x90).ToImmutableArray()), memory.Read(0x1242A0C, 0x4000).ToImmutableArray(), wave, memory.Read(0x1242284, 6).ToImmutableArray(), memory.Read(0x124228C, 0x780).ToImmutableArray(), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x1242280, 4)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x1242278, 4)), BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(0x124227C, 4)));
        ValidatePayloads(result);
        return result;

        NativeBattleModuleResource Sprite(uint address) => new(0, address, 0, 0, NativeBattleEffectStaticResourcesReader.ReadProjectedSprite(memory, address));
    }

    public static void ValidatePayloads(NativeBattleElectricDischarge126Resources resources)
    {
        resources.Validate();
        ReadOnlySpan<byte> data = resources.Act.Data.AsSpan();
        int shape = Read(data, 8);
        int animation = Read(data, 4);
        if (Read(data, 0) != 1 || shape != 12 || Read(data, shape) != 12 + 51 * 3 * 8 || Read(data, shape + 4) != 51 || Read(data, shape + 8) != 3)
        {
            throw new InvalidDataException("Electric Discharge ACT requires its original three-variant shape.");
        }
        int cursor = checked(shape + Read(data, shape));
        for (int group = 0; group < 8; group++)
        {
            int count = Read(data, cursor);
            cursor += 4;
            if (count != (group == 3 ? 32 : 0))
            {
                throw new InvalidDataException("Electric Discharge ACT requires its actual FT4 primitive group.");
            }
            for (int face = 0; face < count; face++)
            {
                for (int corner = 0; corner < 4; corner++)
                {
                    int index = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(cursor + 4 + corner * 2, 2));
                    if ((index & 1) != 0 || index / 2 >= 51)
                    {
                        throw new InvalidDataException("Electric Discharge ACT selects an absent original vertex.");
                    }
                }
                cursor = checked(cursor + 24);
            }
        }
        if (cursor != animation || Read(data, animation) != 2 || Read(data, animation + 4) != 32)
        {
            throw new InvalidDataException("Electric Discharge ACT directory or animation duration is incomplete.");
        }
        NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(new byte[0xC8].ToImmutableArray()).Initialize7016B0(resources.Act, 0xC8);
        for (int frame = 0; frame < 32; frame++)
        {
            NativeBattleGfAnimationAdvance next = workspace.Advance701970(resources.Act, 0, (current, node) =>
            {
                if (node.GeometryIndex != 0 || node.Header != 0x40200 || node.AnimationFlags != 0x33D8 || node.Word40 is < 0 or > 2 || node.Word42 is < 0 or > 2)
                {
                    throw new InvalidDataException("Electric Discharge ACT selects an unsupported original shape or variant.");
                }
                return current;
            });
            if (next.Remaining != 31 - frame)
            {
                throw new InvalidDataException("Electric Discharge ACT changes its original programme duration.");
            }
            workspace = next.Workspace;
        }
        NativeBattleBonePathResources.Validate(resources.BonePaths, 16);
    }

    private static int Read(ReadOnlySpan<byte> data, int offset)
    {
        if (offset < 0 || offset > data.Length - 4)
        {
            throw new InvalidDataException("Electric Discharge ACT directory leaves its original source.");
        }
        return BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, 4));
    }
}

public sealed partial class GameAssets
{
    private NativeBattleElectricDischarge126Resources? nativeElectricDischarge126Resources;
    public NativeBattleElectricDischarge126Resources ReadNativeElectricDischarge126Resources() => nativeElectricDischarge126Resources ??= NativeBattleElectricDischarge126ResourcesReader.Read(GameDataResources.ReadNativeExecutable(resources));
}
