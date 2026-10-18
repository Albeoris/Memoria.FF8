using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGilgameshSwordTrailPacketResult(NativeBattleGfGeometryBatch Batch, int PoolCursorAdvance);

/// <summary>5943D0's thirty G4 pairs and native extra36-byte final pool publication.</summary>
public static class NativeBattleGilgameshSwordTrailPackets
{
    public static NativeBattleGilgameshSwordTrailPacketResult Emit(NativeBattleGilgameshSwordTrailState state, NativeBattleActionMatrix worldView, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory? registers, NativeBattleSummonColors colors, uint baseColor)
    {
        state.Validate();
        if ((baseColor >> 24) != 0x3A)
        {
            throw new NotSupportedException("Native sword trail requires its actual G4 code channel.");
        }
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocation = 0;
        uint previousEnd = baseColor;
        for (int segment = 0; segment < 30; segment++)
        {
            NativeBattleModuleProjectedVertex[] points = new NativeBattleModuleProjectedVertex[4];
            points[0] = NativeBattleModuleProjection.Project45FE10(worldView, ReadPoint(NativeBattleGilgameshSwordTrailState.CurveA, segment), projection);
            points[1] = NativeBattleModuleProjection.Project45FE10(worldView, ReadPoint(NativeBattleGilgameshSwordTrailState.CurveA, segment + 1), projection);
            points[2] = NativeBattleModuleProjection.Project45FE10(worldView, ReadPoint(NativeBattleGilgameshSwordTrailState.CurveB, segment), projection);
            if (((points[0].Flags | points[1].Flags | points[2].Flags) & 0x60000) != 0)
            {
                byte[] bytes = new byte[36];
                BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x08000000);
                ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Range(0, 36).Select(index => index < 4 ? NativeBattleGfPacketByteState.Known : NativeBattleGfPacketByteState.Untouched).ToImmutableArray();
                attempts.Add(new(allocation, bytes.ToImmutableArray(), mask, null));
                continue;
            }
            NativeBattleScriptedGraphicsMemory current = registers ?? throw new NotSupportedException("Native sword trail requires its actual current DQA/DQB and ZSF4 registers.");
            points[3] = NativeBattleModuleProjection.Project45FAA0(worldView, ReadPoint(NativeBattleGilgameshSwordTrailState.CurveB, segment + 1), projection, current.Dqa, current.Dqb);
            short zsf = current.Zsf4 ?? throw new NotSupportedException("Native sword trail requires its actual current ZSF4 register.");
            uint depth = unchecked((uint)points.Sum(point => (int)point.Depth) * (uint)(int)zsf) >> 12;
            int bucket = unchecked(17 + (unchecked((int)depth) >> 2));
            uint start = previousEnd;
            uint end = colors.Interpolate(baseColor, segment * 4096 / 30, 0);
            previousEnd = end;
            attempts.Add(Packet(allocation, bucket, points, [start, end, start, end]));
            attempts.Add(Packet(allocation + 36, bucket, points, [0x3A000000, 0x3A000000, start, end]));
            allocation = checked(allocation + 72);
        }
        return new(new(attempts.ToImmutable(), allocation), checked(allocation + 36));

        NativeBattleVector ReadPoint(int bank, int index)
        {
            byte[] bytes = state.Memory.Read(bank + index * 8, 6);
            return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4)));
        }
    }

    private static NativeBattleGfGeometryAttempt Packet(int offset, int bucket, NativeBattleModuleProjectedVertex[] points, uint[] colors)
    {
        uint[] words = [0x08000000, colors[0], points[0].Screen, colors[1], points[1].Screen, colors[2], points[2].Screen, colors[3], points[3].Screen];
        byte[] bytes = new byte[36];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), words[index]);
        }
        ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 36).ToImmutableArray();
        NativeBattleGeometryPrimitive primitive = new(bucket, words.ToImmutableArray(), points.Select(point => (int)point.Depth).ToImmutableArray()) { PhysicalWriteMask = mask };
        return new(offset, bytes.ToImmutableArray(), mask, primitive);
    }
}
