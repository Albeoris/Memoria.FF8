namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScanResultTaskFrame(NativeBattleScanFrame Scan, NativeBattleScanResultMemory Results, NativeBattleScanTextFrame Text, ushort Complete269D93A);
public sealed record NativeBattleScanResultTaskStep(NativeBattleScanResultTaskFrame Frame, int Result);

public static class NativeBattleScanResultTask
{
    public static NativeBattleScanResultTaskStep Advance84FAF0(NativeBattleScanResultTaskFrame frame, uint address, ushort mappedHeld, NativeBattleScanResultServices services)
    {
        return Advance(frame, address, mappedHeld, services, false);
    }

    public static NativeBattleScanResultTaskStep Advance850FD0(NativeBattleScanResultTaskFrame frame, uint address, ushort mappedHeld, NativeBattleScanResultServices services)
    {
        return Advance(frame, address, mappedHeld, services, true);
    }

    private static NativeBattleScanResultTaskStep Advance(NativeBattleScanResultTaskFrame frame, uint address, ushort mappedHeld, NativeBattleScanResultServices services, bool alternate)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Scan.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback != (alternate ? 0x850FD0u : 0x84FAF0u) || task.PayloadLength != 0x78 - 12)
        {
            throw new InvalidDataException("Native Scan result window requires its matching seventy-eight-byte callback task.");
        }
        task = task.WriteDword(0x60, task.ReadDword(0x5C)).WriteDword(0x5C, mappedHeld);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                frame = frame with { Complete269D93A = 0, Results = NativeBattleScanResults.BuildB68390(frame.Results, task.ReadByte(0x2D), services) };
                task = task.WriteWord(0x6A, unchecked((byte)frame.Results.Count));
                Next();
                break;
            case 1:
                uint pointer = NativeBattleScanResults.PageB68810(frame.Results, task.ReadByte(0x2D), task.ReadByte(0x6C));
                NativeBattleScanTextWindows opened = frame.Text.Windows.SetText4A0410(1, pointer).SetFlags4A0700(1, 0).SetRect4A07A0(1, new(16, 180, 296, 28)).SetValue49FBC0(1, 0).Open4A0640(1);
                frame = frame with { Text = frame.Text with { Windows = opened } };
                task = task.WriteWord(0x66, 45);
                Next();
                break;
            case 2:
                uint held = task.ReadDword(0x5C);
                uint previous = task.ReadDword(0x60);
                if ((held & 0x40) != 0)
                {
                    Next();
                }
                else if ((previous & 0x20) == 0 && (held & 0x20) != 0)
                {
                    NextPage();
                }
                else
                {
                    task = task.WriteWord(0x66, unchecked((ushort)(task.ReadWord(0x66) - 1)));
                    if (unchecked((short)task.ReadWord(0x66)) <= 0)
                    {
                        NextPage();
                    }
                }
                break;
            case 3:
                frame = frame with { Text = frame.Text with { Windows = frame.Text.Windows.Close4A0680(1).SetText4A0410(1, 0) }, Complete269D93A = 1 };
                task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
                Next();
                break;
            case 4:
                break;
            default:
                throw new InvalidDataException("Native Scan result window signed phase exceeds its five callbacks.");
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        hierarchy = hierarchy.Write(address, task);
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        if (complete)
        {
            hierarchy = hierarchy.Complete8DC530(address);
        }
        frame = frame with { Scan = frame.Scan with { Effect = frame.Scan.Effect with { Pools = hierarchy.Pools } } };
        return new(frame, complete ? 2 : 0);

        void Next() => task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
        void NextPage()
        {
            short page = unchecked((short)(task.ReadWord(0x6C) + 1));
            bool last = page >= unchecked((short)task.ReadWord(0x6A));
            task = task.WriteWord(0x6C, !alternate && last ? (ushort)0 : unchecked((ushort)page));
            if (alternate && last)
            {
                Next();
            }
            else
            {
                task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) - 1)));
            }
        }
    }
}
