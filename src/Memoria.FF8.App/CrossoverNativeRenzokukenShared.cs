using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class CrossoverNativeRenzokukenProgram
{
    private static NativeBattleModuleState AppendShared(NativeBattleModuleState modules, NativeBattleRenzokuken160SharedTask task)
    {
        (modules, int slot) = modules.AppendCallback(task.Callback);
        byte[] bytes = modules.Slots[slot].Payload.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, task.Counter);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), task.Actor);
        return modules with { Slots = modules.Slots.SetItem(slot, modules.Slots[slot] with { Payload = bytes.ToImmutableArray() }) };
    }

    private (NativeBattleModuleState State, NativeBattleRenzokuken160Frame Frame, NativeBattleLogicalOrdering Ordering, ImmutableArray<NativeBattleModuleDraw> Draws) AdvanceShared(NativeBattleModuleState modules, NativeBattleRenzokuken160Frame frame, NativeBattleRenzokuken160Resources resources, NativeBattleGraphicsModel model, NativeBattleGraphicsResource resource, NativeBattleCameraProjection projection, NativeBattleSummonColors colors)
    {
        ushort variant = resources.ActorResource.MagicId;
        NativeBattleRenzokukenVariantProfile profile = NativeBattleRenzokukenVariantProfile.Renzokuken141;
        uint emitter = variant == 141 ? profile.SharedEmitterCallback : 0x5FF4E0;
        uint streak = variant == 141 ? profile.SharedStreakCallback : 0x5FF770;
        NativeBattlePoseSource pose = resource.PoseSource!;
        NativeBattleModelTransform transform = model.Transform!;
        NativeBattleActionMatrix view = projection.View!;
        NativeBattlePrimitiveProcess primitive = frame.Memory.PrimitiveProcess ?? NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400();
        primitive = primitive.Reset500900(0) with { Buffer80 = (byte)(1 - primitive.Buffer80!.Value) };
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        NativeBattleModuleInputs inputs = new(0)
        {
            Resources = game.ReadNativeBattleModuleResources(),
            CrtRandom = frame.Random,
            ReadModelAnchor = actor => NativeBattlePoseMatrices.ReadAnchor(ReadModel(actor), pose) with { W = transform.Angle36 },
            ReadModelTransform = actor => ReadModel(actor).Transform!,
            ReadModelBoneCount = actor => ReadModel(actor).Index == 0 ? checked((byte)pose.BoneCount) : throw new InvalidDataException("Renzokuken shared module queried a foreign skeleton."),
            ReadModelBone = (actor, bone, fraction) => NativeBattlePoseMatrices.ReadBone(ReadModel(actor), pose, bone, fraction),
            TransformModelPosition = (actor, local) => NativeBattlePoseMatrices.TransformModulePosition570CC0(ReadModel(actor), local)
        };
        NativeBattleModuleStep step = modules.Advance(inputs, (state, slot, stepInputs) => state.Slots[slot].Callback switch
        {
            uint callback when callback == emitter => NativeBattleRenzokuken160Controller.AdvanceShared5FF4E0(state, slot, stepInputs, streak),
            uint callback when callback == streak => NativeBattleRenzokuken160Controller.AdvanceShared5FF770(state, slot, stepInputs, resources, resources.StreakReferencePoint, view, projection, colors, Draw),
            _ => throw new NotSupportedException("Renzokuken reached an unbound shared finisher callback.")
        });
        return (step.State, frame with { Random = step.CrtRandomAfter!, Memory = frame.Memory with { PrimitiveProcess = primitive } }, ordering, step.Draws);

        NativeBattleGraphicsModel ReadModel(int actor) => actor == 0 ? model : throw new InvalidDataException("Renzokuken shared module queried a foreign model.");

        void Draw(NativeBattleGfGeometryBatch batch)
        {
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(primitive, ordering, batch);
            primitive = written.Process;
            ordering = written.Ordering;
        }
    }
}
