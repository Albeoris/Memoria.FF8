namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCuragaNodeDrawStep(NativeBattleCuragaFrame Frame, NativeBattleLogicalOrdering Ordering, NativeBattleGfGeometryBatch Batch);
public sealed record NativeBattleCuragaNodePresentationStep(NativeBattleCuragaFrame Frame, NativeCrtRandomState Random, NativeBattleLogicalOrdering Ordering, NativeBattleGfGeometryBatch Batch);

public static partial class NativeBattleCuragaNodeKinematics
{
    public static NativeBattleCuragaNodePresentationStep AdvanceAndDraw87EC80(NativeBattleCuragaFrame frame, uint contextTask, NativeCrtRandomState random, Func<int, NativeBattleGraphicsModel> readModel, NativeBattleCameraProjection projection, short cameraRoll, NativeBattleLogicalOrdering ordering, NativeBattleSummonColors? colors = null)
    {
        using NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        NativeBattleEffectNodeBank bank = CurrentBank(memory);
        NativeBattleEffectNodeLifecycle core = CreateLifecycle(memory, bank, readModel);
        if (memory.Profile.StreamsPacketProducers)
        {
            NativeBattleGfGeometryBatch streamed = core.AdvanceAndDraw(ref random, projection, cameraRoll, PrepareMeshes(memory, projection, colors), (meshes, packetBatch) => ordering = StreamBatch(memory, frame, ordering, packetBatch, meshes));
            return new(FlushCurrent(memory, bank), random, ordering, streamed);
        }
        NativeBattleGfGeometryBatch batch = core.AdvanceAndDraw(ref random, projection, cameraRoll, PrepareMeshes(memory, projection, colors));
        NativeBattleCuragaNodeDrawStep drawn = CommitDraws(memory, bank, ordering, batch);
        return new(drawn.Frame, random, drawn.Ordering, batch);
    }

    public static NativeBattleCuragaNodeDrawStep DrawNodes880390And87ED10(NativeBattleCuragaFrame frame, uint contextTask, NativeBattleCameraProjection projection, short cameraRoll, NativeBattleLogicalOrdering ordering, NativeBattleSummonColors? colors = null)
    {
        using NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        NativeBattleEffectNodeBank bank = CurrentBank(memory);
        if (memory.Profile.StreamsPacketProducers)
        {
            NativeBattleGfGeometryBatch streamed = CreateLifecycle(memory, bank).DrawNodes(projection, cameraRoll, PrepareMeshes(memory, projection, colors), (meshes, packetBatch) => ordering = StreamBatch(memory, frame, ordering, packetBatch, meshes));
            return new(FlushCurrent(memory, bank), ordering, streamed);
        }
        NativeBattleGfGeometryBatch batch = CreateLifecycle(memory, bank).DrawNodes(projection, cameraRoll, PrepareMeshes(memory, projection, colors));
        return CommitDraws(memory, bank, ordering, batch);
    }

    private static NativeBattleCuragaNodeDrawStep CommitDraws(NativeBattleCuragaNodeMemory memory, NativeBattleEffectNodeBank bank, NativeBattleLogicalOrdering ordering, NativeBattleGfGeometryBatch batch)
    {
        return CommitBatch(FlushCurrent(memory, bank), ordering, batch);
    }

    public static NativeBattleCuragaNodeDrawStep CommitBatch(NativeBattleCuragaFrame result, NativeBattleLogicalOrdering ordering, NativeBattleGfGeometryBatch batch)
    {
        if (NativeBattleCuragaFamilyProfile.ForGenericNodes(result.Effect.Binding.Effect).StreamsPacketProducers)
        {
            throw new NotSupportedException("The generic node module requires its actual sprite or mesh packet producer.");
        }
        return CommitBatch(result, ordering, batch, false);
    }

    public static NativeBattleCuragaNodeDrawStep CommitBatch(NativeBattleCuragaFrame result, NativeBattleLogicalOrdering ordering, NativeBattleGfGeometryBatch batch, bool meshes)
    {
        if (batch.Attempts.Length != 0)
        {
            NativeBattleCuragaFamilyProfile profile = NativeBattleCuragaFamilyProfile.ForGenericNodes(result.Effect.Binding.Effect);
            if (profile.UsesCommonPacketCursor || meshes && profile.UsesSplitPacketCursors)
            {
                NativeBattleCommonPacketWrite common = NativeBattleCommonPacketArena.Append(result.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native generic node packets require their allocated common packet owner."), ordering, batch);
                return new(result with { Memory = result.Memory with { PrimitiveProcess = common.Process } }, common.Ordering, batch);
            }
            int cursor = result.State.PrimaryDrawCursor ?? throw new NotSupportedException("Native Curaga packet writes require87DC50's actual primary26D75C0 cursor.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(result.Memory, cursor, ordering, batch);
            result = result with { Memory = written.Memory, State = result.State with { PrimaryDrawCursor = written.Cursor } };
            ordering = written.Ordering;
        }
        return new(result, ordering, batch);
    }

    private static NativeBattleLogicalOrdering StreamBatch(NativeBattleCuragaNodeMemory memory, NativeBattleCuragaFrame frame, NativeBattleLogicalOrdering ordering, NativeBattleGfGeometryBatch batch, bool meshes)
    {
        NativeBattleCuragaNodeDrawStep written = CommitBatch(frame with { Memory = memory.CaptureProcessMemory(), State = memory.State }, ordering, batch, meshes);
        memory.PublishProcessMemory(written.Frame.Memory);
        memory.State = written.Frame.State;
        return written.Ordering;
    }
}
