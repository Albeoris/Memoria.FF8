namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool NativeGraphicsUsesForeignPose(NativeBattleGraphicsState graphics, int index)
    {
        return presentationForeignActors?.TryGetValue(index, out NativeBattleForeignActor? foreign) == true && foreign.NativeResource is null && graphics.Models[index].ActiveStagedResourceRow100 is null && graphics.Models[index].ResourceMagicId is null && graphics.Models[index].ResourceRenzokukenTargetBank is null;
    }

    private NativeBattleEffectActorGeometry? ReadNativeGraphicsForeignTarget(NativeBattleGraphicsState graphics, int index)
    {
        return NativeGraphicsUsesForeignPose(graphics, index) ? ReadNativeForeignTarget(graphics, index) : null;
    }

    private NativeBattleForeignModelGeometry? ReadNativeGraphicsForeignModel(NativeBattleGraphicsState graphics, int index)
    {
        return NativeGraphicsUsesForeignPose(graphics, index) ? ReadNativeForeignModel(graphics, index) : null;
    }

    private bool NativeGraphicsActorAtStandingPose(NativeBattleGraphicsState graphics, int index)
    {
        if (presentationForeignActors?.TryGetValue(index, out NativeBattleForeignActor? foreign) == true && foreign.NativeResource is null)
        {
            return foreign.AtStandingPose ?? throw new NotSupportedException("The host actor has no current standing animation observation.");
        }
        return ((graphics.Models[index].ResourceFlags44 ?? throw new NotSupportedException("Native actor standing observation requires its current resource flags.")) & 0x40) != 0;
    }

    private bool NativeGraphicsModelIs(NativeBattleGraphicsState graphics, int index, byte modelId)
    {
        if (presentationForeignActors?.TryGetValue(index, out NativeBattleForeignActor? foreign) == true && foreign.NativeModel is null)
        {
            return false;
        }
        return (graphics.Models[index].ModelId4 ?? throw new NotSupportedException("Native model identity comparison requires the current native resource identity.")) == modelId;
    }

    private NativeBattlePosedActor? ReadNativeForeignPose(NativeBattleGraphicsState graphics, int index)
    {
        if (!NativeGraphicsUsesForeignPose(graphics, index) || presentationForeignActors?.TryGetValue(index, out NativeBattleForeignActor? foreign) != true)
        {
            return null;
        }
        NativeBattlePosedActor pose = foreign!.Pose ?? throw new NotSupportedException("The host actor cannot supply its actual posed bone segments.");
        NativeBattleGraphicsModel model = graphics.Models[index];
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("The host pose has no current transform owner.");
        return pose with { World = NativeBattleForeignPlacement.World(transform, model.Rotation12 ?? 0, model.Rotation14 ?? foreign.Facing, model.Rotation16 ?? 0) };
    }

    private (NativeBattleEffectMemory Memory, NativeBattleVector Point) ObserveNativeGraphicsBoneAnchor(NativeBattleEffectMemory memory, NativeBattleGraphicsState graphics, int index, byte selector, short fraction)
    {
        NativeBattleGraphicsModel model = graphics.Models[index];
        if (NativeGraphicsUsesForeignPose(graphics, index) && presentationForeignActors?.TryGetValue(index, out NativeBattleForeignActor? foreign) == true)
        {
            if ((model.Flags0 & 2) == 0)
            {
                return (memory, model.Transform?.Position28 ?? throw new NotSupportedException("The host actor has no current position owner."));
            }
            NativeBattlePosedActor pose = foreign.Pose ?? throw new NotSupportedException("The host actor cannot supply its current posed bone segments.");
            NativeBattleVector local = pose.ReadLocalBone(selector, fraction);
            NativeBattleActionMatrix basis = NativeBattleGfMatrices.Compose56C2F0(NativeBattleForeignPlacement.Delta(foreign, model), pose.World);
            NativeBattleGeometryRegisterStep step = new(memory.GeometryRegisters ?? throw new NotSupportedException("The host bone transform requires its current geometry registers."), memory.ScriptedColorRegisters ?? throw new NotSupportedException("The host bone transform requires its current arithmetic registers."));
            step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, basis, true, true, null) };
            step = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(step, 0, local, null));
            return (memory with { GeometryRegisters = step.Geometry, ScriptedColorRegisters = step.Colors }, new(unchecked((short)step.Colors.Mac[0]!.Value), unchecked((short)step.Colors.Mac[1]!.Value), unchecked((short)step.Colors.Mac[2]!.Value)));
        }
        NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native bone transforms require the selected actor rig.");
        NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source, selector, fraction);
        NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(memory, producer);
        NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
        return (observed, point);
    }

    private NativeBattleEffectActorGeometry? ReadNativeForeignTarget(int index)
    {
        return ReadNativeForeignTarget(NativeGraphics!, index);
    }

    private NativeBattleEffectActorGeometry? ReadNativeForeignTarget(NativeBattleGraphicsState graphics, int index)
    {
        if (presentationForeignActors?.TryGetValue(index, out NativeBattleForeignActor? foreign) != true)
        {
            return null;
        }
        NativeBattleEffectActorGeometry anchors = foreign!.Geometry?.Anchors ?? throw new NotSupportedException("The host actor cannot supply posed skeleton bounds.");
        NativeBattleActionMatrix delta = NativeBattleForeignPlacement.Delta(foreign, graphics.Models[index]);
        return anchors with { Anchor240 = NativeBattleGfMatrices.Transform460840(delta, anchors.Anchor240), Anchor241 = NativeBattleGfMatrices.Transform460840(delta, anchors.Anchor241) };
    }

    private NativeBattleForeignModelGeometry? ReadNativeForeignModel(int index)
    {
        return ReadNativeForeignModel(NativeGraphics!, index);
    }

    private NativeBattleForeignModelGeometry? ReadNativeForeignModel(NativeBattleGraphicsState graphics, int index)
    {
        if (presentationForeignActors?.TryGetValue(index, out NativeBattleForeignActor? foreign) != true)
        {
            return null;
        }
        NativeBattleForeignModelGeometry geometry = foreign!.Geometry ?? throw new NotSupportedException("The host actor cannot supply posed triangles and their original materials.");
        return geometry with { World = NativeBattleGfMatrices.Compose56C2F0(NativeBattleForeignPlacement.Delta(foreign, graphics.Models[index]), geometry.World) };
    }

    internal NativeBattleActionMatrix ReadEffectPresentationForeignPlacement(int index)
    {
        NativeBattleForeignActor actor = presentationForeignActors?[index] ?? throw new InvalidOperationException("The selected actor is not owned by the host.");
        return NativeBattleForeignPlacement.Delta(actor, NativeGraphics!.Models[index]);
    }
}
