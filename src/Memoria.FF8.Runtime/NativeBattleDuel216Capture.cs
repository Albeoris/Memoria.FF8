using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel216CaptureStep(NativeBattleDuel216Frame Frame, NativeBattleLogicalOrdering Ordering);

public static class NativeBattleDuel216Capture
{
    public static NativeBattleDuel216CaptureStep Append69A790(NativeBattleDuel216Frame frame, NativeBattleLogicalOrdering ordering, NativeBattleDuel216CameraTransform transform, Func<NativeBattleDuel216Frame, NativeBattleDuel216Frame> resetIntensity5713E0, Func<NativeBattleDuel216Frame, NativeBattleDuel216CameraTransform, NativeBattleLogicalOrdering, NativeBattleDuel216CaptureStep> drawStageGroup1)
    {
        frame = resetIntensity5713E0(frame);
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native69A790 requires its retained primitive process.");
        byte buffer = process.Buffer80 ?? throw new NotSupportedException("Native69A790 requires its actual environment selector.");
        if (buffer > 1)
        {
            throw new InvalidDataException("Native69A790 environment selector exceeds its two records.");
        }
        NativeSoftwareDrawingEnvironmentBuffers environments = process.Software?.EnvironmentBuffers ?? throw new NotSupportedException("Native69A790 requires its retained drawing environments.");
        ImmutableArray<byte> environment = buffer == 0 ? environments.Buffer1 : environments.Buffer0;
        Append(64, source => NativeBattleDrawEnvironmentPackets.Restore45C0F0(source, environment), environment[24] == 0 ? 24 : 36, 0);
        NativeBattleDuel216CaptureStep stage = drawStageGroup1(frame, transform, ordering);
        frame = stage.Frame;
        ordering = stage.Ordering;
        Append(24, _ => Words(0x05000000, 0x28000000, 0, 128, 128u << 16, 128u << 16 | 128), 24, 1);
        Append(12, _ => Words(0x01000000, 0xE6000001), 8, 0);
        Append(12, source => NativeBattleDrawEnvironmentPackets.Area45C940(source, 640, 384, 128, 128), 12, 0);
        Append(12, source => NativeBattleDrawEnvironmentPackets.Offset45C9B0(source.AsSpan().Slice(0, 8).ToArray().ToImmutableArray(), 640, 384), 8, 0);
        return new(frame, ordering);

        void Append(int allocation, Func<ImmutableArray<byte>, ImmutableArray<byte>> produce, int written, ushort flags)
        {
            NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native69A790 requires its actual private packet arena.");
            int cursor = frame.Effect.DrawArenaOffset ?? throw new NotSupportedException("Native69A790 requires its actual private cursor.");
            (byte[] source, _) = arena.ReadSnapshot(cursor, allocation);
            ImmutableArray<byte> produced = produce(source.ToImmutableArray());
            byte[] bytes = new byte[allocation];
            produced.AsSpan().CopyTo(bytes);
            NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[allocation];
            Array.Fill(mask, NativeBattleGfPacketByteState.Known, 3, written - 3);
            ImmutableArray<uint> words = Enumerable.Range(0, allocation / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray();
            NativeBattleEffectPacketWrite appended = NativeBattleEffectPacketArena.Append(frame.Memory, cursor, ordering, new NativeBattleGfGeometryBatch([new(0, bytes.ToImmutableArray(), mask.ToImmutableArray(), new(17, words, [0, 0, 0, 0]))], allocation), flags);
            frame = frame with { Memory = appended.Memory, Effect = frame.Effect with { DrawArenaOffset = appended.Cursor }, Staging = frame.Staging.Write(0x2508874, BitConverter.GetBytes(checked(0x20DFAB8u + (uint)appended.Cursor))) };
            ordering = appended.Ordering;
        }
    }
    private static ImmutableArray<byte> Words(params uint[] words)
    {
        byte[] bytes = new byte[words.Length * 4];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), words[index]);
        }
        return bytes.ToImmutableArray();
    }
}
