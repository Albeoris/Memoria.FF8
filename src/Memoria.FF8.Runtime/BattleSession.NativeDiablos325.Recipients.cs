using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleDiablos325Frame Frame, NativeBattleGraphicsState Graphics) DrawNativeDiablosRecipient657CB0(NativeBattleDiablos325Frame frame, int index, int scaleY, NativeBattleGraphicsState graphics)
    {
        NativeBattleGraphicsModel actor = graphics.Models[index];
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, index);
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Native Diablos sink requires its current actor rig.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Diablos sink requires its current projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Diablos sink requires its current camera matrix.");
        NativeBattleGraphicsDefinition rules = definition.NativeGraphics ?? throw new NotSupportedException("Native Diablos sink requires its original graphics definition.");
        NativeBattlePrimitiveCapabilities capabilities = rules.PrimitiveCapabilities ?? throw new NotSupportedException("Native Diablos sink requires its actual renderer capabilities.");
        NativeBattleSummonColors colors = rules.Rules.SummonColors ?? throw new NotSupportedException("Native Diablos sink requires its original interpolation constants.");
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Diablos sink requires its current COMMON packet owner.");
        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Diablos sink requires its current ordering table.");
        NativeBattlePilotVisualIdentity? recipientIdentity = NativeBattleDiablos325VisualGeometry.Identity(frame, index);
        int bodyPart = 0;
        if ((actor.Flags0 & 0x20) == 0)
        {
            NativeBattleEffectLocalBounds bounds = actor.GeometryBounds52 ?? throw new NotSupportedException("Native Diablos shadow requires its current model bounds.");
            NativeBattleShadowSource shadowSource = rules.Rules.ShadowSource ?? throw new NotSupportedException("Native Diablos shadow requires its original template.");
            NativeBattleShadowGeometry shadow = NativeBattlePoseMatrices.ShadowGeometry5088A0(actor, bounds, view, shadowSource, capabilities.GouraudAlpha);
            NativeBattleModuleProjectedVertex[] projected = shadow.Vertices.Select(point => NativeBattleModuleProjection.Project45FE10(shadow.CameraMatrix, point, projection)).ToArray();
            NativeBattleGfGeometryBatch shadowPackets = NativeBattleShadowPackets.EmitBatch5088A0(projected, actor.Color44 ?? throw new NotSupportedException("Native Diablos shadow requires its current packed model color."), capabilities.GouraudAlpha, 0x1010, 16, shadow.Vertices.Select(point => NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(shadow.CameraMatrix, point), projection)).ToArray(), recipientIdentity);
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, primitive.Pending, shadowPackets);
            process = written.Process;
            primitive = primitive with { Pending = written.Ordering };
        }
        NativeBattleActionMatrix world = actor.GeometryWorldMatrix ?? throw new NotSupportedException("Native Diablos sink requires its retained actor world matrix.");
        NativeBattleActionMatrix scaled = NativeBattlePoseMatrices.Scale56BEF0(world, 4096, scaleY, 4096);
        NativeBattleActionMatrix basis = NativeBattleGfMatrices.Compose56C2F0(view, scaled);
        process = process.PublishTemporary5082B0Matrix(basis);
        actor = NativeBattleBodyBoneMatrices.Compose5095B0(actor, basis);
        graphics = graphics with { Models = graphics.Models.SetItem(index, actor) };
        ImmutableArray<NativeBattleRawMesh> body = DiablosActorGeometry(resource, actor);
        DrawBody(body, NativeBattleBodyBoneMatrices.Require(actor, source), actor.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Native Diablos sink requires its current body object mask."));
        actor = NativeBattleBodyBoneMatrices.Publish508C90(actor, source);
        graphics = graphics with { Models = graphics.Models.SetItem(index, actor) };
        ImmutableArray<NativeBattleRawMesh> linked = resource.LinkedGeometry ?? throw new NotSupportedException("Native Diablos sink requires its actual linked geometry presence.");
        if (!linked.IsEmpty && !(resource.LinkedGeometryUsesBodySkeleton ?? throw new NotSupportedException("Native Diablos sink requires its original linked skeleton layout.")))
        {
            NativeBattlePoseSource linkedSource = resource.LinkedPoseSource ?? throw new NotSupportedException("Native Diablos sink requires its current linked rig source.");
            NativeBattleSequenceState sequence = actor.SequenceState ?? throw new NotSupportedException("Native Diablos sink requires its actual linked pose owner.");
            NativeBattleDecodedPose pose = NativeBattlePoseMatrices.ReadLinkedDecodedPose(sequence, linkedSource);
            ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattlePoseMatrices.ComposeStageCamera(NativeBattlePoseMatrices.StageBoneMatrices(linkedSource, pose), basis, 0);
            DrawBody(DiablosRelocate(linked, actor.LinkedTextureSlots, resource.GeometryRelocated507550), matrices, uint.MaxValue);
        }
        return (frame with { Memory = frame.Memory with { PrimitiveProcess = process } }, graphics with { PrimitiveFrame = primitive });

        void DrawBody(ImmutableArray<NativeBattleRawMesh> geometry, ImmutableArray<NativeBattleActionMatrix> bones, uint mask)
        {
            var gate = process.Enter5099D0(capabilities.HardwareEnabled ?? throw new NotSupportedException("Native Diablos sink requires its current hardware capability."));
            process = gate.Process;
            if (!gate.Draw)
            {
                return;
            }
            uint color = actor.Color40 ?? throw new NotSupportedException("Native Diablos sink requires its actual packed body color.");
            process = process.PublishModelColor(color);
            ushort maximum = NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Native Diablos actor projection requires its actually loaded scratch maximum.");
            int capacity = checked(maximum * 8);
            uint address = checked(0x196B018u - (uint)capacity);
            NativeBattleRawModelBodyDraw drawn = NativeBattleRawModelBodyDrawing.Draw5099D0(geometry, bones, projection, color, mask, new byte[capacity].ToImmutableArray(), colors, 4, pilotIdentity: recipientIdentity is { } identity ? identity with { Primitive = unchecked(0x657CB0 + identity.Primitive * 31 + bodyPart++) } : null);
            if (drawn.ProjectedBytesWritten != 0)
            {
                NativeFileStaging = (NativeFileStaging ?? throw new NotSupportedException("Native Diablos actor projection requires its canonical shared byte owner.")).Write(address, drawn.ProjectedScratch.AsSpan().Slice(0, drawn.ProjectedBytesWritten));
            }
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, primitive.Pending, drawn.Packets);
            process = written.Process.Publish50FDF0(unchecked((uint)(written.Process.ByteCursor ?? throw new NotSupportedException("Native Diablos actor draw lost its current COMMON cursor."))), null);
            primitive = primitive with { Pending = written.Ordering };
        }
    }

    private static ImmutableArray<NativeBattleRawMesh> DiablosActorGeometry(NativeBattleGraphicsResource resource, NativeBattleGraphicsModel actor)
    {
        ImmutableArray<NativeBattleRawMesh> geometry = resource.Geometry ?? throw new NotSupportedException("Native Diablos actor requires its original body geometry.");
        ImmutableArray<NativeBattleRawMesh> linked = resource.LinkedGeometry ?? throw new NotSupportedException("Native Diablos actor requires its actual linked geometry presence.");
        bool shared = !linked.IsEmpty && (resource.LinkedGeometryUsesBodySkeleton ?? throw new NotSupportedException("Native Diablos actor requires its original linked skeleton layout."));
        if (shared)
        {
            if (geometry.Length < 2 || linked.Length != 1)
            {
                throw new InvalidDataException("Native Diablos shared weapon requires its actual directory slot.");
            }
            return geometry.Select((mesh, index) => DiablosRelocate([index == 1 ? linked[0] : mesh], index == 1 ? actor.LinkedTextureSlots : actor.BodyTextureSlots, false)[0]).ToImmutableArray();
        }
        return DiablosRelocate(geometry, actor.BodyTextureSlots, resource.GeometryRelocated507550);
    }

    private static ImmutableArray<NativeBattleRawMesh> DiablosRelocate(ImmutableArray<NativeBattleRawMesh> geometry, NativeBattleModelTextureBinding? binding, bool relocated)
    {
        if (relocated)
        {
            return geometry;
        }
        NativeBattleModelTextureBinding actual = binding ?? throw new NotSupportedException("Native Diablos actor requires its executed original texture relocation.");
        return geometry.Select(mesh => mesh with { Polygons = mesh.Polygons.Select(polygon => polygon with { Bytes = NativeBattleRawGeometryPackets.Relocate507550(polygon, actual) }).ToImmutableArray() }).ToImmutableArray();
    }
}
