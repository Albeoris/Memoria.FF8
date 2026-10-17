using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleQuezacotlFlashTests
{
    [Theory]
    [InlineData(73, 0x010203u)]
    [InlineData(79, 0x010203u)]
    [InlineData(80, 0x040506u)]
    [InlineData(81, 0x070809u)]
    [InlineData(227, 0x040506u)]
    [InlineData(228, 0x070809u)]
    [InlineData(248, 0x010203u)]
    [InlineData(250, 0x010203u)]
    [InlineData(253, 0x010203u)]
    [InlineData(257, 0x040506u)]
    [InlineData(258, 0x010203u)]
    [InlineData(259, 0x040506u)]
    [InlineData(315, 0x070809u)]
    [InlineData(316, 0x040506u)]
    [InlineData(317, 0x010203u)]
    [InlineData(318, 0x040506u)]
    [InlineData(319, 0x070809u)]
    public void PlainFlashWritesOriginalQuadTagCommandCoordinatesBucketAndProxyFlags(ushort phase, uint color)
    {
        NativeBattleQuezacotlScreenPacket packet = Assert.Single(NativeBattleQuezacotlFlash.Emit(phase, Resources()));
        Assert.Equal((ushort)1, packet.Flags20);
        Assert.Equal(24, packet.Batch.AllocationBytes);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(packet.Batch.Attempts);
        Assert.Equal(8, attempt.Primitive!.Bucket);
        Assert.Equal(new uint[] { 0x05000000, 0x2A000000 | color, 0, 320, 216u << 16, (216u << 16) | 320 }, attempt.Primitive.Words.ToArray());
        Assert.Equal(new[] { 0, 0, 0, 0 }, attempt.Primitive.VertexPayload4.ToArray());
        Assert.All(attempt.WriteMask, value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
    }

    [Fact]
    public void Phase78PreservesOriginalInsertionOrderAndIndependentCursorAcrossThreePackets()
    {
        ImmutableArray<NativeBattleQuezacotlScreenPacket> packets = NativeBattleQuezacotlFlash.Emit(78, Resources());
        Assert.Equal(new ushort[] { 0, 1, 0 }, packets.Select(packet => packet.Flags20).ToArray());
        Assert.Equal(new uint[] { 0xE10002A0, 0x2A040506, 0xE10002C0 }, packets.Select(packet => packet.Batch.Attempts[0].Primitive!.Words[1]).ToArray());
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(116);
        NativeBattleOriginalEffectState effect = new(116, 115, new(116, 0x6C3550, 0x6C3560), null, null) { DrawArenaOffset = 128 };
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        foreach (NativeBattleQuezacotlScreenPacket packet in packets)
        {
            NativeBattleQuezacotlPacketWrite written = NativeBattleQuezacotlPacketArena.Append(memory, effect, ordering, packet.Batch, packet.Flags20);
            memory = written.Memory;
            effect = written.Effect;
            ordering = written.Ordering;
        }
        Assert.Equal(168, effect.DrawArenaOffset);
        Assert.Equal(new[] { 128, 136, 160 }, ordering.Packets.Select(packet => packet.Reference.ByteOffset).ToArray());
        Assert.Equal(new ushort[] { 0, 1, 0 }, ordering.Proxies.Select(proxy => proxy.Flags20).ToArray());
        Assert.Throws<NotSupportedException>(() => memory.Arena!.Read(128, 3));
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(memory))!;
        Assert.Equal(memory.Arena!.Read(136 + 4, 20), restored.Arena!.Read(136 + 4, 20));
    }

    [Fact]
    public void MissingOriginalColorsRemainUnknownOnlyWhenAFlashIsActuallyReached()
    {
        NativeBattleQuezacotlResources unknown = Resources() with { FlashColors = null };
        Assert.Empty(NativeBattleQuezacotlFlash.Emit(72, unknown));
        Assert.Throws<NotSupportedException>(() => NativeBattleQuezacotlFlash.Emit(73, unknown));
    }

    [Fact]
    public void FourthColorIsStrictAt229AndDoesNotChangeTheOldThreeColorDirectory()
    {
        NativeBattleQuezacotlResources original = Resources();
        Assert.Single(NativeBattleQuezacotlFlash.Emit(228, original));
        Assert.Throws<NotSupportedException>(() => NativeBattleQuezacotlFlash.Emit(229, original));
        Assert.DoesNotContain("Frame229FlashColor", JsonSerializer.Serialize(original));
        NativeBattleQuezacotlResources observed = original with { Frame229FlashColor = 0x00505050 };
        NativeBattleQuezacotlScreenPacket packet = Assert.Single(NativeBattleQuezacotlFlash.Emit(229, observed));
        Assert.Equal(0x2A505050u, packet.Batch.Attempts[0].Primitive!.Words[1]);
        Assert.Equal(original.FlashColors, observed.FlashColors);
        Assert.Single(NativeBattleQuezacotlFlash.Emit(229, observed with { FlashColors = null }));
        Assert.Equal(JsonSerializer.Serialize(observed), JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleQuezacotlResources>(JsonSerializer.Serialize(observed))));
    }

    private static NativeBattleQuezacotlResources Resources() => new(new("mag115.tim", []), new("source0", []), new("source1", []), new("test", [], [])) { FlashColors = [0x010203, 0x040506, 0x070809] };
}
