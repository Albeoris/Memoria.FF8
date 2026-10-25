using static Memoria.FF8.Runtime.NativeBattleEffect315Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect315Controller
{
    private static (NativeBattleEffect315Frame Frame, int Result) AdvanceBodyTask(NativeBattleEffect315Frame frame, uint address, NativeBattleEffect315Resources resources, NativeBattleEffect315Inputs inputs, bool recipient)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 0xC);
        uint model = ModelAddress(Signed(task, 0xE));
        if (age < (recipient ? 110 : 65))
        {
            if (recipient)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect315ModelPosition28XZ(model, Signed(task, 0x10), Signed(task, 0x14)));
            }
            (frame, uint temporary) = Allocate(frame, 156);
            int factor = recipient ? age < 30 ? 4096 : 4096 + (30 - age) * 4096 / 80 : age < 70 ? age * 4096 / 70 : 4096;
            NativeBattleEffectArena context = Context(frame, temporary, 156).Write(0x2C, BitConverter.GetBytes(factor)).Write(0x30, BitConverter.GetBytes((int)Signed(task, 0x20))).Write(0x34, BitConverter.GetBytes(recipient ? 900 : 700)).Write(0x38, BitConverter.GetBytes(recipient ? 11 : 10)).Write(0x3C, BitConverter.GetBytes((int)Signed(task, 0x22)));
            context = NativeBattleEffectNodeContextMemory.Copy(TaskView(task, 0x10, 8), 0, context, 0x74, 8);
            frame = PublishContext(frame, temporary, context, 156);
            frame = inputs.Execute(frame, new NativeBattleEffect315ModelFlags(model, 0, 4));
            frame = inputs.Execute(frame, new NativeBattleEffect315DrawBody156(temporary, model));
            frame = Release(frame, 156);
        }
        if (Paused(frame, inputs))
        {
            return (frame, 0);
        }
        task = Read(frame, address);
        if (recipient ? age >= 30 : age < 70)
        {
            task = task.WriteWord(0x12, unchecked((ushort)(task.ReadWord(0x12) + task.ReadWord(0x22))));
        }
        short next = unchecked((short)(age + 1));
        frame = Write(frame, address, task.WriteWord(0xC, unchecked((ushort)next)));
        if (next <= (recipient ? 110 : 65))
        {
            return (frame, 0);
        }
        return (inputs.Execute(frame, new NativeBattleEffect315ModelFlags(model, recipient ? (ushort)4 : (ushort)0, recipient ? (ushort)0 : (ushort)4)), 2);
    }

    private static NativeBattleEffect315Frame LoadTaskMatrix(NativeBattleEffect315Frame frame, NativeBattleModulePoolTask task, NativeBattleEffect315Inputs inputs, bool equal)
    {
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(0, Signed(task, 0x18), 0)) with { X = Signed(task, 0x10), Y = Signed(task, 0x12), Z = Signed(task, 0x14) };
        int scale = Signed(task, 0x1C);
        local = NativeBattlePoseMatrices.Scale56BEF0(local, scale, equal ? scale : Signed(task, 0x20), scale);
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
        return LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(matrix, null));
    }

    private static (NativeBattleEffect315Frame Frame, int Result) AdvanceGrowingMesh(NativeBattleEffect315Frame frame, uint address, NativeBattleEffect315Resources resources, NativeBattleEffect315Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 0xC);
        frame = LoadTaskMatrix(frame, task, inputs, false);
        (frame, uint temporary) = Allocate(frame, 88);
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(0xCD3F08u)).Write(8, new byte[4]).Write(0x1C, BitConverter.GetBytes(0x33u));
        if (age == 0 || age >= 4)
        {
            context = context.Write(0xC, BitConverter.GetBytes(age == 0 ? 2048 : (age - 4) * 341)).Write(0x1C, BitConverter.GetBytes(0xF3u));
        }
        frame = PublishContext(frame, temporary, context, 88);
        frame = inputs.Execute(frame, new NativeBattleEffect315DrawMesh88(temporary));
        frame = Release(frame, 88);
        if (Paused(frame, inputs))
        {
            return (frame, 0);
        }
        task = Read(frame, address);
        short speed = Signed(task, 0x1E);
        task = task.WriteWord(0x18, unchecked((ushort)(task.ReadWord(0x18) + task.ReadWord(0x1A)))).WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) + speed))).WriteWord(0x1E, unchecked((ushort)(speed - speed / 24)));
        frame = Write(frame, address, task.WriteWord(0xC, unchecked((ushort)(age + 1))));
        return (frame, unchecked((short)(age + 1)) >= 16 ? 2 : 0);
    }

    private static (NativeBattleEffect315Frame Frame, int Result) AdvanceScrollingMesh(NativeBattleEffect315Frame frame, uint address, NativeBattleEffect315Resources resources, NativeBattleEffect315Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 0xC);
        frame = LoadTaskMatrix(frame, task, inputs, false);
        (frame, uint temporary) = Allocate(frame, 124);
        NativeBattleEffectArena context = Context(frame, temporary, 124).Write(0, BitConverter.GetBytes(0xCD47B0u)).Write(8, new byte[4]).Write(0x10, BitConverter.GetBytes(age * 4)).Write(0x14, new byte[4]).Write(0x18, BitConverter.GetBytes((short)0)).Write(0x1A, BitConverter.GetBytes((short)192)).Write(0x1C, BitConverter.GetBytes((short)128)).Write(0x1E, BitConverter.GetBytes((short)64)).Write(0x20, BitConverter.GetBytes(0x33u));
        if (age < 16 || age >= 68)
        {
            context = context.Write(0xC, BitConverter.GetBytes(age < 16 ? (16 - age) << 8 : (age - 68) * 341)).Write(0x20, BitConverter.GetBytes(0xF3u));
        }
        frame = PublishContext(frame, temporary, context, 124);
        frame = inputs.Execute(frame, new NativeBattleEffect315DrawScrolling124(temporary));
        frame = Release(frame, 124);
        if (Paused(frame, inputs))
        {
            return (frame, 0);
        }
        task = Read(frame, address).WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) + task.ReadWord(0x1E)))).WriteWord(0x20, unchecked((ushort)(task.ReadWord(0x20) - task.ReadWord(0x22)))).WriteWord(0xC, unchecked((ushort)(age + 1)));
        return (Write(frame, address, task), unchecked((short)(age + 1)) >= 80 ? 2 : 0);
    }

    private static (NativeBattleEffect315Frame Frame, int Result) AdvanceRandomMesh(NativeBattleEffect315Frame frame, uint address, NativeBattleEffect315Resources resources, NativeBattleEffect315Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int delay = Signed(task, 0xE);
        if (delay > 0)
        {
            return (Paused(frame, inputs) ? frame : Write(frame, address, task.WriteWord(0xE, unchecked((ushort)(delay - 1)))), 0);
        }
        int age = Signed(task, 0xC);
        frame = LoadTaskMatrix(frame, task, inputs, false);
        (frame, uint temporary) = Allocate(frame, 96);
        NativeBattleEffectArena context = Context(frame, temporary, 96).Write(0, BitConverter.GetBytes(0xCD3A48u)).Write(8, new byte[4]).Write(0x1C, BitConverter.GetBytes(0x33u)).Write(0x58, BitConverter.GetBytes(0xE1000220u)).Write(0x5C, BitConverter.GetBytes(0xE1000220u));
        if (age >= 8)
        {
            context = context.Write(0x1C, BitConverter.GetBytes(0xF3u)).Write(0xC, BitConverter.GetBytes((age - 8) * 341));
        }
        frame = PublishContext(frame, temporary, context, 96);
        frame = inputs.Execute(frame, new NativeBattleEffect315DrawMesh96(temporary));
        frame = Release(frame, 96);
        if (Paused(frame, inputs))
        {
            return (frame, 0);
        }
        task = Read(frame, address);
        short speed = Signed(task, 0x22);
        task = task.WriteWord(0x20, unchecked((ushort)(task.ReadWord(0x20) + speed))).WriteWord(0x22, unchecked((ushort)(speed - speed / 6))).WriteWord(0xC, unchecked((ushort)(age + 1)));
        return (Write(frame, address, task), unchecked((short)(age + 1)) >= 20 ? 2 : 0);
    }

    private static (NativeBattleEffect315Frame Frame, int Result) AdvanceRingMesh(NativeBattleEffect315Frame frame, uint address, NativeBattleEffect315Resources resources, NativeBattleEffect315Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int age = Signed(task, 0xC);
        frame = LoadTaskMatrix(frame, task, inputs, true);
        (frame, uint temporary) = Allocate(frame, 88);
        int fade = age < 16 ? (32 - age) << 7 : age >= 72 ? (age - 56) << 7 : 2048;
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(0xCD3198u)).Write(8, new byte[4]).Write(0xC, BitConverter.GetBytes(fade)).Write(0x1C, BitConverter.GetBytes(0xF3u));
        frame = PublishContext(frame, temporary, context, 88);
        frame = inputs.Execute(frame, new NativeBattleEffect315DrawMesh88(temporary));
        frame = Release(frame, 88);
        if (Paused(frame, inputs))
        {
            return (frame, 0);
        }
        task = Read(frame, address).WriteWord(0x18, unchecked((ushort)(task.ReadWord(0x18) + task.ReadWord(0x1A)))).WriteWord(0xC, unchecked((ushort)(age + 1)));
        return (Write(frame, address, task), unchecked((short)(age + 1)) >= 88 ? 2 : 0);
    }
}
