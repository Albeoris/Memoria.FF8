using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect76Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect76Lower;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect76Bursts
{
    public static NativeBattleEffect76FactoryStep Factory(NativeBattleEffect76Frame frame, uint function, NativeBattleEffect76Inputs inputs)
    {
        uint callback = function switch
        {
            0x620BA0 => 0x620BD0,
            0x620D60 => 0x620DD0,
            0x620F20 => 0x620F60,
            0x6210A0 => 0x621110,
            _ => throw new NotSupportedException($"Effect76 factory {function:X8} is unsupported.")
        };
        int actor = checked((int)Dword(frame, 0x24C6658));
        if (function == 0x620D60)
        {
            NativeBattleEffect76PointStep sampled = inputs.Point502170(frame, actor, 0xF0, 2048, ReadBytes(frame, Point, 8));
            frame = WriteBytes(sampled.Frame, Point, sampled.Output8, 8);
        }
        (frame, uint address) = Append(frame, 0x24C5830, callback);
        if (address == 0)
        {
            throw new InvalidDataException("Effect76 mandatory factory requires its actual available task slot.");
        }
        NativeBattleModulePoolTask task = Read(frame, address).WriteWord(12, 0);
        frame = Write(frame, address, task);
        if (function is 0x620D60 or 0x620F20)
        {
            frame = Write(frame, address, CopyTask(Read(frame, address), 0x10, ReadBytes(frame, Point, 8), 8));
            task = Read(frame, address);
        }
        if (function == 0x6210A0)
        {
            NativeBattleEffect76PointStep sampled = inputs.Point502170(frame, actor, 0xF1, 0, TaskView(task, 0x10, 8));
            frame = Write(sampled.Frame, address, CopyTask(Read(sampled.Frame, address), 0x10, sampled.Output8, 8));
            task = Read(frame, address).WriteWord(0x18, (ushort)(Roll(ref frame) % 2048)).WriteWord(0x1A, 16);
        }
        int size = function == 0x620BA0 ? 2048 : function == 0x620D60 ? 3072 : function == 0x620F20 ? 597 : 1664;
        int velocity = function == 0x620BA0 ? 6144 : size;
        task = task.WriteWord(0x1C, (ushort)size).WriteWord(0x1E, (ushort)velocity);
        if (function == 0x620D60)
        {
            task = task.WriteWord(0x20, 160);
        }
        return new(Write(frame, address, task), address);
    }

    public static NativeBattleEffect76Step Advance(NativeBattleEffect76Frame frame, uint address, NativeBattleEffect76Resources resources, NativeBattleEffect76Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        uint callback = task.Callback;
        bool attached = callback is 0x620BD0 or 0x620DD0;
        short size = unchecked((short)task.ReadWord(0x1C));
        short age = unchecked((short)task.ReadWord(12));
        NativeBattleVector euler = new(0, 0, callback == 0x621110 ? unchecked((short)task.ReadWord(0x18)) : (short)0);
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(euler);
        local = NativeBattlePoseMatrices.Scale56BEF0(local, size, callback == 0x620DD0 ? unchecked((short)task.ReadWord(0x20)) : size, size);
        NativeBattleEffectArena point;
        if (attached)
        {
            NativeBattleEffect76PointStep sampled = inputs.Point502170(frame, checked((int)Dword(frame, 0x24C6658)), 0xF0, 2048, NativeBattleEffectArena.Clear().MarkUnknown(0, 8));
            frame = sampled.Frame;
            point = sampled.Output8;
        }
        else
        {
            point = TaskView(task, 0x10, 8);
        }
        (byte[] w, bool[] known) = point.ReadSnapshot(6, 2);
        short? sourceW = known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(w) : null;
        short depth = callback == 0x620BD0 ? (short)-300 : callback == 0x621110 ? (short)2000 : (short)-1000;
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), NativeBattleEffect76Lower.Vector(point, 0), sourceW, callback == 0x621110 ? (short)0 : size, depth) };
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(local, null), true, false);
        (frame, uint temporary) = Allocate(frame, 88);
        uint source = callback is 0x620BD0 or 0x620DD0 ? 0xDE27F0u : callback == 0x620F60 ? 0xDE37A8u : 0xDE1748u;
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(source)).Write(8, BitConverter.GetBytes(0u));
        context = Cue(context, callback, age);
        frame = PublishContext(frame, temporary, context, 88);
        frame = NativeBattleEffect76Meshes.Draw(frame, temporary, source, PrivateCursor, false, resources, inputs);
        if (callback == 0x620BD0)
        {
            frame = NativeBattleEffect76Meshes.Draw(frame, temporary, source, PrivateCursor, false, resources, inputs);
        }
        frame = Release(frame, 88);
        if (Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        task = Read(frame, address);
        int velocity = unchecked((short)task.ReadWord(0x1E));
        if (callback == 0x620BD0)
        {
            if (age >= 344)
            {
                task = task.WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) + velocity))).WriteWord(0x1E, unchecked((ushort)(velocity - velocity / 4)));
            }
        }
        else if (callback == 0x621110)
        {
            task = task.WriteWord(0x18, unchecked((ushort)(task.ReadWord(0x18) + unchecked((short)task.ReadWord(0x1A)))));
            if (age < 16)
            {
                task = task.WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) + velocity))).WriteWord(0x1E, unchecked((ushort)(velocity - velocity / 6)));
            }
            else
            {
                task = task.WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) + (velocity >> 6))));
            }
        }
        else
        {
            task = task.WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) + velocity))).WriteWord(0x1E, unchecked((ushort)(velocity - velocity / (callback == 0x620DD0 ? 6 : 5))));
        }
        frame = Write(frame, address, task);
        int end = callback == 0x620BD0 ? 360 : callback == 0x620DD0 ? 26 : callback == 0x620F60 ? 20 : 79;
        return Tick(frame, address, unchecked((short)(age + 1)) >= end ? 2 : 0);
    }

    public static NativeBattleEffectArena Cue(NativeBattleEffectArena context, uint callback, short age)
    {
        if (callback == 0x620BD0)
        {
            int cue = age < 6 ? 4096 - age * 682 : age >= 344 ? 0 : (age & 1) != 0 ? 2048 : 256;
            return Long(Long(context, 0x1C, 0xF3), 12, cue);
        }
        int start = callback == 0x620DD0 ? 18 : callback == 0x620F60 ? 14 : 63;
        if (callback == 0x620F60 && age < 2)
        {
            return Long(Long(context, 0x1C, 0xF3), 12, (2 - age) << 11);
        }
        context = Long(context, 0x1C, age >= start ? 0xF3 : 0x33);
        return age >= start ? Long(context, 12, (age - start) * (callback == 0x620F60 ? 682 : callback == 0x621110 ? 256 : 512)) : context;
    }
}
