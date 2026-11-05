namespace Memoria.FF8.Runtime;

/// <summary>Call-local relative geometry reader with its actual resource owner and guest-address domain.</summary>
public readonly struct NativeBattleModelGeometrySource
{
    private readonly Func<int, int, byte[]>? read;
    private readonly uint? numericAddress;
    private readonly Func<int, int, NativeBattleEffectArena>? readArena;
    public NativeBattleResourcePointer Owner { get; }
    public long PresentationIdentity { get; }

    private NativeBattleModelGeometrySource(NativeBattleResourcePointer owner, Func<int, int, byte[]> read, long identity, Func<int, int, NativeBattleEffectArena>? readArena = null)
    {
        if (!owner.HasValidOwner() || owner.CpuRow is not null)
        {
            throw new InvalidDataException("Model geometry requires a selected resource section owner.");
        }
        ArgumentNullException.ThrowIfNull(read);
        Owner = owner;
        this.read = read;
        this.readArena = readArena;
        numericAddress = owner.Cpu?.TryPhysicalAddress() ?? owner.StagingAddress;
        PresentationIdentity = identity;
    }

    internal static NativeBattleModelGeometrySource FromNumeric(uint address, Func<int, int, byte[]> read)
    {
        if (address == 0)
        {
            throw new InvalidDataException("Model geometry requires its actual numeric source address.");
        }
        return new(new(null, address), read, address);
    }

    internal static NativeBattleModelGeometrySource FromNumericArena(uint address, Func<uint, int, NativeBattleEffectArena> read)
    {
        ArgumentNullException.ThrowIfNull(read);
        return new(new(null, address), (offset, length) => read(checked(address + (uint)offset), length).Read(0, length), address, (offset, length) => read(checked(address + (uint)offset), length));
    }

    internal NativeBattleEffectArena ReadArena(int offset, int length)
    {
        Require(offset, length);
        return readArena is not null ? readArena(offset, length) : NativeBattleEffectArena.Clear().Write(0, read!(offset, length));
    }

    internal int AlignmentResidue => Owner.Cpu is { EnemyResourceId: not null } enemy ? (int)(enemy.NativePlacementAddress & 3) : numericAddress is uint address ? (int)(address & 3) : (int)((Owner.Cpu!.EffectiveLayout.BaseAddress + (uint)(Owner.Cpu.Offset & 3)) & 3);

    public static NativeBattleModelGeometrySource Open(NativeBattleModelCpuResources cpu, NativeBattleFileStaging staging, NativeBattleResourcePointer pointer, Func<int, int, byte[]> readShared)
    {
        ArgumentNullException.ThrowIfNull(cpu);
        ArgumentNullException.ThrowIfNull(staging);
        ArgumentNullException.ThrowIfNull(pointer);
        if (pointer.Cpu is { IsOwnedResource: true } reference)
        {
            NativeBattleModelCpuBank bank = pointer.CpuPublication ?? cpu.GetResourceBank(reference);
            int available = checked((reference.ResourceOnly ? bank.ResourceCapacity : bank.Capacity) - reference.ResourceStorageOffset);
            int length = Math.Min(pointer.Length ?? available, available);
            NativeBattleModelResourceView view = NativeBattleModelResourceView.Open(cpu, staging, pointer, length);
            long identity = reference.ResourceOnly || reference.EnemyResourceId is not null ? NativeBattlePilotVisualLifetimes.EpochFor(bank) : reference.PhysicalAddress;
            return new(pointer with { Length = length }, view.Read, identity);
        }
        return OpenShared(pointer, readShared);
    }

    internal static NativeBattleModelGeometrySource OpenEditor(NativeBattleModelCpuResources cpu, NativeBattleFileStaging staging, NativeBattleResourcePointer pointer, Func<int, int, byte[]> readCurrent)
    {
        NativeBattleModelGeometrySource initial = Open(cpu, staging, pointer, readCurrent);
        return new(initial.Owner, readCurrent, initial.PresentationIdentity);
    }

    public static NativeBattleModelGeometrySource OpenShared(NativeBattleResourcePointer pointer, Func<int, int, byte[]> read)
    {
        ArgumentNullException.ThrowIfNull(pointer);
        if (pointer.Cpu is { IsOwnedResource: true })
        {
            throw new InvalidDataException("Party model geometry requires its CPU publication owner.");
        }
        uint address = pointer.Cpu?.TryPhysicalAddress() ?? pointer.StagingAddress ?? throw new InvalidDataException("Shared model geometry requires its addressed byte owner.");
        return new(pointer, read, address);
    }

    public byte[] Read(int offset, int length)
    {
        Require(offset, length);
        return read!(offset, length);
    }

    internal uint? NumericCursor(int offset)
    {
        RequireOffset(offset);
        return numericAddress is uint address ? checked(address + (uint)offset) : null;
    }

    internal NativeBattleEffectArena WriteCursor(NativeBattleEffectArena destination, int field, int offset)
    {
        // Native callers can publish a cursor before a masked object is skipped; reads remain section-bounded.
        return Owner.CpuPublication is null && Owner.Cpu?.EnemyResourceId is null && NumericCursor(offset) is uint address ? destination.Write(field, BitConverter.GetBytes(address)) : destination.WriteResourcePointer(field, Cursor(offset));
    }

    public NativeBattleResourcePointer Cursor(int offset)
    {
        RequireOffset(offset);
        return Owner with
        {
            Cpu = Owner.Cpu is { } reference ? reference.Advance(offset) : null,
            StagingAddress = Owner.StagingAddress is uint address ? checked(address + (uint)offset) : null,
            Length = Owner.Length is int length ? Math.Max(0, length - offset) : null
        };
    }

    private void RequireOffset(int offset)
    {
        if (read is null || offset < 0)
        {
            throw new InvalidDataException("Model source cursor requires its observed owner and nonnegative offset.");
        }
    }

    private void Require(int offset, int length)
    {
        if (read is null || offset < 0 || length < 0 || offset > int.MaxValue - length || Owner.Length is int available && offset > available - length)
        {
            throw new InvalidDataException("Model geometry read or cursor leaves its actual source extent.");
        }
    }
}
