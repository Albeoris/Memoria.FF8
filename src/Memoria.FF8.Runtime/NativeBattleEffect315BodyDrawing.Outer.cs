using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect315BodyFrame(NativeBattleEffectMemory Memory, NativeBattleFileStaging Staging, NativeBattleGraphicsModel Model, NativeBattleEffectArena Temporary156, uint CommonCursor);
public sealed record NativeBattleEffect315BodyServices(Func<NativeBattleEffect315BodyFrame, NativeBattleEffect315BodyFrame> Shadow5088A0, Func<NativeBattleEffect315BodyFrame, NativeBattlePackedMatrix32, NativeBattleEffect315BodyFrame> Compose5095B0, Func<NativeBattleEffect315BodyFrame, NativeBattleEffect315BodyFrame> DrawAndDeliver, Func<NativeBattleEffect315BodyFrame, NativeBattleEffect315BodyFrame> Restore508C90)
{
    public Func<NativeBattleEffect315BodyFrame, NativeBattlePackedMatrix32, NativeBattleEffect315BodyFrame>? DrawLinkedAndDeliver { get; init; }
}

public sealed record NativeBattleEffect315BodyDrawingStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary156, NativeBattleEffectArena Scratch, NativeBattleGfGeometryBatch Batch);

public static partial class NativeBattleEffect315BodyDrawing
{
    public static NativeBattleEffect315BodyFrame DrawOuter(NativeBattleEffect315BodyFrame frame, NativeBattlePackedMatrix32 camera, uint scratchAddress, uint primitiveFlags, NativeBattleEffect315BodyServices services)
    {
        if ((frame.Model.Flags0 & 0x20) == 0)
        {
            frame = services.Shadow5088A0(frame);
        }
        NativeBattleActionMatrix world = frame.Model.GeometryWorldMatrix ?? throw new NotSupportedException("Effect315 body requires its current model world matrix.");
        NativeBattlePackedMatrix32 basis = NativeBattlePackedMatrix32.FromMatrix(world, null);
        NativeBattlePackedMatrix32 composed = NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(camera.ReadMatrix(), world), null);
        NativeBattleEffectArena temporary = CopyMatrix(composed, frame.Temporary156, 0x7C);
        frame = services.Compose5095B0(frame with { Temporary156 = temporary }, basis);
        byte alpha = frame.Model.Alpha7 ?? throw new NotSupportedException("Effect315 body requires its current model alpha byte.");
        temporary = Dword(Dword(Dword(frame.Temporary156, 4, scratchAddress), 0x10, primitiveFlags), 0x1C, frame.Model.Color40 ?? throw new NotSupportedException("Effect315 body requires its current model color."));
        temporary = temporary.Write(0x14, new byte[4]);
        temporary = Word(Word(temporary, 0x18, 320), 0x1A, 216);
        temporary = Dword(temporary, 0x20, (frame.Model.SequenceState ?? throw new NotSupportedException("Effect315 body requires its current object mask.")).MeshMask124).Write(0x28, [alpha, alpha, alpha]);
        frame = services.DrawAndDeliver(frame with { Temporary156 = temporary });
        frame = services.Restore508C90(frame);
        if (frame.Model.LinkedTransform is not null && (frame.Model.Flags0 & 0x200) == 0)
        {
            Func<NativeBattleEffect315BodyFrame, NativeBattlePackedMatrix32, NativeBattleEffect315BodyFrame> linked = services.DrawLinkedAndDeliver ?? throw new NotSupportedException("Effect315 body requires its current linked drawing service.");
            frame = linked(frame with { Temporary156 = Dword(frame.Temporary156, 0x20, uint.MaxValue) }, basis);
        }
        return frame;
    }

    private static NativeBattleEffectArena CopyMatrix(NativeBattlePackedMatrix32 matrix, NativeBattleEffectArena destination, int offset)
    {
        for (int index = 0; index < 32; index++)
        {
            if (index is 18 or 19)
            {
                continue;
            }
            destination = matrix.Known[index] ? destination.Write(offset + index, [matrix.Bytes[index]]) : destination.MarkUnknown(offset + index, 1);
        }
        return destination;
    }

    private static NativeBattleEffectArena Dword(NativeBattleEffectArena value, int offset, uint data) => value.Write(offset, BitConverter.GetBytes(data));
    private static NativeBattleEffectArena Word(NativeBattleEffectArena value, int offset, ushort data) => value.Write(offset, BitConverter.GetBytes(data));
    private static ushort U16(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(value.Read(offset, 2));
    private static short S16(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadInt16LittleEndian(value.Read(offset, 2));
    private static uint U32(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(value.Read(offset, 4));
    private static int S32(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadInt32LittleEndian(value.Read(offset, 4));
}
