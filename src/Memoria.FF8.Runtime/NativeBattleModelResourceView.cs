namespace Memoria.FF8.Runtime;

/// <summary>A bounded, nonserialized section view over one published CPU bank or the current shared staging authority.</summary>
/// <remarks>CPU banks are immutable publications. Staging views observe current writes and retain their transaction's lifetime checks. Reopen a view after its row is rebound or relocated. Public reads return copies; synchronous internal animation readers may borrow immutable CPU slices.</remarks>
public readonly struct NativeBattleModelResourceView
{
    private readonly NativeBattleModelCpuBank? bank;
    private readonly NativeBattleFileStaging? staging;
    private readonly int bankOffset;
    private readonly ulong address;
    private readonly bool resourceOnly;

    private NativeBattleModelResourceView(NativeBattleModelCpuBank? bank, NativeBattleFileStaging? staging, int bankOffset, ulong address, int length, bool resourceOnly = false)
    {
        this.bank = bank;
        this.staging = staging;
        this.bankOffset = bankOffset;
        this.address = address;
        Length = length;
        this.resourceOnly = resourceOnly;
    }

    public int Length { get; }

    public static NativeBattleModelResourceView Open(NativeBattleModelCpuResources cpu, NativeBattleFileStaging staging, NativeBattleResourcePointer pointer, int length)
    {
        ArgumentNullException.ThrowIfNull(cpu);
        ArgumentNullException.ThrowIfNull(staging);
        ArgumentNullException.ThrowIfNull(pointer);
        if (!pointer.HasValidOwner() || length < 0 || pointer.Length is int extent && length > extent)
        {
            throw new InvalidDataException("Native model section exceeds its observed resource pointer owner or extent.");
        }
        if (pointer.CpuRow is not null)
        {
            throw new NotSupportedException("A native model resource row must select a section before reading bytes.");
        }
        if (pointer.Cpu is { } reference)
        {
            return OpenCpu(cpu, staging, reference, length, pointer.CpuPublication);
        }
        return OpenStaging(staging, pointer.StagingAddress!.Value, length);
    }

    public static NativeBattleModelResourceView Open(NativeBattleModelCpuResources cpu, NativeBattleFileStaging staging, NativeBattleModelCpuReference reference, int length)
    {
        ArgumentNullException.ThrowIfNull(cpu);
        ArgumentNullException.ThrowIfNull(staging);
        ArgumentNullException.ThrowIfNull(reference);
        if (length < 0 || !new NativeBattleResourcePointer(reference, null, length).HasValidOwner())
        {
            throw new InvalidDataException("Native model section requires its observed CPU resource owner.");
        }
        return OpenCpu(cpu, staging, reference, length);
    }

    public NativeBattleModelResourceView Slice(int offset, int length)
    {
        RequireOwner();
        RequireRange(offset, length, Length);
        return bank is not null ? new(bank, null, checked(bankOffset + offset), 0, length, resourceOnly) : new(null, staging, 0, address + (uint)offset, length);
    }

    public byte[] Read(int offset, int length)
    {
        RequireOwner();
        RequireRange(offset, length, Length);
        if (bank is not null)
        {
            return resourceOnly ? bank.Read(checked(bankOffset + offset), length) : bank.ReadGuest(checked(bankOffset + offset), length);
        }
        ulong selected = address + (uint)offset;
        if (selected > uint.MaxValue)
        {
            staging!.Validate();
            return [];
        }
        return staging!.Read((uint)selected, length);
    }

    internal ReadOnlyMemory<byte> ReadMemory(int offset, int length)
    {
        RequireOwner();
        RequireRange(offset, length, Length);
        if (bank is not null)
        {
            _ = resourceOnly ? bank.Read(0, 0) : bank.ReadGuest(0, 0);
            int first = checked(bankOffset + offset);
            foreach (NativeBattleModelResourceSegment segment in resourceOnly ? bank.ResourceSegments : bank.GuestSegments)
            {
                if (first >= segment.Offset && first - segment.Offset <= segment.Length - length)
                {
                    return segment.Source.AsMemory().Slice(segment.SourceOffset + first - segment.Offset, length);
                }
            }
        }
        return Read(offset, length);
    }

    public (byte[] Data, bool[] Known) ReadSnapshot(int offset, int length)
    {
        RequireOwner();
        RequireRange(offset, length, Length);
        if (bank is null)
        {
            ulong selected = address + (uint)offset;
            if (selected > uint.MaxValue)
            {
                staging!.Validate();
                return ([], []);
            }
            return staging!.ReadSnapshot((uint)selected, length);
        }
        _ = resourceOnly ? bank.Read(0, 0) : bank.ReadGuest(0, 0);
        byte[] data = new byte[length];
        bool[] known = new bool[length];
        int first = checked(bankOffset + offset);
        foreach (NativeBattleModelResourceSegment segment in resourceOnly ? bank.ResourceSegments : bank.GuestSegments)
        {
            int start = Math.Max(first, segment.Offset);
            int end = Math.Min(first + length, segment.Offset + segment.Length);
            if (end > start)
            {
                segment.Source.AsSpan(segment.SourceOffset + start - segment.Offset, end - start).CopyTo(data.AsSpan(start - first));
                Array.Fill(known, true, start - first, end - start);
            }
        }
        return (data, known);
    }

    internal static NativeBattleModelCpuResources WritePartySection(NativeBattleModelCpuResources cpu, NativeBattleFileStaging staging, NativeBattleResourcePointer pointer, int offset, ReadOnlySpan<byte> bytes)
    {
        if (pointer.CpuPublication is not null || pointer.Cpu is not { IsOwnedResource: true } reference)
        {
            throw new InvalidDataException("CPU section writes require their actual party resource owner.");
        }
        int available = checked((reference.ResourceOnly ? cpu.GetResourceBank(reference).ResourceCapacity : cpu.GetResourceBank(reference).Capacity) - reference.ResourceStorageOffset);
        int extent = Math.Min(pointer.Length ?? available, available);
        NativeBattleModelResourceView view = Open(cpu, staging, pointer, extent);
        RequireRange(offset, bytes.Length, view.Length);
        NativeBattleModelCpuBank bank = cpu.GetResourceBank(reference);
        int destination = checked(reference.ResourceStorageOffset + offset);
        bank = reference.ResourceOnly ? bank.Write(destination, bytes) : bank.WriteGuest(destination, bytes);
        return reference.EnemyResourceId is int id ? cpu with { EnemyResources = cpu.EnemyResources!.SetItem(id, bank) } : cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(reference.Bank, bank) } };
    }

    private static NativeBattleModelResourceView OpenStaging(NativeBattleFileStaging staging, uint address, int length)
    {
        NativeBattleFileStaging.RequirePhysicalRange(address, length);
        return new(null, staging, 0, address, length);
    }

    private static NativeBattleModelResourceView OpenCpu(NativeBattleModelCpuResources cpu, NativeBattleFileStaging staging, NativeBattleModelCpuReference reference, int length, NativeBattleModelCpuBank? publication = null)
    {
        if (!reference.IsOwnedResource)
        {
            return OpenStaging(staging, reference.SharedAddress, length);
        }
        if (reference.Bank < 3 && reference.Bank >= cpu.Cpu.Banks.Length)
        {
            throw new InvalidDataException("Native model section selects an absent CPU bank.");
        }
        NativeBattleModelCpuBank bank = publication ?? cpu.GetResourceBank(reference);
        if (reference.ResourceOnly && !bank.HasResourceStorage)
        {
            throw new InvalidDataException("A logical model section requires its retained resource owner.");
        }
        RequireRange(reference.ResourceStorageOffset, length, reference.ResourceOnly ? bank.ResourceCapacity : bank.Capacity);
        return new(bank, null, reference.ResourceStorageOffset, 0, length, reference.ResourceOnly);
    }

    private void RequireOwner()
    {
        if (bank is null && staging is null)
        {
            throw new InvalidOperationException("A native model section view must be opened against its resource owner.");
        }
    }

    private static void RequireRange(int offset, int length, int extent)
    {
        if (offset < 0 || length < 0 || offset > extent - length)
        {
            throw new InvalidDataException("Native model section access exceeds its bounded resource view.");
        }
    }
}
