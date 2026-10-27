using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleFatedCircle162CaptureStep(NativeBattleFatedCircle162Frame Frame, NativeBattleLogicalOrdering Ordering);

public static class NativeBattleFatedCircle162Capture
{
    public const string PacketBank = "fated-circle-capture";

    public static NativeBattleThundaga105CaptureTransform Transform5FC5E0(NativeBattleModuleAnchor point, NativeBattleCameraProjection projection, short globalFocal)
    {
        short priorX = projection.OffsetX ?? throw new NotSupportedException("Native5FC5E0 requires its actual prior screen X offset.");
        short priorY = projection.OffsetY ?? throw new NotSupportedException("Native5FC5E0 requires its actual prior screen Y offset.");
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler56CD50(new(1024, 0, 0));
        matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, 0x1000000 / (0x130E000 / 128), 0, 0x1000000 / (0x1143000 / 256)) with { X = -6 - point.X / 39, Y = point.Z / 18 + 21, Z = globalFocal };
        return new(matrix, projection with { View = matrix, OffsetX = 64, OffsetY = 128 }, priorX, priorY);
    }

    public static NativeBattleFatedCircle162CaptureStep Append5FC5E0(NativeBattleFatedCircle162Frame frame, NativeBattleLogicalOrdering ordering, NativeBattleThundaga105CaptureTransform transform, Func<NativeBattleFatedCircle162Frame, NativeBattleThundaga105CaptureTransform, NativeBattleLogicalOrdering, NativeBattleFatedCircle162CaptureStep> stageDraw)
    {
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native5FC5E0 requires its retained primitive process.");
        byte buffer = process.Buffer80 ?? throw new NotSupportedException("Native5FC5E0 requires the actual draw environment selector.");
        if (buffer > 1)
        {
            throw new InvalidDataException("Native5FC5E0 requires one of two actual drawing environments.");
        }
        NativeSoftwareDrawingEnvironmentBuffers environments = process.Software?.EnvironmentBuffers ?? throw new NotSupportedException("Native5FC5E0 requires its retained drawing environment records.");
        ImmutableArray<byte> environment = buffer == 0 ? environments.Buffer1 : environments.Buffer0;
        ImmutableArray<byte> restored = NativeBattleDrawEnvironmentPackets.Restore45C0F0(frame.Staging.Read(0x23CF3C8, 64).ToImmutableArray(), environment);
        frame = frame with { Staging = frame.Staging.Write(0x23CF3C8, restored.AsSpan()) };
        Link(0x23CF3C8, 0);
        NativeBattleFatedCircle162CaptureStep drawn = stageDraw(frame, transform, ordering);
        frame = drawn.Frame;
        ordering = drawn.Ordering;
        Link(0xDA8D00, 1);
        ImmutableArray<byte> area = NativeBattleDrawEnvironmentPackets.Area45C940(frame.Staging.Read(0x23CE610, 12).ToImmutableArray(), 512, 256, 128, 256);
        frame = frame with { Staging = frame.Staging.Write(0x23CE610, area.AsSpan()) };
        Link(0x23CE610, 0);
        byte[] rectangle = new byte[4];
        BinaryPrimitives.WriteInt16LittleEndian(rectangle, 512);
        BinaryPrimitives.WriteInt16LittleEndian(rectangle.AsSpan(2), 256);
        ImmutableArray<byte> offsets = NativeBattleDrawEnvironmentPackets.Offset45C9B0(frame.Staging.Read(0x23CEA10, 8).ToImmutableArray(), 512, 256);
        frame = frame with { Staging = frame.Staging.Write(0x23D0D78, rectangle).Write(0x23CEA10, offsets.AsSpan()) };
        Link(0x23CEA10, 0);
        return new(frame, ordering);

        void Link(uint address, ushort flags)
        {
            byte[] header = frame.Staging.Read(address, 4);
            int length = checked(4 + header[3] * 4);
            byte[] bytes = frame.Staging.Read(address, length);
            ImmutableArray<uint> words = Enumerable.Range(0, length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray();
            using NativeBattleLogicalOrdering.Builder links = new(ordering);
            NativeBattlePrimitiveReference reference = new(PacketBank, 0, checked((int)address));
            links.InvalidatePacketRange(reference, length);
            bool linked = links.ProxyCount < NativeBattlePrimitiveOrdering.ProxyCapacity;
            links.Prepend(17, reference, words, flags, [0, 0, 0, 0]);
            if (linked)
            {
                frame = frame with { Staging = frame.Staging.Write(address, NativeBattleLogicalOrdering.LinkBytes(links.LastHeaderLink)) };
            }
            ordering = links.ToImmutable();
        }
    }
}
