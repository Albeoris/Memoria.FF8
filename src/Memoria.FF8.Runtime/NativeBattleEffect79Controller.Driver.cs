using static Memoria.FF8.Runtime.NativeBattleEffect79Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect79Controller
{
    public static NativeBattleEffect79Step AdvanceDriver(NativeBattleEffect79Frame frame, uint address, NativeBattleEffect79Resources resources, NativeBattleEffect79Inputs inputs)
    {
        frame = frame with { State = frame.State.WriteWord(TargetAddress + 0x46, unchecked((ushort)(frame.State.ReadWord(TargetAddress + 0x46) + 1))) };
        if (frame.State.ReadWord(TargetAddress + 0x40) != frame.State.ReadWord(TargetAddress + 0x42))
        {
            frame = frame with { State = frame.State.WriteWord(TargetAddress + 0x40, frame.State.ReadWord(TargetAddress + 0x42)).WriteWord(TargetAddress + 0x46, 0) };
            frame = SpawnDriver(frame, address, resources, inputs);
        }
        if (frame.State.ReadWord(TargetAddress + 0x42) != frame.State.ReadWord(TargetAddress + 0x44))
        {
            frame = frame with { State = frame.State.WriteWord(TargetAddress + 0x42, frame.State.ReadWord(TargetAddress + 0x44)) };
        }
        switch (Phase(frame, address))
        {
            case 0:
                (frame, bool requested) = RequestState(frame, 1);
                if (requested)
                {
                    frame = inputs.Execute(frame, new NativeBattleEffect79StartVibration4A29A0(VibrationAddress, 1, 128, TargetAddress + 0x52));
                    frame = inputs.Execute(frame, new NativeBattleEffect79MusicFade46BBC0(0, 90, 0));
                    frame = NextPhase(frame, address);
                }
                break;
            case 1:
                if (Acknowledged(frame, 1))
                {
                    frame = PartyFlags(frame, false, inputs);
                    frame = NextPhase(frame, address);
                }
                break;
            case 2:
                if (frame.State.ReadWord(ControlAddress + 0x166) == 0 && frame.State.ReadWord(frame.State.ReadDword(0x25EA348)) == 0)
                {
                    frame = WriteIo(frame, NativeBattleEffect79IoProfile.ReleaseWait(ReadIo(frame)).State);
                    frame = NextPhase(frame, address);
                }
                break;
            case 3:
                if (frame.State.ReadWord(ControlAddress + 0x166) == 0)
                {
                    frame = TogglePause(frame, 0);
                    frame = NextPhase(frame, address);
                }
                else if (unchecked((short)frame.State.ReadWord(TargetAddress + 0x46)) >= 0x62)
                {
                    frame = TogglePause(frame, 1);
                }
                break;
            case 4:
                if (unchecked((short)frame.State.ReadWord(TargetAddress + 0x46)) >= 0x63)
                {
                    frame = NextPhase(frame, address);
                }
                break;
            case 5:
                (frame, bool second) = RequestState(frame, 2);
                if (second)
                {
                    frame = NextPhase(frame, address);
                }
                break;
            case 6:
                if (Acknowledged(frame, 2))
                {
                    frame = NextPhase(frame, address);
                }
                break;
            case 7:
                if (unchecked((short)frame.State.ReadWord(TargetAddress + 0x46)) >= 0x5A)
                {
                    frame = frame with { State = frame.State.WriteWord(TargetAddress + 0x48, 1) };
                    frame = NextPhase(frame, address);
                }
                break;
            case 8:
                (frame, bool third) = RequestState(frame, 3);
                if (third)
                {
                    frame = PartyFlags(frame, true, inputs);
                    frame = inputs.Execute(frame, new NativeBattleEffect79StopVibration4A2940(unchecked((short)frame.State.ReadWord(TargetAddress + 0x52))));
                    frame = NextPhase(frame, address);
                }
                break;
            case 9:
                frame = NextPhase(MarkComplete(frame, address), address);
                break;
            case 10:
                break;
            default:
                throw new InvalidDataException("Effect79 driver phase leaves its eleven native handlers.");
        }
        return Finish(frame, address);
    }

    private static (NativeBattleEffect79Frame Frame, bool Requested) RequestState(NativeBattleEffect79Frame frame, int value)
    {
        bool requested = frame.State.ReadWord(TargetAddress + 0x44) == frame.State.ReadWord(TargetAddress + 0x40) && unchecked((short)frame.State.ReadWord(TargetAddress + 0x40)) == value - 1;
        return (requested ? frame with { State = frame.State.WriteWord(TargetAddress + 0x44, (ushort)value) } : frame, requested);
    }

    private static bool Acknowledged(NativeBattleEffect79Frame frame, int value) => unchecked((short)frame.State.ReadWord(TargetAddress + 0x40)) >= value;

    private static NativeBattleEffect79Frame TogglePause(NativeBattleEffect79Frame frame, uint value)
    {
        if (frame.State.ReadDword(0x25EB3B4) != value)
        {
            frame = frame with { State = frame.State.WriteDword(0x25EB3B4, value).WriteDword(0x25E8954, value) };
        }
        return frame;
    }

    public static NativeBattleEffect79Frame PartyFlags(NativeBattleEffect79Frame frame, bool restore, NativeBattleEffect79Inputs inputs)
    {
        for (int actor = 0; actor < 3; actor++)
        {
            NativeBattleGraphicsModel model = inputs.ReadModel(frame, actor);
            if ((model.Flags0 & 2) == 0)
            {
                continue;
            }
            uint saved = TargetAddress + 0x20u + (uint)(actor * 4);
            if (!restore)
            {
                frame = frame with { State = frame.State.WriteDword(saved, model.Flags0) };
            }
            frame = inputs.Execute(frame, new NativeBattleEffect79TargetFlags(PhysicalModelAddress(actor), restore ? (ushort)0xFFFB : (ushort)0xFFFF, restore ? (ushort)(frame.State.ReadByte(saved) & 4) : (ushort)4, true));
        }
        return frame;
    }

    private static NativeBattleEffect79Frame SpawnDriver(NativeBattleEffect79Frame frame, uint address, NativeBattleEffect79Resources resources, NativeBattleEffect79Inputs inputs)
    {
        int value = frame.State.ReadWord(TargetAddress + 0x40);
        if (value == 1)
        {
            foreach (uint callback in new uint[] { 0x7BCF10, 0x7BC610, 0x7BCD50 })
            {
                (frame, _) = AppendHierarchy(frame, 5, callback, 0xB4, address, inputs);
            }
        }
        else if (value == 2)
        {
            frame = SpawnNode(frame, address, 0x176A958, 0, 15, 0, resources, inputs);
            foreach (uint callback in new uint[] { 0x7BCB40, 0x7BBC20 })
            {
                (frame, _) = AppendHierarchy(frame, 5, callback, 0xB4, address, inputs);
            }
            frame = SpawnProgramme(frame, address, 0x7B5B80, frame.State.ReadDword(ControlAddress + 0x124), 0xEC, 0, inputs);
            frame = SpawnProgramme(frame, address, 0x7B80B0, frame.State.ReadDword(ControlAddress + 0x128), 0x134, 1, inputs);
            frame = SpawnProgramme(frame, address, 0x7B81C0, frame.State.ReadDword(ControlAddress + 0x12C), 0x134, 2, inputs);
            (frame, _) = AppendHierarchy(frame, 5, 0x7BCE40, 0xB4, address, inputs);
        }
        return frame;
    }
}
