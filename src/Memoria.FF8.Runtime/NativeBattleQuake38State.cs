using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleQuake38State : INativeBattleStorageNode, IJsonOnSerializing
{
    private const int StorageLength = 0x188;
    private static readonly uint[] BankStarts = [0x278C8E0, 0x279299C, 0x2792B40, 0x2792D68, 0x2792E04];
    private static readonly int[] BankLengths = [0xD8, 0x34, 0x10, 0x10, 0x5C];
    private static readonly int[] BankOffsets = [0, 0xD8, 0x10C, 0x11C, 0x12C];
    private readonly Owner? owner;
    private ImmutableArray<byte> header;
    private ImmutableArray<byte> middle;
    private ImmutableArray<byte> pointers;
    private ImmutableArray<byte> tail;
    private ImmutableArray<byte> end;
    private ImmutableArray<NativeBattleQuake38SavedActor> savedActors;

    [JsonConstructor]
    public NativeBattleQuake38State(ImmutableArray<byte> header, ImmutableArray<byte> middle, ImmutableArray<byte> pointers, ImmutableArray<byte> tail, ImmutableArray<byte> end, ImmutableArray<NativeBattleQuake38SavedActor> savedActors)
    {
        this.header = header;
        this.middle = middle;
        this.pointers = pointers;
        this.tail = tail;
        this.end = end;
        this.savedActors = savedActors;
        Validate();
    }

    private NativeBattleQuake38State(Owner owner)
    {
        this.owner = owner;
    }

    [JsonRequired]
    public ImmutableArray<byte> Header
    {
        get => ReadBank(0);
        init
        {
            RequireSnapshotInitializer();
            header = value;
        }
    }

    [JsonRequired]
    public ImmutableArray<byte> Middle
    {
        get => ReadBank(1);
        init
        {
            RequireSnapshotInitializer();
            middle = value;
        }
    }

    [JsonRequired]
    public ImmutableArray<byte> Pointers
    {
        get => ReadBank(2);
        init
        {
            RequireSnapshotInitializer();
            pointers = value;
        }
    }

    [JsonRequired]
    public ImmutableArray<byte> Tail
    {
        get => ReadBank(3);
        init
        {
            RequireSnapshotInitializer();
            tail = value;
        }
    }

    [JsonRequired]
    public ImmutableArray<byte> End
    {
        get => ReadBank(4);
        init
        {
            RequireSnapshotInitializer();
            end = value;
        }
    }

    [JsonRequired]
    public ImmutableArray<NativeBattleQuake38SavedActor> SavedActors
    {
        get => owner?.SavedActors ?? savedActors;
        init
        {
            RequireSnapshotInitializer();
            savedActors = value;
        }
    }

    bool INativeBattleStorageNode.IsSnapshot => owner is null;
    internal object StorageIdentity => RequireOwner().Storage.BackingIdentity;
    internal bool HasCheckpoint => RequireOwner().Storage.HasCheckpoint;

    public static NativeBattleQuake38State FromColdProcess()
    {
        ImmutableArray<NativeBattleQuake38SavedActor> actors = Enumerable.Range(0, 7).Select(_ => NativeBattleQuake38SavedActor.FromColdProcess()).ToImmutableArray();
        return new(new Owner(actors));
    }

    public void Validate()
    {
        if (owner is not null)
        {
            if (owner.Storage.LogicalLength != StorageLength)
            {
                throw new InvalidDataException("Native Quake working field storage has the wrong extent.");
            }
            ValidateActors(owner.SavedActors);
            return;
        }
        if (header.IsDefault || header.Length != 0xD8 || middle.IsDefault || middle.Length != 0x34 || pointers.IsDefault || pointers.Length != 0x10 || tail.IsDefault || tail.Length != 0x10 || end.IsDefault || end.Length != 0x5C)
        {
            throw new InvalidDataException("Native Quake requires its complete retained module fields, independent of task pools and the process effect arena.");
        }
        ValidateActors(savedActors);
    }

    public ImmutableArray<byte> Read(uint address, int length)
    {
        var region = Resolve(address, length);
        if (owner is null)
        {
            Validate();
            return NativeBattleTelemetry.Immutable(1002, FrozenBank(region.Bank).AsSpan().Slice(region.Offset, length));
        }
        byte[] bytes = new byte[length];
        owner.Storage.ReadInto(region.StorageOffset, bytes);
        return ImmutableCollectionsMarshal.AsImmutableArray(bytes);
    }

    public void ReadInto(uint address, Span<byte> destination)
    {
        var region = Resolve(address, destination.Length);
        if (owner is null)
        {
            Validate();
            NativeBattleTelemetry.Copy(1002, FrozenBank(region.Bank).AsSpan().Slice(region.Offset, destination.Length), destination);
            return;
        }
        owner.Storage.ReadInto(region.StorageOffset, destination);
    }

    public NativeBattleQuake38State Write(uint address, ReadOnlySpan<byte> source)
    {
        var region = Resolve(address, source.Length);
        RequireOwner().Storage.Write(region.StorageOffset, source);
        return this;
    }

    public byte Byte(uint address)
    {
        var region = Resolve(address, 1);
        if (owner is not null)
        {
            return owner.Storage.ReadByte(region.StorageOffset);
        }
        Validate();
        return FrozenBank(region.Bank)[region.Offset];
    }

    public short Word(uint address)
    {
        var region = Resolve(address, 2);
        if (owner is not null)
        {
            return owner.Storage.ReadInt16(region.StorageOffset);
        }
        Validate();
        return BinaryPrimitives.ReadInt16LittleEndian(FrozenBank(region.Bank).AsSpan().Slice(region.Offset, 2));
    }

    public uint Dword(uint address)
    {
        var region = Resolve(address, 4);
        if (owner is not null)
        {
            return owner.Storage.ReadUInt32(region.StorageOffset);
        }
        Validate();
        return BinaryPrimitives.ReadUInt32LittleEndian(FrozenBank(region.Bank).AsSpan().Slice(region.Offset, 4));
    }

    public NativeBattleQuake38State SetByte(uint address, byte value)
    {
        var region = Resolve(address, 1);
        RequireOwner().Storage.WriteByte(region.StorageOffset, value);
        return this;
    }

    public NativeBattleQuake38State SetWord(uint address, short value)
    {
        var region = Resolve(address, 2);
        RequireOwner().Storage.WriteInt16(region.StorageOffset, value);
        return this;
    }

    public NativeBattleQuake38State SetDword(uint address, uint value)
    {
        var region = Resolve(address, 4);
        RequireOwner().Storage.WriteUInt32(region.StorageOffset, value);
        return this;
    }

    public NativeBattleQuake38State SetSavedActors(ImmutableArray<NativeBattleQuake38SavedActor> actors)
    {
        Owner current = RequireOwner();
        ValidateActors(actors);
        current.SavedActors = actors;
        return this;
    }

    public NativeBattleQuake38State SetSavedActor(int index, NativeBattleQuake38SavedActor actor)
    {
        Owner current = RequireOwner();
        ArgumentNullException.ThrowIfNull(actor);
        if ((uint)index >= 7)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        current.SavedActors = current.SavedActors.SetItem(index, actor);
        return this;
    }

    internal void BeginCheckpoint()
    {
        Owner current = RequireOwner();
        current.Storage.BeginCheckpoint();
        current.CheckpointActors = current.SavedActors;
    }

    internal void CommitCheckpoint()
    {
        Owner current = RequireOwner();
        current.Storage.CommitCheckpoint();
        current.CheckpointActors = default;
    }

    internal void RollbackCheckpoint()
    {
        Owner current = RequireOwner();
        if (!current.Storage.HasCheckpoint)
        {
            throw new InvalidOperationException("Native Quake scalar rollback requires its active checkpoint.");
        }
        current.SavedActors = current.CheckpointActors;
        current.CheckpointActors = default;
        current.Storage.RollbackCheckpoint();
    }

    object INativeBattleStorageNode.CaptureDetached(NativeBattleStorageCaptureContext context)
    {
        Owner current = RequireOwner();
        if (current.Storage.HasCheckpoint)
        {
            throw new InvalidOperationException("Native Quake snapshot requires its committed scalar boundary.");
        }
        return context.GetOrAddOwner(current, () => new NativeBattleQuake38State(ReadBank(0), ReadBank(1), ReadBank(2), ReadBank(3), ReadBank(4), current.SavedActors));
    }

    object INativeBattleStorageNode.RestoreOwned(NativeBattleStorageRestoreContext context)
    {
        if (owner is not null)
        {
            throw new InvalidOperationException("Native Quake restore requires detached snapshot fields.");
        }
        Validate();
        Owner restored = context.GetOrAddOwner(this, () =>
        {
            Owner result = new(savedActors);
            result.Storage.Write(0, header.AsSpan());
            result.Storage.Write(0xD8, middle.AsSpan());
            result.Storage.Write(0x10C, pointers.AsSpan());
            result.Storage.Write(0x11C, tail.AsSpan());
            result.Storage.Write(0x12C, end.AsSpan());
            return result;
        });
        return new NativeBattleQuake38State(restored);
    }

    void IJsonOnSerializing.OnSerializing()
    {
        if (owner is not null)
        {
            throw new InvalidOperationException("Native Quake working storage must be captured before serialization.");
        }
        Validate();
    }

    private ImmutableArray<byte> ReadBank(int bank)
    {
        if (owner is null)
        {
            return FrozenBank(bank);
        }
        byte[] bytes = new byte[BankLengths[bank]];
        owner.Storage.ReadInto(BankOffsets[bank], bytes);
        return ImmutableCollectionsMarshal.AsImmutableArray(bytes);
    }

    private ImmutableArray<byte> FrozenBank(int bank) => bank switch
    {
        0 => header,
        1 => middle,
        2 => pointers,
        3 => tail,
        4 => end,
        _ => throw new InvalidDataException("Native Quake field bank is unknown.")
    };

    private static (int Bank, int Offset, int StorageOffset) Resolve(uint address, int length)
    {
        for (int index = 0; index < BankStarts.Length; index++)
        {
            if (length >= 0 && address >= BankStarts[index] && (ulong)address + (uint)length <= (ulong)BankStarts[index] + (uint)BankLengths[index])
            {
                int offset = checked((int)(address - BankStarts[index]));
                return (index, offset, BankOffsets[index] + offset);
            }
        }
        throw new InvalidDataException("Native Quake access leaves its actual retained field banks.");
    }

    private Owner RequireOwner() => owner ?? throw new InvalidOperationException("Native Quake snapshot fields cannot be mutated; restore owned working state first.");

    private void RequireSnapshotInitializer()
    {
        if (owner is not null)
        {
            throw new InvalidOperationException("Native Quake working fields require explicit owned mutation rather than record initializers.");
        }
    }

    private static void ValidateActors(ImmutableArray<NativeBattleQuake38SavedActor> actors)
    {
        if (actors.IsDefault || actors.Length != 7 || actors.Any(actor => actor is null))
        {
            throw new InvalidDataException("Native Quake requires all seven retained saved actor records.");
        }
    }

    private sealed class Owner(ImmutableArray<NativeBattleQuake38SavedActor> actors)
    {
        public NativeBattleWorkingMemory Storage { get; } = new(StorageLength, initialCapacity: StorageLength);
        public ImmutableArray<NativeBattleQuake38SavedActor> SavedActors = actors;
        public ImmutableArray<NativeBattleQuake38SavedActor> CheckpointActors;
    }
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleQuake38State? Quake38
    {
        get => effectState.Quake38;
        init => effectState = effectState with { Quake38 = value };
    }
}
