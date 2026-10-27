using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleFullscreenOverlayTests
{
    [Fact]
    public void FixedPacketsTraverseModeThenReverseQuadrantsAndRetainUnusedProxyDepth()
    {
        var fixture = Fixture([0x00123456, 0xFF000000]);
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear().Prepend(1, new("prior", 0, 0), [0], 0, [17, 23, 31, 47]).ResetAfterDraw45D080();
        NativeBattleFullscreenOverlayFrame original = fixture.Frame with { Ordering = ordering };
        string arena = JsonSerializer.Serialize(original.Memory.Arena);
        string common = JsonSerializer.Serialize(original.Memory.PrimitiveProcess);
        NativeBattleFullscreenOverlayStep step = NativeBattleFullscreenOverlay.Advance8DDEC0(original, fixture.Address, 0, fixture.Program);
        Assert.Equal(arena, JsonSerializer.Serialize(step.Frame.Memory.Arena));
        Assert.Equal(common, JsonSerializer.Serialize(step.Frame.Memory.PrimitiveProcess));
        Assert.Equal(5, step.Frame.Ordering.ProxyCursor);
        Assert.Equal(new[] { 17, 23, 31, 47 }, step.Frame.Ordering.Proxies[0].VertexPayload4);
        Assert.All(step.Frame.Ordering.Proxies.Skip(1), proxy => Assert.Empty(proxy.VertexPayload4));
        Assert.Equal(new[] { 96, 72, 48, 24, 0 }, Chain(step.Frame.Ordering));
        NativeBattleFullscreenOverlayState state = step.Frame.Memory.FullscreenOverlay!;
        Assert.Empty(state.Packets.UnknownRegions);
        Assert.Equal(0x02000000u, U32(state.Read(96, 12), 0) & 0xFF000000);
        Assert.Equal(0xE100002Au, U32(state.Read(96, 12), 4));
        Assert.Equal(0u, U32(state.Read(96, 12), 8));
        for (int quadrant = 0; quadrant < 4; quadrant++)
        {
            byte[] packet = state.Read(quadrant * 24, 24);
            Assert.Equal(5, packet[3]);
            Assert.Equal(0x2A123456u, U32(packet, 4));
            int x = (quadrant & 1) * 160;
            int y = (quadrant >> 1) * 120;
            Assert.Equal(new[] { (x, y), (x + 160, y), (x, y + 120), (x + 160, y + 120) }, Enumerable.Range(0, 4).Select(index => ((int)BinaryPrimitives.ReadInt16LittleEndian(packet.AsSpan(8 + index * 4)), (int)BinaryPrimitives.ReadInt16LittleEndian(packet.AsSpan(10 + index * 4)))));
        }
        string beforeTerminal = JsonSerializer.Serialize(state);
        NativeBattleFullscreenOverlayStep terminal = NativeBattleFullscreenOverlay.Advance8DDEC0(step.Frame, fixture.Address, 0, fixture.Program);
        Assert.Equal(2, terminal.Result);
        Assert.Equal(0, terminal.Frame.Pools[0].Head);
        NativeBattleEffectHierarchyStep released = new NativeBattleEffectHierarchy(terminal.Frame.Pools).Advance(0, (current, _) => new(current, terminal.Result));
        Assert.Equal(0, released.Survivors);
        Assert.Null(released.Hierarchy.Pools[0].Head);
        Assert.Equal(beforeTerminal, JsonSerializer.Serialize(terminal.Frame.Memory.FullscreenOverlay));
    }

    [Fact]
    public void PausedFirstStageStillDrawsAndCountersSeparateFromProgramProgress()
    {
        var fixture = Fixture([0x00234567, 0x00456789, 0xFF000000]);
        NativeBattleFullscreenOverlayFrame frame = fixture.Frame;
        for (int index = 0; index < 3; index++)
        {
            frame = frame with { Ordering = NativeBattleLogicalOrdering.Clear() };
            frame = NativeBattleFullscreenOverlay.Advance8DDEC0(frame, fixture.Address, 1, fixture.Program).Frame;
            NativeBattleModulePoolTask task = frame.Pools[0].Slots[0];
            Assert.Equal((byte)1, task.ReadByte(0x29));
            Assert.Equal((ushort)(index + 1), task.ReadWord(0x24));
            Assert.Equal((ushort)0, task.ReadWord(0x3A));
            Assert.Equal((ushort)0, task.ReadWord(0x3C));
            Assert.Equal(0x00234567u, task.ReadDword(0x30));
        }
        NativeBattleFullscreenOverlayFrame restored = JsonSerializer.Deserialize<NativeBattleFullscreenOverlayFrame>(JsonSerializer.Serialize(frame))!;
        NativeBattleFullscreenOverlayStep next = NativeBattleFullscreenOverlay.Advance8DDEC0(frame, fixture.Address, 0, fixture.Program);
        NativeBattleFullscreenOverlayStep coldNext = NativeBattleFullscreenOverlay.Advance8DDEC0(restored, fixture.Address, 0, fixture.Program);
        Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(coldNext));
        Assert.Equal((ushort)1, next.Frame.Pools[0].Slots[0].ReadWord(0x3A));
        Assert.Equal((ushort)1, next.Frame.Pools[0].Slots[0].ReadWord(0x3C));
    }

    [Fact]
    public void ThresholdLoadsFollowingDwordWithoutRecursivelyInterpretingItsOpcode()
    {
        var fixture = Fixture([0xFE020000, 0xFF123456]);
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Pools);
        NativeBattleFullscreenOverlayFrame frame = fixture.Frame with { Pools = hierarchy.Write(fixture.Address, hierarchy.Read(fixture.Address).WriteDword(0x30, 0x00345678)).Pools };
        for (int callback = 0; callback < 2; callback++)
        {
            frame = NativeBattleFullscreenOverlay.Advance8DDEC0(frame with { Ordering = NativeBattleLogicalOrdering.Clear() }, fixture.Address, 0, fixture.Program).Frame;
            Assert.Equal((ushort)0, frame.Pools[0].Slots[0].ReadWord(0x3A));
            Assert.Equal(0x00345678u, frame.Pools[0].Slots[0].ReadDword(0x30));
        }
        frame = NativeBattleFullscreenOverlay.Advance8DDEC0(frame with { Ordering = NativeBattleLogicalOrdering.Clear() }, fixture.Address, 1, fixture.Program).Frame;
        Assert.Equal((ushort)0, frame.Pools[0].Slots[0].ReadWord(0x3A));
        NativeBattleFullscreenOverlayStep loaded = NativeBattleFullscreenOverlay.Advance8DDEC0(frame with { Ordering = NativeBattleLogicalOrdering.Clear() }, fixture.Address, 0, fixture.Program);
        Assert.Equal(0, loaded.Result);
        Assert.Equal((ushort)1, loaded.Frame.Pools[0].Slots[0].ReadWord(0x3A));
        Assert.Equal(0xFF123456u, loaded.Frame.Pools[0].Slots[0].ReadDword(0x30));
        Assert.Equal(0x2A123456u, U32(loaded.Frame.Memory.FullscreenOverlay!.Read(0, 24), 4));
        Assert.Equal(2, NativeBattleFullscreenOverlay.Advance8DDEC0(loaded.Frame, fixture.Address, 0, fixture.Program).Result);
    }

    [Fact]
    public void SignedDelayWrapAndBoundedProgramRejectInventedCompletion()
    {
        var fixture = Fixture([0x00112233]);
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(fixture.Address).WriteWord(0x38, 0x8000);
        NativeBattleFullscreenOverlayFrame frame = fixture.Frame with { Pools = hierarchy.Write(fixture.Address, task).Pools };
        NativeBattleFullscreenOverlayStep delayed = NativeBattleFullscreenOverlay.Advance8DDEC0(frame, fixture.Address, 1, fixture.Program);
        Assert.Equal((ushort)0x7FFF, delayed.Frame.Pools[0].Slots[0].ReadWord(0x38));
        Assert.Equal((byte)0, delayed.Frame.Pools[0].Slots[0].ReadByte(0x29));
        Assert.Empty(delayed.Frame.Ordering.Packets);
        NativeBattleFullscreenOverlayStep first = NativeBattleFullscreenOverlay.Advance8DDEC0(fixture.Frame, fixture.Address, 0, fixture.Program);
        Assert.Throws<InvalidDataException>(() => NativeBattleFullscreenOverlay.Advance8DDEC0(first.Frame, fixture.Address, 0, fixture.Program));
        hierarchy = new(fixture.Frame.Pools);
        frame = fixture.Frame with { Pools = hierarchy.Write(fixture.Address, hierarchy.Read(fixture.Address).WriteWord(0x3A, 0xFFFF)).Pools };
        Assert.Throws<InvalidDataException>(() => NativeBattleFullscreenOverlay.Advance8DDEC0(frame, fixture.Address, 0, fixture.Program));
        Assert.Throws<NotSupportedException>(() => NativeBattleFullscreenOverlay.Advance8DDEC0(fixture.Frame with { Memory = fixture.Frame.Memory with { FullscreenOverlay = null } }, fixture.Address, 0, fixture.Program));
    }

    [Fact]
    public void SaturatedProxyBankClearsF4TagsAndRetainsUnwrittenE1LinkAsUnknown()
    {
        var fixture = Fixture([0x00112233]);
        NativeBattleLogicalProxy proxy = new(new("unread", 0, 0), 0, [], null);
        NativeBattleLogicalOrdering ordering = NativeBattleOrderingTestFixture.Working(NativeBattleLogicalOrdering.Clear(), data => data with { Proxies = Enumerable.Repeat(proxy, NativeBattlePrimitiveOrdering.ProxyCapacity).ToImmutableArray(), ProxyCursor = NativeBattlePrimitiveOrdering.ProxyCapacity });
        NativeBattleFullscreenOverlayStep step = NativeBattleFullscreenOverlay.Advance8DDEC0(fixture.Frame with { Ordering = ordering }, fixture.Address, 0, fixture.Program);
        Assert.Empty(step.Frame.Ordering.Packets);
        for (int quadrant = 0; quadrant < 4; quadrant++)
        {
            Assert.Equal(0x05000000u, U32(step.Frame.Memory.FullscreenOverlay!.Read(quadrant * 24, 4), 0));
        }
        Assert.Throws<NotSupportedException>(() => step.Frame.Memory.FullscreenOverlay!.Read(96, 3));
        Assert.Equal(0x2A112233u, U32(step.Frame.Memory.FullscreenOverlay!.Read(4, 4), 0));
    }

    private static IEnumerable<int> Chain(NativeBattleLogicalOrdering ordering)
    {
        NativeBattlePrimitiveLink link = ordering.Table[7];
        while (link.Proxy)
        {
            NativeBattlePrimitiveReference reference = ordering.Proxies[link.Index].Packet;
            yield return reference.ByteOffset;
            link = ordering.Packets.Single(packet => packet.Reference == reference).Next;
        }
    }
    private static uint U32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
    private static (NativeBattleFullscreenOverlayFrame Frame, uint Address, NativeBattleFullscreenOverlayProgram Program) Fixture(ImmutableArray<uint> commands)
    {
        NativeBattleEffectHierarchy hierarchy = new([NativeBattleEffectTaskPool.Initialize(0x1000, 0x2000, 0x40, 1)]);
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(0, NativeBattleFullscreenOverlay.Callback, 0x40);
        hierarchy = appended.Hierarchy.Write(appended.Address, appended.Hierarchy.Read(appended.Address).WriteDword(0x34, 0x3000));
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(0x100, new byte[] { 3, 5, 7 }), PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess(), FullscreenOverlay = NativeBattleFullscreenOverlayState.Unknown() };
        return (new(memory, hierarchy.Pools, NativeBattleLogicalOrdering.Clear()), appended.Address, new(0x3000, commands));
    }
}
