using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private (NativeBattlePandemona291Frame Frame, NativeBattleGraphicsState Graphics) DrawNativePandemona291Actor(NativeBattlePandemona291Frame frame, BattleModelOwner index, NativeBattleVector angles, NativeBattleVector pivot, NativeBattleVector scale, NativeBattleGraphicsState graphics)
    {
        NativeBattleGraphicsModel actor = graphics.ActorModels.Get(index);
        NativeBattleModelTextureOwner? material = presentationForeignActors?.ContainsKey(index) == true
            ? null
            : SelectNativeModelMaterial(graphics, index, frame.Memory.Textures);
        NativeEffectMemory = frame.Memory;
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Pandemona target draw requires its actual projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Pandemona target draw requires its current view.");
        graphics = SubmitNativeShadowPackets5088A0(graphics, index, actor, projection, 0x1010, false);
        frame = frame with { Memory = NativeEffectMemory! };
        if (ReadNativeForeignModel(graphics, index) is { } foreign)
        {
            NativeBattleActionMatrix hostBasis = NativeBattlePandemona291ActorDrawing.Prepare68D1D0(foreign.World, view, angles, pivot, scale);
            uint hostColor = actor.Color40 ?? throw new NotSupportedException("Native actor drawing requires its current packed color.");
            NativeBattleSummonColors hostColors = this.resources.NativeGraphics!.Rules.SummonColors ?? throw new NotSupportedException("Native actor drawing requires its original color arithmetic.");
            if (Enter(hostColor))
            {
                NativeBattlePilotVisualIdentity identity = NativePilotModelIdentity(0x502D40, index) ?? throw new NotSupportedException("Native actor drawing requires its current visual occurrence.");
                ImmutableArray<NativeBattleGeometryPrimitive> primitives = NativeBattleForeignModelDrawing.Draw(foreign, hostBasis, projection, hostColor, hostColors, identity);
                graphics = AppendNativeModelRendererPackets(graphics, primitives, !primitives.IsEmpty);
            }
            return (frame with { Memory = NativeEffectMemory! }, graphics);
        }
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, index);
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Native Pandemona target draw requires its current body rig.");
        NativeBattleActionMatrix world = actor.GeometryWorldMatrix ?? throw new NotSupportedException("Native Pandemona target draw requires its actual world matrix.");
        NativeBattleActionMatrix basis = NativeBattlePandemona291ActorDrawing.Prepare68D1D0(world, view, angles, pivot, scale);
        actor = NativeBattleBodyBoneMatrices.Compose5095B0(actor, basis);
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(index, actor) };
        ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattleBodyBoneMatrices.Require(actor, source);
        uint color = actor.Color40 ?? throw new NotSupportedException("Native Pandemona target draw requires its actual color DWORD.");
        uint mask = actor.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Native Pandemona target draw requires its actual object mask.");
        ImmutableArray<NativeBattleRawMesh> body = resource.Geometry ?? throw new NotSupportedException("Native Pandemona target draw requires its actual geometry directory.");
        ImmutableArray<NativeBattleRawMesh> linked = resource.LinkedGeometry ?? throw new NotSupportedException("Native Pandemona target draw requires its actual linked geometry presence.");
        bool shared = !linked.IsEmpty && (resource.LinkedGeometryUsesBodySkeleton ?? throw new NotSupportedException("Native Pandemona target draw requires its executed linked skeleton layout."));
        NativeBattleModelTextureBinding binding = actor.BodyTextureSlots ?? throw new NotSupportedException("Native Pandemona target draw requires its executed texture relocation.");
        if (Enter(color))
        {
            ImmutableArray<NativeBattleGeometryPrimitive>.Builder primitives = ImmutableArray.CreateBuilder<NativeBattleGeometryPrimitive>();
            if (shared)
            {
                if (body.Length < 2 || linked.Length != 1)
                {
                    throw new NotSupportedException("Native Pandemona target draw requires its actual shared linked directory slot.");
                }
                for (int mesh = 0; mesh < body.Length; mesh++)
                {
                    if ((mask & (1u << (mesh & 31))) == 0)
                    {
                        continue;
                    }
                    NativeBattleRawMesh selected = mesh == 1 ? linked[0] : body[mesh];
                    NativeBattleModelTextureBinding textures = mesh == 1 ? actor.LinkedTextureSlots ?? throw new NotSupportedException("Native Pandemona target draw requires its executed shared linked texture relocation.") : binding;
                    primitives.AddRange(NativeBattleRawGeometryPackets.Emit5099D0([selected], matrices, projection, color, 1, 0, 17, 4, textures, this.resources.NativeGraphics!.Rules.SummonColors, pilotIdentity: NativePilotModelIdentity(0x502D40, index, mesh)));
                }
            }
            else
            {
                primitives.AddRange(NativeBattleRawGeometryPackets.Emit5099D0(body, matrices, projection, color, mask, 0, 17, 4, resource.GeometryRelocated507550 ? null : binding, this.resources.NativeGraphics!.Rules.SummonColors, pilotIdentity: NativePilotModelIdentity(0x502D40, index)));
            }
            graphics = AppendNativeModelRendererPackets(graphics, BindNativeModelMaterial(primitives.ToImmutable(), material), Enumerable.Range(0, body.Length).Any(mesh => (mask & (1u << (mesh & 31))) != 0));
        }
        actor = NativeBattleBodyBoneMatrices.Publish508C90(actor, source);
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(index, actor) };
        if (!shared && !linked.IsEmpty)
        {
            NativeBattlePoseSource linkedSource = resource.LinkedPoseSource ?? throw new NotSupportedException("Native Pandemona target draw requires its actual independent linked rig.");
            NativeBattleDecodedPose linkedPose = NativeBattlePoseMatrices.ReadLinkedDecodedPose(actor.SequenceState ?? throw new NotSupportedException("Native Pandemona target draw requires its actual linked pose cursor."), linkedSource);
            ImmutableArray<NativeBattleActionMatrix> linkedMatrices = NativeBattlePoseMatrices.ComposeStageCamera(NativeBattlePoseMatrices.StageBoneMatrices(linkedSource, linkedPose), basis, 0);
            if (Enter(color))
            {
                ImmutableArray<NativeBattleGeometryPrimitive> primitives = NativeBattleRawGeometryPackets.Emit5099D0(linked, linkedMatrices, projection, color, uint.MaxValue, 0, 17, 4, actor.LinkedTextureSlots ?? throw new NotSupportedException("Native Pandemona target draw requires its executed linked material relocation."), this.resources.NativeGraphics!.Rules.SummonColors, pilotIdentity: NativePilotModelIdentity(0x507BF0, index));
                graphics = AppendNativeModelRendererPackets(graphics, BindNativeModelMaterial(primitives, material), true);
            }
        }
        return (frame with { Memory = NativeEffectMemory! }, graphics);

        bool Enter(uint rgb)
        {
            NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native Pandemona target draw requires its retained process.");
            NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native Pandemona target draw requires its current renderer gate.");
            var gate = process.Enter5099D0(this.resources.NativeGraphics?.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Native Pandemona target draw requires its actual hardware capability."));
            NativeEffectMemory = memory with { PrimitiveProcess = gate.Draw ? gate.Process.PublishModelColor(rgb) : gate.Process };
            return gate.Draw;
        }
    }
}
