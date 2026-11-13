using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleForeignModelDrawing
{
    public static NativeBattleModelContext44Step DrawContext44(NativeBattleEffectMemory memory, NativeBattleEffectArena context44, NativeBattleForeignModelGeometry geometry, NativeBattleActionMatrix basis, NativeBattleEffectArena projectedScratch, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, bool hardware, NativeBattlePilotVisualIdentity identity, int tableBaseBucket = 17, byte depthShift = 4)
    {
        context44.Validate();
        context44.ValidateExtent(44);
        projectedScratch.ValidateExtent(24);
        NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Host body context requires its current renderer process.");
        var gate = process.Enter5099D0(hardware);
        memory = memory with { PrimitiveProcess = gate.Process };
        if (!gate.Draw || BinaryPrimitives.ReadUInt32LittleEndian(context44.Read(0x20, 4)) == 0)
        {
            return new(memory, context44, projectedScratch, new([], 0));
        }
        uint color = BinaryPrimitives.ReadUInt32LittleEndian(context44.Read(0x1C, 4));
        (byte[] depthBytes, bool[] depthKnown) = context44.ReadSnapshot(0x24, 2);
        short? depth = depthKnown.All(known => known) ? BinaryPrimitives.ReadInt16LittleEndian(depthBytes) : null;
        NativeBattleGeometryViewport viewport = new(Word(0x14), Word(0x16), Word(0x18), Word(0x1A));
        NativeBattleGeometryRegisters registers = NativeBattleGeometry.LoadMatrix(memory.GeometryRegisters ?? throw new NotSupportedException("Host body context requires its current geometry registers."), basis, true, true, null);
        NativeBattleRawModelBodyDraw drawn = DrawBody(geometry, basis, projection, color, colors, identity, depthShift, viewport, tableBaseBucket, depth);
        if (drawn.ProjectedBytesWritten != 0)
        {
            projectedScratch = projectedScratch.Write(0, drawn.ProjectedScratch.AsSpan().Slice(0, drawn.ProjectedBytesWritten));
        }
        return new(memory with { PrimitiveProcess = gate.Process.PublishModelColor(color), GeometryRegisters = registers }, context44, projectedScratch, drawn.Packets);

        short Word(int offset) => BinaryPrimitives.ReadInt16LittleEndian(context44.Read(offset, 2));
    }
}
