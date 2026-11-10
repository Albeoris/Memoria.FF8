namespace Memoria.FF7.Runtime;

public sealed partial class Ff7WorldRuntime
{
    internal void WriteGlobalPersistentByte(int address, byte value)
    {
        EnsureHealthy();
        ArgumentOutOfRangeException.ThrowIfNegative(address);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(address, persistent.Length);
        persistent[address] = value;
    }
}
