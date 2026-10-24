using static Memoria.FF8.Runtime.NativeBattleEffect80Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect80Controller
{
    public static NativeBattleEffect80Step AdvanceControl(NativeBattleEffect80Frame frame, uint address, NativeBattleEffect80Resources resources, NativeBattleEffect80Inputs inputs)
    {
        frame = frame with { State = frame.State.WriteWord(TargetAddress + 0x46, unchecked((ushort)(frame.State.ReadWord(TargetAddress + 0x46) + 1))) };
        if (frame.State.ReadWord(TargetAddress + 0x40) != frame.State.ReadWord(TargetAddress + 0x42))
        {
            frame = frame with { State = frame.State.WriteWord(TargetAddress + 0x40, frame.State.ReadWord(TargetAddress + 0x42)).WriteWord(TargetAddress + 0x46, 0) };
            frame = BirthControlState7ABE80(frame, address, inputs);
        }
        if (frame.State.ReadWord(TargetAddress + 0x42) != frame.State.ReadWord(TargetAddress + 0x44))
        {
            frame = frame with { State = frame.State.WriteWord(TargetAddress + 0x42, frame.State.ReadWord(TargetAddress + 0x44)) };
        }
        switch (Phase(frame, address))
        {
            case 0:
                frame = inputs.Execute(frame, new NativeBattleEffect80Vibration4A29A0(VibrationAddress, 1, 0x80, TargetAddress + 0x52));
                frame = NextPhase(frame, address);
                break;
            case 1:
            case 4:
            case 7:
            case 10:
                short request = Phase(frame, address) switch { 1 => 1, 4 => 2, 7 => 3, _ => 4 };
                (frame, bool requested) = RequestControlState7ABC40(frame, request);
                if (requested)
                {
                    if (request == 4)
                    {
                        frame = PublishPartyVisibility(frame, true, inputs);
                        frame = inputs.Execute(frame, new NativeBattleEffect80StopVibration4A2940(unchecked((short)frame.State.ReadWord(TargetAddress + 0x52))));
                    }
                    frame = NextPhase(frame, address);
                }
                break;
            case 2:
                if (unchecked((short)frame.State.ReadWord(TargetAddress + 0x40)) >= 1)
                {
                    frame = PublishPartyVisibility(frame, false, inputs);
                    frame = NextPhase(frame, address);
                }
                break;
            case 3:
            case 6:
            case 9:
                int threshold = Phase(frame, address) switch { 3 => 0x2D, 6 => 0x76, _ => 0xA0 };
                if (unchecked((short)frame.State.ReadWord(TargetAddress + 0x46)) >= threshold)
                {
                    if (Phase(frame, address) == 9)
                    {
                        frame = frame with { State = frame.State.WriteWord(TargetAddress + 0x48, 1) };
                    }
                    frame = NextPhase(frame, address);
                }
                break;
            case 5:
            case 8:
                short state = Phase(frame, address) == 5 ? (short)2 : (short)3;
                if (unchecked((short)frame.State.ReadWord(TargetAddress + 0x40)) >= state)
                {
                    frame = NextPhase(frame, address);
                }
                break;
            case 11:
                frame = NextPhase(MarkComplete(frame, address), address);
                break;
            case 12:
                break;
            default:
                throw new InvalidDataException("Effect80 control signed phase leaves its thirteen actual handlers.");
        }
        return Finish(frame, address);
    }

    public static (NativeBattleEffect80Frame Frame, bool Requested) RequestControlState7ABC40(NativeBattleEffect80Frame frame, short value)
    {
        ushort current = frame.State.ReadWord(TargetAddress + 0x40);
        if (frame.State.ReadWord(TargetAddress + 0x44) != current || unchecked((short)current) != value - 1)
        {
            return (frame, false);
        }
        return (frame with { State = frame.State.WriteWord(TargetAddress + 0x44, unchecked((ushort)value)) }, true);
    }

    private static NativeBattleEffect80Frame BirthControlState7ABE80(NativeBattleEffect80Frame frame, uint address, NativeBattleEffect80Inputs inputs)
    {
        switch (unchecked((short)frame.State.ReadWord(TargetAddress + 0x40)))
        {
            case 1:
                (frame, _) = AppendHierarchy(frame, 5, 0x7AF390, 0xA0, address, inputs);
                (frame, _) = AppendHierarchy(frame, 5, 0x7AF1D0, 0xA0, address, inputs);
                break;
            case 2:
                (frame, _) = SpawnAct7ABF40(frame, address, 0x7ABF90, 0x1754B2C, 0xC4, 0, 1, inputs);
                (frame, _) = AppendHierarchy(frame, 5, 0x7AEFA0, 0xA0, address, inputs);
                break;
            case 3:
                (frame, _) = AppendHierarchy(frame, 5, 0x7AE690, 0xA0, address, inputs);
                (frame, _) = AppendHierarchy(frame, 5, 0x7AF2C0, 0xA0, address, inputs);
                break;
        }
        return frame;
    }

    private static NativeBattleEffect80Frame PublishPartyVisibility(NativeBattleEffect80Frame frame, bool restore, NativeBattleEffect80Inputs inputs)
    {
        for (int index = 0; index < 3; index++)
        {
            ushort flags = inputs.ReadModel(frame, index).Flags0;
            if ((flags & 2) == 0)
            {
                continue;
            }
            if (!restore)
            {
                frame = frame with { State = frame.State.WriteDword(TargetAddress + 0x20u + 4u * (uint)index, flags) };
            }
            ushort bit = restore ? (ushort)(frame.State.Read(TargetAddress + 0x20u + 4u * (uint)index, 1)[0] & 4) : (ushort)4;
            frame = inputs.Execute(frame, new NativeBattleEffect80TargetFlags(PhysicalModelAddress(index), restore ? (ushort)0xFFFB : (ushort)0xFFFF, bit, true));
        }
        return frame;
    }

    public static (NativeBattleEffect80Frame Frame, uint Address) SpawnAct7ABF40(NativeBattleEffect80Frame frame, uint parent, uint callback, uint source, short size, ushort selector, ushort mode, NativeBattleEffect80Inputs inputs)
    {
        (frame, uint address) = AppendHierarchy(frame, 4, callback, 0x180, parent, inputs);
        return (Write(frame, address, Read(frame, address).WriteDword(0x74, source).WriteDword(0x78, unchecked((uint)size)).WriteWord(0x80, selector).WriteWord(0x82, mode)), address);
    }

    public static (NativeBattleEffect80Frame Frame, uint Address) SpawnNode7B0A90(NativeBattleEffect80Frame frame, uint parent, uint source, ushort delay, ushort selector, ushort mode, NativeBattleEffect80Inputs inputs)
    {
        (frame, uint address) = AppendHierarchy(frame, 3, 0x7B0AE0, 0x2A4, parent, inputs);
        return (Write(frame, address, Read(frame, address).WriteDword(0x30, source).WriteWord(0x298, delay).WriteWord(0x29C, mode).WriteWord(0x29E, selector)), address);
    }
}
