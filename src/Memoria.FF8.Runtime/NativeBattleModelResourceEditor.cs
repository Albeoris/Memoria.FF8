namespace Memoria.FF8.Runtime;

/// <summary>One synchronous CPU-source mutation scope. Publish Current at the caller's operation boundary.</summary>
internal sealed class NativeBattleModelResourceEditor
{
    private readonly NativeBattleFileStaging staging;
    private readonly NativeBattleResourcePointer pointer;
    internal NativeBattleModelCpuResources Current { get; private set; }
    internal NativeBattleModelGeometrySource Geometry { get; }

    internal NativeBattleModelResourceEditor(NativeBattleModelCpuResources cpu, NativeBattleFileStaging staging, NativeBattleResourcePointer pointer)
    {
        if (pointer.Cpu is not { Bank: < 3 })
        {
            throw new InvalidDataException("CPU resource editing requires its selected party bank and address domain.");
        }
        Current = cpu;
        this.staging = staging;
        Geometry = NativeBattleModelGeometrySource.OpenEditor(cpu, staging, pointer, Read);
        this.pointer = Geometry.Owner;
    }

    internal byte[] Read(int offset, int length)
    {
        Require(offset, length);
        return NativeBattleModelResourceView.Open(Current, staging, pointer, pointer.Length!.Value).Read(offset, length);
    }

    internal void Write(int offset, ReadOnlySpan<byte> bytes)
    {
        Require(offset, bytes.Length);
        NativeBattleModelCpuReference reference = pointer.Cpu!;
        NativeBattleModelCpuBank bank = Current.Cpu.Banks[reference.Bank];
        int destination = checked(reference.Offset + offset);
        bank = reference.ResourceOnly ? bank.Write(destination, bytes) : bank.WriteGuest(destination, bytes);
        Current = Current with { Cpu = Current.Cpu with { Banks = Current.Cpu.Banks.SetItem(reference.Bank, bank) } };
    }

    private void Require(int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > pointer.Length!.Value - length)
        {
            throw new InvalidDataException("CPU resource mutation leaves its selected source section.");
        }
    }
}
