using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private (NativeBattleGraphicsState Graphics, short Depth) DrawNativeEffect107Model(NativeBattleGraphicsState graphics, BattleModelOwner model, short screenX, short screenY, short priorDepth, int intensity, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleGraphicsModel target = graphics.ActorModels.Get(model);
        if (ReadNativeForeignModel(model) is { } foreign)
        {
            NativeBattleEffect107Resources lookup = graphics.OriginalEffect?.Binding.Effect107Resources ?? throw new NotSupportedException("Native6D19A0 requires its original lookup tables.");
            NativeBattleCameraProjection camera = NativeCamera?.Projection ?? throw new NotSupportedException("Native6D19A0 requires its current projection.");
            graphics = SubmitNativeShadowPackets5088A0(graphics, model, target, camera, 4112, false);
            short maximum = 0;
            NativeBattleGfGeometryBatch batch = NativeBattleForeignModelDrawing.Deform(foreign, camera, point => NativeBattleEffect107Geometry.Deform6D1C30(point, screenX, screenY, priorDepth, ref maximum, intensity, lookup.Read), (mesh, projected, identity) => NativeBattleEffect107Geometry.Emit6BCB50(mesh.Polygons, projected, target.Color40!.Value, null, pilotIdentity: identity), pilotIdentity ?? NativeBattlePilotVisualIdentity.ForModel(107, 0x6D19A0, model));
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(NativeEffectMemory!.PrimitiveProcess!, graphics.PrimitiveFrame!.Pending, batch);
            NativeEffectMemory = NativeEffectMemory with { PrimitiveProcess = written.Process };
            return (graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } }, maximum);
        }
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, model);
        NativeBattleModelTextureOwner? material = SelectNativeModelMaterial(graphics, model);
        NativeBattleEffect107Resources resources = graphics.OriginalEffect?.Binding.Effect107Resources ?? throw new NotSupportedException("Native6D19A0 requires its original effect107 lookup tables.");
        NativeBattleCameraProjection projection = NativeCamera?.Projection ?? throw new NotSupportedException("Native6D19A0 requires its prior camera registers.");
        NativeBattleActionMatrix view = projection.View ?? throw new NotSupportedException("Native6D19A0 requires its prior camera matrix.");
        graphics = SubmitNativeShadowPackets5088A0(graphics, model, target, projection, 4112, false);
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Native6D19A0 requires its actual body skeleton.");
        target = NativeBattlePoseMatrices.ComposeGeometryMatrices5095B0(target, source, view);
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(model, target) };
        ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattleBodyBoneMatrices.Require(target, source);
        ImmutableArray<NativeBattleRawMesh> geometry = resource.Geometry ?? throw new NotSupportedException("Native6D19A0 requires its actual body geometry.");
        ImmutableArray<NativeBattleRawMesh> linked = resource.LinkedGeometry ?? throw new NotSupportedException("Native6D19A0 requires its observed linked geometry presence.");
        bool shared = !linked.IsEmpty && (resource.LinkedGeometryUsesBodySkeleton ?? throw new NotSupportedException("Native6D19A0 requires its actual linked skeleton ownership."));
        uint objectMask = target.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Native6D19A0 requires its actual model object mask.");
        uint color = target.Color40 ?? throw new NotSupportedException("Native6D19A0 requires its model color DWORD40.");
        NativeBattleModelTextureBinding bodyBinding = target.BodyTextureSlots ?? throw new NotSupportedException("Native6D19A0 requires its body texture relocation.");
        short extent = 0;
        for (int index = 0; index < geometry.Length; index++)
        {
            if ((objectMask & (1u << (index & 31))) == 0)
            {
                continue;
            }
            NativeBattleRawMesh mesh = shared && index == 1 ? linked.Single() : geometry[index];
            NativeBattleModelTextureBinding binding = shared && index == 1 ? target.LinkedTextureSlots ?? throw new NotSupportedException("Native6D19A0 requires its linked texture relocation.") : bodyBinding;
            graphics = Submit(graphics, mesh, matrices, resource.GeometryRelocated507550 ? null : binding, index);
        }
        if (!shared && !linked.IsEmpty)
        {
            ImmutableArray<NativeBattleActionMatrix> linkedMatrices = NativeBattlePoseMatrices.LinkedGeometryMatrices502D40(target, resource.LinkedPoseSource ?? throw new NotSupportedException("Native6D19A0 requires its independent linked skeleton."), view);
            for (int index = 0; index < linked.Length; index++)
            {
                graphics = Submit(graphics, linked[index], linkedMatrices, target.LinkedTextureSlots ?? throw new NotSupportedException("Native6D19A0 requires its linked texture relocation."), 0x10000 + index);
            }
        }
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(model, NativeBattleBodyBoneMatrices.Publish508C90(graphics.ActorModels.Get(model), source)) };
        return (graphics, extent);

        NativeBattleGraphicsState Submit(NativeBattleGraphicsState current, NativeBattleRawMesh mesh, ImmutableArray<NativeBattleActionMatrix> bones, NativeBattleModelTextureBinding? binding, int meshIndex)
        {
            mesh.Validate();
            List<NativeBattleProjectedVertex> projected = [];
            foreach (NativeBattleRawVertex vertex in mesh.Vertices)
            {
                if (vertex.Bone >= bones.Length)
                {
                    throw new InvalidDataException("Native6D1AD0 geometry group exceeds its actual posed skeleton.");
                }
                NativeBattleModuleProjectedVertex point = NativeBattleModuleProjection.Project45FE10(bones[vertex.Bone], vertex.Position, projection);
                projected.Add(NativeBattleEffect107Geometry.Deform6D1C30(point, screenX, screenY, priorDepth, ref extent, intensity, resources.Read));
            }
            NativeBattleGfGeometryBatch batch = NativeBattleEffect107Geometry.Emit6BCB50(mesh.Polygons, projected, color, binding, pilotIdentity: pilotIdentity is { } identity ? identity with { Primitive = unchecked(identity.Primitive * 397 + meshIndex) } : null);
            NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native6D19A0 requires its retained packet memory.");
            NativeBattlePrimitiveFrame frame = current.PrimitiveFrame ?? throw new NotSupportedException("Native6D19A0 requires its initialized OT.");
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(memory.PrimitiveProcess ?? throw new NotSupportedException("Native6D19A0 requires its actual common packet cursor."), frame.Pending, BindNativeModelMaterial(batch, material));
            NativeEffectMemory = memory with { PrimitiveProcess = written.Process };
            return current with { PrimitiveFrame = frame with { Pending = written.Ordering } };
        }
    }
}
