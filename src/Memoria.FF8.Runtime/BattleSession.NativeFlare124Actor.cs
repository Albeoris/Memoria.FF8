using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState DrawNativeFlare124Actor(NativeBattleGraphicsState graphics, BattleModelOwner owner, short centerX, short centerY, short radius, int step, int amplitude, int phase, NativeBattleFlare124Resources resources, out short maximum)
    {
        NativeBattleGraphicsModel actor = graphics.ActorModels.Get(owner);
        NativeBattleCameraProjection projection = NativeCamera!.Projection!;
        graphics = SubmitNativeShadowPackets5088A0(graphics, owner, actor, projection, 4112, false);
        short zsf3 = NativeEffectMemory!.ScriptedGraphics!.Zsf3 ?? throw new NotSupportedException("Native Flare requires its retained ZSF3.");
        short zsf4 = NativeEffectMemory.ScriptedGraphics.Zsf4 ?? throw new NotSupportedException("Native Flare requires its retained ZSF4.");
        if (ReadNativeForeignModel(owner) is { } foreign)
        {
            short hostExtent = 0;
            NativeBattleGfGeometryBatch batch = NativeBattleForeignModelDrawing.Deform(foreign, projection, Deform, (mesh, projected, identity) => NativeBattleFlare124ActorPackets.EmitProjected(mesh.Polygons, projected.Select(point => new NativeBattleModuleProjectedVertex(point.Screen, unchecked((ushort)point.DepthAndClip), point.DepthAndClip >> 16)).ToArray(), actor.Color40!.Value, zsf3, zsf4, identity), NativePilotModelIdentity(0x6BCAD0, owner) ?? NativeBattlePilotVisualIdentity.ForModel(124, 0x6BCAD0, owner));
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(NativeEffectMemory.PrimitiveProcess!, graphics.PrimitiveFrame!.Pending, batch);
            NativeEffectMemory = NativeEffectMemory with { PrimitiveProcess = written.Process };
            maximum = hostExtent;
            return graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };

            NativeBattleProjectedVertex Deform(NativeBattleModuleProjectedVertex point)
            {
                NativeBattleModuleProjectedVertex deformed = NativeBattleFlare124ActorPackets.Ripple(point, centerX, centerY, radius, step, amplitude, phase, resources, out short currentExtent);
                hostExtent = Math.Max(hostExtent, currentExtent);
                return new(deformed.Screen, deformed.Depth | deformed.Flags << 16);
            }
        }
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, owner);
        NativeBattleModelTextureOwner? material = SelectNativeModelMaterial(graphics, owner);
        NativeBattleActionMatrix view = projection.View!;
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Native Flare requires its original target skeleton.");
        NativeBattleActionMatrix world = actor.GeometryWorldMatrix ?? throw new NotSupportedException("Native Flare requires its target world matrix.");
        actor = NativeBattleBodyBoneMatrices.Compose5095B0(actor, world);
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(actor, source).Select(matrix => NativeBattleGfMatrices.Compose56C2F0(view, matrix)).ToImmutableArray();
        ImmutableArray<NativeBattleRawMesh> geometry = NativeBattleActorMaterialGeometry.PrepareBody(resource, actor);
        uint mask = actor.SequenceState!.MeshMask124;
        ushort capacity = NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Native Flare requires its loaded vertex capacity.");
        uint workAddress = checked(0x196B018u - (uint)(capacity * 8));
        short extent = 0;
        for (int mesh = 0; mesh < geometry.Length; mesh++)
        {
            if ((mask & (1u << (mesh & 31))) != 0)
            {
                Draw(geometry[mesh], bones, mesh);
            }
        }
        actor = NativeBattleBodyBoneMatrices.Publish508C90(actor, source);
        graphics = graphics with { ActorModels = graphics.ActorModels.Set(owner, actor) };
        ImmutableArray<NativeBattleRawMesh> linked = NativeBattleActorMaterialGeometry.PrepareSeparateLinked(resource, actor);
        if (!linked.IsEmpty)
        {
            NativeBattlePoseSource linkedSource = resource.LinkedPoseSource ?? throw new NotSupportedException("Native Flare requires its linked skeleton.");
            NativeBattleDecodedPose pose = NativeBattlePoseMatrices.ReadLinkedDecodedPose(actor.SequenceState!, linkedSource);
            ImmutableArray<NativeBattleActionMatrix> linkedBones = NativeBattlePoseMatrices.ComposeStageCamera(NativeBattlePoseMatrices.StageBoneMatrices(linkedSource, pose), world, 0).Select(matrix => NativeBattleGfMatrices.Compose56C2F0(view, matrix)).ToImmutableArray();
            for (int linkedIndex = 0; linkedIndex < linked.Length; linkedIndex++)
            {
                Draw(linked[linkedIndex], linkedBones, geometry.Length + linkedIndex);
            }
        }
        maximum = extent;
        return graphics;

        void Draw(NativeBattleRawMesh mesh, ImmutableArray<NativeBattleActionMatrix> matrices, int meshIdentity)
        {
            if (mesh.Vertices.Length > capacity)
            {
                throw new InvalidDataException("Native Flare target exceeds its allocated vertex workspace.");
            }
            NativeBattleFlare124ActorDraw drawn = NativeBattleFlare124ActorPackets.Emit(mesh, matrices, projection, actor.Color40!.Value, centerX, centerY, radius, step, amplitude, phase, resources, zsf3, zsf4, NativePilotModelIdentity(0x6BCAD0, owner, meshIdentity));
            extent = Math.Max(extent, drawn.Radius);
            for (int vertex = 0; vertex < mesh.Vertices.Length; vertex++)
            {
                int offset = vertex * 8;
                if (drawn.Vertices[offset + 6] == 16)
                {
                    NativeFileStaging = NativeFileStaging!.Write(workAddress + (uint)offset + 6, drawn.Vertices.AsSpan().Slice(offset + 6, 2));
                }
                else
                {
                    NativeFileStaging = NativeFileStaging!.Write(workAddress + (uint)offset, drawn.Vertices.AsSpan().Slice(offset, 8));
                }
            }
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(NativeEffectMemory!.PrimitiveProcess!, graphics.PrimitiveFrame!.Pending, BindNativeModelMaterial(drawn.Packets, material));
            NativeEffectMemory = NativeEffectMemory with { PrimitiveProcess = written.Process };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
        }
    }
}
