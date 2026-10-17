using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>One native callback's SZ0..SZ3 knowledge; unknown entry registers are replaced only by actual RTPS/RTPT depth writes.</summary>
public sealed class NativeBattleGteDepthTrace
{
    private int?[] depths;

    public NativeBattleGteDepthTrace(ImmutableArray<int?> incoming = default)
    {
        depths = incoming.IsDefault ? new int?[4] : Validate(incoming);
    }

    public ImmutableArray<int?> DepthFifo4 => depths.ToImmutableArray();

    public void Push(ushort depth)
    {
        Array.Copy(depths, 1, depths, 0, 3);
        depths[3] = depth;
    }

    public void ObserveFinal(ImmutableArray<int?> actual)
    {
        depths = Validate(actual);
    }

    private static int?[] Validate(ImmutableArray<int?> actual)
    {
        if (actual.IsDefault || actual.Length != 4 || actual.Any(depth => depth is < 0 or > ushort.MaxValue))
        {
            throw new InvalidDataException("Native GTE depth trace requires four actual or explicitly unknown unsigned-depth register values.");
        }
        return actual.ToArray();
    }
}
