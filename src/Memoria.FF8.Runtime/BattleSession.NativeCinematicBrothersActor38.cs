using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleWaterExecutionFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeCinematicBrothersActor38(NativeBattleWaterExecutionFrame frame, int resourceLength, NativeBattleGraphicsState graphics, bool scaledActor = false)
    {
        NativeBattleCinematicBrothersModelServices ringServices = new(_ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (current, address) =>
        {
            NativeBattleEffectMemory memory = RequireWaterMemory();
            NativeBattleScriptedColorRegisters registers = memory.ScriptedColorRegisters ?? throw new NotSupportedException("Native Brothers fan requires its canonical V0 owner.");
            NativeEffectMemory = memory with { ScriptedColorRegisters = NativeBattleCinematicRibbonInput.Capture(current, address, registers) };
            return current;
        }, (current, matrix, address) =>
        {
            NativeBattleScriptedGraphicsMemory controls = RequireWaterMemory().ScriptedGraphics ?? throw new NotSupportedException("Native Brothers fan requires its retained depth-cue controls.");
            NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Brothers fan requires its current projection controls.");
            int offset = checked((int)(address - current.TemporaryFrame.Address));
            NativeBattleVector point = NativeBattleScriptedMeshPreparation.Vector(current.ReadTemporary(offset, 6));
            NativeBattleModuleDepthCueVertex result = NativeBattleModuleProjection.ProjectWithCue45FAA0(matrix, point, projection, controls.Dqa, controls.Dqb);
            return new(current, result.Projected.Screen, result.Cue, result.Projected.Flags) { PilotWorld = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(matrix, point), projection) };
        }, () => 0, (current, output, ordering, batch) =>
        {
            var written = AppendNativeCinematicPackets(current, output, ordering, batch, graphics);
            graphics = written.Graphics;
            return written.Frame;
        });
        NativeBattleWaterSlotBank visualBank = NativeBattleWaterInstructionDispatcher.CurrentBank(frame, resourceLength);
        ringServices = ringServices with { PilotIdentity = NativeBattleCinematicVisualIdentity.Identity(graphics.OriginalEffect!.Binding.Effect, frame, visualBank, NativeBattleWaterInstructionDispatcher.CurrentIndex(frame, visualBank)) };
        NativeBattleCinematicBrothersActor38Services services = new((_, address) => graphics.ActorModels.Get(ActorIndex(address)), (current, address, actor) =>
        {
            graphics = graphics with { ActorModels = graphics.ActorModels.Set(ActorIndex(address), actor) };
            return current;
        }, Restore, (current, address, basis, position) =>
        {
            int index = ActorIndex(address);
            graphics = graphics with { ActorModels = graphics.ActorModels.Set(index, graphics.ActorModels.Get(index) with { Flags0 = (ushort)(graphics.ActorModels.Get(index).Flags0 | 0x20) }) };
            NativeBattleCinematicBonePose pose = Restore(current, address);
            current = pose.Frame;
            if (pose.Pose is { } foreignPose)
            {
                if (foreignPose.Endpoints.IsDefaultOrEmpty)
                {
                    throw new NotSupportedException("The host actor cannot supply its posed segment endpoints.");
                }
                NativeBattleFullMacVertex root = NativeBattleGfMatrices.TransformMac460840(basis, foreignPose.Endpoints[0]);
                return NativeBattleCinematicBrothersModel3.DrawPreparedRing(current, foreignPose.Endpoints.Length, root.X, root.Z, null, null, position, -1, 0, 0, NativeBattleCameraMath.Cosine(512), _ => graphics.ActorModels.Get(index).Color44 ?? throw new NotSupportedException("Native actor fan requires its current packed color."), ringServices, !scaledActor, (active, endpoint) =>
                {
                    NativeBattleVector point = foreignPose.Endpoints[endpoint];
                    active = active.WriteTemporaryWord(0xD0, unchecked((ushort)point.X)).WriteTemporaryWord(0xD2, unchecked((ushort)point.Y)).WriteTemporaryWord(0xD4, unchecked((ushort)point.Z));
                    active = ringServices.PublishInput(active, active.TemporaryFrame.Address + 0xD0);
                    return (active, NativeBattleGfMatrices.TransformMac460840(basis, point));
                });
            }
            graphics = graphics with { ActorModels = graphics.ActorModels.Set(index, NativeBattleBodyBoneMatrices.Compose5095B0(graphics.ActorModels.Get(index), basis)) };
            ImmutableArray<NativeBattleActionMatrix> bones = graphics.ActorModels.Get(index).CurrentBodyBoneMatrices!.Matrices;
            NativeBattleCinematicBrothersModelServices selectedRingServices = ringServices;
            if (graphics.OriginalEffect is { Binding.Effect: 331 } && ringServices.PilotIdentity is { } identity)
            {
                selectedRingServices = ringServices with { PilotIdentity = NativeBattleCinematicVisualIdentity.SelectedResource(identity, NativeModelResourceAddress100(graphics.ActorModels.Get(index))) };
            }
            current = NativeBattleCinematicBrothersModel3.DrawPreparedRing(current, bones.Length, bones[0].X, bones[0].Z, (_, bone) => graphics.ActorModels.Get(index).CurrentBodyBoneMatrices!.Matrices[bone], (_, bone) => BinaryPrimitives.ReadInt16LittleEndian(pose.Skeleton.AsSpan().Slice(0x12 + 48 * bone, 2)), position, -1, 0, 0, NativeBattleCameraMath.Cosine(512), _ => graphics.ActorModels.Get(index).Color44 ?? throw new NotSupportedException("Native Brothers actor fan requires its current packed physical model color."), selectedRingServices, !scaledActor);
            return Restore(current, address).Frame;
        }, ringServices.PublishInput, CreateNativeActorAnchorServices());
        if (scaledActor)
        {
            NativeBattleCinematicScaledActor72Services scaledServices = new(services.ReadActor, services.WriteActor, services.RestorePose, services.DrawShadow, services.PublishInput, CreateNativeActorAnchorServices());
            return (NativeBattleCinematicScaledActor72.DrawAE44E0(frame, scaledServices), graphics);
        }
        return (NativeBattleCinematicBrothersActor38.DrawAF59D0(frame, services), graphics);

        int ActorIndex(uint address)
        {
            if (!NativeBattleGraphicsModelAddress.TryDecode(address, out int actorIndex, graphics.Models.Length))
            {
                throw new NotSupportedException("Native Brothers actor positioning requires its actual physical model pointer.");
            }
            return actorIndex;
        }

        NativeBattleCinematicBonePose Restore(NativeBattleWaterExecutionFrame current, uint address)
        {
            int index = ActorIndex(address);
            if (presentationForeignActors?.TryGetValue(index, out NativeBattleForeignActor? foreign) == true)
            {
                NativeBattleGraphicsModel model = graphics.ActorModels.Get(index);
                NativeBattlePosedActor pose = foreign.Pose ?? throw new NotSupportedException("The host actor cannot supply its current posed anchors.");
                return new(current, [], [], NativeBattleGfMatrices.Compose56C2F0(NativeBattleForeignPlacement.Delta(foreign, model), pose.World)) { Pose = pose };
            }
            NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native Brothers actor positioning requires its current physical rig source.");
            NativeBattleGraphicsModel actor = NativeBattleBodyBoneMatrices.Publish508C90(graphics.ActorModels.Get(index), source);
            graphics = graphics with { ActorModels = graphics.ActorModels.Set(index, actor) };
            return new(current, source.Skeleton, NativeBattleBodyBoneMatrices.Require(actor, source), null);
        }
    }
}
