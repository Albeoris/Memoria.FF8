using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel274ResourcesTests
{
    [Fact]
    public void BoundedResourceReadCopiesItsImmutableResultOnlyOnce()
    {
        byte[] bytes = new byte[NativeBattleDuel274Resources.EndAddress - NativeBattleDuel274Resources.Address];
        bytes[123] = 0xA5;
        NativeBattleDuel274Resources resources = new(bytes.ToImmutableArray(), []);
        _ = resources.Read(NativeBattleDuel274Resources.Address, 1);
        long before = GC.GetAllocatedBytesForCurrentThread();
        ImmutableArray<byte> result = resources.Read(NativeBattleDuel274Resources.Address + 100, 65536);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0xA5, result[23]);
        Assert.True(allocated < 70000, $"A resource slice allocated {allocated} bytes.");
        Assert.Throws<NotSupportedException>(() => resources.Read(NativeBattleDuel274Resources.Address - 1, 1));
        Assert.Throws<NotSupportedException>(() => resources.Read(NativeBattleDuel274Resources.EndAddress, 1));
        Assert.Throws<NotSupportedException>(() => resources.Read(NativeBattleDuel274Resources.Address, -1));
    }
}
