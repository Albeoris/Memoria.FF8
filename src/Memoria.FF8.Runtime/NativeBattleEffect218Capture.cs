using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect218CaptureServices(NativeBattleCameraProjection Projection, short NormalDepth, ImmutableArray<byte> OppositeEnvironment92, Func<NativeBattleEffect218Frame, short, short, NativeBattleEffect218Frame> SetOffsets, Func<NativeBattleEffect218Frame, NativeBattlePackedMatrix32, NativeBattleCameraProjection, NativeBattleEffect218Frame> DrawStageGroup1, Func<NativeBattleEffect218Frame, uint, NativeBattleGfGeometryBatch, ushort, NativeBattleEffect218Frame> DeliverStatic);

public static class NativeBattleEffect218Capture
{
    public static NativeBattlePackedMatrix32 Matrix634E40(NativeBattleVector point, short focal)
    {
        NativeBattleActionMatrix first = NativeBattleGfMatrices.Euler56CD50(new(1024, 0, 0)) with { X = 4 - point.X / 17, Y = point.Z / 17 - 3, Z = focal };
        int scaleX = 0x1000000 / (0x820000 / 128);
        int scaleZ = 0x1000000 / (0x10A4000 / 256);
        first = NativeBattlePoseMatrices.Scale56BEF0(first, scaleX, 0, scaleZ);
        NativeBattleActionMatrix turn = NativeBattleGfMatrices.Euler56CD50(new(0, 0, -1024));
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Multiply56C090(turn, first);
        (int x, int y, int z) = NativeBattleGfMatrices.RotateLong56C600(turn, first.X, first.Y, first.Z);
        return NativeBattlePackedMatrix32.FromMatrix(matrix with { X = x, Y = y, Z = z }, null);
    }

    public static NativeBattleEffect218Frame Capture634E40(NativeBattleEffect218Frame frame, NativeBattleVector point, NativeBattleEffect218CaptureServices services)
    {
        short priorX = services.Projection.OffsetX ?? throw new NotSupportedException("Effect218 capture requires its current screen offset X.");
        short priorY = services.Projection.OffsetY ?? throw new NotSupportedException("Effect218 capture requires its current screen offset Y.");
        NativeBattlePackedMatrix32 packed = Matrix634E40(point, services.NormalDepth);
        NativeBattleEffectArena retained = frame.State.ReadView(0x24F24D8, 64);
        (byte[] bytes, _) = retained.ReadSnapshot(0, 64);
        ImmutableArray<byte> environment = NativeBattleDrawEnvironmentPackets.Restore45C0F0(bytes.ToImmutableArray(), services.OppositeEnvironment92);
        int written = services.OppositeEnvironment92[0x18] == 0 ? 24 : 36;
        frame = services.DeliverStatic(frame, 0x24F24D8, Packet(environment, 64, 3, written), 0);
        frame = services.SetOffsets(frame, 64, 128);
        frame = NativeBattleEffect218Controller.LoadMatrix(frame, packed);
        frame = services.DrawStageGroup1(frame, packed, services.Projection with { OffsetX = 64, OffsetY = 128 });
        frame = services.SetOffsets(frame, priorX, priorY);
        frame = services.DeliverStatic(frame, NativeBattleEffect218State.CaptureQuadAddress, Packet(frame.State.ReadView(NativeBattleEffect218State.CaptureQuadAddress, 24).Read(0, 24).ToImmutableArray(), 24, 0, 0), 1);
        frame = services.DeliverStatic(frame, 0x24F3538, Mask634E40(frame.State.ReadView(0x24F3538, 8)), 0);
        frame = services.DeliverStatic(frame, 0x24F23C8, Area634E40(frame.State.ReadView(0x24F23C8, 12)), 0);
        frame = frame with { State = frame.State.WriteWord(0x24F34F8, 576).WriteWord(0x24F34FA, 256) };
        return services.DeliverStatic(frame, 0x24F24C8, Offset634E40(frame.State.ReadView(0x24F24C8, 8)), 0);
    }

    public static NativeBattleGfGeometryBatch Area634E40(NativeBattleEffectArena retained)
    {
        (byte[] bytes, _) = retained.ReadSnapshot(0, 12);
        return Packet(NativeBattleDrawEnvironmentPackets.Area45C940(bytes.ToImmutableArray(), 576, 256, 128, 256), 12, 3, 12);
    }

    public static NativeBattleGfGeometryBatch Offset634E40(NativeBattleEffectArena retained)
    {
        (byte[] bytes, _) = retained.ReadSnapshot(0, 8);
        return Packet(NativeBattleDrawEnvironmentPackets.Offset45C9B0(bytes.ToImmutableArray(), 576, 256), 8, 3, 8);
    }

    public static NativeBattleGfGeometryBatch Mask634E40(NativeBattleEffectArena retained)
    {
        (byte[] bytes, _) = retained.ReadSnapshot(0, 8);
        bytes[3] = 1;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0xE6000000);
        return Packet(bytes.ToImmutableArray(), 8, 3, 8);
    }

    private static NativeBattleGfGeometryBatch Packet(ImmutableArray<byte> bytes, int allocation, int firstWritten, int endWritten)
    {
        int length = checked(4 + bytes[3] * 4);
        if (bytes.Length < allocation || length > allocation)
        {
            throw new InvalidDataException("Effect218 capture packet leaves its real static allocation.");
        }
        NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[allocation];
        if (endWritten > firstWritten)
        {
            Array.Fill(mask, NativeBattleGfPacketByteState.Known, firstWritten, endWritten - firstWritten);
        }
        ImmutableArray<uint> words = Enumerable.Range(0, length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(index * 4, 4))).ToImmutableArray();
        NativeBattleGeometryPrimitive primitive = new(17, words, [0, 0, 0, 0]);
        return new([new(0, bytes, mask.ToImmutableArray(), primitive)], allocation);
    }
}
