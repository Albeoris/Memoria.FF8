using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>A bounded slot view over one persistent physical bank, or a detached persistence node.</summary>
[JsonConverter(typeof(NativeBattleModulePoolTaskJsonConverter))]
public sealed partial record NativeBattleModulePoolTask : INativeBattleStorageNode
{
    private readonly NativeBattleTaskBankOwner? owner;
    private readonly NativeBattleTaskBankSnapshot? snapshot;
    private readonly int slot;

    public NativeBattleModulePoolTask(ushort Flags, ushort State, int? Next, uint Callback, ImmutableArray<byte> Payload)
    {
        if (Payload.IsDefault || Payload.Length > ushort.MaxValue - 12)
        {
            throw new InvalidDataException("Native standalone task requires its bounded physical payload shape.");
        }
        owner = new(0, 0, checked((ushort)(Payload.Length + 12)), [new(Flags, State, Next, Callback, Payload, null, null)], null, null, false);
    }

    private NativeBattleModulePoolTask(NativeBattleTaskBankOwner owner, int slot)
    {
        this.owner = owner;
        this.slot = slot;
    }

    private NativeBattleModulePoolTask(NativeBattleTaskBankSnapshot snapshot, int slot)
    {
        this.snapshot = snapshot;
        this.slot = slot;
    }

    internal static NativeBattleModulePoolTask FromOwner(NativeBattleTaskBankOwner owner, int slot) => new(owner, slot);
    internal static NativeBattleModulePoolTask FromSnapshot(NativeBattleTaskBankSnapshot snapshot, int slot) => new(snapshot, slot);
    private int Start => checked(RequireOwner().StorageOffset + slot * RequireOwner().Stride);
    internal NativeBattleTaskBankOwner RequireOwner() => owner?.RequireActive() ?? throw new InvalidOperationException("Frozen native task storage must be restored before mutation.");
    internal NativeBattleTaskSnapshotData RequireSnapshotData() => snapshot?.Data[slot] ?? throw new InvalidOperationException("Native task working storage requires explicit capture before DTO access.");
    private static void RejectInitializer() => throw new InvalidOperationException("Native task header/payload initializers cannot change storage authority; use explicit task mutations.");

    [JsonRequired]
    public ushort Flags { get => owner is null ? RequireSnapshotData().Flags : owner.RequireActive().Body.ReadUInt16(owner.StorageOffset + slot * owner.Stride); init => RejectInitializer(); }
    [JsonRequired]
    public ushort State { get => owner is null ? RequireSnapshotData().State : owner.RequireActive().Body.ReadUInt16(owner.StorageOffset + slot * owner.Stride + 2); init => RejectInitializer(); }
    [JsonRequired]
    public int? Next { get => owner is null ? RequireSnapshotData().Next : owner.RequireActive().Next(slot); init => RejectInitializer(); }
    [JsonRequired]
    public uint Callback { get => owner is null ? RequireSnapshotData().Callback : owner.RequireActive().Body.ReadUInt32(owner.StorageOffset + slot * owner.Stride + 8); init => RejectInitializer(); }
    [JsonRequired]
    public ImmutableArray<byte> Payload { get => RequireSnapshotData().Payload; init => RejectInitializer(); }
    [JsonIgnore]
    public int PayloadLength => owner is null ? RequireSnapshotData().Payload.Length : owner.RequireActive().Stride - 12;
    [JsonIgnore]
    public bool IsSnapshot => owner is null;
    [JsonIgnore]
    internal object StorageIdentity => RequireOwner().Storage.BackingIdentity;
    internal int PilotSlot => slot;

    public NativeBattleModulePoolTask UpdateHeader(ushort flags, ushort state, int? next, uint callback)
    {
        RequireOwner().WriteHeader(slot, flags, state, next, callback);
        return this;
    }

    public NativeBattleModulePoolTask SetNext(int? next)
    {
        return UpdateHeader(Flags, State, next, Callback);
    }

    public byte ReadByte(int offset)
    {
        RequireKnownPayload(offset, 1);
        return owner is null ? RequireSnapshotData().Payload[offset - 12] : owner.Body.ReadByte(Start + offset);
    }

    public ushort ReadWord(int offset)
    {
        RequireKnownPayload(offset, 2);
        return owner is null ? BinaryPrimitives.ReadUInt16LittleEndian(RequireSnapshotData().Payload.AsSpan().Slice(offset - 12)) : owner.Body.ReadUInt16(Start + offset);
    }

    public uint ReadDword(int offset)
    {
        RequireKnownPayload(offset, 4);
        return owner is null ? BinaryPrimitives.ReadUInt32LittleEndian(RequireSnapshotData().Payload.AsSpan().Slice(offset - 12)) : owner.Body.ReadUInt32(Start + offset);
    }

    public NativeBattleModulePoolTask WriteByte(int offset, byte value)
    {
        RequirePayloadRange(offset, 1);
        RequireOwner().Body.WriteByte(Start + offset, value);
        return this;
    }

    public NativeBattleModulePoolTask WriteWord(int offset, ushort value)
    {
        RequirePayloadRange(offset, 2);
        RequireOwner().Body.WriteUInt16(Start + offset, value);
        return this;
    }

    public NativeBattleModulePoolTask WriteDword(int offset, uint value)
    {
        RequirePayloadRange(offset, 4);
        RequireOwner().Body.WriteUInt32(Start + offset, value);
        return this;
    }

    public void BeginCheckpoint() => RequireOwner().BeginCheckpoint();
    public void CommitCheckpoint() => (owner ?? throw new InvalidOperationException("Frozen task journal cannot be completed.")).CommitCheckpoint();
    public void RollbackCheckpoint() => (owner ?? throw new InvalidOperationException("Frozen task journal cannot be completed.")).RollbackCheckpoint();

    object INativeBattleStorageNode.CaptureDetached(NativeBattleStorageCaptureContext context)
    {
        NativeBattleTaskBankOwner current = RequireOwner();
        NativeBattleTaskBankSnapshot captured = context.GetOrAddOwner(current, () => current.Capture(context));
        return captured.Tasks[slot];
    }

    object INativeBattleStorageNode.RestoreOwned(NativeBattleStorageRestoreContext context)
    {
        NativeBattleTaskBankSnapshot frozen = snapshot ?? throw new InvalidOperationException("Native task restore requires detached storage.");
        NativeBattleTaskBankOwner restored = context.GetOrAddOwner(frozen, () => RestoreBank(frozen, context));
        return restored.Tasks[slot];
    }

    internal static NativeBattleTaskBankOwner RestoreBank(NativeBattleTaskBankSnapshot frozen, NativeBattleStorageRestoreContext context)
    {
        if (frozen.Kernel is { } kernel)
        {
            NativeBattleWorkingMemory restored = context.GetOrAddOwner(kernel, () => NativeBattleWorkingMemory.Restore(kernel));
            NativeBattleWorkingMemory? descriptor = frozen.DescriptorKernel is { } descriptorKernel ? context.GetOrAddOwner(descriptorKernel, () => NativeBattleWorkingMemory.Restore(descriptorKernel)) : null;
            return new(frozen, restored, descriptor);
        }
        ImmutableArray<NativeBattleTaskSnapshotData> data = [.. frozen.Data.Select(task => task with { Pointers = context.Map(task.Pointers) })];
        return new(frozen.Descriptor, frozen.Address, frozen.Stride, data, frozen.Head, frozen.Tail, frozen.IsPool);
    }
}

public enum NativeBattleTaskStorageBinding
{
    PublishTaskState,
    AdoptPhysicalState
}

public sealed record NativeBattleEffectTaskCallback(NativeBattleEffectTaskPool Pool, int Result);
public sealed record NativeBattleEffectTaskStep(NativeBattleEffectTaskPool Pool, int Survivors);

/// <summary>One authoritative508300/508360/508420 descriptor and physical task bank.</summary>
[JsonConverter(typeof(NativeBattleEffectTaskPoolJsonConverter))]
public sealed record NativeBattleEffectTaskPool : INativeBattleStorageNode
{
    private readonly NativeBattleTaskBankOwner? owner;
    private readonly NativeBattleTaskBankSnapshot? snapshot;

    public NativeBattleEffectTaskPool(uint DescriptorAddress, uint BankAddress, ushort Stride, ImmutableArray<NativeBattleModulePoolTask> Slots, int? Head, int? Tail)
    {
        if (Slots.IsDefaultOrEmpty || Slots.Any(task => task is null))
        {
            throw new InvalidDataException("Native task pool requires its actual slot values.");
        }
        owner = new(DescriptorAddress, BankAddress, Stride, [.. Slots.Select(task => task.ExportSnapshotData())], Head, Tail, true);
        Validate();
    }

    private NativeBattleEffectTaskPool(NativeBattleTaskBankOwner owner) => this.owner = owner;
    private NativeBattleEffectTaskPool(NativeBattleTaskBankSnapshot snapshot) => this.snapshot = snapshot;
    internal static NativeBattleEffectTaskPool FromSnapshot(NativeBattleTaskBankSnapshot snapshot) => new(snapshot);
    internal static NativeBattleEffectTaskPool FromOwner(NativeBattleTaskBankOwner owner) => new(owner);
    internal NativeBattleTaskBankOwner RequireOwner() => owner?.RequireActive() ?? throw new InvalidOperationException("Frozen native task pools must be restored before mutation.");
    internal void RequireSnapshot()
    {
        if (snapshot is null)
        {
            throw new InvalidOperationException("Native task working pools require explicit capture before serialization.");
        }
    }
    private static void RejectInitializer() => throw new InvalidOperationException("Native pool initializers cannot replace shared bank authority; use WriteSlot or UpdateLinks.");

    [JsonRequired]
    public uint DescriptorAddress { get => owner?.Descriptor ?? snapshot!.Descriptor; init => RejectInitializer(); }
    [JsonRequired]
    public uint BankAddress { get => owner?.Address ?? snapshot!.Address; init => RejectInitializer(); }
    [JsonRequired]
    public ushort Stride { get => owner?.Stride ?? snapshot!.Stride; init => RejectInitializer(); }
    [JsonRequired]
    public ImmutableArray<NativeBattleModulePoolTask> Slots { get => owner?.RequireActive().Tasks ?? snapshot!.Tasks; init => RejectInitializer(); }
    [JsonRequired]
    public int? Head { get => owner is null ? snapshot!.Head : owner.RequireActive().Head; init => RejectInitializer(); }
    [JsonRequired]
    public int? Tail { get => owner is null ? snapshot!.Tail : owner.RequireActive().Tail; init => RejectInitializer(); }
    [JsonIgnore]
    public bool IsSnapshot => owner is null;
    [JsonIgnore]
    public bool HasCheckpoint => owner?.HasLocalCheckpoint ?? false;
    [JsonIgnore]
    internal object StorageIdentity => RequireOwner().Storage.BackingIdentity;
    [JsonIgnore]
    internal NativeBattleWorkingMemory CheckpointStorage => (owner ?? throw new InvalidOperationException("Frozen task pools have no working journal.")).Storage;
    [JsonIgnore]
    internal int CurrentCheckpointDepth => owner?.CurrentCheckpointDepth ?? 0;

    /// <summary>Publishes the current task bank once, then uses the main arena as its sole physical authority.</summary>
    public NativeBattleEffectTaskPool BindArena(NativeBattleEffectArena arena, NativeBattleTaskStorageBinding binding = NativeBattleTaskStorageBinding.PublishTaskState)
    {
        return BindStorage(arena, NativeBattleEffectNodeMemory.ArenaAddress, binding);
    }

    public NativeBattleEffectTaskPool BindStorage(NativeBattleEffectArena arena, uint physicalBase, NativeBattleTaskStorageBinding binding = NativeBattleTaskStorageBinding.PublishTaskState)
    {
        NativeBattleTaskBankOwner current = RequireOwner();
        arena.RequireWritable();
        if (!Enum.IsDefined(binding))
        {
            throw new ArgumentOutOfRangeException(nameof(binding));
        }
        if (current.IsBoundToStorage(arena, physicalBase))
        {
            return this;
        }
        Validate();
        current.BindStorage(arena, physicalBase, binding);
        return this;
    }

    public bool IsBoundToStorage(NativeBattleEffectArena arena, uint physicalBase)
    {
        return owner is not null && owner.IsBoundToStorage(arena, physicalBase);
    }

    public static NativeBattleEffectTaskPool Initialize(uint descriptorAddress, uint bankAddress, ushort stride, ushort capacity)
    {
        if (stride < 12 || capacity == 0 || (descriptorAddress & 3) != 0 || (bankAddress & 3) != 0)
        {
            throw new ArgumentException("Native508300 requires its aligned descriptor/bank and physical task dimensions.");
        }
        _ = checked(bankAddress + (uint)stride * capacity);
        NativeBattleTaskSnapshotData empty = new(0, 0, null, 0, ImmutableArray.Create(new byte[stride - 12]), null, null);
        return new(new NativeBattleTaskBankOwner(descriptorAddress, bankAddress, stride, [.. Enumerable.Repeat(empty, capacity)], null, null, true));
    }

    public void Validate()
    {
        if (Stride < 12 || Slots.IsDefault || Slots.Length is < 1 or > 65535 || (DescriptorAddress & 3) != 0 || (BankAddress & 3) != 0 || Head is int head && (uint)head >= Slots.Length || Tail is int tail && (uint)tail >= Slots.Length || Slots.Any(task => task is null || task.PayloadLength != Stride - 12 || task.Next is int next && (uint)next >= Slots.Length || (task.Flags & 1) != 0 && task.Callback == 0))
        {
            throw new InvalidDataException("Native effect task pool lost its physical descriptor, stride, slots or callback.");
        }
        foreach (NativeBattleModulePoolTask task in Slots)
        {
            task.ValidateUnknownPayloadRanges();
            task.ValidateResourcePointers();
        }
        HashSet<int> seen = [];
        int? last = null;
        for (int? next = Head; next is int index; next = Slots[index].Next)
        {
            if (!seen.Add(index) || Slots[index].Callback == 0)
            {
                throw new InvalidDataException("Native effect task links contain a cycle or an absent callback.");
            }
            last = index;
        }
        if (last != Tail)
        {
            throw new InvalidDataException("Native effect task descriptor tail contradicts its linked physical bank.");
        }
    }

    public NativeBattleEffectTaskPool UpdateLinks(int? head, int? tail)
    {
        RequireOwner().UpdateLinks(head, tail);
        return this;
    }

    public NativeBattleEffectTaskPool WriteSlot(int index, NativeBattleModulePoolTask task)
    {
        if ((uint)index >= Slots.Length || task.PayloadLength != Stride - 12)
        {
            throw new InvalidDataException("Native task replacement changes its bounded physical slot.");
        }
        NativeBattleTaskBankOwner current = RequireOwner();
        NativeBattleModulePoolTask target = current.Tasks[index];
        if (ReferenceEquals(target, task))
        {
            return this;
        }
        NativeBattleTaskSnapshotData data = task.ExportSnapshotData();
        target.Replace(data);
        return this;
    }

    public void BeginCheckpoint() => RequireOwner().BeginCheckpoint();
    public void CommitCheckpoint() => (owner ?? throw new InvalidOperationException("Frozen task journal cannot be completed.")).CommitCheckpoint();
    public void RollbackCheckpoint() => (owner ?? throw new InvalidOperationException("Frozen task journal cannot be completed.")).RollbackCheckpoint();
    internal void Revoke() => RequireOwner().Revoke();

    public (NativeBattleEffectTaskPool Pool, int Slot) Append(uint callback)
    {
        RequireOwner();
        if (callback == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(callback));
        }
        int index = -1;
        for (int candidate = 0; candidate < Slots.Length; candidate++)
        {
            if ((Slots[candidate].Flags & 1) == 0)
            {
                index = candidate;
                break;
            }
        }
        if (index < 0)
        {
            throw new InvalidOperationException("Native508360 exhausted its original effect task bank.");
        }
        RequireOwner().PrepareDescriptorMutation();
        NativeBattleModulePoolTask selected = Slots[index];
        int? oldTail = Tail;
        selected.UpdateHeader((ushort)(selected.Flags | 1), 0, null, callback);
        NativeBattlePilotVisualLifetimes.Allocated(this, index);
        if (oldTail is int tail)
        {
            NativeBattleModulePoolTask previous = Slots[tail];
            previous.UpdateHeader(previous.Flags, previous.State, index, previous.Callback);
        }
        UpdateLinks(oldTail is null ? index : Head, index);
        return (this, index);
    }

    public NativeBattleEffectTaskStep Advance(Func<NativeBattleEffectTaskPool, int, NativeBattleEffectTaskCallback> callback)
    {
        if (Head is null)
        {
            return AdvanceCore(callback);
        }
        using NativeBattleTaskOperation operation = new([this]);
        NativeBattleEffectTaskStep result = AdvanceCore(callback);
        operation.Commit();
        return result;
    }

    internal NativeBattleEffectTaskStep AdvanceCore(Func<NativeBattleEffectTaskPool, int, NativeBattleEffectTaskCallback> callback)
    {
        RequireOwner();
        if (Head is null)
        {
            if (Tail is not null)
            {
                throw new InvalidDataException("Native empty task queue retains an invalid tail.");
            }
            return new(this, 0);
        }
        NativeBattleEffectTaskPool pool = this;
        int? previous = null;
        int survivors = 0;
        for (int? next = Head; next is int index;)
        {
            NativeBattleEffectTaskCallback step = callback(pool, index);
            pool = step.Pool;
            if (!ReferenceEquals(pool.owner, owner))
            {
                throw new InvalidDataException("Native callback replaced its physical effect task bank.");
            }
            //508420 rereads Next after the callback, including same-pass children.
            next = pool.Slots[index].Next;
            if ((step.Result & 2) != 0)
            {
                NativeBattleModulePoolTask selected = pool.Slots[index];
                selected.UpdateHeader(0, selected.State, selected.Next, selected.Callback);
                if (previous is int before)
                {
                    NativeBattleModulePoolTask retained = pool.Slots[before];
                    retained.UpdateHeader(retained.Flags, retained.State, next, retained.Callback);
                }
                pool.UpdateLinks(previous is null ? next : pool.Head, pool.Tail);
            }
            else
            {
                previous = index;
                survivors++;
            }
        }
        pool.UpdateLinks(pool.Head, previous);
        return new(pool, survivors);
    }

    object INativeBattleStorageNode.CaptureDetached(NativeBattleStorageCaptureContext context)
    {
        NativeBattleTaskBankOwner current = RequireOwner();
        return FromSnapshot(context.GetOrAddOwner(current, () => current.Capture(context)));
    }

    object INativeBattleStorageNode.RestoreOwned(NativeBattleStorageRestoreContext context)
    {
        NativeBattleTaskBankSnapshot frozen = snapshot ?? throw new InvalidOperationException("Native pool restore requires detached storage.");
        return new NativeBattleEffectTaskPool(context.GetOrAddOwner(frozen, () => NativeBattleModulePoolTask.RestoreBank(frozen, context)));
    }
}

/// <summary>The independent original task descriptors initialized by6472E0 and58D760.</summary>
public sealed record NativeBattleGuardianEffectPools([property: JsonRequired] NativeBattleEffectTaskPool Main, [property: JsonRequired] NativeBattleEffectTaskPool Children, [property: JsonRequired] NativeBattleEffectTaskPool? Particles)
{
    public static NativeBattleGuardianEffectPools Odin()
    {
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(0x24FD6C0, 0x24FD6A0, 16, 2);
        (main, _) = main.Append(0x64DD50);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x24FD990, 0x24FD9A0, 36, 100);
        (children, _) = children.Append(0x6474C0);
        return new(main, children, null);
    }

    public static NativeBattleGuardianEffectPools Gilgamesh()
    {
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(0x21FF4A8, 0x21FF468, 16, 2);
        (main, _) = main.Append(0x596B70);
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x21FFD58, 0x21FFD68, 36, 100);
        (children, _) = children.Append(0x592300);
        return new(main, children, NativeBattleEffectTaskPool.Initialize(0x21FF458, 0x21FF3C8, 36, 4));
    }
}
