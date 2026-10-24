using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect80Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect80Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect80Trails
{
    public static NativeBattleEffect80Frame Draw7AF4D0(NativeBattleEffect80Frame frame, NativeBattleEffect80Resources resources, NativeBattleEffect80Inputs inputs)
    {
        frame = Bind(frame, resources);
        uint pointer = frame.State.ReadDword(TrailPointerAddress);
        (frame, uint address) = Allocate(frame, 688);
        NativeBattleEffectArena temporary = Context(frame, address, 688).MarkUnknown(0, 4).Write(0xC, BitConverter.GetBytes(0u)).Write(0x10, BitConverter.GetBytes(0x80F0F0u)).Write(0x14, BitConverter.GetBytes(0x40F0u)).Write(0x18, BitConverter.GetBytes(0x80F0F0u)).Write(0x2C, BitConverter.GetBytes((int)SignedWord(frame.State, 0x25DF322)));
        frame = LoadMatrix(frame, inputs.ReadView(frame));
        NativeBattleGeometryRegisterStep registers = Registers(frame);
        List<NativeBattleGfGeometryAttempt> attempts = [];
        int allocated = 0;
        for (uint recordIndex = 0; recordIndex < 64; recordIndex++)
        {
            uint record = pointer + 0x3000 + recordIndex * 48;
            uint node = frame.State.ReadDword(record);
            int count = SignedWord(frame.State, record + 0x2C);
            if (node == 0 || count < 2)
            {
                continue;
            }
            if (count > 32)
            {
                throw new NotSupportedException("Effect80 trail projection leaves its actual temporary row owner.");
            }
            short fade = SignedWord(frame.State, record + 0x2E);
            for (int color = 3; color >= 0; color--)
            {
                if (fade >= 4096)
                {
                    temporary = NativeBattleEffectNodeContextMemory.Copy(temporary, 0xC + color * 4, temporary, 0x1C + color * 4, 4);
                }
                else
                {
                    registers = WeightColor(registers, temporary, 0xC + color * 4, fade, false);
                    temporary = StoreColor(temporary, 0x1C + color * 4, registers);
                }
            }
            int rowCount = 0;
            int shape = 0;
            while (node != 0)
            {
                if (rowCount >= 32)
                {
                    throw new InvalidDataException("Effect80 trail chain exceeds its current projected row owner.");
                }
                NativeBattleEffectArena source = frame.State.ReadView(node, 8);
                ReadOnlySpan<byte> xyz = source.Read(0, 6);
                registers = NativeBattleGeometry.LoadVector(registers, 0, new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz[2..]), BinaryPrimitives.ReadInt16LittleEndian(xyz[4..])), SignedWord(frame.State, node + 6));
                NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect80 trail requires current projection controls.");
                registers = NativeBattleGeometry.ProjectSingle(registers, inputs.ReadProjection(frame), controls.Dqa, controls.Dqb);
                int row = 0x30 + rowCount * 20;
                temporary = temporary.Write(row, BitConverter.GetBytes(registers.Geometry.Sxy[2]!.Value)).Write(row + 4, BitConverter.GetBytes(unchecked((uint)((int)registers.Geometry.Sz[3]!.Value >> 2))));
                short coefficient = BinaryPrimitives.ReadInt16LittleEndian(resources.ReadStatic(ShapeTableAddress + (uint)shape, 2).AsSpan());
                temporary = temporary.Write(row + 6, BitConverter.GetBytes(unchecked((short)((SignedWord(frame.State, node + 6) * coefficient) >> 12))));
                int phase = SignedWord(frame.State, node + 0x10);
                int fraction = phase & 0xFFF;
                int colorIndex = phase >> 12;
                registers = WeightColor(registers, temporary, 0x1C + colorIndex * 4, 4096 - fraction, false);
                registers = WeightColor(registers, temporary, 0x20 + colorIndex * 4, fraction, true);
                temporary = StoreColor(temporary, row + 0x10, registers);
                rowCount++;
                shape += (2048 / count) * 2;
                node = frame.State.ReadDword(node + 0x14);
            }
            if (rowCount != count)
            {
                throw new InvalidDataException("Effect80 trail record count differs from its current physical chain.");
            }
            (byte[] directionBytes, bool[] directionKnown) = temporary.ReadSnapshot(4, 4);
            short? previousX = directionKnown[0] && directionKnown[1] ? BinaryPrimitives.ReadInt16LittleEndian(directionBytes) : null;
            short? previousY = directionKnown[2] && directionKnown[3] ? BinaryPrimitives.ReadInt16LittleEndian(directionBytes.AsSpan(2)) : null;
            for (int index = 0; index < count; index++)
            {
                int row = 0x30 + index * 20;
                int next = index == count - 1 ? -1 : row + 20;
                if (Word(temporary, row + 4) < 0 || next >= 0 && Word(temporary, next + 4) < 0)
                {
                    continue;
                }
                int dx = 0;
                int dy = 0;
                if (next >= 0)
                {
                    int differenceX = Word(temporary, next) - Word(temporary, row);
                    int differenceY = Word(temporary, row + 2) - Word(temporary, next + 2);
                    int distance = resources.ReadStatic(DistanceAddress + (uint)(Math.Min(Math.Abs(differenceY), 127) * 128 + Math.Min(Math.Abs(differenceX), 127)), 1)[0];
                    NativeBattleRibbonDirectionStep direction = NativeBattleRibbonDirection.Resolve(differenceX, differenceY, distance, index != 0, previousX, previousY);
                    previousX = direction.PreviousX;
                    previousY = direction.PreviousY;
                    if (!direction.Valid)
                    {
                        temporary = temporary.Write(row + 4, BitConverter.GetBytes((short)-1));
                        continue;
                    }
                    int width = Word(temporary, next + 6) / (Word(temporary, row + 4) + 128);
                    dx = unchecked(width * direction.X) >> 12;
                    dy = unchecked(width * direction.Y) >> 12;
                    temporary = temporary.Write(4, BitConverter.GetBytes(previousX!.Value)).Write(6, BitConverter.GetBytes(previousY!.Value));
                }
                short x = Word(temporary, row);
                short y = Word(temporary, row + 2);
                temporary = temporary.Write(row, BitConverter.GetBytes(unchecked((short)(x - dx)))).Write(row + 2, BitConverter.GetBytes(unchecked((short)(y - dy)))).Write(row + 8, BitConverter.GetBytes(unchecked((short)(x + dx)))).Write(row + 10, BitConverter.GetBytes(unchecked((short)(y + dy))));
            }
            for (int index = 0; index < count - 1; index++)
            {
                int row = 0x30 + index * 20;
                if (Word(temporary, row + 4) <= 0 || Word(temporary, row + 24) <= 0)
                {
                    continue;
                }
                int bias = BinaryPrimitives.ReadInt32LittleEndian(temporary.Read(0x2C, 4));
                int bucket = 17 + Math.Max(0, ((Word(temporary, row + 4) + Word(temporary, row + 24)) >> 3) + bias);
                for (int variant = 0; variant < 2; variant++)
                {
                    NativeBattleEffectArena packet = NativeBattleEffectArena.Clear().MarkUnknown(0, 52).Write(0, BitConverter.GetBytes(0x0C000000u)).Write(12, BitConverter.GetBytes(variant == 0 ? 0x3E54F808u : 0x3E54F908u)).Write(24, BitConverter.GetBytes(variant == 0 ? 0x00B9F838u : 0x00D9F938u)).Write(36, BitConverter.GetBytes(variant == 0 ? (ushort)0xF808 : (ushort)0xF908)).Write(48, BitConverter.GetBytes(variant == 0 ? (ushort)0xF838 : (ushort)0xF938));
                    int[] destinations = [8, 20, 32, 44, 4, 16, 28, 40];
                    int[] sources = [row, row + 8, row + 20, row + 28, row + 16, row + 16, row + 36, row + 36];
                    for (int field = 0; field < destinations.Length; field++)
                    {
                        packet = NativeBattleEffectNodeContextMemory.Copy(temporary, sources[field], packet, destinations[field], 4);
                    }
                    packet = packet.Write(7, [0x3E]);
                    (byte[] data, bool[] known) = packet.ReadSnapshot(0, 52);
                    NativeBattleGfPacketByteState[] mask = Enumerable.Range(0, 52).Select(offset => offset is 38 or 39 or 50 or 51 ? NativeBattleGfPacketByteState.Untouched : known[offset] ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Unknown).ToArray();
                    NativeBattleGeometryPrimitive primitive = new(bucket, Enumerable.Range(0, 13).Select(word => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(word * 4))).ToImmutableArray(), [0, 0, 0, 0]) { PhysicalWriteMask = mask.ToImmutableArray() };
                    attempts.Add(new(allocated, data.ToImmutableArray(), mask.ToImmutableArray(), primitive));
                    allocated += 52;
                }
            }
        }
        frame = PublishContext(Publish(frame, registers), address, temporary, 688);
        frame = inputs.Execute(frame, new NativeBattleEffect80DeliverBatch(new(attempts.ToImmutableArray(), allocated), 0x1D8E054));
        return Release(frame, 688);
    }

    private static short Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    private static NativeBattleGeometryRegisterStep WeightColor(NativeBattleGeometryRegisterStep registers, NativeBattleEffectArena arena, int offset, int factor, bool accumulate)
    {
        ReadOnlySpan<byte> color = arena.Read(offset, 3);
        registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)factor) } }, color[0], color[1], color[2]);
        return NativeBattleRaldo19BallRendering.Weighted(registers, accumulate, NativeBattleGteColorInterpolation.CodeConstants45F270);
    }
    private static NativeBattleEffectArena StoreColor(NativeBattleEffectArena arena, int offset, NativeBattleGeometryRegisterStep registers)
    {
        return arena.Write(offset, [unchecked((byte)registers.Colors.Ir[0]!.Value), unchecked((byte)registers.Colors.Ir[1]!.Value), unchecked((byte)registers.Colors.Ir[2]!.Value)]);
    }
}
