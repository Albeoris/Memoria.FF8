namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSiren95WaveServices(NativeBattleActionPacketsState Packets, Func<NativeBattleSiren95Frame, uint, NativeBattleSiren95Frame> Draw);

public static class NativeBattleSiren95Waves
{
    public static NativeBattleSiren95TaskStep Advance(NativeBattleSiren95Frame frame, uint address, NativeBattleSiren95WaveServices services)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int phase = unchecked((sbyte)task.ReadByte(0x29));
        if (task.Callback == 0x745250)
        {
            switch (phase)
            {
                case 0:
                    frame = Put(frame, address, task);
                    var child = NativeBattleSiren95Actors.AppendTask(frame, 0x2585EF0, 0x7452F0, 0x64, address, services.Packets);
                    frame = child.Frame;
                    task = Read(frame, address);
                    frame = Put(frame, child.Address, Read(frame, child.Address).WriteWord(0x60, task.ReadWord(0x60)));
                    task = task.WriteWord(0x60, unchecked((ushort)(task.ReadWord(0x60) + 1)));
                    task = task.WriteWord(0x5C, (ushort)((Random() & 3) + 4));
                    Next();
                    break;
                case 1:
                    task = task.WriteWord(0x5C, unchecked((ushort)(task.ReadWord(0x5C) - 1)));
                    NativeBattleSiren95State state = State();
                    if (unchecked((short)state.ReadWord((state.DirectorAddress1533010 ?? throw new NotSupportedException("Native Siren waves require their director pointer.")) + 0x40)) >= 7)
                    {
                        task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
                        Next();
                    }
                    else if (unchecked((short)task.ReadWord(0x5C)) <= 0)
                    {
                        task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) - 1)));
                    }
                    break;
                case 2:
                    break;
                default:
                    Unsupported();
                    break;
            }
        }
        else if (task.Callback == 0x7452F0)
        {
            switch (phase)
            {
                case 0:
                    task = task.WriteDword(0x4C, State().ReadDword(unchecked(State().ReadDword(0x258EAA0) + 0x144))).WriteWord(0x52, 0xFE00);
                    bool odd = (task.ReadByte(0x60) & 1) != 0;
                    if ((Random() & 1) != 0)
                    {
                        task = task.WriteWord(0x1C, unchecked((ushort)(Random() % 600 - 300)));
                        int y = Random() % 30 + 120;
                        task = task.WriteWord(0x1E, unchecked((ushort)(odd ? -y : y)));
                    }
                    else
                    {
                        int x = Random() % 150 + 150;
                        task = task.WriteWord(0x1C, unchecked((ushort)(odd ? -x : x))).WriteWord(0x1E, unchecked((ushort)(Random() % 240 - 120)));
                    }
                    uint camera = State().CameraAddress15339D0 ?? throw new NotSupportedException("Native Siren waves require their camera control pointer.");
                    task = task.WriteWord(0x20, unchecked((ushort)(State().ReadWord(camera + 0xE0) << 1)));
                    int speed = Random() % 3 + 2;
                    if ((Random() & 1) != 0)
                    {
                        speed = -speed;
                    }
                    task = task.WriteWord(0x54, unchecked((ushort)speed));
                    uint scale = (uint)(Random() % 4095 + 1024);
                    task = task.WriteDword(0x30, scale).WriteDword(0x34, scale).WriteDword(0x38, scale).WriteWord(0x48, (ushort)(Random() & 4095));
                    task = task.WriteWord(0x50, 4096).WriteWord(0x5E, (ushort)((Random() & 15) + 16));
                    Next();
                    break;
                case 1:
                    task = task.WriteWord(0x50, unchecked((ushort)(task.ReadWord(0x50) - 512)));
                    if (unchecked((short)task.ReadWord(0x50)) <= 0)
                    {
                        task = task.WriteWord(0x50, 0).WriteWord(0x5C, (ushort)((Random() & 3) + 4));
                        Next();
                    }
                    break;
                case 2:
                    task = task.WriteWord(0x5C, unchecked((ushort)(task.ReadWord(0x5C) - 1)));
                    if (unchecked((short)task.ReadWord(0x5C)) <= 0)
                    {
                        Next();
                    }
                    break;
                case 3:
                    task = task.WriteWord(0x50, unchecked((ushort)(task.ReadWord(0x50) + 512)));
                    if (unchecked((short)task.ReadWord(0x50)) >= 4096)
                    {
                        task = task.WriteWord(0x50, 4096).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
                        Next();
                    }
                    break;
                case 4:
                    break;
                default:
                    Unsupported();
                    break;
            }
            task = task.WriteWord(0x48, (ushort)((task.ReadWord(0x48) + task.ReadWord(0x5E)) & 4095)).WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) + task.ReadWord(0x54))));
            frame = services.Draw(Put(frame, address, task), address);
            task = Read(frame, address);
        }
        else
        {
            throw new InvalidDataException("Native Siren wave task requires its original callback.");
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        return new(Put(frame, address, task), (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0);

        ushort Random()
        {
            frame = frame with { Random = frame.Random.Draw(out ushort random) };
            return random;
        }
        void Next() => task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
        void Unsupported() => throw new NotSupportedException($"Native Siren wave callback 0x{task.Callback:X8}, signed phase {phase} is unsupported.");
        NativeBattleSiren95State State() => frame.Memory.Siren95 ?? throw new NotSupportedException("Native Siren waves require their retained module bytes.");
    }
    public static NativeBattleActionMatrix Matrix745380(NativeBattleModulePoolTask task)
    {
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770();
        matrix = NativeBattleCuragaMatrices.RotateY8DD8A0(matrix, unchecked((short)task.ReadWord(0x46)));
        matrix = NativeBattleCuragaMatrices.RotateX8DD7E0(matrix, unchecked((short)task.ReadWord(0x44)));
        matrix = NativeBattleCuragaMatrices.RotateZ8DD960(matrix, unchecked((short)task.ReadWord(0x48)));
        matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, unchecked((int)task.ReadDword(0x30)), unchecked((int)task.ReadDword(0x34)), unchecked((int)task.ReadDword(0x38)));
        return matrix with { X = unchecked((short)task.ReadWord(0x1C)), Y = unchecked((short)task.ReadWord(0x1E)), Z = unchecked((short)task.ReadWord(0x20)) };
    }

    private static NativeBattleModulePoolTask Read(NativeBattleSiren95Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
    private static NativeBattleSiren95Frame Put(NativeBattleSiren95Frame frame, uint address, NativeBattleModulePoolTask task) => frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
}
