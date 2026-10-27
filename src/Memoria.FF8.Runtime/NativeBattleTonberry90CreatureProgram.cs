using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleTonberry90Actors
{
    public static NativeBattleTonberry90TaskStep Advance767B40(NativeBattleTonberry90Frame frame, uint address, NativeBattleTonberry90ActorServices services)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback != 0x767B40)
        {
            throw new InvalidDataException("Native Tonberry creature programme requires its selected callback.");
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                NativeBattleGfAnimationSource source = services.ReadSource(frame, task.ReadDword(0x74));
                if (source.Address != task.ReadDword(0x74))
                {
                    throw new InvalidDataException("Native Tonberry creature programme source differs from its actual task pointer.");
                }
                NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 12 + (0x94 - 12), task.PayloadLength - (0x94 - 12));
                workspace = workspace.Initialize7016B0(source, unchecked((int)task.ReadDword(0x78)));
                task = (NativeBattleEffectTaskMemory.PublishAnimationWorkspace(task, 12 + (0x94 - 12), workspace)).WriteWord(0x1E, 0).WriteDword(0x50, 4096).WriteDword(0x54, 4096).WriteDword(0x58, 4096);
                frame = PublishAnchor(Write(frame, address, task));
                frame = services.Draw764130(frame, address).Frame;
                frame = Next(frame, address);
                break;
            case 1:
                frame = PublishAnchor(frame);
                if (Read(frame, address).ReadWord(0x1C) == 0x7FFF)
                {
                    frame = Complete(frame, address);
                }
                NativeBattleTonberry90ActorDraw draw = services.Draw764130(frame, address);
                frame = draw.Frame;
                if (draw.Remaining == 0)
                {
                    frame = Complete(frame, address);
                }
                break;
            case 2:
                break;
            default:
                throw new NotSupportedException($"Native Tonberry creature programme signed phase {unchecked((sbyte)task.ReadByte(0x29))} has no selected state.");
        }
        return Finish(frame, address);

        NativeBattleTonberry90Frame PublishAnchor(NativeBattleTonberry90Frame current)
        {
            NativeBattleTonberry90State state = State(current);
            NativeBattleModulePoolTask actual = Read(current, address).WriteDword(0x1C, state.ReadDword(0x25A4C10)).WriteDword(0x20, state.ReadDword(0x25A4C14)).WriteWord(0x88, state.ReadWord(0x25A2B78));
            return Write(current, address, actual);
        }
    }
}
