using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect167Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect167Controller
{
    public static NativeBattleEffect167Step AdvanceMain5E2AD0(NativeBattleEffect167Frame frame, uint address, NativeBattleEffect167Resources resources, NativeBattleEffect167Inputs inputs)
    {
        frame = frame with { State = frame.State.WriteDword(MarkerGlobal, 1) };
        if ((inputs.ReadFlags(frame) & 0x201) != 0)
        {
            return new(frame, 0);
        }
        int age = Signed(Read(frame, address), 12);
        if (age == 27)
        {
            frame = Birth167(frame, 0x5E2F30, task => task);
            for (int index = 0; index < 3; index++)
            {
                frame = frame with { State = frame.State.WriteDword(FirstHistoryAddress + (uint)(index * 20), 0) };
            }
        }
        if (age == 44)
        {
            frame = Birth167(frame, 0x5E33E0, task => task);
            for (int index = 0; index < 10; index++)
            {
                frame = frame with { State = frame.State.WriteDword(SecondHistoryAddress + (uint)(index * 20), 0) };
            }
        }
        if (age == 64)
        {
            frame = Birth167(frame, 0x5E38D0, task => task.WriteWord(0x10, unchecked((ushort)-170)).WriteWord(0x12, unchecked((ushort)-50)).WriteWord(0x1C, 0x700).WriteWord(0x1E, 0x700).WriteWord(0x20, 0x800));
        }
        if (age == 68)
        {
            frame = Birth167(frame, 0x5E3A00, task => task.WriteWord(0x10, 0).WriteWord(0x12, 0).WriteWord(0x1C, 0x500).WriteWord(0x1E, 0x500));
        }
        if (age == 97)
        {
            NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
            NativeBattleActionHeaderSlot header = packets.Headers[frame.Effect.Parameters!.HeaderIndex7C];
            int count = header.ReadByte(0x10);
            if (count != 0)
            {
                int first = header.FirstContactIndex ?? throw new NotSupportedException("Effect167 requires its actual contact pointer.");
                for (int index = 0; index < count; index++)
                {
                    int actor = packets.ReadContact(first + index).Primary.ActorIndex;
                    (frame, uint child) = Append(frame, ChildDescriptor, 0x5E3C20);
                    if (child == 0)
                    {
                        throw new InvalidDataException("Effect167 requires its selected target allocation.");
                    }
                    frame = Write(frame, child, Read(frame, child).WriteWord(12, 0).WriteWord(14, checked((ushort)actor)));
                    NativeBattleEffect167PointStep point = inputs.LocalBonePoint3B20(frame, actor, 0xF1, 0, TaskView(Read(frame, child), 0x10, 8));
                    frame = point.Frame;
                    NativeBattleModulePoolTask task = CopyTask(Read(frame, child), 0x10, point.Output8, 8);
                    int scale = (inputs.ReadModel(frame, actor).Transform ?? throw new NotSupportedException("Effect167 requires its current target scale WORD.")).Scale38;
                    int radius = unchecked((Next167(ref frame) % 256 + 128) * scale) >> 12;
                    int divisor = unchecked(radius * 2);
                    for (int lane = 0; lane < 3; lane++)
                    {
                        int offset = 0x10 + lane * 2;
                        int value = Signed(task, offset) + Next167(ref frame) % divisor - radius;
                        task = task.WriteWord(offset, unchecked((ushort)value));
                    }
                    task = task.WriteWord(0x18, unchecked((ushort)(Next167(ref frame) % 1280 - 640))).WriteWord(0x1C, 0);
                    int limitedScale = Math.Min(scale, 2560);
                    int velocity = unchecked((Next167(ref frame) % 48 + 48) * limitedScale) >> 12;
                    frame = Write(frame, child, task.WriteWord(0x1E, unchecked((ushort)velocity)));
                }
            }
        }
        if (age == 64)
        {
            frame = SetStage167(frame, false, inputs);
            frame = SetModelFlags167(frame, 4, true, false, inputs);
        }
        if (age == 77)
        {
            frame = SetStage167(frame, true, inputs);
            frame = SetModelFlags167(frame, 4, false, true, inputs);
        }
        if (age == 72)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect167Flash5712C0(4, 3, 5, 255));
        }
        int caster = checked((int)frame.State.ReadDword(CasterGlobal));
        if (age == 74)
        {
            frame = SetModelFlags167(frame, 8, true, false, inputs);
            NativeBattleEffectArena model = inputs.ReadModelArena9C(frame, caster);
            frame = WriteBytes(frame, SavedPositionAddress, CopyBytes(model, 0x1C, NativeBattleEffectArena.Clear(), 0, 8), 8);
            short y = BinaryPrimitives.ReadInt16LittleEndian(model.Read(0x1E, 2));
            short z = BinaryPrimitives.ReadInt16LittleEndian(model.Read(0x20, 2));
            NativeBattleEffectArena updated = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(unchecked((short)(y + 700))));
            frame = inputs.Execute(frame, new NativeBattleEffect167WriteModelBytes(caster, 0x1E, updated, 2));
            updated = updated.Write(0, BitConverter.GetBytes(unchecked((short)(z + 4400))));
            frame = inputs.Execute(frame, new NativeBattleEffect167WriteModelBytes(caster, 0x20, updated, 2));
        }
        if (age == 143)
        {
            NativeBattleActionHeaderSlot header = inputs.ReadPackets(frame).Headers[frame.Effect.Parameters!.HeaderIndex7C];
            int count = header.ReadByte(0x10);
            int first = header.FirstContactIndex ?? throw new NotSupportedException("Effect167 requires its actual all-contact pointer.");
            frame = inputs.Execute(frame, new NativeBattleEffect167AllContacts506BA0(first, count));
        }
        if (age == 28)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect167Sound501330(SoundAddress, 0, 128));
        }
        short next = unchecked((short)(age + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)next)));
        if (next > 145)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect167ResourceFlags44(caster, ushort.MaxValue, 0x40));
            return new(frame, 2);
        }
        return new(frame, 0);
    }

    private static NativeBattleEffect167Frame Birth167(NativeBattleEffect167Frame frame, uint callback, Func<NativeBattleModulePoolTask, NativeBattleModulePoolTask> initialize)
    {
        (frame, uint address) = Append(frame, ChildDescriptor, callback);
        if (address == 0)
        {
            throw new InvalidDataException("Effect167 requires its selected child allocation.");
        }
        return Write(frame, address, initialize(Read(frame, address).WriteWord(12, 0)));
    }

    private static int Next167(ref NativeBattleEffect167Frame frame)
    {
        NativeCrtRandomState random = frame.Random.Draw(out ushort value);
        frame = frame with { Random = random };
        return value;
    }

    private static NativeBattleEffect167Frame SetStage167(NativeBattleEffect167Frame frame, bool set, NativeBattleEffect167Inputs inputs)
    {
        for (int index = 0; index < 4; index++)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect167StageGroupFlags(index, set ? byte.MaxValue : (byte)0xFD, set ? (byte)2 : (byte)0));
        }
        return frame;
    }

    private static NativeBattleEffect167Frame SetModelFlags167(NativeBattleEffect167Frame frame, ushort mask, bool set, bool casterWord, NativeBattleEffect167Inputs inputs)
    {
        int caster = checked((int)frame.State.ReadDword(CasterGlobal));
        frame = WriteFlags167(frame, caster, mask, set, casterWord, inputs);
        for (int actor = 0; actor < 3; actor++)
        {
            if ((inputs.ReadModel(frame, actor).Flags0 & 2) != 0)
            {
                frame = WriteFlags167(frame, actor, mask, set, true, inputs);
            }
        }
        return frame;
    }

    private static NativeBattleEffect167Frame WriteFlags167(NativeBattleEffect167Frame frame, int actor, ushort mask, bool set, bool word, NativeBattleEffect167Inputs inputs)
    {
        ushort flags = inputs.ReadModel(frame, actor).Flags0;
        ushort value = unchecked((ushort)(set ? flags | mask : flags & ~mask));
        NativeBattleEffectArena bytes = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(value));
        return inputs.Execute(frame, new NativeBattleEffect167WriteModelBytes(actor, 0, bytes, word ? 2 : 1));
    }
}
