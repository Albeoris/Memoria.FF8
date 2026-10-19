using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel212CaptureStep(NativeBattleDuel212Frame Frame, NativeBattleLogicalOrdering Ordering);

/// <summary>6A12B0's original128-by256 scene capture in the current private packet buffer.</summary>
public static class NativeBattleDuel212Capture
{
    public static NativeBattleThundaga105CaptureTransform Transform6A12B0(NativeBattleVector point, NativeBattleThundaga105Bounds bounds, NativeBattleCameraProjection projection, short focal)
    {
        int negativeX = -bounds.Minimum.X;
        int negativeZ = -bounds.Minimum.Z;
        int maximumX = bounds.Maximum.X;
        int maximumZ = bounds.Maximum.Z;
        int width = negativeX + maximumX;
        int height = negativeZ + maximumZ;
        int stepX = width / 128;
        int stepZ = height / 256;
        if (stepX % 128 != 0)
        {
            stepX++;
        }
        if (stepZ % 256 != 0)
        {
            stepZ++;
        }
        int divisorX = unchecked(width << 12) / 128;
        int divisorZ = unchecked(height << 12) / 256;
        if (stepX == 0 || stepZ == 0 || divisorX == 0 || divisorZ == 0)
        {
            throw new NotSupportedException("Native6A12B0 requires its original nonzero capture geometry divisors.");
        }
        NativeBattleActionMatrix yaw = NativeBattleGfMatrices.Euler56CE30(new(0, 2048, 0));
        NativeBattleActionMatrix pitch = NativeBattleGfMatrices.Euler56CE30(new(-1024, 0, 0));
        (int x, _, int z) = NativeBattleGfMatrices.RotateLong56C600(yaw, -point.X / stepX, 0, point.Z / stepZ);
        NativeBattleActionMatrix basis = NativeBattleGfMatrices.Multiply56C090(pitch, yaw);
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Scale56BEF0(basis, 0x1000000 / divisorX, 0, 0x1000000 / divisorZ) with { X = x + (negativeX - maximumX) / 2 / stepX, Y = z - (negativeZ - maximumZ) / 2 / stepZ, Z = focal };
        short priorX = projection.OffsetX ?? throw new NotSupportedException("Native6A12B0 requires its actual prior screen X offset.");
        short priorY = projection.OffsetY ?? throw new NotSupportedException("Native6A12B0 requires its actual prior screen Y offset.");
        return new(matrix, projection with { View = matrix, OffsetX = 64, OffsetY = 128 }, priorX, priorY);
    }

    public static NativeBattleDuel212CaptureStep Append6A12B0(NativeBattleDuel212Frame frame, NativeBattleLogicalOrdering ordering, NativeBattleThundaga105CaptureTransform transform, Func<NativeBattleDuel212Frame, NativeBattleThundaga105CaptureTransform, NativeBattleLogicalOrdering, NativeBattleDuel212CaptureStep> drawGround)
    {
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native6A12B0 requires its retained primitive process.");
        byte buffer = process.Buffer80 ?? throw new NotSupportedException("Native6A12B0 requires its actual environment selector.");
        if (buffer > 1)
        {
            throw new InvalidDataException("Native6A12B0 environment selector exceeds its two physical records.");
        }
        NativeSoftwareDrawingEnvironmentBuffers environments = process.Software?.EnvironmentBuffers ?? throw new NotSupportedException("Native6A12B0 requires its retained drawing environments.");
        ImmutableArray<byte> environment = buffer == 0 ? environments.Buffer1 : environments.Buffer0;
        Append(64, source => NativeBattleDrawEnvironmentPackets.Restore45C0F0(source, environment), environment[0x18] == 0 ? 24 : 36, 0);
        NativeBattleDuel212CaptureStep drawn = drawGround(frame, transform, ordering);
        frame = drawn.Frame;
        ordering = drawn.Ordering;
        Append(24, _ => Words(0x05000000, 0x28000000, 0, 128, 256u << 16, 256u << 16 | 128), 24, 1);
        Append(12, source => NativeBattleDrawEnvironmentPackets.Area45C940(source, 704, 256, 128, 256), 12, 0);
        Append(12, source => NativeBattleDrawEnvironmentPackets.Offset45C9B0(source.AsSpan().Slice(0, 8).ToArray().ToImmutableArray(), 704, 256), 8, 0);
        return new(frame, ordering);

        void Append(int allocation, Func<ImmutableArray<byte>, ImmutableArray<byte>> produce, int written, ushort flags)
        {
            NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native6A12B0 requires its current physical packet arena.");
            int cursor = frame.Effect.DrawArenaOffset ?? throw new NotSupportedException("Native6A12B0 requires its actual double-buffered cursor.");
            (byte[] source, _) = arena.ReadSnapshot(cursor, allocation);
            ImmutableArray<byte> produced = produce(source.ToImmutableArray());
            byte[] bytes = new byte[allocation];
            produced.AsSpan().CopyTo(bytes);
            NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[allocation];
            Array.Fill(mask, NativeBattleGfPacketByteState.Known, 3, written - 3);
            ImmutableArray<uint> words = Enumerable.Range(0, allocation / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray();
            NativeBattleGfGeometryAttempt attempt = new(0, bytes.ToImmutableArray(), mask.ToImmutableArray(), new(17, words, [0, 0, 0, 0]));
            NativeBattleEffectPacketWrite appended = NativeBattleEffectPacketArena.Append(frame.Memory, cursor, ordering, new NativeBattleGfGeometryBatch([attempt], allocation), flags);
            frame = frame with { Memory = appended.Memory, Effect = frame.Effect with { DrawArenaOffset = appended.Cursor }, Staging = frame.Staging.Write(0x2510258, BitConverter.GetBytes(checked(0x20DFAB8u + (uint)appended.Cursor))) };
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
