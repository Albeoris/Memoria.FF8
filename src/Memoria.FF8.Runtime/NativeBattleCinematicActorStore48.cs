using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public static class NativeBattleCinematicActorStore48
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, Func<uint, NativeBattleGraphicsModel> readActor, Action<uint, NativeBattleGraphicsModel> writeActor)
    {
        uint header = CoreDword(frame, 0x279744C);
        byte index = ReadSlot(frame, 0x1B, 1)[0];
        uint address = CoreDword(frame, unchecked(header + 0x60 + index * 4u));
        NativeBattleGraphicsModel actor = readActor(address);
        actor = actor with { Flags0 = (ushort)(actor.Flags0 | 0x1000) };
        writeActor(address, actor);
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        int mode = instruction >> 9 & 15;
        if (mode == 0)
        {
            NativeBattleModelTransform transform = actor.Transform ?? throw new NotSupportedException("Native actor position store requires its current transform owner.");
            NativeBattleVector position = new(unchecked((short)SlotWord(frame, 0x94)), unchecked((short)SlotWord(frame, 0x96)), unchecked((short)SlotWord(frame, 0x98)));
            transform = transform with { Position28 = position };
            if ((instruction & 0x8000) != 0)
            {
                transform = transform with { Angle36 = position.Y };
            }
            writeActor(address, actor with { Transform = transform });
        }
        if (mode == 1)
        {
            writeActor(address, actor with { Rotation12 = unchecked((short)SlotWord(frame, 0x8C)), Rotation14 = unchecked((short)SlotWord(frame, 0x8E)), Rotation16 = unchecked((short)SlotWord(frame, 0x90)) });
        }
        return NativeBattleScriptedCommandReads.Advance(frame, 2);
    }
    public static NativeBattleWaterExecutionFrame StoreColor38(NativeBattleWaterExecutionFrame frame, uint baseColor, Func<uint, NativeBattleGraphicsModel> readActor, Action<uint, NativeBattleGraphicsModel> writeActor)
    {
        int offset = (frame.ReadTemporaryWord(0x14A) & 0xFE00) == 0 ? 0x94 : 0x8C;
        uint color = 0;
        for (int lane = 0; lane < 3; lane++)
        {
            int value = Math.Clamp((byte)(baseColor >> (lane * 8)) + unchecked((short)SlotWord(frame, offset + lane * 2)) - 128, 0, 255);
            frame = frame.WriteTemporaryDword(0xF0 + lane * 4, unchecked((uint)value));
            color |= unchecked((uint)value << (lane * 8));
        }
        frame = frame.WriteTemporaryDword(0xFC, color);
        uint header = CoreDword(frame, 0x279744C);
        uint address = CoreDword(frame, unchecked(header + 0x60 + ReadSlot(frame, 0x1B, 1)[0] * 4u));
        writeActor(address, readActor(address) with { Color40 = color });
        return NativeBattleScriptedCommandReads.Advance(frame, 2);
    }
    public static NativeBattleWaterExecutionFrame BranchScale49(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, Func<uint, NativeBattleGraphicsModel> readActor)
    {
        uint header = CoreDword(frame, 0x279744C);
        uint address = CoreDword(frame, unchecked(header + 0x60 + ReadSlot(frame, 0x1B, 1)[0] * 4u));
        short scale = (readActor(address).Transform ?? throw new NotSupportedException("Native actor scale branch requires its current transform owner.")).Scale38;
        short boundary = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        bool reached = (frame.ReadTemporaryWord(0x14A) & 0xFE00) == 0 ? scale <= boundary : scale >= boundary;
        if (reached)
        {
            return NativeBattleScriptedCommandReads.Advance(frame, 6);
        }
        short displacement = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 4, 2));
        return WriteCoreDword(frame, 0x2797450, unchecked(CoreDword(frame, 0x2797450) + (uint)displacement));
    }
}
