using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHoly175WarpDrawingTests
{
    private static readonly Lazy<NativeBattleHoly175WarpInitializationResult> Grid = new(() => NativeBattleHoly175WarpInitialization.InitializeGrid5D2210(NativeBattleHoly175WarpState.Unknown(), new(1)));
    private static NativeBattleCameraProjection Projection => new(null, 200, 160, 108);

    [Fact]
    public void PendingFaceProjectsAllPointsAndLinksOnlySelectedInterleavedBufferAt4122()
    {
        NativeBattleHoly175WarpFrame frame = Fixture(delay: 10);
        byte[] face = frame.State.Faces.Read(0, 36);
        NativeBattleHoly175WarpDraw draw = Advance(frame, 10, 1);
        Assert.Equal(1, draw.AliveFaces);
        Assert.Equal(17u, draw.WarpAlive);
        Assert.Equal(face, draw.Frame.State.Faces.Read(0, 36));
        NativeBattleLogicalProxy proxy = Assert.Single(draw.Frame.Ordering.Proxies);
        Assert.Equal(new NativeBattlePrimitiveReference(NativeBattleHoly175WarpDrawing.PacketBank, 0, 32), proxy.Packet);
        Assert.Equal(new[] { 200, 200, 200, 200 }, proxy.VertexPayload4.ToArray());
        Assert.True(draw.Frame.Ordering.Table[4122].Proxy);
        Assert.Equal(0x24808080u, U(draw.Frame.State.PhysicalPackets.Read(36, 4)));
        Assert.Equal(draw.Frame.State.GridA.Read(0, 4), draw.Frame.State.PhysicalPackets.Read(40, 4));
        Assert.Equal(draw.Frame.State.GridA.Read(16, 4), draw.Frame.State.PhysicalPackets.Read(48, 4));
        Assert.Equal(draw.Frame.State.GridB.Read(0, 4), draw.Frame.State.PhysicalPackets.Read(56, 4));
        Assert.Throws<NotSupportedException>(() => draw.Frame.State.PhysicalPackets.Read(8, 4));
        Assert.Equal(4, draw.Frame.State.GridA.Read(314 * 16, 4).Length);
        Assert.Equal(4, draw.Frame.State.GridB.Read(279 * 16, 4).Length);
        Assert.Equal(frame.PrivateCursor, draw.Frame.PrivateCursor);
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(0x200u)]
    public void PauseGatesMotionOnlyAndRetainsFt3UvsWhileStillDrawing(uint pause)
    {
        NativeBattleHoly175WarpFrame frame = Fixture(delay: 0, scale: 400, offsetX: 7, offsetY: 3);
        byte[] before = frame.State.Faces.Read(0, 36);
        byte[] uv = frame.State.PhysicalPackets.Read(12, 4);
        NativeBattleHoly175WarpDraw draw = Advance(frame, 1, 0, pause);
        Assert.Equal(before, draw.Frame.State.Faces.Read(0, 36));
        Assert.Equal(1, draw.AliveFaces);
        Assert.Equal(0x26808080u, U(draw.Frame.State.PhysicalPackets.Read(4, 4)));
        Assert.Equal((byte)7, draw.Frame.State.PhysicalPackets.Read(3, 1)[0]);
        Assert.Equal(uv, draw.Frame.State.PhysicalPackets.Read(12, 4));
        Assert.True(draw.Frame.Ordering.Table[4121].Proxy);
    }

    [Fact]
    public void ActiveFaceUsesOldTranslationAndNewClampedScaleThenRetainsFlatTailUnknown()
    {
        NativeBattleHoly175WarpFrame frame = Fixture(delay: 0, scale: 400, offsetX: 7, offsetY: 3);
        NativeBattleHoly175WarpDraw draw = Advance(frame, 1, 0);
        byte[] face = draw.Frame.State.Faces.Read(0, 36);
        Assert.Equal((short)9, S(face, 0x14));
        Assert.Equal((short)6, S(face, 0x16));
        Assert.Equal((short)2, S(face, 0x1C));
        Assert.Equal((short)1, S(face, 0x1E));
        Assert.Equal((short)384, S(face, 0x1A));
        Assert.Equal((byte)4, draw.Frame.State.PhysicalPackets.Read(3, 1)[0]);
        Assert.Equal(0x20FFFFFFu, U(draw.Frame.State.PhysicalPackets.Read(4, 4)));
        NativeBattleActionMatrix expected = new([384, 0, 0, 0, 384, 0, 0, 0, 4096], 15, 7, 200, 4096);
        uint screen = NativeBattleModuleProjection.Project45FE10(expected, new(-168, -120, 0), Projection).Screen;
        Assert.Equal(screen, U(draw.Frame.State.PhysicalPackets.Read(8, 4)));
        Assert.Throws<NotSupportedException>(() => draw.Frame.State.PhysicalPackets.Read(24, 4));
        string json = JsonSerializer.Serialize(draw.Frame);
        NativeBattleHoly175WarpFrame restored = JsonSerializer.Deserialize<NativeBattleHoly175WarpFrame>(json)!;
        Assert.Equal(JsonSerializer.Serialize(Advance(draw.Frame, 2, 1)), JsonSerializer.Serialize(Advance(restored, 2, 1)));
    }

    [Fact]
    public void DeadOrCulledFacesDoNotReadUnconsumedFieldsAndOnlyZeroAliveClearsGlobal()
    {
        NativeBattleHoly175WarpFrame frame = Fixture(delay: 0, centerX: 169);
        NativeBattleHoly175WarpDraw killed = Advance(frame, 1, 0, 0x201);
        Assert.Equal(0, killed.AliveFaces);
        Assert.Equal(0u, killed.WarpAlive);
        Assert.Equal((short)-1, S(killed.Frame.State.Faces.Read(0x12, 2), 0));
        Assert.Empty(killed.Frame.Ordering.Proxies);
        Assert.Equal(4, killed.Frame.State.GridB.Read(279 * 16, 4).Length);
        NativeBattleHoly175WarpDraw again = Advance(killed.Frame, 2, 1);
        Assert.Equal(0, again.AliveFaces);
        Assert.Empty(again.Frame.Ordering.Proxies);
    }

    [Fact]
    public void UntexturedPrivateF3Allocates32ButWritesOnly20AndLeavesStaticPacketsUntouched()
    {
        NativeBattleHoly175WarpFrame frame = Fixture(delay: 0, scale: 384);
        frame = frame with { PrivateArena = frame.PrivateArena.MarkUnknown(frame.PrivateCursor, 64) };
        NativeBattleHoly175WarpDraw draw = Advance(frame, 1, 0, 0x201, 0);
        Assert.Equal(frame.PrivateCursor + 32, draw.Frame.PrivateCursor);
        Assert.Equal(0x20FFFFFFu, U(draw.Frame.PrivateArena.Read(frame.PrivateCursor + 4, 4)));
        Assert.Throws<NotSupportedException>(() => draw.Frame.PrivateArena.Read(frame.PrivateCursor + 20, 12));
        Assert.Throws<NotSupportedException>(() => draw.Frame.State.PhysicalPackets.Read(8, 4));
        Assert.Equal(new NativeBattlePrimitiveReference("20DFAB8", 0, frame.PrivateCursor), Assert.Single(draw.Frame.Ordering.Proxies).Packet);
        NativeBattleHoly175WarpDraw hidden = Advance(Fixture(delay: 0, scale: 400), 1, 0, 0x201, 0);
        Assert.Equal(1, hidden.AliveFaces);
        Assert.Equal(17u, hidden.WarpAlive);
        Assert.Empty(hidden.Frame.Ordering.Proxies);
        Assert.Equal(0x100, hidden.Frame.PrivateCursor);
    }

    [Fact]
    public void SignedFocalTranslationClampsRealSzHistoryWithoutRejectingPendingFaces()
    {
        NativeBattleHoly175WarpDraw draw = NativeBattleHoly175WarpDrawing.Advance5D2BC0(Fixture(delay: 10), 10, 0, 0x00808080, 0, -20, 1, 1, Projection);
        Assert.Equal(1, draw.AliveFaces);
        Assert.Equal(new[] { 0, 0, 0, 0 }, Assert.Single(draw.Frame.Ordering.Proxies).VertexPayload4.ToArray());
    }

    [Fact]
    public void ExhaustedProxyCapacityStillWritesPacketAndDoesNotReadUnknownTagLinkBytes()
    {
        NativeBattleHoly175WarpFrame frame = Fixture(delay: 10);
        NativeBattleLogicalProxy retainedProxy = new(new("retained-proxy", 0, 0), 0, [11, 22, 33, 44], null);
        NativeBattleLogicalOrdering exhausted = NativeBattleOrderingTestFixture.Working(frame.Ordering, data => data with { Proxies = Enumerable.Repeat(retainedProxy, NativeBattlePrimitiveOrdering.ProxyCapacity).ToImmutableArray(), ProxyCursor = NativeBattlePrimitiveOrdering.ProxyCapacity });
        frame = frame with { Ordering = exhausted, State = frame.State with { PhysicalPackets = frame.State.PhysicalPackets.MarkUnknown(0, 3) } };
        NativeBattleHoly175WarpDraw draw = Advance(frame, 10, 0);
        Assert.Equal(1, draw.AliveFaces);
        Assert.Equal(0x24808080u, U(draw.Frame.State.PhysicalPackets.Read(4, 4)));
        Assert.Throws<NotSupportedException>(() => draw.Frame.State.PhysicalPackets.Read(0, 3));
        Assert.Equal(exhausted.Proxies.ToArray(), draw.Frame.Ordering.Proxies.ToArray());
        Assert.Equal(NativeBattlePrimitiveOrdering.ProxyCapacity, draw.Frame.Ordering.ProxyCursor);
    }

    private static NativeBattleHoly175WarpDraw Advance(NativeBattleHoly175WarpFrame frame, int time, int buffer, uint flags = 0, uint textured = 1) => NativeBattleHoly175WarpDrawing.Advance5D2BC0(frame, time, buffer, 0x00808080, flags, 200, textured, 17, Projection);

    private static NativeBattleHoly175WarpFrame Fixture(short delay, short scale = 4096, short offsetX = 0, short offsetY = 0, short centerX = 0)
    {
        NativeBattleHoly175WarpState state = Grid.Value.State;
        NativeBattleEffectArenaWriter faces = new(NativeBattleEffectArena.Clear().MarkUnknown(0, NativeBattleHoly175WarpState.FacesLength));
        for (int face = 0; face < 1120; face++)
        {
            faces.Write(face * 36 + 0x12, new byte[] { 0xFF, 0xFF });
        }
        byte[] first = new byte[36];
        state.Faces.Read(0, 12).CopyTo(first, 0);
        W(first, 0xC, centerX);
        W(first, 0x12, delay);
        W(first, 0x14, offsetX);
        W(first, 0x16, offsetY);
        W(first, 0x1A, scale);
        W(first, 0x1C, 2);
        W(first, 0x1E, 3);
        W(first, 0x20, -3);
        W(first, 0x22, 32);
        faces.Write(0, first);
        return new(state with { Faces = faces.Freeze() }, NativeBattleEffectArena.Clear(), 0x100, NativeBattleLogicalOrdering.Clear());
    }

    private static short S(byte[] bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset));
    private static uint U(byte[] bytes) => BinaryPrimitives.ReadUInt32LittleEndian(bytes);
    private static void W(byte[] bytes, int offset, short value) => BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), value);
}
