using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect227Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect227Lower;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect227Records
{
    private const uint FreeCursorGlobal = 0x2508704;
    private const uint CasterGlobal = 0x2508714;
    private const int FirstOffset = 0x61C;
    private const int Count = 128;
    private const int Stride = 16;

    public static (NativeBattleEffect227Frame Frame, uint Address) Allocate(NativeBattleEffect227Frame frame)
    {
        uint first = checked(frame.State.ReadDword(ArenaGlobal) + FirstOffset);
        uint candidate = frame.State.ReadDword(FreeCursorGlobal);
        if (candidate < first || candidate >= first + Count * Stride || (candidate - first) % Stride != 0)
        {
            throw new InvalidDataException("The record allocator requires its current physical free cursor.");
        }
        if (ReadBytes(frame, candidate + 15, 1).Read(0, 1)[0] != 0)
        {
            candidate = first;
            int remaining = Count;
            while (ReadBytes(frame, candidate + 15, 1).Read(0, 1)[0] != 0)
            {
                remaining--;
                if (remaining == 0)
                {
                    return (frame, 0);
                }
                candidate += Stride;
            }
        }
        uint next = candidate >= first + (Count - 1) * Stride ? first : candidate + Stride;
        return (frame with { State = frame.State.WriteDword(FreeCursorGlobal, next) }, candidate);
    }

    public static NativeBattleEffect227Frame Birth(NativeBattleEffect227Frame frame, uint pointBank, int pointCount, NativeBattleEffect227Resources resources, NativeBattleEffect227Inputs inputs)
    {
        (frame, uint address) = Allocate(frame);
        if (address == 0)
        {
            return frame;
        }
        int selected = unchecked(Roll(ref frame) * pointCount) / 32768;
        NativeBattleEffectArena local = NativeBattleEffectArena.Clear().Write(0, resources.ReadStatic(checked(pointBank + (uint)(selected * 8)), 8));
        NativeBattleEffect227PointStep point = inputs.BonePoint693990(frame, frame.State.ReadDword(CasterGlobal), local, ReadBytes(frame, address + 4, 8));
        frame = WriteBytes(point.Frame, address + 4, point.Output8, 8);
        frame = WriteBytes(frame, address, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(0x112D2E0u)), 4);
        byte horizontal = unchecked((byte)((Roll(ref frame) & 3) - 2));
        frame = WriteBytes(frame, address + 12, NativeBattleEffectArena.Clear().Write(0, [horizontal]), 1);
        byte depth = unchecked((byte)((Roll(ref frame) & 3) - 2));
        frame = WriteBytes(frame, address + 14, NativeBattleEffectArena.Clear().Write(0, [depth]), 1);
        byte vertical = unchecked((byte)((Roll(ref frame) & 3) + 3));
        frame = WriteBytes(frame, address + 10, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)512)), 2);
        frame = WriteBytes(frame, address + 15, NativeBattleEffectArena.Clear().Write(0, [18]), 1);
        return WriteBytes(frame, address + 13, NativeBattleEffectArena.Clear().Write(0, [vertical]), 1);
    }

    public static NativeBattleEffect227Frame Advance(NativeBattleEffect227Frame frame)
    {
        uint first = checked(frame.State.ReadDword(ArenaGlobal) + FirstOffset);
        for (int index = 0; index < Count; index++)
        {
            uint address = checked(first + (uint)(index * Stride));
            NativeBattleEffectArena record = ReadBytes(frame, address, Stride);
            byte life = record.Read(15, 1)[0];
            if (life == 0)
            {
                continue;
            }
            record = record.Write(15, [unchecked((byte)(life - 1))]);
            for (int axis = 0; axis < 3; axis++)
            {
                int velocity = unchecked((sbyte)record.Read(12 + axis, 1)[0]) << 2;
                record = PutWord(record, 4 + axis * 2, SignedWord(record, 4 + axis * 2) + velocity);
            }
            frame = WriteBytes(frame, address, record, Stride);
        }
        return frame;
    }

    public static NativeBattleEffect227Frame Draw(NativeBattleEffect227Frame frame, NativeBattleEffect227Resources resources, NativeBattleEffect227Inputs inputs)
    {
        (frame, uint temporaryAddress) = NativeBattleEffect227Controller.Allocate(frame, 180);
        NativeBattleEffectArena temporary = Context(frame, temporaryAddress, 180);
        foreach (int offset in new[] { 0x48, 0x50, 0x58, 0x60 })
        {
            temporary = PutWord(temporary, offset, 0);
        }
        frame = PublishContext(frame, temporaryAddress, temporary, 180);
        uint first = checked(frame.State.ReadDword(ArenaGlobal) + FirstOffset);
        for (int index = 0; index < Count; index++)
        {
            NativeBattleEffectArena record = ReadBytes(frame, first + checked((uint)(index * Stride)), Stride);
            sbyte life = unchecked((sbyte)record.Read(15, 1)[0]);
            if (life == 0)
            {
                continue;
            }
            temporary = Context(frame, temporaryAddress, 180);
            frame = LoadMatrix(frame, inputs.ReadView(frame));
            NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(Registers(frame), 0, new(SignedWord(record, 4), SignedWord(record, 6), SignedWord(record, 8)), SignedWord(record, 10)));
            frame = Publish(frame, registers);
            for (int axis = 0; axis < 3; axis++)
            {
                temporary = Put(temporary, 0x98 + axis * 4, unchecked((uint)registers.Colors.Mac[axis]!.Value));
            }
            temporary = temporary.Write(0x84, new byte[20]);
            foreach (int offset in new[] { 0x84, 0x8C, 0x94 })
            {
                temporary = PutWord(temporary, offset, SignedWord(record, 10));
            }
            uint sourceAddress = Dword(record, 0);
            NativeBattleModuleResource sprite = resources.Sprite(sourceAddress);
            temporary = Put(temporary, 0, sourceAddress);
            int phases = BinaryPrimitives.ReadInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8, 2));
            if (phases == 0)
            {
                throw new DivideByZeroException("The current sprite frame count is zero.");
            }
            int selected = phases - (life - 1) % phases;
            int displacement = BinaryPrimitives.ReadInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8 + selected * 2, 2));
            int count = BinaryPrimitives.ReadInt32LittleEndian(sprite.Bytes.AsSpan().Slice(displacement, 4));
            temporary = Put(Put(temporary, 0x2C, checked(sourceAddress + (uint)displacement)), 0x30, unchecked((uint)count));
            if (count < 0)
            {
                temporary = temporary.Write(0x25, [unchecked((byte)(temporary.Read(0x25, 1)[0] | 1))]);
                temporary = Put(temporary, 0x30, unchecked((uint)(count & int.MaxValue)));
            }
            temporary = Put(temporary, 0x2C, checked(sourceAddress + (uint)displacement + 4));
            frame = PublishContext(frame, temporaryAddress, temporary, 180);
            NativeBattleShockwavePulsar260PuffStep draw = NativeBattleShockwavePulsar260ParticlesDrawing.DrawPuffQuads(frame.Memory, temporary, sprite, inputs.ReadProjection(frame));
            frame = PublishContext(frame with { Memory = draw.Memory }, temporaryAddress, draw.Temporary180, 180);
            frame = inputs.Execute(frame, new NativeBattleEffect227DrawBatch(draw.Batch, CursorGlobal));
        }
        return Release(frame, 180);
    }
}
