using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel216CaptureTests
{
    [Fact]
    public void CapturePublishesNativeMaskPacketAndRetainsAllThreeTwelveByteAllocationTails()
    {
        byte[] environment = new byte[92];
        BinaryPrimitives.WriteInt16LittleEndian(environment.AsSpan(4), 320);
        BinaryPrimitives.WriteInt16LittleEndian(environment.AsSpan(6), 216);
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400();
        process = process with { Buffer80 = 0, Software = process.Software! with { EnvironmentBuffers = new(environment.ToImmutableArray(), environment.ToImmutableArray(), null) } };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { PrimitiveProcess = process, Arena = NativeBattleEffectArena.Clear().MarkUnknown(12000, 124) };
        NativeBattleOriginalEffectState effect = new(216, 215, new(216, 0x6959E0, 0x6959F0), new(0, 0, 8, 0, 0, 0), NativeBattleDuel216Controller.Root) { DrawArenaOffset = 12000 };
        NativeBattleDuel216Frame frame = new(memory, effect, NativeBattleFileStaging.Unknown(), NativeCrtRandomState.FromColdProcess());
        NativeBattleDuel216CameraTransform transform = NativeBattleDuel216Camera.Transform69A790(new(0, 0, 0, 0), new(new(-2560, 0, -5120), new(2560, 0, 5120)), new(null, 288, 160, 108), 288);
        bool reset = false;
        NativeBattleDuel216CaptureStep result = NativeBattleDuel216Capture.Append69A790(frame, NativeBattleLogicalOrdering.Clear(), transform, current =>
        {
            reset = true;
            Assert.Equal(12000, current.Effect.DrawArenaOffset);
            return current;
        }, (current, _, ordering) =>
        {
            Assert.True(reset);
            Assert.Equal(12064, current.Effect.DrawArenaOffset);
            Assert.Single(ordering.Packets);
            return new(current, ordering);
        });
        Assert.Equal(12124, result.Frame.Effect.DrawArenaOffset);
        Assert.Equal(new[] { 12000, 12064, 12088, 12100, 12112 }, result.Ordering.Packets.Select(packet => packet.Reference.ByteOffset));
        Assert.Equal((ushort)1, result.Ordering.Proxies[1].Flags20);
        Assert.Equal(0x28000000u, result.Ordering.Packets[1].Words[1]);
        Assert.Equal(0x00800080u, result.Ordering.Packets[1].Words[5]);
        Assert.Equal(0xE6000001u, result.Ordering.Packets[2].Words[1]);
        Assert.Throws<NotSupportedException>(() => result.Frame.Memory.Arena!.Read(12000 + 24, 1));
        Assert.Throws<NotSupportedException>(() => result.Frame.Memory.Arena!.Read(12088 + 8, 4));
        Assert.Throws<NotSupportedException>(() => result.Frame.Memory.Arena!.Read(12112 + 8, 4));
        Assert.Equal(0x20DFAB8u + 12124, BinaryPrimitives.ReadUInt32LittleEndian(result.Frame.Staging.Read(0x2508874, 4)));
    }
}
