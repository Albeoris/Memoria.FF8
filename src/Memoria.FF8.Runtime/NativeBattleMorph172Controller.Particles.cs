using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleMorph172Controller
{
    public static (NativeBattleMorph172Frame Frame, int Result) AdvanceSlots(NativeBattleMorph172Frame frame, uint address, NativeBattleMorph172Resources resources, NativeBattleMorph172Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback != 0x5DA2C0)
        {
            throw new NotSupportedException("Morph particle callback is unsupported.");
        }
        (frame, uint temporary) = Allocate(frame, 180);
        frame = PublishContext(frame, temporary, Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(0xD61F8Cu)).Write(0x24, BitConverter.GetBytes((ushort)2)).Write(0x14, BitConverter.GetBytes(4096)), 180);
        int survivors = 0;
        for (int index = 0; index < 100; index++)
        {
            uint row = 0x239A908 + (uint)(index * 24);
            if ((frame.State.ReadDword(row) & 1) == 0)
            {
                continue;
            }
            short phase = unchecked((short)frame.State.ReadWord(row + 4));
            short size = unchecked((short)frame.State.ReadWord(row + 6));
            NativeBattleEffectArena context = Context(frame, temporary, 180).Write(4, BitConverter.GetBytes(phase)).Write(0x0C, BitConverter.GetBytes((int)unchecked((short)frame.State.ReadWord(row + 16)))).Write(0x10, BitConverter.GetBytes((int)unchecked((short)frame.State.ReadWord(row + 18)))).Write(0x14, BitConverter.GetBytes(4096));
            frame = PublishContext(frame, temporary, context, 180);
            frame = SetupSprite172(frame, inputs, frame.State.ReadView(row + 8, 8), size, unchecked((short)-(size >> 4)));
            frame = inputs.Execute(frame, new NativeBattleMorph172DrawSprite180(temporary, 0xD61F8C, 17, 2));
            if ((inputs.GraphicsFlags & 0x201) != 0)
            {
                continue;
            }
            frame = frame with { State = frame.State.WriteWord(row + 4, unchecked((ushort)(phase + 1))) };
            if (BinaryPrimitives.ReadInt16LittleEndian(Context(frame, temporary, 180).Read(0x28, 2)) < 0)
            {
                frame = frame with { State = frame.State.WriteDword(row, 0) };
            }
            else
            {
                survivors++;
            }
        }
        frame = Release(frame, 180);
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = Read(frame, address);
        short age = Signed(task, 12);
        if (age >= 0 && age <= 42)
        {
            int actor = checked((int)frame.State.ReadDword(0x239D4F4));
            int count = inputs.ReadBoneCount(frame, actor);
            if (count <= 0)
            {
                throw new InvalidDataException("Morph requires its actual positive current body bone count.");
            }
            for (int index = 0; index < 100; index++)
            {
                uint row = 0x239A908 + (uint)(index * 24);
                if (frame.State.ReadDword(row) != 0)
                {
                    continue;
                }
                frame = frame with { State = frame.State.WriteDword(row, 1).WriteWord(row + 4, 0) };
                frame = DrawRandom(frame, out ushort sizeDraw);
                frame = frame with { State = frame.State.WriteWord(row + 6, (ushort)(sizeDraw % 1664 + 320)) };
                frame = DrawRandom(frame, out ushort distance);
                frame = DrawRandom(frame, out ushort selector);
                NativeBattleMorph172PointStep bone = inputs.ReadBone502170(frame, actor, selector % count, distance & 4095);
                frame = bone.Frame with { State = bone.Frame.State.WriteView(row + 8, bone.Point8, 6) };
                for (int axis = 0; axis < 3; axis++)
                {
                    frame = DrawRandom(frame, out ushort jitter);
                    uint at = row + 8 + (uint)(axis * 2);
                    frame = frame with { State = frame.State.WriteWord(at, unchecked((ushort)(unchecked((short)frame.State.ReadWord(at)) + jitter % 360 - 180))) };
                }
                short x = unchecked((short)frame.State.ReadWord(row + 8));
                short y = unchecked((short)frame.State.ReadWord(row + 10));
                frame = frame with { State = frame.State.WriteDword(row, 1).WriteWord(row + 4, 0).WriteWord(row + 16, unchecked((ushort)((x & 1) != 0 ? 4096 : -4096))).WriteWord(row + 18, unchecked((ushort)((y & 1) != 0 ? 4096 : -4096))) };
                break;
            }
        }
        task = Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= 4 && survivors == 0 ? 2 : 0);
    }
}
