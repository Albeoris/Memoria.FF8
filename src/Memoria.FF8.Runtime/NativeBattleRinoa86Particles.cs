namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa86ParticleInputs(Func<uint, byte, NativeBattleVector> ReadBone502170, short OriginB8B7F4, Func<int> Roll55CBD2, Func<NativeBattleRinoa86Frame, uint, uint, NativeBattleRinoa86Frame> Draw);

/// <summary>783350/783DC0/782930/780640's physical particle lifecycles and pre-draw ordering.</summary>
public static class NativeBattleRinoa86Particles
{
    public static NativeBattleRinoa86LeafStep Advance(NativeBattleRinoa86Frame frame, uint address, NativeBattleRinoa86ParticleInputs inputs)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        int phase = unchecked((sbyte)task.ReadByte(41));
        if (phase is < 0 or > 2)
        {
            throw new InvalidDataException("Native Rinoa86 particle signed phase exceeds its three callbacks.");
        }
        uint draw;
        switch (task.Callback)
        {
            case 0x783350:
                if (phase == 0)
                {
                    FollowBody(false);
                    task = task.WriteWord(86, 0).WriteWord(84, 0).WriteDword(76, 0x1561920).WriteWord(88, 128).WriteWord(90, 128).WriteDword(48, 4096).WriteDword(52, 4096).WriteDword(56, 4096).WriteWord(80, 4096);
                    Next();
                }
                else if (phase == 1)
                {
                    task = task.WriteWord(80, unchecked((ushort)(task.ReadWord(80) - 256)));
                    if (Signed(80) < 2048)
                    {
                        task = task.WriteWord(80, 2048);
                    }
                    FollowBody(false);
                    CompleteAt16();
                }
                Scroll();
                draw = 0x7833D0;
                break;
            case 0x783DC0:
                if (phase == 0)
                {
                    FollowBody(true);
                    task = task.WriteWord(86, 128).WriteWord(88, 128).WriteWord(90, 128).WriteDword(48, 4096).WriteDword(52, 4096).WriteDword(56, 4096).WriteDword(76, 0x15638C8).WriteWord(70, 2048).WriteWord(84, 0);
                    Next();
                }
                else if (phase == 1)
                {
                    FollowBody(true);
                    CompleteAt16();
                }
                Scroll();
                draw = 0x7833D0;
                break;
            case 0x782930:
                if (phase == 0)
                {
                    task = task.WriteDword(76, 0x156177C).WriteWord(86, unchecked((ushort)-64)).WriteWord(82, 7);
                    Next();
                }
                else if (phase == 1)
                {
                    task = task.WriteWord(80, unchecked((ushort)(task.ReadWord(80) + 1)));
                    if (Signed(80) > Signed(82))
                    {
                        task = task.WriteByte(38, (byte)(task.ReadByte(38) | 5)).WriteWord(80, task.ReadWord(82));
                        Next();
                    }
                    short vx = unchecked((short)(Signed(120) - Signed(120) / 8));
                    short vz = unchecked((short)(Signed(124) - Signed(124) / 8));
                    task = task.WriteWord(122, unchecked((ushort)(task.ReadWord(122) + task.ReadWord(130)))).WriteWord(120, unchecked((ushort)vx)).WriteWord(124, unchecked((ushort)vz));
                    task = task.WriteWord(28, unchecked((ushort)(task.ReadWord(28) + vx / 16))).WriteWord(30, unchecked((ushort)(task.ReadWord(30) + Signed(122) / 16))).WriteWord(32, unchecked((ushort)(task.ReadWord(32) + vz / 16)));
                }
                draw = 0x782990;
                break;
            case 0x780640:
                if (phase == 0)
                {
                    task = task.WriteDword(76, 0x15618C0).WriteDword(48, 4096).WriteDword(52, 4096).WriteDword(56, 4096).WriteWord(72, unchecked((ushort)(inputs.Roll55CBD2() & 4095)));
                    Next();
                }
                else if (phase == 1)
                {
                    task = Advance782850(task);
                }
                draw = 0x7806A0;
                break;
            default:
                throw new NotSupportedException($"Native Rinoa86 particle callback {task.Callback:X8} has no original selected leaf.");
        }
        hierarchy = hierarchy.Write(address, task);
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        if ((task.ReadByte(38) & 4) == 0)
        {
            frame = inputs.Draw(frame, address, draw);
        }
        hierarchy = new(frame.Effect.Pools);
        task = hierarchy.Read(address).WriteWord(36, unchecked((ushort)(task.ReadWord(36) + 1)));
        hierarchy = hierarchy.Write(address, task);
        bool complete = (task.ReadByte(38) & 1) != 0 && task.ReadByte(40) == 0;
        if (complete)
        {
            hierarchy = hierarchy.Complete8DC530(address);
        }
        return new(frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } }, complete ? 2 : 0);

        short Signed(int offset) => unchecked((short)task.ReadWord(offset));
        void Next() => task = task.WriteByte(41, unchecked((byte)(task.ReadByte(41) + 1)));
        void CompleteAt16()
        {
            if (Signed(36) >= 16)
            {
                task = task.WriteByte(38, (byte)(task.ReadByte(38) | 1));
                Next();
            }
        }
        void Scroll() => task = task.WriteWord(146, (ushort)((task.ReadByte(146) - 16) & 127));
        void FollowBody(bool contact)
        {
            NativeBattleVector point = inputs.ReadBone502170(task.ReadDword(140), 241);
            task = task.WriteWord(28, unchecked((ushort)point.X)).WriteWord(30, unchecked((ushort)(contact ? point.Y + 64 : -256))).WriteWord(32, unchecked((ushort)(contact ? point.Z : inputs.OriginB8B7F4 + 1536)));
        }
    }

    public static NativeBattleModulePoolTask Advance782850(NativeBattleModulePoolTask task)
    {
        task = task.WriteWord(72, unchecked((ushort)(task.ReadWord(72) + 64)));
        if (Signed(30) >= 0)
        {
            task = task.WriteWord(30, 0);
        }
        if (Signed(36) >= 10)
        {
            int scale = unchecked((int)task.ReadDword(56) - 1024);
            if (scale <= 0)
            {
                scale = 16;
                task = task.WriteByte(38, (byte)(task.ReadByte(38) | 1)).WriteByte(41, unchecked((byte)(task.ReadByte(41) + 1)));
            }
            task = task.WriteDword(56, unchecked((uint)scale)).WriteDword(52, unchecked((uint)scale)).WriteDword(48, unchecked((uint)scale));
        }
        short vx = unchecked((short)(Signed(124) - Signed(124) / 16));
        short vy = unchecked((short)(Signed(126) + 128));
        vy = unchecked((short)(vy - vy / 16));
        short vz = unchecked((short)(Signed(128) - Signed(128) / 16));
        task = task.WriteWord(124, unchecked((ushort)vx)).WriteWord(126, unchecked((ushort)vy)).WriteWord(128, unchecked((ushort)vz));
        return task.WriteWord(28, unchecked((ushort)(task.ReadWord(28) + vx / 16))).WriteWord(30, unchecked((ushort)(task.ReadWord(30) + vy / 16))).WriteWord(32, unchecked((ushort)(task.ReadWord(32) + vz / 16)));

        short Signed(int offset) => unchecked((short)task.ReadWord(offset));
    }
}
