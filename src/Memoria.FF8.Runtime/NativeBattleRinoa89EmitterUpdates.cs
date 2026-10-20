using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa89SparkAllocation(NativeBattleRinoa89Frame Frame, int? Offset);

public static partial class NativeBattleRinoa89Controller
{
    public static NativeBattleRinoa89SparkAllocation AllocateSpark6FCC90(NativeBattleRinoa89Frame frame)
    {
        NativeBattleEffectArena arena = RequireArena(frame);
        uint cursor = BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging.Read(0x2556590, 4));
        if (cursor < ArenaAddress + 4672 || cursor >= ArenaAddress + 16960 || (cursor - ArenaAddress - 4672) % 24 != 0)
        {
            throw new InvalidDataException("Native6FCC90 requires its actual retained spark cursor.");
        }
        int offset = checked((int)(cursor - ArenaAddress));
        if (BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset + 10, 2)) != 0)
        {
            offset = 4672;
            while (BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset + 10, 2)) != 0)
            {
                offset += 24;
                if (offset == 16960)
                {
                    return new(frame, null);
                }
            }
        }
        int next = offset < 16936 ? offset + 24 : 4672;
        return new(frame with { Staging = Dword(frame.Staging, 0x2556590, checked(ArenaAddress + (uint)next)) }, offset);
    }

    public static NativeBattleRinoa89Frame AdvanceEmitters6FC750(NativeBattleRinoa89Frame frame)
    {
        for (int index = 0; index < 128; index++)
        {
            int offset = 16960 + index * 48;
            NativeBattleEffectArena arena = RequireArena(frame);
            byte life = arena.Read(offset, 1)[0];
            if (life == 0)
            {
                continue;
            }
            _ = arena.Read(offset, 34);
            _ = arena.Read(offset + 36, 12);
            byte[] emitter = arena.ReadSnapshot(offset, 48).Data;
            byte[] descriptor = frame.Staging.Read(checked(0x14AF510u + (uint)emitter[1] * 16), 16);
            emitter[0] = unchecked((byte)(life - 1));
            if (life == 1)
            {
                frame = frame with { Memory = frame.Memory with { Arena = RequireArena(frame).Write(offset, emitter.AsSpan(0, 1)) } };
                if (descriptor[7] == 0)
                {
                    continue;
                }
                NativeBattleRinoa89SparkAllocation allocation = AllocateSpark6FCC90(frame);
                frame = allocation.Frame;
                if (allocation.Offset is not int spark)
                {
                    continue;
                }
                NativeBattleEffectArenaWriter writer = new(RequireArena(frame));
                byte[] bytes = new byte[24];
                BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x14A07F4);
                for (int axis = 0; axis < 3; axis++)
                {
                    BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4 + axis * 2), unchecked((short)BinaryPrimitives.ReadInt32LittleEndian(emitter.AsSpan(36 + axis * 4))));
                    BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12 + axis * 2), unchecked((short)(ReadWord(18 + axis * 6) >> 7)));
                }
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 16);
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(18), 2048);
                writer.Write(spark, bytes);
                frame = frame with { Memory = frame.Memory with { Arena = writer.Freeze() } };
                continue;
            }
            NativeBattleEffectArenaWriter updates = new(RequireArena(frame));
            updates.Write(offset, emitter.AsSpan(0, 1));
            for (int axis = 0; axis < 4; axis++)
            {
                StoreWord(2 + axis * 2, unchecked((short)(ReadWord(2 + axis * 2) + BinaryPrimitives.ReadInt16LittleEndian(descriptor.AsSpan(8 + axis * 2)))));
            }
            for (int axis = 0; axis < 3; axis++)
            {
                int velocityOffset = 10 + axis * 2;
                short velocity = ReadWord(velocityOffset);
                int positionOffset = 36 + axis * 4;
                byte[] position = new byte[4];
                BinaryPrimitives.WriteInt32LittleEndian(position, unchecked(BinaryPrimitives.ReadInt32LittleEndian(emitter.AsSpan(positionOffset)) + velocity));
                updates.Write(offset + positionOffset, position);
                StoreWord(velocityOffset, unchecked((short)(velocity - ReadWord(18 + axis * 6) / 80)));
            }
            frame = frame with { Memory = frame.Memory with { Arena = updates.Freeze() } };

            short ReadWord(int wordOffset) => BinaryPrimitives.ReadInt16LittleEndian(emitter.AsSpan(wordOffset));
            void StoreWord(int wordOffset, short value)
            {
                byte[] bytes = new byte[2];
                BinaryPrimitives.WriteInt16LittleEndian(bytes, value);
                updates.Write(offset + wordOffset, bytes);
            }
        }
        return frame;
    }
}
