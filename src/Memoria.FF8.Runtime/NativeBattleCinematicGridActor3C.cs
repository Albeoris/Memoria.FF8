using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicGridActorServices(Func<uint, NativeBattleGraphicsModel> ReadActor, Action<uint, NativeBattleGraphicsModel> WriteActor, Func<NativeBattleWaterExecutionFrame, short, uint> ResolveSlot, Func<NativeBattleWaterExecutionFrame, NativeBattleActionMatrix, uint, uint, NativeBattleWaterExecutionFrame> Transform);

/// <summary>Move the physical actor along the current lattice while publishing the same cached script position.</summary>
public static class NativeBattleCinematicGridActor3C
{
    public static NativeBattleWaterExecutionFrame Draw(NativeBattleWaterExecutionFrame frame, NativeBattleCinematicGridActorServices services)
    {
        uint originalNode = CoreDword(frame, 0x27973E8);
        uint header = CoreDword(frame, 0x279744C);
        byte actorIndex = ReadSlot(frame, 0x1B, 1)[0];
        uint actorAddress = CoreDword(frame, unchecked(header + 0x60 + 4u * actorIndex));
        NativeBattleGraphicsModel actor = services.ReadActor(actorAddress);
        actor = actor with { Flags0 = (ushort)(actor.Flags0 | 0x1000) };
        services.WriteActor(actorAddress, actor);
        uint payload = SlotDword(0xB8);
        int factor = SignedWord(payload + 2);
        frame = frame.WriteTemporaryDword(0x84, unchecked((uint)factor));
        uint scale = unchecked(header + 0x120 + 8u * actorIndex);
        short Scale(int lane) => unchecked((short)(unchecked((short)CoreWord(frame, scale + (uint)(lane * 2)) * factor) >> 12));
        NativeBattleVector scaled = new(Scale(0), Scale(1), Scale(2));
        NativeBattleModelTransform transform = actor.Transform ?? throw new NotSupportedException("Native grid actor requires its retained physical transform.");
        actor = actor with { Transform = transform with { Scale148 = scaled } };
        services.WriteActor(actorAddress, actor);
        uint captured = unchecked(header + 0x84 + 8u * actorIndex);
        short actorX = unchecked((short)(unchecked((short)CoreWord(frame, captured) * factor) >> 12));
        short offset = unchecked((short)((unchecked((unchecked((short)CoreWord(frame, captured + 4)) + 2048) * factor) >> 12) - 1024));
        frame = frame.WriteTemporaryWord(0xA0, unchecked((ushort)actorX)).WriteTemporaryWord(0xA2, 0).WriteTemporaryWord(0xA4, unchecked((ushort)offset));
        frame = frame.WriteTemporaryDword(0x6C, unchecked((uint)-offset));
        frame = frame.WriteTemporaryDword(0x64, unchecked((uint)(unchecked((short)SlotWord(frame, 0x8E)) << 4)));
        frame = frame.WriteTemporaryDword(0x80, originalNode);
        uint grid = services.ResolveSlot(frame, SignedWord(payload));
        frame = WriteCoreDword(frame, 0x27973E8, grid);
        uint reference = services.ResolveSlot(frame, unchecked((short)SlotWord(frame, 0x9C)));
        uint gridPayload = SlotDword(0xB8);
        int period = Word(gridPayload + 4);
        frame = frame.WriteTemporaryDword(0x60, unchecked((uint)period));
        int start = -SignedWord(reference + 0x98);
        frame = frame.WriteTemporaryDword(0x68, unchecked((uint)start));
        int distance = unchecked((int)frame.ReadTemporaryDword(0x6C) + (int)frame.ReadTemporaryDword(0x64));
        short actorY;
        short actorZ;
        short pitch;
        if (distance <= start)
        {
            actorY = 0;
            actorZ = unchecked((short)-distance);
            pitch = 0;
        }
        else if (distance >= unchecked(start + (period << 5)))
        {
            PrepareChain();
            for (int index = 0; index < 32; index++)
            {
                AdvanceChain();
            }
            actorY = unchecked((short)frame.ReadTemporaryWord(0xE2));
            actorZ = unchecked((short)(period - (period << 5) - start - start + distance));
            pitch = 2048;
        }
        else
        {
            distance = unchecked(distance - start);
            int remainder = distance % period;
            frame = frame.WriteTemporaryDword(0xB0, 0).WriteTemporaryWord(0xB4, unchecked((ushort)-remainder));
            int count = distance / period;
            PrepareChain();
            NativeBattleActionMatrix? matrix = null;
            for (int index = 0; index <= count; index++)
            {
                matrix = AdvanceChain();
            }
            frame = services.Transform(frame, matrix ?? throw new NotSupportedException("Native grid actor fractional transform requires its preceding native curve rotation."), frame.TemporaryFrame.Address + 0xB0, frame.TemporaryFrame.Address + 0xC0);
            actorY = unchecked((short)frame.ReadTemporaryWord(0xC2));
            actorZ = unchecked((short)(frame.ReadTemporaryWord(0xC4) - start));
            pitch = unchecked((short)-frame.ReadTemporaryWord(0xD0));
        }
        actor = services.ReadActor(actorAddress);
        actor = actor with { Rotation12 = pitch, Transform = (actor.Transform ?? throw new NotSupportedException("Native grid actor lost its current physical transform.")) with { Position28 = new(actorX, actorY, actorZ) } };
        services.WriteActor(actorAddress, actor);
        frame = WriteCoreDword(frame, 0x27973E8, frame.ReadTemporaryDword(0x80));
        NativeBattleVector position = actor.Transform.Position28;
        short[] values = [position.X, position.Y, position.Z];
        for (int lane = 0; lane < 3; lane++)
        {
            frame = WriteSlotWord(frame, 0x94 + lane * 2, unchecked((ushort)values[lane]));
            frame = WriteSlotDword(frame, 0x5C + lane * 4, unchecked((uint)(values[lane] << 16)));
        }
        return frame;

        void PrepareChain()
        {
            frame = frame.WriteTemporaryDword(0xC0, 0).WriteTemporaryWord(0xC4, unchecked((ushort)-period));
            frame = frame.WriteTemporaryDword(0xD0, 0).WriteTemporaryWord(0xD4, 0);
            frame = frame.WriteTemporaryDword(0xE0, 0).WriteTemporaryDword(0xE4, 0);
        }

        NativeBattleActionMatrix AdvanceChain()
        {
            frame = frame.WriteTemporaryWord(0xD0, unchecked((ushort)(frame.ReadTemporaryWord(0xD0) + SlotWord(frame, 0x8E))));
            NativeBattleVector angles = new(unchecked((short)frame.ReadTemporaryWord(0xD0)), unchecked((short)frame.ReadTemporaryWord(0xD2)), unchecked((short)frame.ReadTemporaryWord(0xD4)));
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler56CD50(angles) with { X = unchecked((short)frame.ReadTemporaryWord(0xE0)), Y = unchecked((short)frame.ReadTemporaryWord(0xE2)), Z = unchecked((short)frame.ReadTemporaryWord(0xE4)) };
            for (int index = 0; index < 9; index++)
            {
                frame = frame.WriteTemporaryWord(0xE0 + index * 2, unchecked((ushort)matrix.Rotation[index]));
            }
            frame = services.Transform(frame, matrix, frame.TemporaryFrame.Address + 0xC0, frame.TemporaryFrame.Address + 0xE0);
            return matrix;
        }

        uint SlotDword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, offset, 4));
        ushort Word(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
        short SignedWord(uint address) => unchecked((short)Word(address));
    }
}
