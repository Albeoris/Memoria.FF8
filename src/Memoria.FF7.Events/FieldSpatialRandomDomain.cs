using System.Security.Cryptography;

namespace Memoria.FF7.Events;

/// <summary>Reachable phases of the host's byte table, without advancing its random owner.</summary>
public sealed class FieldSpatialRandomDomain
{
    private readonly byte[] table;
    private readonly byte stride;
    internal byte[] Phases { get; }
    public string Fingerprint { get; }
    public byte Stride => stride;

    public FieldSpatialRandomDomain(IReadOnlyList<byte> table, byte stride, byte initialIndex)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (table.Count != 256)
        {
            throw new ArgumentException("A native byte random domain requires its complete table.", nameof(table));
        }
        this.table = table.ToArray();
        this.stride = stride;
        HashSet<byte> phases = [];
        byte index = initialIndex;
        while (phases.Add(index))
        {
            index = unchecked((byte)(index + stride));
        }
        Phases = phases.Order().ToArray();
        byte[] identity = new byte[257 + Phases.Length];
        this.table.CopyTo(identity, 0);
        identity[256] = stride;
        Phases.CopyTo(identity, 257);
        Fingerprint = Convert.ToHexString(SHA256.HashData(identity));
    }

    internal byte Value(byte phase, int draw) => table[unchecked((byte)(phase + draw * stride))];
}
