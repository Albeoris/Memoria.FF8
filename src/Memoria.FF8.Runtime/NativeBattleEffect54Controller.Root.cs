using static Memoria.FF8.Runtime.NativeBattleEffect54Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect54Controller
{
    private static NativeBattleEffect54Step AdvanceRoot(NativeBattleEffect54Frame frame, uint address, NativeBattleEffect54Resources resources, NativeBattleEffect54Inputs inputs)
    {
        frame = WriteBytes(frame, 0x2793E58, PackedArena(inputs.ReadView(frame)), 32);
        frame = frame with { State = frame.State.WriteDword(ScratchPointerGlobal, 0x2793E58).WriteDword(ViewPointerGlobal, 0x2793E58) };
        bool odd = (Read(frame, address).ReadWord(0x5C) & 1) != 0;
        frame = frame with { State = frame.State.WriteDword(PrimaryCursorGlobal, frame.State.ReadDword(odd ? OddPrimaryPacketGlobal : EvenPrimaryPacketGlobal)).WriteDword(SecondaryCursorGlobal, frame.State.ReadDword(odd ? OddSecondaryPacketGlobal : EvenSecondaryPacketGlobal)) };
        frame = inputs.Execute(frame, new NativeBattleEffect54UpdateCaster8DC740(address));
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
            case 1:
            case 7:
            case 8:
                frame = IncrementPhase(frame, address);
                break;
            case 2:
                if (task.ReadByte(0x28) == 0)
                {
                    uint first = frame.State.ReadDword(AllocationCursorGlobal);
                    uint second = checked(first + 0xCA8);
                    frame = frame with { State = frame.State.WriteDword(EmitterRecordsGlobal, first).WriteDword(DrawRecordsGlobal, second).WriteDword(AllocationCursorGlobal, checked(second + 0x9D80)) };
                    frame = ClearBytes(ClearBytes(frame, first, 0xCA8), second, 0x9D80);
                    frame = frame with { State = frame.State.WriteWord(EmitterHintGlobal, 0).WriteWord(DrawHintGlobal, 0).WriteWord(DrawCounter, 0).WriteWord(EmitterCounter, 0) };
                    (frame, _) = AppendHierarchy(frame, 3, FadeInCallback, 64, address, inputs);
                    frame = IncrementPhase(frame, address);
                }
                break;
            case 3:
                frame = Write(frame, address, task.WriteByte(0x63, 1));
                (frame, _) = AppendHierarchy(frame, 1, DriverCallback, 88, address, inputs);
                frame = IncrementPhase(frame, address);
                break;
            case 4:
                if (task.ReadByte(0x63) == 0)
                {
                    sbyte group = unchecked((sbyte)task.ReadByte(0x2A));
                    frame = group < Signed(task, 0x58) ? Write(frame, address, task.WriteByte(0x2A, unchecked((byte)(group + 1))).WriteByte(0x2E, unchecked((byte)(task.ReadByte(0x2E) + 1))).WriteByte(0x29, 3)) : Write(frame, address, task.WriteWord(0x60, 15).WriteByte(0x29, 5));
                }
                break;
            case 5:
                task = task.WriteWord(0x60, unchecked((ushort)(task.ReadWord(0x60) - 1)));
                frame = Write(frame, address, task);
                if (Signed(task, 0x60) <= 0)
                {
                    (frame, _) = AppendHierarchy(frame, 3, FadeOutCallback, 64, address, inputs);
                    frame = IncrementPhase(frame, address);
                }
                break;
            case 6:
                if (task.ReadWord(0x5E) == 0)
                {
                    frame = IncrementPhase(frame, address);
                }
                break;
            case 9:
                frame = Write(frame, address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                frame = IncrementPhase(frame, address);
                break;
            case 10:
                break;
            default:
                throw new InvalidDataException("Effect54 root signed phase leaves its eleven native callbacks.");
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(0x5E, 0));
        frame = frame with { State = frame.State.WriteWord(EmitterVisitCounter, 0).WriteWord(DrawVisitCounter, 0) };
        for (int index = 1; index < 4; index++)
        {
            (frame, int survivors) = Scan(frame, index, resources, inputs);
            frame = Write(frame, address, Read(frame, address).WriteWord(0x5E, unchecked((ushort)(Read(frame, address).ReadWord(0x5E) + survivors))));
        }
        task = Read(frame, address);
        frame = Write(frame, address, task.WriteWord(0x5C, unchecked((ushort)(task.ReadWord(0x5C) + 1))).WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1))));
        return Complete(frame, address);
    }

    private static NativeBattleEffect54Step AdvanceFade(NativeBattleEffect54Frame frame, uint address, bool outgoing, NativeBattleEffect54Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int phase = unchecked((sbyte)task.ReadByte(0x29));
        if (phase == 0)
        {
            frame = Write(frame, address, task.WriteWord(0x1C, outgoing ? (ushort)1024 : (ushort)0).WriteByte(0x29, 1));
            if (!outgoing)
            {
                for (int group = 0; group < 4; group++)
                {
                    frame = inputs.Execute(frame, new NativeBattleEffect54StageTransition(group, 0, true));
                }
            }
        }
        else if (phase == 1)
        {
            short value = unchecked((short)(Signed(task, 0x1C) + (outgoing ? -256 : 256)));
            bool done = outgoing ? value <= 0 : value >= 1024;
            if (done)
            {
                value = outgoing ? (short)0 : (short)1024;
                task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)).WriteByte(0x29, 2);
            }
            frame = Write(frame, address, task.WriteWord(0x1C, unchecked((ushort)value)));
            for (int group = 0; group < 4; group++)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect54StageTransition(group, unchecked((ushort)value), false));
            }
        }
        else if (phase != 2)
        {
            throw new InvalidDataException("Effect54 fade signed phase leaves its native callback table.");
        }
        task = Read(frame, address);
        frame = Write(frame, address, task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1))));
        return Complete(frame, address);
    }

    private static NativeBattleEffect54Step AdvanceDriver(NativeBattleEffect54Frame frame, uint address, NativeBattleEffect54Resources resources, NativeBattleEffect54Inputs inputs)
    {
        frame = inputs.Execute(frame, new NativeBattleEffect54UpdateTarget8DC610(address));
        frame = inputs.Execute(frame, new NativeBattleEffect54UpdateBounds8DC870(address));
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                (frame, uint child) = AppendHierarchy(frame, 2, ModelCallback, 676, address, inputs);
                frame = Write(frame, child, Read(frame, child).WriteDword(0x30, SourceAddress).WriteWord(0x298, 0).WriteWord(0x29C, 0).WriteWord(0x29E, 45));
                frame = IncrementPhase(frame, address);
                break;
            case 1:
                if (task.ReadWord(0x24) == 30)
                {
                    uint root = task.ReadDword(0x10);
                    frame = Write(frame, root, Read(frame, root).WriteByte(0x63, 0));
                    frame = IncrementPhase(frame, address);
                }
                break;
            case 2:
                if (Signed(task, 0x24) >= 30)
                {
                    NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
                    int header = Parameters(frame).HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A));
                    int contact = checked((packets.Headers[header].FirstContactIndex ?? throw new NotSupportedException("Effect54 requires its current contact pointer.")) + unchecked((sbyte)task.ReadByte(0x2B)));
                    frame = inputs.Execute(frame, new NativeBattleEffect54Contact506690(contact));
                    task = Read(frame, address);
                    frame = Write(frame, address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                    frame = IncrementPhase(frame, address);
                }
                break;
            case 3:
                break;
            default:
                throw new InvalidDataException("Effect54 driver signed phase leaves its four callbacks.");
        }
        task = Read(frame, address);
        if (task.ReadWord(0x24) == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect54Sound501330(SoundAddress, 0, 128));
        }
        task = Read(frame, address);
        frame = Write(frame, address, task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1))));
        return Complete(frame, address);
    }

    private static NativeBattleEffect54Step AdvanceModel(NativeBattleEffect54Frame frame, uint address, NativeBattleEffect54Resources resources, NativeBattleEffect54Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                if (Signed(task, 0x24) >= Signed(task, 0x298))
                {
                    frame = NativeBattleEffect54Lower.Initialize80D090(frame, address, resources, inputs);
                    frame = NativeBattleEffect54Lower.Advance80DAE0(frame, address, resources, inputs).Frame;
                    frame = IncrementPhase(frame, address);
                }
                break;
            case 1:
                NativeBattleEffect54Step advanced = NativeBattleEffect54Lower.Advance80DAE0(frame, address, resources, inputs);
                frame = advanced.Frame;
                if (advanced.Result != 0)
                {
                    task = Read(frame, address);
                    frame = Write(frame, address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                    frame = IncrementPhase(frame, address);
                }
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Effect54 model signed phase leaves its three callbacks.");
        }
        task = Read(frame, address);
        frame = Write(frame, address, task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1))));
        return Complete(frame, address);
    }
}
