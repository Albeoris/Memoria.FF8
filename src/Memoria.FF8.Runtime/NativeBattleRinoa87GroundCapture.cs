using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>7789B0/778AB0's original64-square COMMON ground capture and temporary projection.</summary>
public static class NativeBattleRinoa87GroundCapture
{
    public static NativeBattleThundaga105CaptureTransform Transform778AB0(NativeBattleVector point, NativeBattleThundaga105Bounds bounds, NativeBattleCameraProjection projection, short focal)
    {
        int negativeX = -bounds.Minimum.X;
        int negativeZ = -bounds.Minimum.Z;
        int maximumX = bounds.Maximum.X;
        int maximumZ = bounds.Maximum.Z;
        int width = negativeX + maximumX;
        int height = negativeZ + maximumZ;
        int stepX = width / 64;
        int stepZ = height / 64;
        if (stepX % 64 != 0)
        {
            stepX++;
        }
        if (stepZ % 64 != 0)
        {
            stepZ++;
        }
        int divisorX = unchecked(width << 12) / 64;
        int divisorZ = unchecked(height << 12) / 64;
        if (stepX == 0 || stepZ == 0 || divisorX == 0 || divisorZ == 0)
        {
            throw new NotSupportedException("Native778AB0 requires its original nonzero capture geometry divisors.");
        }
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.RotateX8DD7E0(NativeBattleCuragaMatrices.Identity8DD770(), 1024);
        matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, 0x1000000 / divisorX, 0, 0x1000000 / divisorZ) with { X = (negativeX - maximumX) / 2 / stepX - point.X / stepX, Y = point.Z / stepZ - (negativeZ - maximumZ) / 2 / stepZ, Z = focal };
        short priorX = projection.OffsetX ?? throw new NotSupportedException("Native778AB0 requires its actual prior screen X offset.");
        short priorY = projection.OffsetY ?? throw new NotSupportedException("Native778AB0 requires its actual prior screen Y offset.");
        return new(matrix, projection with { View = matrix, OffsetX = 32, OffsetY = 32 }, priorX, priorY);
    }

    public static NativeBattleGroundCaptureWrite Append778AB0(NativeBattleEffectMemory memory, NativeBattleLogicalOrdering ordering, Func<NativeBattleGroundCaptureWrite, NativeBattleGroundCaptureWrite> drawGround)
    {
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native778AB0 requires its actual primitive process.");
        byte buffer = process.Buffer80 ?? throw new NotSupportedException("Native778AB0 requires its actual drawing-environment selector.");
        if (buffer > 1)
        {
            throw new InvalidDataException("Native778AB0 drawing-environment selector exceeds its two physical records.");
        }
        NativeSoftwareDrawingEnvironmentBuffers environments = process.Software?.EnvironmentBuffers ?? throw new NotSupportedException("Native778AB0 requires its retained DRAWENV records.");
        ImmutableArray<byte> source = buffer == 0 ? environments.Buffer1 : environments.Buffer0;
        if (source.IsDefault || source.Length != 92)
        {
            throw new InvalidDataException("Native778AB0 requires its complete alternate92-byte DRAWENV.");
        }
        NativeBattleGroundCaptureWrite current = new(memory, ordering);
        current = Append(current, NativeBattleDrawEnvironmentPackets.Restore45C0F0(new byte[64].ToImmutableArray(), source), 64, source[0x18] == 0 ? 24 : 36);
        current = drawGround(current);
        current = Append(current, Black(true), 24, 24, 1);
        current = Append(current, Words(0x02000000, 0xE1000020, 0), 12, 12);
        current = Append(current, Black(false), 24, 24, 1);
        current = Append(current, NativeBattleDrawEnvironmentPackets.Area45C940(new byte[12].ToImmutableArray(), 768, 384, 64, 64), 12, 12);
        return Append(current, NativeBattleDrawEnvironmentPackets.Offset45C9B0(new byte[8].ToImmutableArray(), 768, 384), 12, 8);
    }

    private static NativeBattleGroundCaptureWrite Append(NativeBattleGroundCaptureWrite current, ImmutableArray<byte> data, int allocation, int written, ushort flags = 0)
    {
        byte[] bytes = new byte[allocation];
        data.AsSpan().CopyTo(bytes);
        NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[allocation];
        Array.Fill(mask, NativeBattleGfPacketByteState.Known, 3, written - 3);
        NativeBattleGeometryPrimitive primitive = new(17, ReadWords(bytes), [0, 0, 0, 0]);
        NativeBattleGfGeometryAttempt attempt = new(0, bytes.ToImmutableArray(), mask.ToImmutableArray(), primitive);
        NativeBattleCommonPacketWrite appended = NativeBattleCommonPacketArena.Append(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native778AB0 lost its COMMON owner."), current.Ordering, new([attempt], allocation), flags);
        return new(current.Memory with { PrimitiveProcess = appended.Process }, appended.Ordering);
    }

    private static ImmutableArray<byte> Black(bool semi) => Words(0x05000000, semi ? 0x2A000000u : 0x28000000u, 0, 63, 63u << 16, 63u << 16 | 63);

    private static ImmutableArray<byte> Words(params uint[] words)
    {
        byte[] bytes = new byte[words.Length * 4];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), words[index]);
        }
        return bytes.ToImmutableArray();
    }

    private static ImmutableArray<uint> ReadWords(byte[] bytes)
    {
        ImmutableArray<uint>.Builder words = ImmutableArray.CreateBuilder<uint>(bytes.Length / 4);
        for (int offset = 0; offset < bytes.Length; offset += 4)
        {
            words.Add(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset)));
        }
        return words.MoveToImmutable();
    }
}
