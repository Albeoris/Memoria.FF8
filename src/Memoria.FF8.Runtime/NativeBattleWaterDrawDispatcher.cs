using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWaterDrawInputs(NativeBattleWater222Resources Resources, short Zsf3, short Dqa, int Dqb, uint OrderingTablePointer);
public sealed record NativeBattleWaterDrawServices(
    Func<NativeBattleWaterExecutionFrame, NativeBattleCameraProjection> ReadProjection,
    Func<NativeBattleWaterExecutionFrame, short, NativeBattleWaterExecutionFrame> PublishFocal,
    Func<NativeBattleWaterExecutionFrame, uint, int> ResolveShapeExtent,
    Func<NativeBattleWaterExecutionFrame, NativeBattleEffectArena> ReadDepthOwner,
    Func<NativeBattleWaterExecutionFrame, NativeBattleEffectArena, NativeBattleWaterExecutionFrame> PublishDepthOwner,
    Func<NativeBattleWaterExecutionFrame, uint, NativeBattleGfGeometryBatch, NativeBattleWaterExecutionFrame> SubmitPackets,
    Func<NativeBattleWaterExecutionFrame, ImmutableArray<NativeBattleEffectHeapAllocation>> ReadCurrentFiles,
    Func<NativeBattleWaterExecutionFrame, NativeBattleWaterRectangleWrite, NativeBattleWaterExecutionFrame> EnqueueScroll,
    Func<NativeBattleWaterExecutionFrame, ImmutableArray<NativeBattleGraphicsModel>> ReadModels,
    Func<NativeBattleWaterExecutionFrame, ImmutableArray<NativeBattleGraphicsModel>, NativeBattleWaterExecutionFrame> PublishModels,
    NativeBattleWaterActorPoseServices ActorPose,
    Func<NativeBattleWaterExecutionFrame, uint, int, byte[]> ReadMetadata,
    Func<uint, int> ResolveActualOtBucket);

/// <summary>The selected draw callers over current resource, graphics and packet owners.</summary>
public static class NativeBattleWaterDrawDispatcher
{
    public static NativeBattleWaterExecutionFrame Draw(NativeBattleWaterExecutionFrame frame, byte kind, NativeBattleWaterSlotBank bank, int currentIndex, NativeBattleWaterDrawInputs input, NativeBattleWaterDrawServices services, int particleInstructionBudget = 4096)
    {
        switch (kind)
        {
            case 0x02:
                NativeBattleCameraProjection projection = services.ReadProjection(frame);
                ushort focal = projection.FocalLength ?? throw new NotSupportedException("Native selected mesh drawing requires its actual current focal register.");
                NativeBattleWaterMeshPrepared prepared = NativeBattleWaterMeshPreparation.PrepareWrapper(frame, bank, currentIndex, 2, unchecked((short)focal));
                frame = services.PublishFocal(prepared.Frame, prepared.Focal);
                prepared = prepared with { Frame = frame };
                uint shape = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xD8, 4));
                int extent = services.ResolveShapeExtent(frame, shape);
                NativeBattleEffectHeapAllocation map = new("shape-resource-map", input.Resources.Profile.Tables.Data.AsSpan().Slice(0x60, 0x10).ToArray().ToImmutableArray());
                NativeBattleWaterMeshProjected projected = NativeBattleWaterMeshPreparation.MorphProject(prepared, bank, currentIndex, input.Resources, map, extent, projection, input.Dqa, input.Dqb, services.ReadDepthOwner(frame));
                frame = services.PublishDepthOwner(projected.Frame, projected.DepthOwner);
                projected = projected with { Frame = frame };
                NativeBattleWaterMeshSubmission mesh = NativeBattleWaterMeshPreparation.Submit(projected, extent, services.ResolveActualOtBucket);
                return Submit(mesh.Frame, mesh.Packets);
            case 0x05:
                return NativeBattleWaterRectangleCommands.AdvanceDraw5(frame, ResolveMatrix, DrawRectangles);
            case 0x06:
                uint programAddress = CoreDword(frame, 0x2798A68);
                int programLength = input.Resources.Files[0].Data.Length;
                NativeBattleWaterScriptProgram program = new(ArenaOffset(programAddress, programLength), programLength, programAddress);
                NativeBattleWaterParticleServices particles = new(services.ReadMetadata, ResolveMatrix, (current, index) => NativeBattleWaterTextureMetadata.SelectTPageB665C0(current, input.Resources.Profile, index), (current, index) => NativeBattleWaterTextureMetadata.ReadClutB66640(current, input.Resources.Profile, index), DrawRectangles);
                return NativeBattleWaterParticles.AdvanceType6(frame, program, bank, particles, particleInstructionBudget);
            case 0x15:
                uint payloadAddress = BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, 0xB8, 4));
                byte[] payloadBytes = frame.Arena.Read(ArenaOffset(payloadAddress, 6), 6);
                NativeBattleWaterScrollPayload payload = new(BinaryPrimitives.ReadInt16LittleEndian(payloadBytes), BinaryPrimitives.ReadInt16LittleEndian(payloadBytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(payloadBytes.AsSpan(4)));
                uint header = CoreDword(frame, 0x279744C);
                byte counter = frame.Core.Read(header + 0x43, 1)[0];
                short scroll = unchecked((short)SlotWord(frame, 0x96));
                NativeBattleWaterScroll.DrawA9C140(input.Resources.Profile, services.ReadCurrentFiles(frame), payload, scroll, counter, write =>
                {
                    frame = frame with { Core = frame.Core.Write(CoreDword(frame, 0x279744C) + 0x43, new byte[] { write.NextCounter }) };
                    frame = frame with { Core = frame.Core.Write(0x27974D4 + (uint)(write.RingIndex * 8), write.RectangleBytes.AsSpan()) };
                    frame = services.EnqueueScroll(frame, write);
                });
                return frame;
            case 0x26:
                NativeBattleWaterActorDrawResult actor = NativeBattleWaterActorDraw.Draw26A9C340(frame, services.ReadModels(frame), services.ActorPose, services.ReadProjection(frame), input.Dqa, input.Dqb, input.OrderingTablePointer, services.ResolveActualOtBucket);
                frame = services.PublishModels(actor.Frame, actor.Models);
                return Submit(frame, actor.Packets);
            default:
                throw new NotSupportedException("Native selected effect draw dispatch requires kind02,05,06,15 or26.");
        }

        uint ResolveMatrix(NativeBattleWaterExecutionFrame current, ushort selector) => NativeBattleWaterMatrixCommands.ResolveB65520(current, bank, currentIndex, selector);

        NativeBattleWaterExecutionFrame DrawRectangles(NativeBattleWaterExecutionFrame current, NativeBattleActionMatrix matrix)
        {
            NativeBattleWaterParticleRectangleResult rectangles = NativeBattleWaterParticleRectanglePackets.DrawA9EE90(current, matrix, services.ReadProjection(current), input.Zsf3, input.Dqa, input.Dqb, services.ResolveActualOtBucket);
            return Submit(rectangles.Frame, rectangles.Packets);
        }

        NativeBattleWaterExecutionFrame Submit(NativeBattleWaterExecutionFrame current, NativeBattleGfGeometryBatch packets)
        {
            if (packets.Attempts.IsEmpty)
            {
                return current;
            }
            uint cursor = CoreDword(current, CoreDword(current, 0x27973EC) + 0x7C);
            return services.SubmitPackets(current, unchecked(cursor - (uint)packets.AllocationBytes), packets);
        }
    }
}
