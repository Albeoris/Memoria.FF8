using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

internal sealed record NativeBattleTaskSnapshotData(ushort Flags, ushort State, int? Next, uint Callback, ImmutableArray<byte> Payload, ImmutableArray<NativeBattleEffectUnknownRegion>? Unknown, ImmutableDictionary<int, NativeBattleResourcePointer>? Pointers);

internal sealed class NativeBattleTaskBankSnapshot
{
    public NativeBattleTaskBankSnapshot(uint descriptor, uint address, ushort stride, ImmutableArray<NativeBattleTaskSnapshotData> tasks, int? head, int? tail, bool pool, NativeBattleWorkingMemorySnapshot? kernel = null, int storageOffset = 0, NativeBattleWorkingMemorySnapshot? descriptorKernel = null, bool arenaBound = false, bool? arenaBindingReceipt = null)
    {
        ArenaBindingReceipt = arenaBindingReceipt;
        Kernel = kernel;
        StorageOffset = storageOffset;
        DescriptorKernel = descriptorKernel;
        ArenaBound = arenaBound;
        Descriptor = descriptor;
        Address = address;
        Stride = stride;
        Data = tasks;
        Head = head;
        Tail = tail;
        IsPool = pool;
        Tasks = [.. Enumerable.Range(0, tasks.Length).Select(index => NativeBattleModulePoolTask.FromSnapshot(this, index))];
    }

    public NativeBattleWorkingMemorySnapshot? Kernel { get; }
    public int StorageOffset { get; }
    public NativeBattleWorkingMemorySnapshot? DescriptorKernel { get; }
    public bool ArenaBound { get; }
    public bool? ArenaBindingReceipt { get; }
    public uint Descriptor { get; }
    public uint Address { get; }
    public ushort Stride { get; }
    public ImmutableArray<NativeBattleTaskSnapshotData> Data { get; }
    public ImmutableArray<NativeBattleModulePoolTask> Tasks { get; }
    public int? Head { get; }
    public int? Tail { get; }
    public bool IsPool { get; }
}

/// <summary>The sole writable physical task bank; task objects are bounded views of this kernel.</summary>
internal sealed partial class NativeBattleTaskBankOwner
{
    private bool revoked;
    private bool arenaBound;
    private readonly List<int> localScopes = [];
    private DescriptorParticipant? descriptorParticipant;
    private ScopeObserver? scopeObserver;
    private RetirementParticipant? retirementParticipant;
    private NativeBattleTaskBody? body;

    public NativeBattleTaskBankOwner(uint descriptor, uint address, ushort stride, ImmutableArray<NativeBattleTaskSnapshotData> tasks, int? head, int? tail, bool pool, bool? restoredArenaBinding = null)
    {
        RestoredArenaBinding = restoredArenaBinding;
        if (stride < 12 || tasks.IsDefaultOrEmpty || tasks.Length > ushort.MaxValue)
        {
            throw new ArgumentException("Native task bank requires its bounded physical slots.");
        }
        if (pool && ((descriptor & 3) != 0 || (address & 3) != 0))
        {
            throw new InvalidDataException("Native task bank descriptor and storage must remain aligned.");
        }
        _ = checked(address + (uint)stride * (uint)tasks.Length);
        Descriptor = descriptor;
        Address = address;
        Stride = stride;
        IsPool = pool;
        BankLength = checked(stride * tasks.Length);
        Storage = new(BankLength, initialCapacity: BankLength) { IsPrivateTaskAuthority = true };
        DescriptorStorage = new(8, initialCapacity: 8) { IsPrivateTaskAuthority = true };
        Tasks = [.. Enumerable.Range(0, tasks.Length).Select(index => NativeBattleModulePoolTask.FromOwner(this, index))];
        for (int index = 0; index < tasks.Length; index++)
        {
            NativeBattleTaskSnapshotData task = tasks[index];
            if (task.Payload.IsDefault || task.Payload.Length != stride - 12)
            {
                throw new InvalidDataException("Native task payload changes its physical bank stride.");
            }
            WriteHeader(index, task.Flags, task.State, task.Next, task.Callback);
            int start = index * stride;
            Body.Write(start + 12, task.Payload.AsSpan());
            foreach (NativeBattleEffectUnknownRegion range in task.Unknown ?? [])
            {
                Body.MarkUnknown(start + range.Offset, range.Length);
            }
            foreach ((int offset, NativeBattleResourcePointer pointer) in task.Pointers ?? ImmutableDictionary<int, NativeBattleResourcePointer>.Empty)
            {
                Body.SetResourcePointer(start + offset, pointer);
            }
        }
        UpdateLinks(head, tail);
    }

    public NativeBattleTaskBankOwner(NativeBattleTaskBankSnapshot frozen, NativeBattleWorkingMemory storage, NativeBattleWorkingMemory? descriptorStorage = null)
    {
        RestoredArenaBinding = frozen.ArenaBindingReceipt;
        Descriptor = frozen.Descriptor;
        Address = frozen.Address;
        Stride = frozen.Stride;
        IsPool = frozen.IsPool;
        BankLength = checked(Stride * frozen.Tasks.Length);
        StorageOffset = frozen.StorageOffset;
        if (StorageOffset < 0 || StorageOffset > storage.LogicalLength - BankLength)
        {
            throw new InvalidDataException("Restored task bank leaves its shared physical storage window.");
        }
        Storage = storage;
        arenaBound = frozen.ArenaBound;
        if (!arenaBound)
        {
            Storage.IsPrivateTaskAuthority = true;
        }
        DescriptorStorage = descriptorStorage ?? new(8, initialCapacity: 8);
        DescriptorStorage.IsPrivateTaskAuthority = true;
        if (DescriptorStorage.LogicalLength != 8)
        {
            throw new InvalidDataException("Restored task descriptor requires its independent eight-byte logical storage.");
        }
        Tasks = [.. Enumerable.Range(0, frozen.Tasks.Length).Select(index => NativeBattleModulePoolTask.FromOwner(this, index))];
        if (descriptorStorage is null)
        {
            UpdateLinks(frozen.Head, frozen.Tail);
        }
    }

    internal bool? RestoredArenaBinding { get; }
    public uint Descriptor { get; }
    public uint Address { get; }
    public ushort Stride { get; }
    public bool IsPool { get; }
    public int BankLength { get; }
    public NativeBattleWorkingMemory Storage { get; private set; }
    public int StorageOffset { get; private set; }
    //Logical descriptor bookkeeping has no address inside or adjacent to the native task-body window.
    public NativeBattleWorkingMemory DescriptorStorage { get; private set; }
    public bool IsArenaBound => arenaBound;
    public bool HasLocalCheckpoint
    {
        get
        {
            _ = Storage.CheckpointDepth;
            return localScopes.Count != 0;
        }
    }
    internal int CurrentCheckpointDepth => localScopes.Count == 0 ? 0 : localScopes[^1];
    public NativeBattleTaskBody Body => body ??= new(this);
    public ImmutableArray<NativeBattleModulePoolTask> Tasks { get; }
    public bool HasCheckpoint => RequireActive().localScopes.Count != 0 || Storage.HasCheckpoint || DescriptorStorage.HasCheckpoint;
    public int? Head => DecodeIndex(DescriptorStorage.ReadUInt32(0));
    public int? Tail => DecodeIndex(DescriptorStorage.ReadUInt32(4));

    public NativeBattleTaskBankOwner RequireActive()
    {
        ObjectDisposedException.ThrowIf(revoked, this);
        return this;
    }

    public int? Next(int slot)
    {
        uint physical = Body.ReadUInt32(StorageOffset + slot * Stride + 4);
        if (physical == 0)
        {
            return null;
        }
        if (!IsPool)
        {
            return checked((int)(physical - 1));
        }
        long relative = (long)physical - Address;
        if (relative < 0 || relative % Stride != 0 || relative / Stride > int.MaxValue || IsPool && relative >= BankLength)
        {
            throw new InvalidDataException("Native task link leaves its physical bank.");
        }
        return (int)(relative / Stride);
    }

    public void WriteHeader(int slot, ushort flags, ushort state, int? next, uint callback)
    {
        RequireActive();
        if (IsPool && (flags & 1) != 0 && callback == 0)
        {
            throw new InvalidDataException("Native active task header requires its callback address.");
        }
        uint link = EncodeNext(next);
        Span<byte> bytes = stackalloc byte[12];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, flags);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes[2..], state);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[4..], link);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[8..], callback);
        Body.Write(StorageOffset + slot * Stride, bytes);
    }

    public void UpdateLinks(int? head, int? tail)
    {
        RequireActive();
        uint first = EncodeIndex(head);
        uint last = EncodeIndex(tail);
        if (Head == head && Tail == tail)
        {
            return;
        }
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, first);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[4..], last);
        PrepareDescriptorMutation();
        DescriptorStorage.Write(0, bytes);
    }

    public bool IsBoundToStorage(NativeBattleEffectArena arena, uint physicalBase)
    {
        RequireActive();
        long relative = (long)Address - physicalBase;
        return arenaBound && !arena.IsSnapshot && relative >= 0 && relative <= arena.Bound - BankLength && ReferenceEquals(Storage, arena.Working) && StorageOffset == arena.WorkingOffset + relative;
    }

    public void BindStorage(NativeBattleEffectArena arena, uint physicalBase, NativeBattleTaskStorageBinding binding)
    {
        RequireActive();
        if (!IsPool || !Enum.IsDefined(binding))
        {
            throw new InvalidOperationException("Physical task binding requires an identified pool and explicit source authority.");
        }
        arena.RequireWritable();
        long relative = (long)Address - physicalBase;
        if (relative < 0 || relative > arena.Bound - BankLength)
        {
            throw new NotSupportedException("Native task bank is not wholly contained in the declared physical storage area.");
        }
        NativeBattleWorkingMemory target = arena.Working;
        int targetOffset = checked(arena.WorkingOffset + (int)relative);
        if (arenaBound)
        {
            if (ReferenceEquals(Storage, target) && StorageOffset == targetOffset)
            {
                return;
            }
            throw new InvalidOperationException("A bound task bank requires capture/restore before changing battle storage authority.");
        }
        bool callerOverlay = NativeBattleTaskCallerOperation.AllowsOverlayBinding(target, targetOffset, BankLength);
        bool sameOperationKernel = ReferenceEquals(Storage, target) && NativeBattleTaskOperation.OwnsKernel(Storage) && !HasLocalCheckpoint && !DescriptorStorage.HasCheckpoint;
        if (HasCheckpoint && !sameOperationKernel || arena.Transaction is not null && !callerOverlay || NativeBattleEffectArenaTransaction.HasActive(Storage) || NativeBattleEffectArenaTransaction.HasActive(target) && !callerOverlay)
        {
            throw new InvalidOperationException("Native task binding requires its committed physical ownership boundary.");
        }
        bool journaled = NativeBattleTaskCallerOperation.EnlistBinding(target) || NativeBattleTaskOperation.EnlistBinding(target);
        if (target.HasCheckpoint && !journaled)
        {
            throw new InvalidOperationException("Native task binding requires its committed physical ownership boundary or active task operation.");
        }
        NativeBattleWorkingMemory previous = Storage;
        BindingParticipant? participant = journaled ? new(this, previous) : null;
        if (participant is not null)
        {
            target.EnlistCheckpointParticipant(participant);
        }
        if (binding == NativeBattleTaskStorageBinding.PublishTaskState)
        {
            PublishBody(arena, (int)relative);
        }
        else
        {
            ValidatePhysicalHeaders(target, targetOffset);
        }
        Storage = target;
        StorageOffset = targetOffset;
        arenaBound = true;
        if (participant is not null)
        {
            participant.SuspendPrevious();
        }
        else if (!ReferenceEquals(previous, target))
        {
            previous.Revoke();
        }
    }

    private void PublishBody(NativeBattleEffectArena arena, int targetOffset)
    {
        byte[] bytes = new byte[Stride];
        bool[] known = new bool[Stride];
        using NativeBattleEffectArenaWriter writer = new(arena);
        for (int slot = 0; slot < Tasks.Length; slot++)
        {
            int source = StorageOffset + slot * Stride;
            Body.ReadWithKnownness(source, bytes, known);
            int destination = checked(targetOffset + slot * Stride);
            writer.Write(destination, bytes);
            for (int index = 0; index < known.Length;)
            {
                if (known[index])
                {
                    index++;
                    continue;
                }
                int first = index++;
                while (index < known.Length && !known[index])
                {
                    index++;
                }
                writer.MarkUnknown(destination + first, index - first);
            }
        }
        using var pointers = Body.EnumerateResourcePointers();
        while (pointers.MoveNext())
        {
            (int offset, NativeBattleResourcePointer? pointer) = pointers.Current;
            if (pointer is null || !pointer.HasValidOwner())
            {
                throw new InvalidDataException("Native task binding cannot publish unresolved pointer ownership.");
            }
            writer.SetResourcePointer(checked(targetOffset + offset - StorageOffset), pointer);
        }
        writer.Freeze();
    }

    private void ValidatePhysicalHeaders(NativeBattleWorkingMemory target, int offset)
    {
        int?[] next = new int?[Tasks.Length];
        uint[] callbacks = new uint[Tasks.Length];
        for (int index = 0; index < Tasks.Length; index++)
        {
            int start = checked(offset + index * Stride);
            ushort flags = target.ReadUInt16(start);
            _ = target.ReadUInt16(start + 2);
            uint physicalNext = target.ReadUInt32(start + 4);
            callbacks[index] = target.ReadUInt32(start + 8);
            if ((flags & 1) != 0 && callbacks[index] == 0)
            {
                throw new InvalidDataException("Adopted native task header has no active callback.");
            }
            if (physicalNext != 0)
            {
                long relative = (long)physicalNext - Address;
                if (relative < 0 || relative >= BankLength || relative % Stride != 0)
                {
                    throw new InvalidDataException("Adopted native task link leaves its physical bank.");
                }
                next[index] = checked((int)(relative / Stride));
            }
        }
        HashSet<int> visited = [];
        int? last = null;
        for (int? current = Head; current is int index; current = next[index])
        {
            if ((uint)index >= Tasks.Length || !visited.Add(index) || callbacks[index] == 0)
            {
                throw new InvalidDataException("Adopted native task queue contradicts its retained descriptor.");
            }
            last = index;
        }
        if (last != Tail)
        {
            throw new InvalidDataException("Adopted native task tail contradicts its retained descriptor.");
        }
    }

    internal void PrepareDescriptorMutation()
    {
        if (Storage.HasCheckpoint)
        {
            Storage.EnlistCheckpointParticipant(descriptorParticipant ??= new(this));
        }
        else if (NativeBattleEffectArenaTransaction.HasActive(Storage))
        {
            throw new InvalidOperationException("Task descriptor mutation inside a public arena transaction requires its physical pool checkpoint.");
        }
    }

    public void BeginCheckpoint()
    {
        RequireActive();
        bool nested = Storage.HasCheckpoint;
        if (nested)
        {
            Storage.BeginNestedCheckpoint();
        }
        else
        {
            Storage.BeginCheckpoint();
        }
        int depth = Storage.CheckpointDepth;
        localScopes.Add(depth);
        RegisterLocalCheckpoint();
        try
        {
            Storage.EnlistCheckpointParticipant(scopeObserver ??= new(this));
        }
        catch
        {
            localScopes.Remove(depth);
            UnregisterLocalCheckpoint();
            if (nested)
            {
                Storage.RollbackNestedCheckpoint();
            }
            else
            {
                Storage.RollbackCheckpoint();
            }
            throw;
        }
    }

    public void CommitCheckpoint()
    {
        RequireCurrentScope();
        if (Storage.CheckpointDepth > 1)
        {
            Storage.CommitNestedCheckpoint();
        }
        else
        {
            Storage.CommitCheckpoint();
        }
    }

    public void RollbackCheckpoint()
    {
        RequireCurrentScope();
        if (Storage.CheckpointDepth > 1)
        {
            Storage.RollbackNestedCheckpoint();
        }
        else
        {
            Storage.RollbackCheckpoint();
        }
    }

    private void RequireCurrentScope()
    {
        if (localScopes.Count == 0 || localScopes[^1] != Storage.CheckpointDepth)
        {
            throw new InvalidOperationException("Task checkpoints sharing a physical kernel must complete in reverse nested order.");
        }
    }

    private sealed class DescriptorParticipant(NativeBattleTaskBankOwner owner) : INativeBattleCheckpointParticipant
    {
        public void BeginCheckpoint()
        {
            if (owner.DescriptorStorage.HasCheckpoint)
            {
                owner.DescriptorStorage.BeginNestedCheckpoint();
            }
            else
            {
                owner.DescriptorStorage.BeginCheckpoint();
            }
        }
        public void CommitCheckpoint()
        {
            if (owner.DescriptorStorage.CheckpointDepth > 1)
            {
                owner.DescriptorStorage.CommitNestedCheckpoint();
            }
            else
            {
                owner.DescriptorStorage.CommitCheckpoint();
            }
        }
        public void RollbackCheckpoint()
        {
            if (owner.DescriptorStorage.CheckpointDepth > 1)
            {
                owner.DescriptorStorage.RollbackNestedCheckpoint();
            }
            else
            {
                owner.DescriptorStorage.RollbackCheckpoint();
            }
        }
    }

    private sealed class RetirementParticipant(NativeBattleTaskBankOwner owner) : INativeBattleCheckpointParticipant
    {
        private readonly List<bool> previous = [];
        public void BeginCheckpoint() => previous.Add(owner.revoked);
        public void CommitCheckpoint()
        {
            if (previous.Count == 0)
            {
                throw new InvalidOperationException("Native task retirement lost its checkpoint frame.");
            }
            previous.RemoveAt(previous.Count - 1);
        }
        public void RollbackCheckpoint()
        {
            if (previous.Count == 0)
            {
                throw new InvalidOperationException("Native task retirement lost its checkpoint frame.");
            }
            owner.revoked = previous[^1];
            previous.RemoveAt(previous.Count - 1);
        }
    }

    private sealed class ScopeObserver(NativeBattleTaskBankOwner owner) : INativeBattleCheckpointParticipant
    {
        private int frames;
        public void BeginCheckpoint() => frames++;
        public void CommitCheckpoint() => Complete();
        public void RollbackCheckpoint() => Complete();
        private void Complete()
        {
            if (frames <= 0)
            {
                throw new InvalidOperationException("Native task scope observer lost its physical checkpoint frame.");
            }
            int completed = frames--;
            for (int index = owner.localScopes.Count - 1; index >= 0; index--)
            {
                if (owner.localScopes[index] == completed)
                {
                    owner.localScopes.RemoveAt(index);
                    owner.UnregisterLocalCheckpoint();
                }
            }
        }
    }

    private uint EncodeNext(int? index)
    {
        if (index is not int slot)
        {
            return 0;
        }
        if (slot < 0 || IsPool && slot >= Tasks.Length)
        {
            throw new InvalidDataException("Native task link leaves its physical bank.");
        }
        //Unbound standalone headers use a private nullable-index codec, never a physical address.
        if (!IsPool)
        {
            return checked((uint)slot + 1);
        }
        return checked(Address + (uint)slot * Stride);
    }

    private uint EncodeIndex(int? index)
    {
        if (index is not int slot)
        {
            return 0;
        }
        if ((uint)slot >= Tasks.Length)
        {
            throw new InvalidDataException("Native task descriptor leaves its physical bank.");
        }
        return checked((uint)slot + 1);
    }

    private static int? DecodeIndex(uint encoded) => encoded == 0 ? null : checked((int)encoded - 1);

    public NativeBattleTaskSnapshotData ReadTask(int slot, NativeBattleStorageCaptureContext? context = null)
    {
        RequireActive();
        int start = StorageOffset + slot * Stride;
        int length = Stride - 12;
        byte[] bytes = new byte[length];
        bool[] known = new bool[length];
        Body.ReadWithKnownness(start + 12, bytes, known);
        ImmutableArray<NativeBattleEffectUnknownRegion>.Builder unknown = ImmutableArray.CreateBuilder<NativeBattleEffectUnknownRegion>();
        for (int index = 0; index < length;)
        {
            if (known[index])
            {
                index++;
                continue;
            }
            int first = index++;
            while (index < length && !known[index])
            {
                index++;
            }
            unknown.Add(new(12 + first, index - first));
        }
        ImmutableDictionary<int, NativeBattleResourcePointer>.Builder pointers = ImmutableDictionary.CreateBuilder<int, NativeBattleResourcePointer>();
        using var pointerEntries = Body.EnumerateResourcePointers();
        while (pointerEntries.MoveNext())
        {
            (int offset, NativeBattleResourcePointer? pointer) = pointerEntries.Current;
            if (offset >= start + 12 && offset + 4 <= start + Stride)
            {
                NativeBattleResourcePointer present = pointer ?? throw new InvalidDataException("Native task pointer metadata lacks its source owner.");
                pointers.Add(offset - start, context is null ? present : context.Map(present));
            }
        }
        return new(Body.ReadUInt16(start), Body.ReadUInt16(start + 2), Next(slot), Body.ReadUInt32(start + 8), System.Runtime.InteropServices.ImmutableCollectionsMarshal.AsImmutableArray(bytes), unknown.Count == 0 ? null : unknown.ToImmutable(), pointers.Count == 0 ? null : pointers.ToImmutable());
    }

    public NativeBattleTaskBankSnapshot Capture(NativeBattleStorageCaptureContext context)
    {
        RequireActive();
        if (HasCheckpoint)
        {
            throw new InvalidOperationException("Native task capture requires its committed bank boundary.");
        }
        if (IsPool)
        {
            NativeBattleEffectTaskPool.FromOwner(this).Validate();
        }
        NativeBattleEffectArenaTransaction.RequireCommittedForCapture(Storage);
        NativeBattleWorkingMemorySnapshot kernel = context.GetOrAddOwner(Storage.BackingIdentity, () => Storage.Capture());
        NativeBattleWorkingMemorySnapshot descriptorKernel = context.GetOrAddOwner(DescriptorStorage.BackingIdentity, () => DescriptorStorage.Capture());
        return new(Descriptor, Address, Stride, [.. Enumerable.Range(0, Tasks.Length).Select(slot => ReadTask(slot, context))], Head, Tail, IsPool, kernel, StorageOffset, descriptorKernel, arenaBound, arenaBound);
    }

    internal void ValidateRetirementBoundary()
    {
        RequireActive();
        if (arenaBound && Storage.HasCheckpoint)
        {
            return;
        }
        if (HasCheckpoint)
        {
            throw new InvalidOperationException("Native task retirement requires its physical checkpoint boundary.");
        }
        if (arenaBound)
        {
            NativeBattleEffectArenaTransaction.RequireCommittedForCapture(Storage);
        }
        else if (NativeBattleEffectArenaTransaction.HasActive(Storage))
        {
            throw new InvalidOperationException("Private task retirement cannot discard an active public arena transaction.");
        }
    }

    public void Revoke()
    {
        ValidateRetirementBoundary();
        if (arenaBound)
        {
            if (Storage.HasCheckpoint)
            {
                Storage.EnlistCheckpointParticipant(retirementParticipant ??= new(this));
            }
            else
            {
                NativeBattleEffectArenaTransaction.RequireCommittedForCapture(Storage);
                if (DescriptorStorage.HasCheckpoint)
                {
                    throw new InvalidOperationException("Native task retirement requires its descriptor checkpoint boundary.");
                }
            }
            //The shared body remains owned by its physical arena. Retire only this task view; its boolean is journaled.
            revoked = true;
            return;
        }
        if (HasCheckpoint || NativeBattleEffectArenaTransaction.HasActive(Storage))
        {
            throw new InvalidOperationException("Private task authority transfer requires its committed bank boundary.");
        }
        Storage.Revoke();
        DescriptorStorage.Revoke();
        revoked = true;
    }

}

internal sealed class NativeBattleModulePoolTaskJsonConverter : JsonConverter<NativeBattleModulePoolTask>
{
    public NativeBattleModulePoolTaskJsonConverter()
    {
    }

    public sealed record Data([property: JsonRequired] ushort Flags, [property: JsonRequired] ushort State, [property: JsonRequired] int? Next, [property: JsonRequired] uint Callback, [property: JsonRequired] ImmutableArray<byte> Payload)
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ImmutableArray<NativeBattleEffectUnknownRegion>? UnknownPayloadRanges { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ImmutableDictionary<int, NativeBattleResourcePointer>? ResourcePointers { get; init; }
    }

    public override NativeBattleModulePoolTask Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Data data = JsonSerializer.Deserialize<Data>(ref reader, options) ?? throw new JsonException("Native task snapshot is missing.");
        if (data.Payload.IsDefault || data.Payload.Length > ushort.MaxValue - 12)
        {
            throw new JsonException("Native task snapshot requires its bounded physical payload.");
        }
        NativeBattleTaskBankSnapshot bank = new(0, 0, checked((ushort)(data.Payload.Length + 12)), [new(data.Flags, data.State, data.Next, data.Callback, data.Payload, data.UnknownPayloadRanges, data.ResourcePointers)], null, null, false);
        NativeBattleModulePoolTask result = bank.Tasks[0];
        result.ValidateUnknownPayloadRanges();
        result.ValidateResourcePointers();
        return result;
    }

    public override void Write(Utf8JsonWriter writer, NativeBattleModulePoolTask value, JsonSerializerOptions options)
    {
        NativeBattleTaskSnapshotData data = value.RequireSnapshotData();
        JsonSerializer.Serialize(writer, new Data(data.Flags, data.State, data.Next, data.Callback, data.Payload) { UnknownPayloadRanges = data.Unknown, ResourcePointers = data.Pointers }, options);
    }
}

internal sealed class NativeBattleEffectTaskPoolJsonConverter : JsonConverter<NativeBattleEffectTaskPool>
{
    public NativeBattleEffectTaskPoolJsonConverter()
    {
    }

    public sealed record Data([property: JsonRequired] uint DescriptorAddress, [property: JsonRequired] uint BankAddress, [property: JsonRequired] ushort Stride, [property: JsonRequired] ImmutableArray<NativeBattleModulePoolTask> Slots, [property: JsonRequired] int? Head, [property: JsonRequired] int? Tail)
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? ArenaBinding { get; init; }
    }

    public override NativeBattleEffectTaskPool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Data data = JsonSerializer.Deserialize<Data>(ref reader, options) ?? throw new JsonException("Native pool snapshot is missing.");
        if (data.Slots.IsDefaultOrEmpty || data.Slots.Any(task => task is null))
        {
            throw new JsonException("Native pool snapshot requires its physical slots.");
        }
        NativeBattleTaskBankSnapshot bank = new(data.DescriptorAddress, data.BankAddress, data.Stride, [.. data.Slots.Select(task => task.RequireSnapshotData())], data.Head, data.Tail, true, arenaBindingReceipt: data.ArenaBinding);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.FromSnapshot(bank);
        pool.Validate();
        return pool;
    }

    public override void Write(Utf8JsonWriter writer, NativeBattleEffectTaskPool value, JsonSerializerOptions options)
    {
        value.RequireSnapshot();
        JsonSerializer.Serialize(writer, new Data(value.DescriptorAddress, value.BankAddress, value.Stride, value.Slots, value.Head, value.Tail) { ArenaBinding = value.SnapshotArenaBinding }, options);
    }
}
