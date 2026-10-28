using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleModuleTask
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public uint? UnlinkedNextPointer { get; init; }
}

public sealed partial record NativeBattleModuleState
{
    [JsonIgnore]
    public NativeBattleRinoaPhysicalHitState? RinoaPhysicalHit
    {
        get
        {
            byte[] bytes = ReadPhysicalModuleBytes(0x209D780, 576);
            return new(checked((byte)Dword(4)), checked((byte)Dword(0)), new(Word(8), Word(10), Word(12), Word(14)), Point(16))
            {
                Controls = Enumerable.Range(0, 5).Select(index => Point(24 + index * 8)).ToImmutableArray(),
                Curve = Enumerable.Range(0, 64).Select(index => Point(64 + index * 8)).ToImmutableArray()
            };

            uint Dword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
            short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset));
            NativeBattleVector Point(int offset) => new(Word(offset), Word(offset + 2), Word(offset + 4));
        }
        init
        {
            if (value is null)
            {
                return;
            }
            NativeBattleModuleState current = this;
            current = current.WritePhysicalModuleBytes(0x209D780, BitConverter.GetBytes((uint)value.Target));
            current = current.WritePhysicalModuleBytes(0x209D784, BitConverter.GetBytes((uint)value.Actor));
            current = current.WritePhysicalModuleBytes(0x209D788, BitConverter.GetBytes(value.TargetAnchor.X).Concat(BitConverter.GetBytes(value.TargetAnchor.Y)).Concat(BitConverter.GetBytes(value.TargetAnchor.Z)).Concat(BitConverter.GetBytes(value.TargetAnchor.W)).ToArray());
            Point(0x209D790, value.Origin);
            for (int index = 0; index < value.Controls.Length; index++)
            {
                Point(checked(0x209D798u + (uint)index * 8), value.Controls[index]);
            }
            for (int index = 0; index < value.Curve.Length; index++)
            {
                Point(checked(0x209D7C0u + (uint)index * 8), value.Curve[index]);
            }
            Slots = current.Slots;

            void Point(uint address, NativeBattleVector point)
            {
                byte[] bytes = new byte[6];
                BinaryPrimitives.WriteInt16LittleEndian(bytes, point.X);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), point.Y);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), point.Z);
                current = current.WritePhysicalModuleBytes(address, bytes);
            }
        }
    }

    public byte[] ReadPhysicalModuleBytes(uint address, int length)
    {
        int offset = PhysicalOffset(address, length);
        byte[] result = new byte[length];
        int copied = 0;
        while (copied < length)
        {
            int index = (offset + copied) / TaskStride;
            int lane = (offset + copied) % TaskStride;
            byte[] task = PhysicalTaskBytes(Slots[index]);
            int count = Math.Min(length - copied, TaskStride - lane);
            task.AsSpan(lane, count).CopyTo(result.AsSpan(copied));
            copied += count;
        }
        return result;
    }

    public NativeBattleModuleState WritePhysicalModuleBytes(uint address, ReadOnlySpan<byte> bytes)
    {
        int offset = PhysicalOffset(address, bytes.Length);
        ImmutableArray<NativeBattleModuleTask> slots = Slots;
        int copied = 0;
        while (copied < bytes.Length)
        {
            int index = (offset + copied) / TaskStride;
            int lane = (offset + copied) % TaskStride;
            NativeBattleModuleTask source = slots[index];
            byte[] task = PhysicalTaskBytes(source);
            int count = Math.Min(bytes.Length - copied, TaskStride - lane);
            bytes.Slice(copied, count).CopyTo(task.AsSpan(lane));
            uint next = BinaryPrimitives.ReadUInt32LittleEndian(task.AsSpan(4));
            uint callback = BinaryPrimitives.ReadUInt32LittleEndian(task.AsSpan(8));
            bool changedCallback = callback != PhysicalCallback(source);
            bool validNext = next >= BankAddress && next < DescriptorAddress && (next - BankAddress) % TaskStride == 0;
            slots = slots.SetItem(index, source with
            {
                Flags = BinaryPrimitives.ReadUInt16LittleEndian(task),
                State = BinaryPrimitives.ReadUInt16LittleEndian(task.AsSpan(2)),
                Next = next == 0 || !validNext ? null : checked((int)((next - BankAddress) / TaskStride)),
                UnlinkedNextPointer = next != 0 && !validNext ? next : null,
                Kind = changedCallback ? NativeBattleModuleKind.None : source.Kind,
                Callback = changedCallback ? callback : source.Callback,
                Payload = task.AsSpan(12).ToArray().ToImmutableArray()
            });
            copied += count;
        }
        return this with { Slots = slots };
    }

    private static byte[] PhysicalTaskBytes(NativeBattleModuleTask task)
    {
        byte[] bytes = new byte[TaskStride];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, task.Flags);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), task.State);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), task.UnlinkedNextPointer ?? (task.Next is int next ? checked(BankAddress + (uint)next * TaskStride) : 0));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), PhysicalCallback(task));
        task.Payload.AsSpan().CopyTo(bytes.AsSpan(12));
        return bytes;
    }

    private static uint PhysicalCallback(NativeBattleModuleTask task) => task.Callback ?? (task.Kind == NativeBattleModuleKind.None ? 0 : Convert.ToUInt32(task.Kind.ToString()[^6..], 16));

    private static int PhysicalOffset(uint address, int length)
    {
        long offset = address - (long)BankAddress;
        if (offset < 0 || length < 0 || offset + length > TaskStride * TaskCapacity)
        {
            throw new InvalidDataException("Native shared module access leaves its physical task bank.");
        }
        return checked((int)offset);
    }
}
