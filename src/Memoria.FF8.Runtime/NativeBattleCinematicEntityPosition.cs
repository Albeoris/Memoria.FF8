using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicEntityAnchor(NativeBattleWaterExecutionFrame Frame, NativeBattleFullMacVertex Point);

public static class NativeBattleCinematicEntityPosition
{
    public static NativeBattleWaterExecutionFrame FromEntityOffset5F(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, Func<BattleModelOwner, NativeBattleVector> readPosition, Func<NativeBattleWaterExecutionFrame, byte, NativeBattleWaterExecutionFrame> updateSlot)
    {
        BattleModelOwner actor = NativeBattleCinematicActorAccess.ReadSlotActor(frame);
        for (int lane = 0; lane < 3; lane++)
        {
            short offset = Operand(2 + lane * 2);
            if (offset != 0x7654)
            {
                NativeBattleVector position = readPosition(actor);
                int component = lane == 0 ? position.X : lane == 1 ? position.Y : position.Z;
                frame = WriteSlotDword(frame, 0x5C + lane * 4, unchecked((uint)((component + offset) << 16)));
            }
        }
        frame = frame with { Arena = frame.Arena.Write(SlotOffset(frame, 0x18, 1), [1]) };
        return NativeBattleScriptedCommandReads.Advance(updateSlot(frame, 1), 8);

        short Operand(int offset) => BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, (uint)offset, 2));
    }

    public static NativeBattleWaterExecutionFrame FromEntityJoint60(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, Func<NativeBattleWaterExecutionFrame, BattleModelOwner, short, NativeBattleCinematicEntityAnchor> readAnchor, Func<BattleModelOwner, short> readScale, Func<NativeBattleWaterExecutionFrame, byte, NativeBattleWaterExecutionFrame> updateSlot)
    {
        BattleModelOwner actor = NativeBattleCinematicActorAccess.ReadSlotActor(frame);
        short selector = Operand(2);
        NativeBattleCinematicEntityAnchor selected = readAnchor(frame, actor, selector);
        frame = selected.Frame;
        int x = selected.Point.X;
        int y = selected.Point.Y;
        int z = selected.Point.Z;
        bool orbit = (frame.ReadTemporaryWord(0x14A) & 0x8000) != 0;
        if (orbit)
        {
            int multiplier = Operand(4);
            int angle = Operand(6) << 4;
            short scale = readScale(actor);
            int cosine = unchecked(NativeBattleCameraMath.Cosine(angle) * scale) >> 4;
            int sine = unchecked(NativeBattleCameraMath.Sine(angle) * scale) >> 4;
            z = unchecked(z + unchecked(cosine * multiplier) / 65536);
            x = unchecked(x + unchecked(sine * multiplier) / 65536);
        }
        frame = WriteSlotDword(frame, 0x5C, unchecked((uint)(x << 16)));
        frame = WriteSlotDword(frame, 0x60, unchecked((uint)(y << 16)));
        frame = WriteSlotDword(frame, 0x64, unchecked((uint)(z << 16)));
        frame = NativeBattleScriptedCommandReads.Advance(frame, orbit ? 8u : 4u);
        return updateSlot(frame, ReadSlot(frame, 0x18, 1)[0]);

        short Operand(int offset) => BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, (uint)offset, 2));
    }

    public static NativeBattleWaterExecutionFrame FromTargetJointAverage6E(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program, Func<NativeBattleWaterExecutionFrame, BattleModelOwner, short, NativeBattleCinematicEntityAnchor> readAnchor, Func<NativeBattleWaterExecutionFrame, byte, NativeBattleWaterExecutionFrame> updateSlot)
    {
        short selector = BinaryPrimitives.ReadInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        uint header = CoreDword(frame, 0x279744C);
        int[] sum = new int[3];
        int index = 0;
        while (index < NativeBattleCinematicActorAccess.ReadCurrentCount(frame, header))
        {
            BattleModelOwner actor = NativeBattleCinematicActorAccess.ReadCurrentActor(frame, index, header);
            NativeBattleCinematicEntityAnchor selected = readAnchor(frame, actor, selector);
            frame = selected.Frame;
            sum[0] = unchecked(sum[0] + selected.Point.X);
            sum[1] = unchecked(sum[1] + selected.Point.Y);
            sum[2] = unchecked(sum[2] + selected.Point.Z);
            header = CoreDword(frame, 0x279744C);
            index++;
        }
        int count = NativeBattleCinematicActorAccess.ReadCurrentCount(frame, header);
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        for (int lane = 0; lane < 3; lane++)
        {
            if ((instruction & (0x1000 << lane)) != 0)
            {
                if (count == 0)
                {
                    throw new InvalidDataException("Native cinematic target joint average reached its original zero divisor.");
                }
                frame = WriteSlotDword(frame, 0x5C + lane * 4, unchecked((uint)((sum[lane] / count) << 16)));
            }
        }
        frame = updateSlot(frame, ReadSlot(frame, 0x18, 1)[0]);
        return NativeBattleScriptedCommandReads.Advance(frame, 4);
    }

    public static NativeBattleWaterExecutionFrame WaitScaledBySlot140(NativeBattleWaterExecutionFrame frame, NativeBattleWaterScriptProgram program)
    {
        BattleModelOwner actor = NativeBattleCinematicActorAccess.ReadSlotActor(frame);
        int scale = NativeBattleCinematicActorAccess.ReadActorOrdinal(frame, actor);
        ushort value = BinaryPrimitives.ReadUInt16LittleEndian(NativeBattleScriptedCommandReads.Program(frame, program, 2, 2));
        ushort product = unchecked((ushort)(scale * value));
        frame = frame.WriteTemporaryWord(0x13E, unchecked((ushort)(product << 7)));
        return NativeBattleScriptedCommandReads.Advance(frame, 4);
    }
}
