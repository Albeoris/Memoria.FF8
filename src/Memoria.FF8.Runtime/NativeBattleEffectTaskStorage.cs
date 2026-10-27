using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

internal sealed record NativeBattleTaskSnapshotData(ushort Flags, ushort State, int? Next, uint Callback, ImmutableArray<byte> Payload, ImmutableArray<NativeBattleEffectUnknownRegion>? Unknown, ImmutableDictionary<int, NativeBattleResourcePointer>? Pointers);

internal sealed class NativeBattleTaskBankSnapshot
{
    public NativeBattleTaskBankSnapshot(uint descriptor, uint address, ushort stride, ImmutableArray<NativeBattleTaskSnapshotData> tasks, int? head, int? tail, bool pool, NativeBattleWorkingMemorySnapshot? kernel = null, int storageOffset = 0, NativeBattleWorkingMemorySnapshot? descriptorKernel = null, bool arenaBound = false)
    {
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
internal sealed class NativeBattleTaskBankOwner
{
    private bool revoked;
    private bool arenaBound;
    private readonly List<int> localScopes = [];
    private DescriptorParticipant? descriptorParticipant;
    private ScopeObserver? scopeObserver;
    private NativeBattleTaskBody? body;

    public NativeBattleTaskBankOwner(uint descriptor, uint address, ushort stride, ImmutableArray<NativeBattleTaskSnapshotData> tasks, int? head, int? tail, bool pool)
    {
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
        Storage = new(BankLength, initialCapacity: BankLength);
        DescriptorStorage = new(8, initialCapacity: 8);
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
        DescriptorStorage = descriptorStorage ?? new(8, initialCapacity: 8);
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

    public uint Descriptor { get; }
    public uint Address { get; }
    public ushort Stride { get; }
    public bool IsPool { get; }
    public int BankLength { get; }
    public NativeBattleWorkingMemory Storage { get; private set; }
    public int StorageOffset { get; private set; }
    public NativeBattleWorkingMemory DescriptorStorage { get; private set; }
    public bool IsArenaBound => arenaBound;
    public bool HasLocalCheckpoint => RequireActive().localScopes.Count != 0;
    public NativeBattleTaskBody Body => body ??= new(this);
    public ImmutableArray<NativeBattleModulePoolTask> Tasks { get; }
    public bool HasCheckpoint => RequireActive().Storage.HasCheckpoint || DescriptorStorage.HasCheckpoint;
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
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, first);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[4..], last);
        PrepareDescriptorMutation();
        DescriptorStorage.Write(0, bytes);
    }

    private void PrepareDescriptorMutation()
    {
        if (Storage.HasCheckpoint)
        {
            Storage.EnlistCheckpointParticipant(descriptorParticipant ??= new(this));
        }
        else if (NativeBattleEffectArenaTransaction.TryRoute(Storage, StorageOffset, BankLength, out _, out _))
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
        try
        {
            Storage.EnlistCheckpointParticipant(scopeObserver ??= new(this));
        }
        catch
        {
            localScopes.Remove(depth);
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
        RequireActive();
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

    private sealed class ScopeObserver(NativeBattleTaskBankOwner owner) : INativeBattleCheckpointParticipant
    {
        private int frames;
        public void BeginCheckpoint() => frames++;
        public void CommitCheckpoint() => Complete();
        public void RollbackCheckpoint() => Complete();
        private void Complete()
        {
            int completed = frames--;
            owner.localScopes.RemoveAll(depth => depth == completed);
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
        return new(Descriptor, Address, Stride, [.. Enumerable.Range(0, Tasks.Length).Select(slot => ReadTask(slot, context))], Head, Tail, IsPool, kernel, StorageOffset, descriptorKernel, arenaBound);
    }

    public void Revoke()
    {
        RequireActive();
        if (HasCheckpoint)
        {
            throw new InvalidOperationException("Native task authority transfer requires its committed bank boundary.");
        }
        if (!arenaBound)
        {
            Storage.Revoke();
        }
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

    public sealed record Data([property: JsonRequired] uint DescriptorAddress, [property: JsonRequired] uint BankAddress, [property: JsonRequired] ushort Stride, [property: JsonRequired] ImmutableArray<NativeBattleModulePoolTask> Slots, [property: JsonRequired] int? Head, [property: JsonRequired] int? Tail);

    public override NativeBattleEffectTaskPool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        Data data = JsonSerializer.Deserialize<Data>(ref reader, options) ?? throw new JsonException("Native pool snapshot is missing.");
        if (data.Slots.IsDefaultOrEmpty || data.Slots.Any(task => task is null))
        {
            throw new JsonException("Native pool snapshot requires its physical slots.");
        }
        NativeBattleTaskBankSnapshot bank = new(data.DescriptorAddress, data.BankAddress, data.Stride, [.. data.Slots.Select(task => task.RequireSnapshotData())], data.Head, data.Tail, true);
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.FromSnapshot(bank);
        pool.Validate();
        return pool;
    }

    public override void Write(Utf8JsonWriter writer, NativeBattleEffectTaskPool value, JsonSerializerOptions options)
    {
        value.RequireSnapshot();
        JsonSerializer.Serialize(writer, new Data(value.DescriptorAddress, value.BankAddress, value.Stride, value.Slots, value.Head, value.Tail), options);
    }
}
