namespace Memoria.FF8.Runtime;

public static class NativeBattleSiren95Decorations
{
    public static NativeBattleSiren95TaskStep AdvanceEmitter(NativeBattleSiren95Frame frame, uint address, NativeBattleActionPacketsState packets)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool first = task.Callback == 0x743C70;
        bool secondSurface = task.Callback == 0x744450;
        if (!first && !secondSurface && task.Callback != 0x744060)
        {
            throw new InvalidDataException("Native Siren emitter requires its actual selected callback.");
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                uint director = Director(frame);
                task = task.WriteDword(0x1C, State(frame).ReadDword(director + 8)).WriteDword(0x20, State(frame).ReadDword(director + 0xC));
                task = task.WriteWord(0x20, unchecked((ushort)(task.ReadWord(0x20) + (secondSurface ? 0xF000 : 0xE000)))).WriteWord(0x1E, 0);
                frame = Next(Write(frame, address, task), address);
                break;
            case 1:
                frame = Next(frame, address);
                break;
            case 2:
                int count = secondSurface ? 3 : first ? (task.ReadWord(0x24) & 1) != 0 ? 1 : 0 : 5;
                for (int index = 0; index < count; index++)
                {
                    var appended = NativeBattleSiren95Actors.AppendTask(frame, 0x257FA90, secondSurface ? 0x744570u : first ? 0x743DD0u : 0x7441D0u, 0xE0, address, packets);
                    frame = appended.Frame;
                    NativeBattleModulePoolTask child = Read(frame, appended.Address);
                    for (int corner = 0; corner < 4; corner++)
                    {
                        frame = frame with { Random = frame.Random.Draw(out ushort segment) };
                        child = child.WriteWord(0xCC + corner * 2, (ushort)(segment % (secondSurface ? 24 : 16)));
                        frame = frame with { Random = frame.Random.Draw(out ushort fraction) };
                        child = child.WriteWord(0xD4 + corner * 2, (ushort)(fraction & 15));
                        if (!secondSurface)
                        {
                            frame = frame with { Random = frame.Random.Draw(out ushort angle) };
                            int phase = angle & 4095;
                            child = child.WriteWord(0xAE + corner * 8, unchecked((ushort)(NativeBattleCameraMath.Sine(phase) / 32))).WriteWord(0xB0 + corner * 8, unchecked((ushort)(NativeBattleCameraMath.Cosine(phase) / 32)));
                        }
                    }
                    frame = Write(frame, appended.Address, child);
                }
                if (Spawned(frame) >= (secondSurface ? 3 : 2))
                {
                    task = Read(frame, address);
                    frame = Next(Write(frame, address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1))), address);
                }
                break;
            case 3:
                break;
            default:
                throw new NotSupportedException($"Native Siren emitter signed phase {unchecked((sbyte)task.ReadByte(0x29))} is unsupported.");
        }
        return Finish(frame, address);
    }

    public static NativeBattleSiren95TaskStep AdvanceChild(NativeBattleSiren95Frame frame, uint address, Func<NativeBattleSiren95Frame, uint, NativeBattleSiren95Frame> draw)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool first = task.Callback == 0x743DD0;
        bool secondSurface = task.Callback == 0x744570;
        if (!first && !secondSurface && task.Callback != 0x7441D0)
        {
            throw new InvalidDataException("Native Siren decoration requires its actual selected callback.");
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                task = task.WriteDword(0x4C, 0x1533024);
                if (first)
                {
                    task = task.WriteWord(0x56, unchecked((ushort)-48));
                }
                frame = frame with { Random = frame.Random.Draw(out ushort random) };
                task = task.WriteWord(0x50, (ushort)(random & 7));
                if (!first)
                {
                    task = task.WriteWord(0x56, unchecked((ushort)(secondSurface ? -16 : -64)));
                }
                task = task.WriteWord(0x52, 15);
                if (Spawned(frame) >= (secondSurface ? 3 : 2))
                {
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 4));
                }
                frame = Next(Write(frame, address, task), address);
                break;
            case 1:
                ushort phase = unchecked((ushort)(task.ReadWord(0x50) + 1));
                task = task.WriteWord(0x50, phase);
                bool completed = unchecked((short)phase) > unchecked((short)task.ReadWord(0x52));
                if (completed)
                {
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 4)).WriteWord(0x50, task.ReadWord(0x52));
                }
                if (completed || Spawned(frame) >= (secondSurface ? 3 : 2))
                {
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5));
                    frame = Next(Write(frame, address, task), address);
                }
                else
                {
                    frame = Write(frame, address, task);
                }
                break;
            case 2:
                break;
            default:
                throw new NotSupportedException($"Native Siren decoration signed phase {unchecked((sbyte)task.ReadByte(0x29))} is unsupported.");
        }
        NativeBattleSiren95State state = State(frame);
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), 2048);
        matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, unchecked((int)state.ReadDword(0x258EC60)), unchecked((int)state.ReadDword(0x258EC64)), unchecked((int)state.ReadDword(0x258EC68)));
        matrix = matrix with
        {
            X = unchecked((short)state.ReadWord(0x258FB80)),
            Y = unchecked((short)state.ReadWord(0x258FB82)) - (first ? 128 : 0),
            Z = unchecked((short)state.ReadWord(Director(frame) + 0xC)) + unchecked((short)state.ReadWord(0x258FB84)) - (secondSurface ? 4096 : 8192)
        };
        uint cache = secondSurface ? 0x258BAA0u : first ? 0x258E9A0u : 0x258E8A0u;
        task = Read(frame, address);
        for (int corner = 0; corner < 4; corner++)
        {
            int segment = unchecked((short)task.ReadWord(0xCC + corner * 2));
            int fraction = unchecked((short)task.ReadWord(0xD4 + corner * 2));
            short[] xyz = new short[3];
            for (int axis = 0; axis < 3; axis++)
            {
                uint source = unchecked(cache + (uint)(segment * 8 + axis * 2));
                int start = unchecked((short)state.ReadWord(source));
                int end = unchecked((short)state.ReadWord(unchecked(source + 8)));
                int product = unchecked((end - start) * fraction);
                short point = unchecked((short)(start + product / 16));
                xyz[axis] = secondSurface ? point : unchecked((short)(point + unchecked((short)task.ReadWord(0xAE + corner * 8 + axis * 2))));
            }
            NativeBattleVector transformed = NativeBattleGfMatrices.Transform460840(matrix, new(xyz[0], xyz[1], xyz[2]));
            task = task.WriteWord(0x6C + corner * 8, unchecked((ushort)transformed.X)).WriteWord(0x6E + corner * 8, unchecked((ushort)transformed.Y)).WriteWord(0x70 + corner * 8, unchecked((ushort)transformed.Z));
        }
        frame = Write(frame, address, task);
        for (int corner = 0; corner < 4; corner++)
        {
            task = Read(frame, address);
            task = task.WriteDword(0x1C, task.ReadDword(0x6C + corner * 8)).WriteDword(0x20, task.ReadDword(0x70 + corner * 8));
            frame = draw(Write(frame, address, task), address);
        }
        return Finish(frame, address);
    }

    private static NativeBattleSiren95TaskStep Finish(NativeBattleSiren95Frame frame, uint address)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        return new(Write(frame, address, task), (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0);
    }

    private static short Spawned(NativeBattleSiren95Frame frame) => unchecked((short)State(frame).ReadWord(Director(frame) + 0x40));
    private static uint Director(NativeBattleSiren95Frame frame) => State(frame).DirectorAddress1533010 ?? throw new NotSupportedException("Native Siren decoration requires its current director pointer.");
    private static NativeBattleModulePoolTask Read(NativeBattleSiren95Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
    private static NativeBattleSiren95Frame Write(NativeBattleSiren95Frame frame, uint address, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
    private static NativeBattleSiren95Frame Next(NativeBattleSiren95Frame frame, uint address) => Write(frame, address, Read(frame, address).WriteByte(0x29, unchecked((byte)(Read(frame, address).ReadByte(0x29) + 1))));
    private static NativeBattleSiren95State State(NativeBattleSiren95Frame frame) => frame.Memory.Siren95 ?? throw new NotSupportedException("Native Siren decoration requires its actual module owner.");
}
