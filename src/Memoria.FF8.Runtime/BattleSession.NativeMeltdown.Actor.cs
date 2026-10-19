using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleMeltdownFrame Frame, NativeBattleGraphicsState Graphics) DrawNativeMeltdownActor(NativeBattleMeltdownFrame frame, NativeBattleGraphicsState graphics, NativeBattleMeltdownActor request)
    {
        int index = request.Actor;
        NativeBattleGraphicsModel actor = graphics.Models[index];
        actor = actor with { Flags0 = (ushort)(actor.Flags0 | 0xC) };
        graphics = graphics with { Models = graphics.Models.SetItem(index, actor) };
        if (!request.Draw)
        {
            return (frame, graphics);
        }
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, index);
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Native Meltdown requires its actor's current rig.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native Meltdown requires its current projection.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native Meltdown requires its current camera matrix.");
        NativeBattleGraphicsDefinition rules = definition.NativeGraphics ?? throw new NotSupportedException("Native Meltdown requires its original graphics definition.");
        NativeBattlePrimitiveCapabilities capabilities = rules.PrimitiveCapabilities ?? throw new NotSupportedException("Native Meltdown requires its actual renderer capabilities.");
        NativeBattleSummonColors colors = rules.Rules.SummonColors ?? throw new NotSupportedException("Native Meltdown requires its original interpolation constants.");
        NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Native Meltdown requires its current COMMON packet owner.");
        NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native Meltdown requires its current ordering table.");
        if ((actor.Flags0 & 0x20) == 0)
        {
            NativeBattleEffectLocalBounds bounds = actor.GeometryBounds52 ?? throw new NotSupportedException("Native Meltdown shadow requires its current model bounds.");
            NativeBattleShadowSource shadowSource = rules.Rules.ShadowSource ?? throw new NotSupportedException("Native Meltdown shadow requires its original template.");
            NativeBattleShadowGeometry shadow = NativeBattlePoseMatrices.ShadowGeometry5088A0(actor, bounds, view, shadowSource, capabilities.GouraudAlpha);
            NativeBattleModuleProjectedVertex[] projected = shadow.Vertices.Select(point => NativeBattleModuleProjection.Project45FE10(shadow.CameraMatrix, point, projection)).ToArray();
            NativeBattleGfGeometryBatch batch = NativeBattleShadowPackets.EmitBatch5088A0(projected, actor.Color44 ?? throw new NotSupportedException("Native Meltdown shadow requires its current packed model color."), capabilities.GouraudAlpha, 0x1010, 16);
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, primitive.Pending, batch);
            process = written.Process;
            primitive = primitive with { Pending = written.Ordering };
        }
        NativeBattleActionMatrix world = actor.GeometryWorldMatrix ?? throw new NotSupportedException("Native Meltdown requires its actor's retained world matrix.");
        NativeBattleActionMatrix cameraWorld = NativeBattleGfMatrices.Compose56C2F0(view, world);
        actor = NativeBattleBodyBoneMatrices.Compose5095B0(actor, world);
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(actor, source);
        ImmutableArray<NativeBattleRawMesh> geometry = DiablosActorGeometry(resource, actor);
        uint mask = actor.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Native Meltdown requires its actual body object mask.");
        uint color = actor.Color40 ?? throw new NotSupportedException("Native Meltdown requires its actual packed body color.");
        ushort maximum = NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Native Meltdown requires its loaded shared vertex capacity.");
        int capacity = checked(maximum * 8);
        uint scratch = checked(0x196B018u - (uint)capacity);
        for (int meshIndex = 0; meshIndex < geometry.Length; meshIndex++)
        {
            if ((mask & (1u << (meshIndex & 31))) == 0)
            {
                continue;
            }
            NativeBattleRawMesh mesh = geometry[meshIndex];
            ImmutableArray<byte> vertices = NativeBattleMeltdownGeometry.MeltVertices(mesh, bones, request.Target, request.Caster, request.Strength);
            if (vertices.Length > capacity)
            {
                throw new InvalidDataException("Native Meltdown world vertices exceed their loaded scratch capacity.");
            }
            NativeFileStaging = (NativeFileStaging ?? throw new NotSupportedException("Native Meltdown requires its canonical shared vertex bytes.")).Write(scratch, vertices.AsSpan());
            NativeBattleGfGeometryBatch batch = NativeBattleMeltdownGeometry.Emit612B40(mesh, vertices, view, projection, color);
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, primitive.Pending, batch);
            process = written.Process;
            primitive = primitive with { Pending = written.Ordering };
        }
        actor = NativeBattleBodyBoneMatrices.Publish508C90(actor, source);
        graphics = graphics with { Models = graphics.Models.SetItem(index, actor) };
        ImmutableArray<NativeBattleRawMesh> linked = resource.LinkedGeometry ?? throw new NotSupportedException("Native Meltdown requires its actual linked geometry presence.");
        if (!linked.IsEmpty && (actor.Flags0 & 0x200) == 0 && !(resource.LinkedGeometryUsesBodySkeleton ?? throw new NotSupportedException("Native Meltdown requires its linked model's skeleton ownership.")))
        {
            NativeBattlePoseSource linkedSource = resource.LinkedPoseSource ?? throw new NotSupportedException("Native Meltdown requires its actual linked rig source.");
            NativeBattleSequenceState sequence = actor.SequenceState ?? throw new NotSupportedException("Native Meltdown requires its actual linked pose owner.");
            NativeBattleDecodedPose pose = NativeBattlePoseMatrices.ReadLinkedDecodedPose(sequence, linkedSource);
            ImmutableArray<NativeBattleActionMatrix> linkedBones = NativeBattlePoseMatrices.ComposeStageCamera(NativeBattlePoseMatrices.StageBoneMatrices(linkedSource, pose), cameraWorld, 0);
            var gate = process.Enter5099D0(capabilities.HardwareEnabled ?? throw new NotSupportedException("Native Meltdown requires its current hardware capability."));
            process = gate.Process;
            if (gate.Draw)
            {
                process = process.PublishModelColor(color);
                NativeBattleRawModelBodyDraw drawn = NativeBattleRawModelBodyDrawing.Draw5099D0(DiablosRelocate(linked, actor.LinkedTextureSlots, resource.GeometryRelocated507550), linkedBones, projection, color, uint.MaxValue, new byte[capacity].ToImmutableArray(), colors, 4);
                if (drawn.ProjectedBytesWritten != 0)
                {
                    NativeFileStaging = (NativeFileStaging ?? throw new NotSupportedException("Native Meltdown linked projection requires its canonical shared vertex bytes.")).Write(scratch, drawn.ProjectedScratch.AsSpan().Slice(0, drawn.ProjectedBytesWritten));
                }
                NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, primitive.Pending, drawn.Packets);
                process = written.Process.Publish50FDF0(unchecked((uint)(written.Process.ByteCursor ?? throw new NotSupportedException("Native Meltdown linked draw lost its current COMMON cursor."))), null);
                primitive = primitive with { Pending = written.Ordering };
            }
        }
        return (frame with { Memory = frame.Memory with { PrimitiveProcess = process } }, graphics with { PrimitiveFrame = primitive });
    }
}
