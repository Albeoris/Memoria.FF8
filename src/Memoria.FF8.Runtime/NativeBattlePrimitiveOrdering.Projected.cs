using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattlePrimitiveProxy
{
    /// <summary>45C870's four original precision DWORDs at proxy offsets4..16;45C8E0 leaves them untouched.</summary>
    public ImmutableArray<int> VertexPayload4 { get; init; } = [];
}

public sealed partial record NativeBattlePrimitiveOrdering
{
    public NativeBattlePrimitiveOrdering PrependProjected45C870(int bucket, NativeBattlePrimitivePacket packet, ImmutableArray<int> vertexPayload4)
    {
        if (vertexPayload4.IsDefault || vertexPayload4.Length != 4)
        {
            throw new InvalidDataException("Native45C870 requires all four original precision DWORDs.");
        }
        NativeBattlePrimitiveOrdering result = Prepend(bucket, packet, 0);
        if (result.Proxies.Length == Proxies.Length)
        {
            return result;
        }
        int index = result.Proxies.Length - 1;
        return result with { Proxies = result.Proxies.SetItem(index, result.Proxies[index] with { VertexPayload4 = vertexPayload4 }) };
    }
}
