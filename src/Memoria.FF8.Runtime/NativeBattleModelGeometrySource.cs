namespace Memoria.FF8.Runtime;

/// <summary>Call-local relative geometry reader with its actual resource owner and guest-address domain.</summary>
public readonly struct NativeBattleModelGeometrySource
{
    private readonly Func<int, int, byte[]>? read;
    private readonly uint? numericAddress;
    public NativeBattleResourcePointer Owner { get; }
    public long PresentationIdentity { get; }

    private NativeBattleModelGeometrySource(NativeBattleResourcePointer owner, Func<int, int, byte[]> read, long identity)
    {
        if (!owner.HasValidOwner() || owner.CpuRow is not null)
        {
            throw new InvalidDataException("Model geometry requires a selected resource section owner.");
        }
        ArgumentNullException.ThrowIfNull(read);
        Owner = owner;
        this.read = read;
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

    public static NativeBattleModelGeometrySource Open(NativeBattleModelCpuResources cpu, NativeBattleFileStaging staging, NativeBattleResourcePointer pointer, Func<int, int, byte[]> readShared)
    {
        ArgumentNullException.ThrowIfNull(cpu);
        ArgumentNullException.ThrowIfNull(staging);
        ArgumentNullException.ThrowIfNull(pointer);
        if (pointer.Cpu is { Bank: < 3 } reference)
        {
            NativeBattleModelCpuBank bank = cpu.Cpu.Banks[reference.Bank];
            int length = pointer.Length ?? checked((reference.ResourceOnly ? bank.ResourceCapacity : bank.Capacity) - reference.Offset);
            NativeBattleModelResourceView view = NativeBattleModelResourceView.Open(cpu, staging, pointer, length);
            long identity = reference.ResourceOnly ? NativeBattlePilotVisualLifetimes.EpochFor(bank) : reference.PhysicalAddress;
            return new(pointer with { Length = length }, view.Read, identity);
        }
        return OpenShared(pointer, readShared);
    }

    public static NativeBattleModelGeometrySource OpenShared(NativeBattleResourcePointer pointer, Func<int, int, byte[]> read)
    {
        ArgumentNullException.ThrowIfNull(pointer);
        if (pointer.Cpu is { Bank: < 3 })
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
        Require(offset, 0);
        return numericAddress is uint address ? checked(address + (uint)offset) : null;
    }

    public NativeBattleResourcePointer Cursor(int offset)
    {
        Require(offset, 0);
        return Owner with
        {
            Cpu = Owner.Cpu is { } reference ? reference with { Offset = checked(reference.Offset + offset) } : null,
            StagingAddress = Owner.StagingAddress is uint address ? checked(address + (uint)offset) : null,
            Length = Owner.Length is int length ? length - offset : null
        };
    }

    private void Require(int offset, int length)
    {
        if (read is null || offset < 0 || length < 0 || offset > int.MaxValue - length || Owner.Length is int available && offset > available - length)
        {
            throw new InvalidDataException("Model geometry read or cursor leaves its actual source extent.");
        }
    }
}
