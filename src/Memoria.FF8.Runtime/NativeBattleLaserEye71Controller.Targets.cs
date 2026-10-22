using static Memoria.FF8.Runtime.NativeBattleLaserEye71Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleLaserEye71Controller
{
    private static NativeBattleLaserEye71TaskStep AdvanceTarget(NativeBattleLaserEye71Frame frame, uint address, NativeBattleLaserEye71Inputs inputs)
    {
        frame = inputs.PrepareTarget(frame, address);
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                (frame, uint node) = Append(frame, 2, NodeCallback, address, inputs);
                frame = Write(frame, node, Read(frame, node).WriteDword(0x30, SourceAddress).WriteWord(0x298, 18).WriteWord(0x29E, 45).WriteWord(0x29C, 0));
                (frame, _) = Append(frame, 3, BindingCallback, address, inputs);
                frame = NextPhase(frame, address);
                break;
            case 1:
                if (task.ReadWord(0x24) == 10)
                {
                    uint root = task.ReadDword(0x10);
                    frame = Write(frame, root, Read(frame, root).WriteByte(0x63, 0));
                    frame = NextPhase(frame, address);
                }
                break;
            case 2:
                if (Signed(task, 0x24) >= 25)
                {
                    frame = inputs.Execute(frame, new NativeBattleLaserEye71Contact(address));
                    task = Read(frame, address);
                    frame = Write(frame, address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                    frame = NextPhase(frame, address);
                }
                break;
            case 3:
                break;
            default:
                throw new InvalidDataException("Laser Eye target phase leaves its original four-entry table.");
        }
        return Finish(frame, address);
    }

    private static NativeBattleLaserEye71TaskStep AdvanceBinding(NativeBattleLaserEye71Frame frame, uint address, NativeBattleLaserEye71Resources resources, NativeBattleLaserEye71Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                byte actor = task.ReadByte(0x2C);
                frame = inputs.Execute(frame, new NativeBattleLaserEye71BindCaster(actor, SavedBindingAddress));
                frame = inputs.Execute(frame, new NativeBattleLaserEye71Ring(actor, 3));
                frame = NextPhase(frame, address);
                break;
            case 1:
                if (Signed(task, 0x24) >= 14)
                {
                    frame = inputs.Execute(frame, new NativeBattleLaserEye71Sound(resources.Sound, 0, 128));
                    frame = NextPhase(frame, address);
                }
                break;
            case 2:
                if (Signed(task, 0x24) >= 18)
                {
                    (frame, uint first) = Append(frame, 3, BeamCallback, address, inputs);
                    frame = Write(frame, first, Read(frame, first).WriteWord(0x90, 64));
                    (frame, uint second) = Append(frame, 3, BeamCallback, address, inputs);
                    frame = Write(frame, second, Read(frame, second).WriteWord(0x90, unchecked((ushort)-64)));
                    task = Read(frame, address);
                    frame = Write(frame, address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                    frame = NextPhase(frame, address);
                }
                break;
            case 3:
                break;
            default:
                throw new InvalidDataException("Laser Eye binding phase leaves its original four-entry table.");
        }
        return Finish(frame, address);
    }
}
