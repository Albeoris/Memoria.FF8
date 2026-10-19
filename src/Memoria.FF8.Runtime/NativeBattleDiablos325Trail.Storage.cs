using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDiablos325TrailBirth(NativeBattleDiablos325Frame Frame, uint? Point);

public sealed partial record NativeBattleDiablos325Trail
{
    public static NativeBattleDiablos325TrailBirth Spawn658890(NativeBattleDiablos325Frame frame, ReadOnlySpan<byte> context)
    {
        if (context.Length != 48)
        {
            throw new InvalidDataException("Native Diablos trail birth requires its complete forty-eight-byte context.");
        }
        NativeBattleEffectArena source = frame.Memory.Arena ?? throw new NotSupportedException("Native Diablos trails require their retained arena.");
        using NativeBattleEffectArenaTransaction? owned = source.Transaction is null ? new(source) : null;
        NativeBattleEffectArenaWriter writer = new(owned?.View ?? source);
        Storage storage = new(writer, NativeBattleDiablos325Entry.RequireState(frame.Memory));
        uint? point = storage.Spawn(context);
        return new(frame with { Memory = frame.Memory with { Arena = owned?.Freeze() ?? writer.Freeze(), Diablos325 = storage.Freeze() } }, point);
    }

    public static NativeBattleDiablos325Frame Advance6583E0(NativeBattleDiablos325Frame frame, ReadOnlySpan<byte> widths)
    {
        if (widths.Length != 4098)
        {
            throw new NotSupportedException("Native Diablos trail updates require their original width table.");
        }
        NativeBattleEffectArena source = frame.Memory.Arena ?? throw new NotSupportedException("Native Diablos trails require their retained arena.");
        using NativeBattleEffectArenaTransaction? owned = source.Transaction is null ? new(source) : null;
        NativeBattleEffectArenaWriter writer = new(owned?.View ?? source);
        Storage storage = new(writer, NativeBattleDiablos325Entry.RequireState(frame.Memory));
        storage.Advance(widths);
        return frame with { Memory = frame.Memory with { Arena = owned?.Freeze() ?? writer.Freeze(), Diablos325 = storage.Freeze() } };
    }

    private sealed class Storage
    {
        private readonly NativeBattleEffectArenaWriter writer;
        private readonly NativeBattleDiablos325State original;
        private readonly NativeBattleDiablos325Trail parameters;
        private readonly uint origin;
        private uint pointCursor;
        private uint blockCursor;
        private uint random;
        private byte mode;

        public Storage(NativeBattleEffectArenaWriter writer, NativeBattleDiablos325State state)
        {
            this.writer = writer;
            original = state;
            parameters = state.Trail ?? throw new NotSupportedException("Native Diablos trails require their executed module initialization.");
            origin = state.ModelBuffer5208;
            pointCursor = parameters.Cursor51F8;
            blockCursor = parameters.BlockCursor51FC;
            random = state.Control51E4;
            mode = state.Mode51F0;
            RequireRecord(pointCursor, 0x4C5C, 24, 512);
            RequireRecord(blockCursor, 0x7C5C, 48, 64);
        }

        public NativeBattleDiablos325State Freeze() => original with { Control51E4 = random, Mode51F0 = mode, Trail = parameters with { Cursor51F8 = pointCursor, BlockCursor51FC = blockCursor } };

        public uint? Spawn(ReadOnlySpan<byte> context)
        {
            uint selected = blockCursor;
            if (Dword(selected) != 0)
            {
                selected = origin + 0x7C5C;
                int remaining = 64;
                while (Dword(selected) != 0)
                {
                    remaining--;
                    if (remaining == 0)
                    {
                        return null;
                    }
                    selected += 48;
                }
            }
            blockCursor = selected >= origin + 0x882C ? origin + 0x7C5C : selected + 48;
            uint? allocated = AllocatePoint();
            if (allocated is not uint point)
            {
                return null;
            }
            Put(point, context.Slice(8, 8));
            Put(point + 8, new byte[8]);
            Put(point + 16, context.Slice(40, 4));
            Dword(point + 20, 0);
            Dword(selected, point);
            Dword(selected + 4, point);
            Put(selected + 8, context.Slice(8, 36));
            Word(selected + 44, 1);
            Put(selected + 46, context.Slice(46, 2));
            mode = 2;
            return point;
        }

        public void Advance(ReadOnlySpan<byte> widths)
        {
            for (int index = 0; index < 64; index++)
            {
                uint block = origin + 0x7C5C + (uint)index * 48;
                uint head = Dword(block);
                if (head == 0)
                {
                    continue;
                }
                short fade = unchecked((short)(Short(block + 46) - 256));
                Word(block + 46, fade);
                if (fade <= 0)
                {
                    pointCursor = head;
                    uint current = head;
                    int bound = 0;
                    do
                    {
                        RequireRecord(current, 0x4C5C, 24, 512);
                        Word(current + 6, -1);
                        current = Dword(current + 20);
                        if (++bound > 512)
                        {
                            throw new InvalidDataException("Native Diablos trail points contain a cyclic physical link.");
                        }
                    }
                    while (current != 0);
                    Dword(block, 0);
                    blockCursor = block;
                    continue;
                }
                if (Short(block + 22) < 4096)
                {
                    Word(block + 22, unchecked((short)(Short(block + 22) + Short(block + 30))));
                    for (uint axis = 0; axis < 3; axis++)
                    {
                        uint coordinate = block + 8 + axis * 2;
                        Word(coordinate, unchecked((short)(Short(coordinate) + Short(block + 24 + axis * 2))));
                    }
                    for (uint axis = 0; axis < 3; axis++)
                    {
                        uint velocity = block + 24 + axis * 2;
                        int jitter = (int)Draw() >> (parameters.Shift5200 & 31);
                        Word(velocity, unchecked((short)(Short(velocity) + jitter - parameters.Step5202)));
                    }
                    if ((int)Draw() < unchecked((short)parameters.Parameter5204))
                    {
                        for (uint axis = 0; axis < 3; axis++)
                        {
                            uint coordinate = block + 8 + axis * 2;
                            int jitter = (int)Draw() >> ((parameters.Shift5200 - 2) & 31);
                            Word(coordinate, unchecked((short)(Short(coordinate) + jitter - unchecked((ushort)(parameters.Step5202 << 2)))));
                        }
                    }
                    short fraction = Short(block + 22);
                    NativeBattleVector position;
                    if (fraction >= 4096)
                    {
                        Word(block + 22, 4096);
                        fraction = 4096;
                        position = Vector(block + 16);
                    }
                    else
                    {
                        position = NativeBattleQuezacotlRibbonMath.Blend(Vector(block + 8), Vector(block + 16), 4096 - fraction, fraction);
                    }
                    uint? selected = AllocatePoint();
                    if (selected is not uint point)
                    {
                        Word(block + 22, 4096);
                        return;
                    }
                    Dword(point + 20, 0);
                    Vector(point, position);
                    Word(point + 6, Short(block + 14));
                    Dword(point + 16, Dword(block + 40));
                    uint tail = Dword(block + 4);
                    RequireRecord(tail, 0x4C5C, 24, 512);
                    Dword(tail + 20, point);
                    Dword(block + 4, point);
                    int widthIndex = fraction >> 1;
                    short weight = BinaryPrimitives.ReadInt16LittleEndian(widths.Slice(widthIndex * 2, 2));
                    Vector(point + 8, NativeBattleQuezacotlRibbonMath.Blend(Vector(block + 32), new(0, 0, 0), weight, 0));
                    Word(block + 44, unchecked((short)(Short(block + 44) + 1)));
                    if (fraction < 3072 && (int)Draw() < unchecked(Short(block + 38) << 13))
                    {
                        Split(block, point, position);
                    }
                }
                uint next = Dword(block);
                int visited = 0;
                do
                {
                    RequireRecord(next, 0x4C5C, 24, 512);
                    short color = unchecked((short)(Short(next + 16) + Short(next + 18)));
                    Word(next + 16, color < 0 ? (short)0 : color);
                    Vector(next, Add(Vector(next), Vector(next + 8)));
                    next = Dword(next + 20);
                    if (++visited > 512)
                    {
                        throw new InvalidDataException("Native Diablos trail points contain a cyclic physical link.");
                    }
                }
                while (next != 0);
                while (true)
                {
                    uint first = Dword(block);
                    RequireRecord(first, 0x4C5C, 24, 512);
                    if (Short(first + 16) > 1024)
                    {
                        break;
                    }
                    uint second = Dword(first + 20);
                    RequireRecord(second, 0x4C5C, 24, 512);
                    if (Short(second + 16) > 1024)
                    {
                        break;
                    }
                    pointCursor = first;
                    Word(first + 6, -1);
                    Dword(block, second);
                    short count = unchecked((short)(Short(block + 44) - 1));
                    Word(block + 44, count);
                    if (count < 2)
                    {
                        Dword(block, 0);
                        blockCursor = block;
                        break;
                    }
                }
            }
        }

        private void Split(uint block, uint point, NativeBattleVector position)
        {
            byte[] context = new byte[48];
            PutVector(context.AsSpan(8), position);
            BinaryPrimitives.WriteInt16LittleEndian(context.AsSpan(14), Short(block + 14));
            for (uint axis = 0; axis < 3; axis++)
            {
                int sign = Draw() > 16384 ? -1 : 1;
                int displacement = sign * ((int)(Draw() & 511) + 512);
                BinaryPrimitives.WriteInt16LittleEndian(context.AsSpan(16 + (int)axis * 2), unchecked((short)(Short(block + 16 + axis * 2) + displacement)));
            }
            BinaryPrimitives.WriteInt16LittleEndian(context.AsSpan(22), Short(block + 22));
            for (uint axis = 0; axis < 3; axis++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(context.AsSpan(24 + (int)axis * 2), unchecked((short)(Short(block + 24 + axis * 2) + (int)(Draw() & 511) - 256)));
            }
            BinaryPrimitives.WriteInt16LittleEndian(context.AsSpan(30), Short(block + 30));
            Put(context.AsSpan(32, 4), Dword(block + 32));
            BinaryPrimitives.WriteInt16LittleEndian(context.AsSpan(36), Short(block + 36));
            Put(context.AsSpan(40, 4), Dword(block + 40));
            BinaryPrimitives.WriteInt16LittleEndian(context.AsSpan(46), Short(block + 46));
            uint? allocated = Spawn(context);
            if (allocated is uint child)
            {
                Dword(child + 8, Dword(point));
                Dword(child + 12, Dword(point + 4));
            }
            Word(block + 38, unchecked((short)(Short(block + 38) - 1)));
        }

        private uint? AllocatePoint()
        {
            uint selected = pointCursor;
            if (Short(selected + 6) != -1)
            {
                selected = origin + 0x4C5C;
                int remaining = 512;
                while (Short(selected + 6) != -1)
                {
                    remaining--;
                    if (remaining == 0)
                    {
                        return null;
                    }
                    selected += 24;
                }
            }
            pointCursor = selected >= origin + 0x7C44 ? origin + 0x4C5C : selected + 24;
            return selected;
        }

        private uint Draw()
        {
            random = unchecked(random * 125 + 14) & 32767;
            return random;
        }

        private static NativeBattleVector Add(NativeBattleVector first, NativeBattleVector second) => new(unchecked((short)(first.X + second.X)), unchecked((short)(first.Y + second.Y)), unchecked((short)(first.Z + second.Z)));
        private NativeBattleVector Vector(uint address) => new(Short(address), Short(address + 2), Short(address + 4));
        private void Vector(uint address, NativeBattleVector value)
        {
            Word(address, value.X);
            Word(address + 2, value.Y);
            Word(address + 4, value.Z);
        }
        private short Short(uint address) => BinaryPrimitives.ReadInt16LittleEndian(writer.ReadCurrent(Offset(address, 2), 2));
        private uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(writer.ReadCurrent(Offset(address, 4), 4));
        private void Word(uint address, short value)
        {
            Span<byte> bytes = stackalloc byte[2];
            BinaryPrimitives.WriteInt16LittleEndian(bytes, value);
            writer.Write(Offset(address, 2), bytes);
        }
        private void Dword(uint address, uint value)
        {
            Span<byte> bytes = stackalloc byte[4];
            Put(bytes, value);
            writer.Write(Offset(address, 4), bytes);
        }
        private void Put(uint address, ReadOnlySpan<byte> bytes) => writer.Write(Offset(address, bytes.Length), bytes);
        private static int Offset(uint address, int length) => NativeBattleWaterExecutionFields.ArenaOffset(address, length);
        private void RequireRecord(uint address, uint start, uint stride, uint count)
        {
            long offset = (long)address - origin - start;
            if (offset < 0 || offset >= stride * count || offset % stride != 0)
            {
                throw new InvalidDataException("Native Diablos trail link leaves its original physical record bank.");
            }
        }
        private static void Put(Span<byte> bytes, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        private static void PutVector(Span<byte> bytes, NativeBattleVector value)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes, value.X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes[2..], value.Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes[4..], value.Z);
        }
    }
}
