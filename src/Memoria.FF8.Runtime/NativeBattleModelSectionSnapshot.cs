namespace Memoria.FF8.Runtime;

/// <summary>Reads selected source bytes and knownness without converting a CPU resource into a guest address.</summary>
internal static class NativeBattleModelSectionSnapshot
{
    internal static (byte[] Data, bool[] Known) Read(NativeBattleModelCpuResources? cpu, NativeBattleFileStaging staging, NativeBattleResourcePointer pointer, int offset, int length, Func<uint, int, (byte[] Data, bool[] Known)> readShared)
    {
        ArgumentNullException.ThrowIfNull(pointer);
        if (!pointer.HasValidOwner() || pointer.CpuRow is not null || offset < 0 || length < 0 || offset > int.MaxValue - length || pointer.Length is int extent && offset > extent - length)
        {
            throw new InvalidDataException("Model snapshot leaves its selected section owner or extent.");
        }
        if (pointer.Cpu is { IsOwnedResource: true })
        {
            return NativeBattleModelResourceView.Open(cpu ?? throw new NotSupportedException("Model snapshot requires its CPU resource owner."), staging, pointer, checked(offset + length)).ReadSnapshot(offset, length);
        }
        uint address = pointer.Cpu?.TryPhysicalAddress() ?? pointer.StagingAddress ?? throw new InvalidDataException("Shared model snapshot requires its actual addressed owner.");
        return readShared(checked(address + (uint)offset), length);
    }
}
