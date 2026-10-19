using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleAngelWing84NodeResourcesTests
{
    [Fact]
    public void Original_node_bank_ends_after_its_complete_single_geometry_record()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleAngelWing84NodeResources resources = game.ReadNativeAngelWing84NodeResources();
        resources.Validate();
        Assert.Equal(NativeBattleAngelWing84NodeResources.Length, resources.Data.Length);
        int source = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(resources.Data.AsSpan().Slice(4)));
        Assert.Equal(resources.Data.Length, source + 0x158);
        Assert.Equal((byte)4, resources.Data[source + 0x16]);
        for (int field = 0xC8; field <= 0x144; field += 4)
        {
            uint channel = BinaryPrimitives.ReadUInt32LittleEndian(resources.Data.AsSpan().Slice(source + field));
            Assert.InRange(channel, 0x104u, (uint)source - 1);
        }
    }
}
