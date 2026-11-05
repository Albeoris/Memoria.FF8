namespace Memoria.FF8.Runtime;

/// <summary>A bounded, nonserialized section view over one published CPU bank or the current shared staging authority.</summary>
/// <remarks>CPU banks are immutable publications. Staging views observe current writes and retain their transaction's lifetime checks. Reopen a view after its row is rebound or relocated. Reads return copies, never borrowed backing spans.</remarks>
public readonly struct NativeBattleModelResourceView
{
    private readonly NativeBattleModelCpuBank? bank;
    private readonly NativeBattleFileStaging? staging;
    private readonly int bankOffset;
    private readonly ulong address;

    private NativeBattleModelResourceView(NativeBattleModelCpuBank? bank, NativeBattleFileStaging? staging, int bankOffset, ulong address, int length)
    {
        this.bank = bank;
        this.staging = staging;
        this.bankOffset = bankOffset;
        this.address = address;
        Length = length;
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
            return OpenCpu(cpu, staging, reference, length);
        }
        return OpenStaging(staging, pointer.StagingAddress!.Value, length);
    }

    public static NativeBattleModelResourceView Open(NativeBattleModelCpuResources cpu, NativeBattleFileStaging staging, NativeBattleModelCpuReference reference, int length)
    {
        ArgumentNullException.ThrowIfNull(cpu);
        ArgumentNullException.ThrowIfNull(staging);
        ArgumentNullException.ThrowIfNull(reference);
        if (length < 0 || !NativeFieldRuntimeProfile.IsSupportedImage(reference.EffectiveLayout.ExecutableHash) || reference.Bank > 4 || reference.Offset < 0 || reference.ResourceOnly && reference.Bank >= 3 || reference.Bank < 3 && !reference.ResourceOnly && reference.Offset > reference.EffectiveLayout.BankLength)
        {
            throw new InvalidDataException("Native model section requires its observed CPU resource owner.");
        }
        return OpenCpu(cpu, staging, reference, length);
    }

    public NativeBattleModelResourceView Slice(int offset, int length)
    {
        RequireOwner();
        RequireRange(offset, length, Length);
        return bank is not null ? new(bank, null, checked(bankOffset + offset), 0, length) : new(null, staging, 0, address + (uint)offset, length);
    }

    public byte[] Read(int offset, int length)
    {
        RequireOwner();
        RequireRange(offset, length, Length);
        if (bank is not null)
        {
            return bank.Read(checked(bankOffset + offset), length);
        }
        ulong selected = address + (uint)offset;
        if (selected > uint.MaxValue)
        {
            staging!.Validate();
            return [];
        }
        return staging!.Read((uint)selected, length);
    }

    private static NativeBattleModelResourceView OpenStaging(NativeBattleFileStaging staging, uint address, int length)
    {
        NativeBattleFileStaging.RequirePhysicalRange(address, length);
        return new(null, staging, 0, address, length);
    }

    private static NativeBattleModelResourceView OpenCpu(NativeBattleModelCpuResources cpu, NativeBattleFileStaging staging, NativeBattleModelCpuReference reference, int length)
    {
        if (reference.Bank >= 3)
        {
            return OpenStaging(staging, reference.SharedAddress, length);
        }
        if (reference.Bank >= cpu.Cpu.Banks.Length)
        {
            throw new InvalidDataException("Native model section selects an absent CPU bank.");
        }
        NativeBattleModelCpuBank bank = cpu.Cpu.Banks[reference.Bank];
        RequireRange(reference.Offset, length, bank.ResourceCapacity);
        return new(bank, null, reference.Offset, 0, length);
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
