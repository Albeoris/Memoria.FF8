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
    private const uint RinoaWorkAddress = 0x209D780;
    private const int RinoaWriteLength = 576;
    private const int RinoaReadLength = RinoaWriteLength + 6;

    [JsonIgnore]
    private RinoaHitWork? RinoaWork { get; init; }

    private sealed record RinoaHitWork(ImmutableArray<byte> Bytes, ImmutableArray<bool> Known, BattleModelOwner? Actor, BattleModelOwner? Target);

    internal NativeBattleModuleState InitializeRinoaPhysicalHitStorage(BattleContactTaskStorageContract contract)
    {
        return contract switch
        {
            BattleContactTaskStorageContract.OriginalArena => ClearRinoaPhysicalHitStorage(),
            BattleContactTaskStorageContract.ExpandableArena when RinoaWork is null => Initialize(),
            BattleContactTaskStorageContract.ExpandableArena => throw new InvalidOperationException("Rinoa's separated physical hit work region is already initialized."),
            _ => throw new ArgumentOutOfRangeException(nameof(contract))
        };

        NativeBattleModuleState Initialize()
        {
            byte[] bytes = ReadPhysicalModuleBytes(RinoaWorkAddress, RinoaReadLength);
            bool[] known = new bool[bytes.Length];
            int start = PhysicalOffset(RinoaWorkAddress, bytes.Length);
            for (int index = 0; index < bytes.Length; index++)
            {
                int lane = (start + index) % TaskStride;
                NativeBattleModuleTask slot = Slots[(start + index) / TaskStride];
                known[index] = lane < 12 || (slot.UnknownModelPointerBytes & (1u << (lane - 12))) == 0;
            }
            return this with { RinoaWork = new(bytes.ToImmutableArray(), known.ToImmutableArray(), null, null) };
        }
    }

    internal NativeBattleModuleState ClearRinoaPhysicalHitStorage() => RinoaWork is null ? this : this with { RinoaWork = null };

    internal NativeBattleRinoaPhysicalHitState ReadRinoaPhysicalHit(BattleContactTaskStorageContract contract, NativeBattleModelAliases aliases)
    {
        BattleModelOwner actor;
        BattleModelOwner target;
        byte[] bytes;
        if (contract == BattleContactTaskStorageContract.OriginalArena)
        {
            bytes = ReadPhysicalModuleBytes(RinoaWorkAddress, RinoaWriteLength);
            actor = aliases.DecodeLiteral(checked((byte)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4))));
            target = aliases.DecodeLiteral(checked((byte)BinaryPrimitives.ReadUInt32LittleEndian(bytes)));
        }
        else
        {
            RinoaHitWork work = RequireRinoaWork(contract);
            actor = RequireCurrent(work.Actor);
            target = RequireCurrent(work.Target);
            bytes = work.Bytes.AsSpan(0, RinoaWriteLength).ToArray();
        }
        return new(actor, target, new(Word(8), Word(10), Word(12), Word(14)), Point(16))
        {
            Controls = Points(24, 5),
            Curve = Points(64, 64)
        };

        BattleModelOwner RequireCurrent(BattleModelOwner? owner) => owner is not null && aliases.Roster.Contains(owner) ? owner : throw new InvalidDataException("Rinoa's physical hit work does not name a current model owner.");
        short Word(int offset)
        {
            if (contract == BattleContactTaskStorageContract.ExpandableArena && RinoaWork!.Known.AsSpan(offset, 2).Contains(false))
            {
                throw new InvalidDataException("Rinoa's physical hit scalar uses unknown retained bytes.");
            }
            return BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset, 2));
        }
        NativeBattleVector Point(int offset) => new(Word(offset), Word(offset + 2), Word(offset + 4));

        ImmutableArray<NativeBattleVector> Points(int first, int count)
        {
            var points = ImmutableArray.CreateBuilder<NativeBattleVector>(count);
            for (int index = 0; index < count; index++)
            {
                int offset = first + index * 8;
                if (contract == BattleContactTaskStorageContract.ExpandableArena && RinoaWork!.Known.AsSpan(offset, 6).Contains(false))
                {
                    return [];
                }
                points.Add(Point(offset));
            }
            return points.MoveToImmutable();
        }
    }

    internal NativeBattleModuleState WriteRinoaPhysicalHit(BattleContactTaskStorageContract contract, NativeBattleModelAliases aliases, NativeBattleRinoaPhysicalHitState value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!aliases.Roster.Contains(value.Actor) || !aliases.Roster.Contains(value.Target) || value.Controls.Length > 5 || value.Curve.Length > 64)
        {
            throw new InvalidDataException("Rinoa's physical hit data exceeds its work region or names an unbound model owner.");
        }
        NativeBattleModuleState current = this;
        RinoaHitWork? work = contract == BattleContactTaskStorageContract.OriginalArena ? null : RequireRinoaWork(contract);
        byte[]? bytes = work?.Bytes.ToArray();
        bool[]? known = work?.Known.ToArray();
        if (work is null)
        {
            ImmutableArray<byte> target = aliases.EncodeOperand(value.Target, 4) ?? throw new InvalidDataException("Rinoa's original target has no exact native alias.");
            ImmutableArray<byte> actor = aliases.EncodeOperand(value.Actor, 4) ?? throw new InvalidDataException("Rinoa's original caster has no exact native alias.");
            Write(0, target.AsSpan());
            Write(4, actor.AsSpan());
        }
        Write(8, BitConverter.GetBytes(value.TargetAnchor.X).Concat(BitConverter.GetBytes(value.TargetAnchor.Y)).Concat(BitConverter.GetBytes(value.TargetAnchor.Z)).Concat(BitConverter.GetBytes(value.TargetAnchor.W)).ToArray());
        Point(16, value.Origin);
        for (int index = 0; index < value.Controls.Length; index++)
        {
            Point(24 + index * 8, value.Controls[index]);
        }
        for (int index = 0; index < value.Curve.Length; index++)
        {
            Point(64 + index * 8, value.Curve[index]);
        }
        return work is null ? current : current with { RinoaWork = new(bytes!.ToImmutableArray(), known!.ToImmutableArray(), value.Actor, value.Target) };

        void Write(int offset, ReadOnlySpan<byte> source)
        {
            if (bytes is null)
            {
                current = current.WritePhysicalModuleBytes(RinoaWorkAddress + (uint)offset, source);
            }
            else
            {
                source.CopyTo(bytes.AsSpan(offset));
                known!.AsSpan(offset, source.Length).Fill(true);
            }
        }
        void Point(int offset, NativeBattleVector point)
        {
            Span<byte> pointBytes = stackalloc byte[6];
            BinaryPrimitives.WriteInt16LittleEndian(pointBytes, point.X);
            BinaryPrimitives.WriteInt16LittleEndian(pointBytes[2..], point.Y);
            BinaryPrimitives.WriteInt16LittleEndian(pointBytes[4..], point.Z);
            Write(offset, pointBytes);
        }
    }

    internal NativeBattleEffectArena ReadRinoaPhysicalHitView(BattleContactTaskStorageContract contract, int offset, int length)
    {
        if (offset < 8 || length < 0 || offset > RinoaReadLength - length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }
        if (contract == BattleContactTaskStorageContract.OriginalArena)
        {
            return NativeBattleEffectArena.Clear().Write(0, ReadPhysicalModuleBytes(RinoaWorkAddress + (uint)offset, length));
        }
        RinoaHitWork work = RequireRinoaWork(contract);
        NativeBattleEffectArena view = NativeBattleEffectArena.Clear().Write(0, work.Bytes.AsSpan(offset, length));
        int index = 0;
        while (index < length)
        {
            while (index < length && work.Known[offset + index])
            {
                index++;
            }
            int start = index;
            while (index < length && !work.Known[offset + index])
            {
                index++;
            }
            if (start != index)
            {
                view = view.MarkUnknown(start, index - start);
            }
        }
        return view;
    }

    private RinoaHitWork RequireRinoaWork(BattleContactTaskStorageContract contract)
    {
        if (contract != BattleContactTaskStorageContract.ExpandableArena)
        {
            throw new ArgumentOutOfRangeException(nameof(contract));
        }
        return RinoaWork ?? throw new InvalidOperationException("Rinoa's separated physical hit work region must be initialized at battle admission.");
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
            int firstPayload = Math.Max(lane - 12, 0);
            int stopPayload = Math.Max(lane + count - 12, 0);
            uint writtenPayload = ((1u << (stopPayload - firstPayload)) - 1) << firstPayload;
            bool droppedBinding = changedCallback || source.ModelPointerOffset is int pointer && (writtenPayload & (15u << pointer)) != 0;
            slots = slots.SetItem(index, source with
            {
                Flags = BinaryPrimitives.ReadUInt16LittleEndian(task),
                State = BinaryPrimitives.ReadUInt16LittleEndian(task.AsSpan(2)),
                Next = next == 0 || !validNext ? null : checked((int)((next - BankAddress) / TaskStride)),
                UnlinkedNextPointer = next != 0 && !validNext ? next : null,
                Kind = changedCallback ? NativeBattleModuleKind.None : source.Kind,
                Callback = changedCallback ? callback : source.Callback,
                Payload = task.AsSpan(12).ToArray().ToImmutableArray(),
                ModelOwner = droppedBinding ? null : source.ModelOwner,
                ModelPointerOffset = droppedBinding ? null : source.ModelPointerOffset,
                UnknownModelPointerBytes = source.UnknownModelPointerBytes & ~writtenPayload
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
