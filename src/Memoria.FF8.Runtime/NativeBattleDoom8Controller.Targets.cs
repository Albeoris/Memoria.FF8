namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDoom8Controller
{
    private static NativeBattleDoom8Step AdvanceTarget(NativeBattleDoom8Frame frame, uint address, NativeBattleDoom8Inputs inputs)
    {
        frame = inputs.Execute(frame, new NativeBattleDoom8UpdateTargetBounds(address));
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                frame = inputs.Execute(frame, new NativeBattleDoom8InitializeTarget(address));
                frame = frame with { State = frame.State.InitializeIo() };
                frame = Append(frame, 9, IoCallback, 48, address, inputs.Packets);
                frame = Append(frame, 2, DirectorCallback, 72, address, inputs.Packets);
                frame = NextPhase(frame, address);
                break;
            case 1:
                if (frame.State.SharedWord(0x48) == 1)
                {
                    NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Doom target requires its captured action parameters.");
                    BattleActionHeaderReference headerIndex = parameters.Header.Advance(unchecked((sbyte)task.ReadByte(0x2A)));
                    BattleContactCursor cursor = inputs.Packets.ReadHeader(headerIndex).FirstContact.Advance(task.ReadTraversalPosition(0x2B));
                    BattleContactOccurrence occurrence = inputs.Packets.ReadContact(cursor).Occurrence ?? throw new InvalidDataException("The reached contact has no retained occurrence.");
                    frame = inputs.Execute(frame, new NativeBattleDoom8Contact(occurrence));
                    frame = NextPhase(frame, address);
                }
                break;
            case 2:
                if (task.ReadByte(0x28) == 0 || frame.State.SharedWord(0x4C) == 1)
                {
                    frame = NextPhase(frame, address);
                }
                break;
            case 3:
                frame = Write(frame, address, task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                uint root = task.ReadDword(0x10);
                frame = Write(frame, root, Read(frame, root).WriteByte(0x63, 0));
                frame = NextPhase(frame, address);
                break;
            case 4:
                break;
            default:
                throw new InvalidDataException("Doom target signed phase exceeds its five handlers.");
        }
        return Finish(frame, address);
    }
}
