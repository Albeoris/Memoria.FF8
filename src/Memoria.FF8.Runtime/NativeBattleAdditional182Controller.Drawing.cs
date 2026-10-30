namespace Memoria.FF8.Runtime;

public static partial class NativeBattleAdditional182Controller
{
    public static (NativeBattleAdditional182Frame Frame, int Result) AdvanceMesh(NativeBattleAdditional182Frame frame, uint address, NativeBattleAdditional182Resources resources, NativeBattleAdditional182Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback == 0x5CB130)
        {
            return AdvanceHistory182(frame, address, inputs);
        }
        bool delayed = task.Callback == 0x5CA810;
        if (!delayed && task.Callback != 0x5CA6B0)
        {
            throw new NotSupportedException("Additional effect mesh callback is unsupported.");
        }
        if (delayed && Signed(task, 0x16) > 0)
        {
            if ((inputs.GraphicsFlags & 0x201) == 0)
            {
                frame = Write(frame, address, task.WriteWord(0x16, unchecked((ushort)(task.ReadWord(0x16) - 1))));
            }
            return (frame, 0);
        }
        short age = Signed(task, 12);
        int scale = Signed(task, 0x1C);
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(new(0, Signed(task, 0x18), 0)), scale, delayed ? Signed(task, 0x20) : scale, scale) with { X = Signed(task, 0x10), Y = 0, Z = Signed(task, 0x14) };
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local), null));
        uint source = delayed ? resources.MeshVariants[Signed(task, 14)].Address : 0xD3B3D8u;
        (frame, uint temporary) = Allocate(frame, 88);
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(source)).Write(8, BitConverter.GetBytes(0u)).Write(0x1C, BitConverter.GetBytes(delayed ? 0x33u : 0xF3u));
        if (!delayed)
        {
            int cue = age < 8 ? (24 - age) << 8 : age >= 26 ? (age - 18) << 8 : 2048;
            context = context.Write(12, BitConverter.GetBytes(cue));
        }
        frame = PublishContext(frame, temporary, context, 88);
        frame = inputs.Execute(frame, new NativeBattleAdditional182DrawStatic88(temporary, source) { PilotIdentity = inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(182, task) with { ResourceGeneration = source } : null });
        frame = Release(frame, 88);
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = Read(frame, address);
        task = task.WriteWord(0x18, unchecked((ushort)(task.ReadWord(0x18) + Signed(task, 0x1A))));
        if (delayed)
        {
            if (age < 10)
            {
                task = task.WriteWord(0x20, unchecked((ushort)(task.ReadWord(0x20) + Signed(task, 0x22))));
            }
            else if (age >= 14)
            {
                task = task.WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) - Signed(task, 0x1E))));
            }
        }
        else
        {
            short speed = Signed(task, 0x1E);
            task = task.WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) + speed))).WriteWord(0x1E, unchecked((ushort)(speed + speed / 32)));
        }
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 12) >= (delayed ? 24 : 34) ? 2 : 0);
    }
}
