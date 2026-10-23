using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleJunk44Controller
{
    public static NativeBattleJunk44Frame SpawnBoneSprites6E81A0(NativeBattleJunk44Frame frame, int count, int selector, int radius, int sizeRange, int sizeBase, NativeBattleJunk44Resources resources, NativeBattleJunk44Inputs inputs)
    {
        NativeBattleJunk44PointStep bone = inputs.ReadBone502170(frame, frame.State.ReadDword(0x2545108), selector, 0);
        return SpawnPointSprites6E8360(bone.Frame, count, bone.Point8, radius, sizeRange, sizeBase, resources, inputs);
    }

    public static NativeBattleJunk44Frame SpawnPointSprites6E8360(NativeBattleJunk44Frame frame, int count, NativeBattleEffectArena point8, int radius, int sizeRange, int sizeBase, NativeBattleJunk44Resources resources, NativeBattleJunk44Inputs inputs)
    {
        while (count != 0)
        {
            count = unchecked(count - 1);
            (frame, uint address) = Append(frame, 0x2545130, 0x6E82C0);
            if (address == 0)
            {
                return frame;
            }
            frame = DrawRandom(frame, out ushort angle);
            short x = unchecked((short)(unchecked(NativeBattleCameraMath.Cosine(angle) * radius) / 4096));
            short z = unchecked((short)(unchecked(NativeBattleCameraMath.Sine(angle) * radius) / 4096));
            frame = DrawRandom(frame, out ushort size);
            short scale = unchecked((short)(unchecked(size * sizeRange) / 32768 + sizeBase));
            NativeBattleModulePoolTask task = Read(frame, address).WriteWord(0x0C, unchecked((ushort)(Word44(point8, 0) + x))).WriteWord(0x0E, 0).WriteWord(0x10, unchecked((ushort)(Word44(point8, 4) + z))).WriteWord(0x12, 13).WriteWord(0x14, unchecked((ushort)(x / 8))).WriteWord(0x16, unchecked((ushort)-(radius / 8))).WriteWord(0x18, unchecked((ushort)(z / 8))).WriteWord(0x1C, unchecked((ushort)scale));
            frame = Write(frame, address, task);
        }
        return frame;
    }

    public static (NativeBattleJunk44Frame Frame, int Result) AdvanceSprite6E82C0(NativeBattleJunk44Frame frame, uint address, NativeBattleJunk44Resources resources, NativeBattleJunk44Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        (frame, uint temporary) = Allocate(frame, 180);
        (byte[] w, bool[] known) = TaskView(task, 0x12, 2).ReadSnapshot(0, 2);
        short scale = Signed(task, 0x1C);
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), new(Signed(task, 0x0C), Signed(task, 0x0E), Signed(task, 0x10)), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(w) : null, scale, unchecked((short)-(scale >> 2))) };
        NativeBattleModuleResource sprite = resources.ReadSprite(0x1354B80);
        ushort phase = unchecked((ushort)(BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)) - task.ReadWord(0x12)));
        NativeBattleEffectArena context = Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(0x1354B80u)).Write(4, BitConverter.GetBytes(phase)).Write(0x24, new byte[2]);
        frame = inputs.Execute(PublishContext(frame, temporary, context, 180), new NativeBattleJunk44DrawSprite180(temporary, 0x1354B80));
        frame = Release(frame, 180);
        task = Read(frame, address).WriteWord(0x12, unchecked((ushort)(Read(frame, address).ReadWord(0x12) - 1)));
        if (task.ReadWord(0x12) == 0)
        {
            return (Write(frame, address, task), 2);
        }
        task = task.WriteWord(0x0C, unchecked((ushort)(task.ReadWord(0x0C) + task.ReadWord(0x14)))).WriteWord(0x0E, unchecked((ushort)(task.ReadWord(0x0E) + task.ReadWord(0x16)))).WriteWord(0x10, unchecked((ushort)(task.ReadWord(0x10) + task.ReadWord(0x18))));
        return (Write(frame, address, task), 0);
    }

    public static (NativeBattleJunk44Frame Frame, uint Address) SpawnDebris6E8470(NativeBattleJunk44Frame frame, short x, short z, uint modelAddress, int tableIndex, NativeBattleJunk44Resources resources, NativeBattleJunk44Inputs inputs)
    {
        (frame, uint address) = Append(frame, 0x2545120, 0x6E84D0);
        if (address == 0)
        {
            return (frame, 0);
        }
        if ((uint)tableIndex >= 7)
        {
            throw new InvalidDataException("Junk debris table selector leaves its seven actual rows.");
        }
        NativeBattleModulePoolTask task = Read(frame, address).WriteWord(0x0C, unchecked((ushort)x)).WriteWord(0x0E, 0xF000).WriteWord(0x10, unchecked((ushort)z)).WriteWord(0x12, 20).WriteWord(0x14, 0).WriteWord(0x16, 800).WriteWord(0x18, 0).WriteDword(0x2C, checked(0x1362730u + (uint)tableIndex * 24)).WriteDword(0x30, modelAddress);
        return (Write(frame, address, task), address);
    }

    public static NativeBattleJunk44Frame ApplyModelContacts6E87A0(NativeBattleJunk44Frame frame, uint modelAddress, NativeBattleJunk44Inputs inputs)
    {
        if (modelAddress < 0x1D972C0 || (modelAddress - 0x1D972C0) % 156 != 0)
        {
            throw new InvalidDataException("Junk contact source is not an actual current graphics-model row.");
        }
        uint actor = (modelAddress - 0x1D972C0) / 156;
        int ordinal = 0;
        while (true)
        {
            NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
            NativeBattleActionHeaderSlot header = Header(frame, packets);
            if (ordinal >= header.ReadByte(16))
            {
                return frame;
            }
            int index = checked((header.FirstContactIndex ?? throw new NotSupportedException("Junk requires its actual physical contact base.")) + ordinal);
            if (packets.ReadContact(index).Primary.ActorIndex == actor)
            {
                frame = inputs.Execute(frame, new NativeBattleJunk44Contact506690(index));
            }
            ordinal++;
        }
    }

    private static short Word44(NativeBattleEffectArena view, int offset) => BinaryPrimitives.ReadInt16LittleEndian(view.Read(offset, 2));
}
