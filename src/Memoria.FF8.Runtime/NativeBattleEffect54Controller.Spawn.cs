using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect54Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect54Controller
{
    public static (NativeBattleEffect54Frame Frame, uint Address) AllocateDraw810EE0(NativeBattleEffect54Frame frame, uint emitter, int sourceIndex)
    {
        int index = unchecked((short)frame.State.ReadWord(DrawHintGlobal));
        if (index is < 0 or >= DrawCount)
        {
            throw new InvalidDataException("Effect54 drawable hint leaves its physical rows.");
        }
        uint result = 0;
        uint start = frame.State.ReadDword(DrawRecordsGlobal);
        for (int attempt = 0; attempt < DrawCount; attempt++)
        {
            uint row = checked(start + (uint)(index * DrawStride));
            if (ReadB54(frame, row + 0x1D7) == 0)
            {
                frame = ClearBytes(frame, row, DrawStride);
                frame = PutU54(frame, row + 0x1BC, emitter);
                frame = PutB54(frame, row + 0x1D7, 1);
                frame = PutB54(frame, row + 0x1D6, unchecked((byte)sourceIndex));
                frame = PutB54(frame, row + 0x1D9, ReadB54(frame, emitter + 0x6B));
                uint context = frame.State.ReadDword(CurrentContextGlobal);
                frame = PutW54(frame, context + 0x16, unchecked((ushort)(ReadW54(frame, context + 0x16) + 1)));
                frame = Link811790(frame, row, 1);
                result = row;
                break;
            }
            index++;
            if (index >= ReachableDrawCount)
            {
                index = 0;
            }
        }
        index++;
        return (frame with { State = frame.State.WriteWord(DrawHintGlobal, index >= ReachableDrawCount ? (ushort)0 : unchecked((ushort)index)) }, result);
    }

    private static NativeBattleEffect54Frame Spawn810700(NativeBattleEffect54Frame frame, uint emitter, int count, byte[] source, NativeBattleEffect54Resources resources, NativeBattleEffect54Inputs inputs)
    {
        if (count is < 1 or > 4 || source[0x16] != 0 || source[0x21] != 0 || source[0x27] != 0 || source[0x1E] != 0)
        {
            throw new NotSupportedException("Effect54 drawable birth leaves its selected finite descriptor.");
        }
        uint scratchAddress = checked(frame.State.ReadDword(ScratchPointerGlobal) - 80);
        frame = frame with { State = frame.State.WriteDword(ScratchPointerGlobal, scratchAddress) };
        NativeBattleEffectArena scratch = ReadBytes(frame, scratchAddress, 80);
        int sourceIndex = unchecked((sbyte)ReadB54(frame, emitter + 0x6A));
        (frame, uint rowAddress) = AllocateDraw810EE0(frame, emitter, sourceIndex);
        if (rowAddress == 0)
        {
            throw new InvalidDataException("Effect54 dereferenced its exhausted mandatory drawable allocation.");
        }
        NativeBattleEffectArena row = ReadBytes(frame, rowAddress, DrawStride);
        uint context = frame.State.ReadDword(CurrentContextGlobal);
        uint spriteTable = ReadU54(frame, context + 0x228);
        uint sprite = Dword54(Source(frame, resources, spriteTable + (uint)(sourceIndex * 4), 4), 0);
        ushort frames = unchecked((ushort)Word54(Source(frame, resources, sprite + 8, 2), 0));
        row = row.Write(0x170, BitConverter.GetBytes(sprite)).Write(0x1D1, [unchecked((byte)(frames - 1))]).Write(0x1D8, [unchecked((byte)count)]).Write(0x1DA, source.AsSpan(0x64, 2));
        int age = ReadS54(frame, emitter + 0x60);
        scratch = scratch.Write(0x4E, BitConverter.GetBytes(unchecked((short)age)));
        for (int instance = 0; instance < count; instance++)
        {
            row = CopyBytes(ReadBytes(frame, emitter + 0x4C, 16), 0, row, 0xDC + instance * 16, 16);
            if (source[0x22] == 0)
            {
                row = CopyBytes(ReadBytes(frame, emitter + 0x4C, 16), 0, row, 0x12C + instance * 16, 16);
            }
            if (source[0x34] == 1)
            {
                NativeBattleVector placement = new(unchecked((short)(Roll54(ref frame) & 4095)), unchecked((short)(Roll54(ref frame) & 4095)), unchecked((short)(Roll54(ref frame) & 4095)));
                scratch = PutPoint(scratch, 0x28, placement);
                (frame, NativeBattleEffectArena matrix) = EulerMatrix(frame, CopyBytes(scratch, 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 32), 0, 32), placement);
                scratch = CopyBytes(matrix, 0, scratch, 0, 32);
                short[] radii = new short[3];
                for (int lane = 0; lane < 3; lane++)
                {
                    uint channel = BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(0xCC + lane * 4));
                    radii[lane] = BinaryPrimitives.ReadInt16LittleEndian(resources.ReadStatic(unchecked(channel + (uint)(age * 2)), 2));
                    if (source[0x33] == 0 && radii[lane] != 0)
                    {
                        radii[lane] = unchecked((short)(Roll54(ref frame) % radii[lane]));
                    }
                    scratch = scratch.Write(0x48 + lane * 2, BitConverter.GetBytes(radii[lane]));
                }
                scratch = PutPoint(scratch, 0x20, new(0, -4096, 0));
                (frame, NativeBattleVector unit) = TransformVector(frame, Packed(scratch), CopyBytes(scratch, 0x20, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8));
                scratch = PutPoint(scratch, 0x20, unit);
                short[] units = [unit.X, unit.Y, unit.Z];
                for (int lane = 0; lane < 3; lane++)
                {
                    int offset = unchecked(radii[lane] * units[lane] << 4);
                    scratch = scratch.Write(0x38 + lane * 4, BitConverter.GetBytes(offset));
                    int at = 0x12C + instance * 16 + lane * 4;
                    row = row.Write(at, BitConverter.GetBytes(unchecked(Dword54(row, at) + (uint)offset)));
                }
            }
            else if (source[0x34] != 0)
            {
                throw new NotSupportedException("Effect54 selected placement only admits its actual descriptor types.");
            }
            NativeBattleEffectNodeRandomVector euler = NativeBattleEffectNodeRandom.Euler881FE0(frame.Random, VectorBytes54(source, 0x3C), VectorBytes54(source, 0x44));
            frame = frame with { Random = euler.Random };
            scratch = PutPoint(scratch, 0x30, euler.Value);
            NativeBattleEffectArena destination = CopyBytes(row, 12 + instance * 32, NativeBattleEffectArena.Clear().MarkUnknown(0, 32), 0, 32);
            (frame, destination) = EulerMatrix(frame, destination, euler.Value);
            (frame, destination) = MultiplyMatrix(frame, Packed(ReadBytes(frame, emitter + 0x2C, 32)), destination);
            row = CopyBytes(destination, 0, row, 12 + instance * 32, 32);
            row = row.Write(0x174 + instance * 4, BitConverter.GetBytes(Product54(ref frame, source, 0x4C))).Write(0x184 + instance * 4, BitConverter.GetBytes(Product54(ref frame, source, 0x54)));
        }
        NativeBattleEffectNodeRandomVector rotation = NativeBattleEffectNodeRandom.Euler881FE0(frame.Random, VectorBytes54(source, 0x148), VectorBytes54(source, 0x150));
        frame = frame with { Random = rotation.Random };
        row = PutPoint(row, 0x194, rotation.Value).Write(0x1DC, BitConverter.GetBytes(Product54(ref frame, source, 0x5C)));
        for (int instance = 0; instance < count; instance++)
        {
            for (int vector = 0; vector < 3; vector++)
            {
                for (int lane = 0; lane < 3; lane++)
                {
                    int sourceOffset = 0x68 + vector * 32 + lane * 4;
                    NativeBattleEffectNodeRandomValue value = NativeBattleEffectNodeRandom.ProductRange881F50(frame.Random, BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan(sourceOffset)), BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan(sourceOffset + 16)));
                    frame = frame with { Random = value.Random };
                    row = row.Write(0x1E0 + vector * 64 + instance * 16 + lane * 4, BitConverter.GetBytes(value.Value));
                }
            }
        }
        frame = WriteBytes(frame, rowAddress, row, DrawStride);
        frame = WriteBytes(frame, scratchAddress, scratch, 80);
        for (int child = 0; child < 4; child++)
        {
            if (source[0x28 + child] == 1)
            {
                (frame, _) = NativeBattleEffect54Lower.AllocateEmitter810560(frame, rowAddress, unchecked((sbyte)source[0x2C + child]), unchecked((sbyte)ReadB54(frame, rowAddress + 0x1D9)), resources, inputs);
            }
        }
        return frame with { State = frame.State.WriteDword(ScratchPointerGlobal, checked(frame.State.ReadDword(ScratchPointerGlobal) + 80)) };
    }

    private static NativeBattleVector VectorBytes54(byte[] source, int offset) => new(BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan(offset)), BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan(offset + 2)), BinaryPrimitives.ReadInt16LittleEndian(source.AsSpan(offset + 4)));
    private static int Product54(ref NativeBattleEffect54Frame frame, byte[] source, int offset)
    {
        NativeBattleEffectNodeRandomValue value = NativeBattleEffectNodeRandom.ProductRange881F50(frame.Random, BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan(offset)), BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan(offset + 4)));
        frame = frame with { Random = value.Random };
        return value.Value;
    }
    private static int Roll54(ref NativeBattleEffect54Frame frame)
    {
        NativeCrtRandomState random = frame.Random.Draw(out ushort value);
        frame = frame with { Random = random };
        return value;
    }
}
