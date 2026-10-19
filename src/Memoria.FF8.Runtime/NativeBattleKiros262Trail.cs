using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleKiros262Tip(NativeBattleWaterExecutionFrame Frame, NativeBattleVector Position, int DirectionX, int DirectionY, int DirectionZ);
public sealed record NativeBattleKiros262TrailStep(NativeBattleWaterExecutionFrame Frame, NativeBattleKiros262TrailState State);
public sealed record NativeBattleKiros262TrailServices(Func<NativeBattleWaterExecutionFrame, uint, ushort, NativeBattleKiros262Tip> ReadTip, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleWaterExecutionFrame> RestorePose, Func<NativeBattleWaterExecutionFrame, NativeBattleKiros262TrailState, uint, NativeBattleWaterExecutionFrame> Emit);

public static class NativeBattleKiros262Trail
{
    public static NativeBattleKiros262TrailStep Draw(NativeBattleWaterExecutionFrame frame, NativeBattleKiros262TrailState state, NativeBattleKiros262TrailServices services)
    {
        state.Validate();
        uint payload = SlotDword(0xB8);
        uint counterAddress = SlotDword(0xBC);
        ushort capacity = 0;
        NativeBattleEffectArena memory = state.Memory;
        if (counterAddress == 0)
        {
            capacity = ReadCapacity();
            (frame, counterAddress) = NativeBattleWaterParticleCommands.AllocateB656E0(frame, 16);
            frame = frame with { Arena = frame.Arena.Write(SlotOffset(frame, 0xBC, 4), BitConverter.GetBytes(counterAddress)).Write(ArenaOffset(counterAddress, 2), new byte[2]) };
            for (int index = 0; index < capacity; index++)
            {
                memory = memory.Write(NativeBattleKiros262TrailState.History + index * 20, new byte[4]);
            }
        }
        if (SlotWord(frame, 0x96) != 0)
        {
            return new(frame, state with { Memory = memory });
        }
        if (capacity == 0)
        {
            capacity = ReadCapacity();
        }
        uint owner = CoreDword(frame, 0x279744C);
        byte actor = ReadSlot(frame, 0x1B, 1)[0];
        uint model = CoreDword(frame, unchecked(owner + 0x60 + actor * 4u));
        NativeBattleKiros262Tip tip = services.ReadTip(frame, model, ArenaWord(payload));
        frame = tip.Frame;
        int length = unchecked((short)SlotWord(frame, 0x94));
        NativeBattleVector end = new(unchecked((short)(tip.Position.X + (unchecked(tip.DirectionX * length) >> 12))), unchecked((short)(tip.Position.Y + (unchecked(tip.DirectionY * length) >> 12))), unchecked((short)(tip.Position.Z + (unchecked(tip.DirectionZ * length) >> 12))));
        ushort counter = ArenaWord(counterAddress);
        int ring = counter % capacity;
        int history = NativeBattleKiros262TrailState.History + ring * 20;
        memory = memory.Write(history, BitConverter.GetBytes(1));
        StorePoint(history + 4, tip.Position);
        StorePoint(history + 12, end);
        memory = memory.MarkUnknown(history + 10, 2).MarkUnknown(history + 18, 2);
        int admitted = counter < 100 - capacity ? capacity : 100 - counter;
        int count = 0;
        for (int index = ring; count < admitted; index = index == 0 ? capacity - 1 : index - 1)
        {
            int source = NativeBattleKiros262TrailState.History + index * 20;
            if (BinaryPrimitives.ReadInt32LittleEndian(memory.Read(source, 4)) == 0)
            {
                break;
            }
            Copy(source + 4, NativeBattleKiros262TrailState.GatherA + count * 8, 8);
            Copy(source + 12, NativeBattleKiros262TrailState.GatherB + count * 8, 8);
            count++;
        }
        if (count > 1)
        {
            NativeBattleEffectArena work = NativeBattleGilgameshSwordTrailCurve.Prepare571620(count, memory, NativeBattleKiros262TrailState.GatherA, NativeBattleEffectArena.Clear().MarkUnknown(0, 400));
            for (int index = 0; index < 32; index++)
            {
                memory = NativeBattleGilgameshSwordTrailCurve.Evaluate571690(count, work, index * 4096 / 31, memory, NativeBattleKiros262TrailState.CurveA + index * 8);
            }
            work = NativeBattleGilgameshSwordTrailCurve.Prepare571620(count, memory, NativeBattleKiros262TrailState.GatherB, work);
            for (int index = 0; index < 32; index++)
            {
                memory = NativeBattleGilgameshSwordTrailCurve.Evaluate571690(count, work, index * 4096 / 31, memory, NativeBattleKiros262TrailState.CurveB + index * 8);
            }
            Copy(NativeBattleKiros262TrailState.CurveB, 0x280, 8);
            Normalize(0x298, Point(NativeBattleKiros262TrailState.CurveB), Point(NativeBattleKiros262TrailState.CurveB + 8));
            Copy(NativeBattleKiros262TrailState.CurveA, 0x2A8, 8);
            Normalize(0x2B0, Point(0x280), Point(NativeBattleKiros262TrailState.CurveA));
            frame = services.Emit(frame, state with { Memory = memory }, 0x3A000000u | (SlotDword(0xCC) & 0xFFFFFF));
        }
        frame = frame with { Arena = frame.Arena.Write(ArenaOffset(counterAddress, 2), BitConverter.GetBytes(unchecked((ushort)(counter + 1)))) };
        frame = services.RestorePose(frame, model);
        state = state with { Memory = memory };
        state.Validate();
        return new(frame, state);

        ushort ReadCapacity()
        {
            ushort value = ArenaWord(payload + 2);
            if (value is 0 or > 10)
            {
                throw new NotSupportedException("Native trail capacity leaves its actual fixed curve workspace.");
            }
            return value;
        }
        uint SlotDword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, offset, 4));
        ushort ArenaWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
        NativeBattleVector Point(int offset)
        {
            byte[] bytes = memory.Read(offset, 6);
            return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)));
        }
        void StorePoint(int offset, NativeBattleVector value)
        {
            byte[] bytes = new byte[6];
            BinaryPrimitives.WriteInt16LittleEndian(bytes, value.X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), value.Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), value.Z);
            memory = memory.Write(offset, bytes);
        }
        void Copy(int source, int destination, int countBytes)
        {
            var bytes = memory.ReadSnapshot(source, countBytes);
            memory = memory.Write(destination, bytes.Data);
            for (int index = 0; index < countBytes; index++)
            {
                if (!bytes.Known[index])
                {
                    memory = memory.MarkUnknown(destination + index, 1);
                }
            }
        }
        void Normalize(int destination, NativeBattleVector first, NativeBattleVector second)
        {
            var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(first.X - second.X, first.Y - second.Y, first.Z - second.Z);
            memory = memory.Write(destination, BitConverter.GetBytes(direction.X)).Write(destination + 4, BitConverter.GetBytes(direction.Y)).Write(destination + 8, BitConverter.GetBytes(direction.Z));
        }
    }
}
