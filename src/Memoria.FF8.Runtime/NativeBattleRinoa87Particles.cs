namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa87ParticleInputs(Func<uint, byte, NativeBattleVector> ReadBone502170, Func<int> Roll55CBD2, Func<NativeBattleRinoa87Frame, uint, uint, NativeBattleRinoa87Frame> Draw);

/// <summary>775720/77DFC0/77EAB0/77EC00's original phase, integration and draw order.</summary>
public static class NativeBattleRinoa87Particles
{
    public static NativeBattleRinoa87LeafStep Advance(NativeBattleRinoa87Frame frame, uint address, NativeBattleRinoa87ParticleInputs inputs)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        int phase = unchecked((sbyte)task.ReadByte(41));
        uint draw;
        switch (task.Callback)
        {
            case 0x775720:
                RequirePhase(2);
                if (phase == 0)
                {
                    task = task.WriteDword(76, 0x1557D34).WriteWord(86, unchecked((ushort)-64)).WriteWord(82, 7);
                    Next();
                }
                else if (phase == 1)
                {
                    Add(80, 1);
                    if (Word(80) > Word(82))
                    {
                        Set(80, Word(82));
                        task = task.WriteByte(38, (byte)(task.ReadByte(38) | 5));
                        Next();
                    }
                    Add(122, Word(130));
                    Set(120, Word(120) - Word(120) / 8);
                    Set(124, Word(124) - Word(124) / 8);
                    Add(28, Word(120) / 16);
                    Add(30, Word(122) / 16);
                    Add(32, Word(124) / 16);
                }
                draw = 0x775780;
                break;
            case 0x77DFC0:
                RequirePhase(4);
                if (phase == 0)
                {
                    task = task.WriteDword(48, 2048).WriteDword(56, 2048).WriteDword(52, 4096).WriteWord(80, 4096).WriteDword(76, 0x1557FBC).WriteWord(82, unchecked((ushort)-512));
                    Position(inputs.ReadBone502170(task.ReadDword(140), 241));
                    Set(30, 0);
                    Next();
                }
                else if (phase == 1)
                {
                    Add(80, -512);
                    if (Word(80) <= 0)
                    {
                        Set(80, 0);
                        Next();
                    }
                }
                else if (phase == 2)
                {
                    if (Word(36) >= 10)
                    {
                        Next();
                    }
                }
                else if (phase == 3)
                {
                    Add(80, 512);
                    if (Word(80) >= 4096)
                    {
                        Set(80, 4096);
                        task = task.WriteByte(38, (byte)(task.ReadByte(38) | 1));
                        Next();
                    }
                }
                Set(70, (Word(70) + 256) & 4095);
                NativeBattleVector point = inputs.ReadBone502170(task.ReadDword(140), 29);
                task = task.WriteDword(52, unchecked((uint)-point.Y));
                draw = 0x77E060;
                break;
            case 0x77EAB0:
            case 0x77EC00:
                RequirePhase(2);
                bool heavy = task.Callback == 0x77EC00;
                if (phase == 0)
                {
                    uint source = heavy ? 0x155B12Cu : (inputs.Roll55CBD2() & 1) != 0 ? 0x155AA5Cu : 0x155ADF4u;
                    task = task.WriteDword(76, source);
                    int scale = heavy ? inputs.Roll55CBD2() % 2047 + 2048 : inputs.Roll55CBD2() % 4095 + 256;
                    task = task.WriteDword(48, unchecked((uint)scale)).WriteDword(52, unchecked((uint)scale)).WriteDword(56, unchecked((uint)scale));
                    Set(150, -64 - (inputs.Roll55CBD2() & 63));
                    Next();
                }
                else if (phase == 1 && Word(36) >= (heavy ? 16 : 25))
                {
                    task = task.WriteByte(38, (byte)(task.ReadByte(38) | 1));
                    Next();
                }
                Add(126, heavy ? 1024 : 512);
                Set(68, (Word(68) + Word(150)) & 4095);
                Add(28, Word(124) / 16);
                Add(30, Word(126) / 16);
                Add(32, Word(128) / 16);
                draw = 0x77E060;
                break;
            default:
                throw new NotSupportedException($"Native Rinoa87 particle callback {task.Callback:X8} is not closed.");
        }
        Store();
        frame = inputs.Draw(frame, address, draw);
        hierarchy = new(frame.Effect.Pools);
        task = hierarchy.Read(address);
        Add(36, 1);
        hierarchy = hierarchy.Write(address, task);
        bool complete = (task.ReadByte(38) & 1) != 0 && task.ReadByte(40) == 0;
        if (complete)
        {
            hierarchy = hierarchy.Complete8DC530(address);
        }
        return new(frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } }, complete ? 2 : 0);

        void RequirePhase(int last)
        {
            if (phase < 0 || phase > last)
            {
                throw new InvalidDataException("Native Rinoa87 particle signed phase exceeds its actual callback table.");
            }
        }
        short Word(int offset) => unchecked((short)task.ReadWord(offset));
        void Set(int offset, int value) => task = task.WriteWord(offset, unchecked((ushort)value));
        void Add(int offset, int value) => Set(offset, Word(offset) + value);
        void Next() => task = task.WriteByte(41, unchecked((byte)(task.ReadByte(41) + 1)));
        void Store() => frame = frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task).Pools } };
        void Position(NativeBattleVector value) => task = task.WriteWord(28, unchecked((ushort)value.X)).WriteWord(30, unchecked((ushort)value.Y)).WriteWord(32, unchecked((ushort)value.Z));
    }
}
