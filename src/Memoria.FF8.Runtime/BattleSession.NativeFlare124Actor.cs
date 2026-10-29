using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState DrawNativeFlare124Actor(NativeBattleGraphicsState graphics, int index, short centerX, short centerY, short radius, int step, int amplitude, int phase, NativeBattleFlare124Resources resources, out short maximum)
    {
        NativeBattleGraphicsModel actor = graphics.Models[index];
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, index);
        NativeBattleCameraProjection projection = NativeCamera!.Projection!;
        NativeBattleActionMatrix view = projection.View!;
        graphics = SubmitNativeShadowPackets5088A0(graphics, actor, projection, 4112, false);
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Native Flare requires its original target skeleton.");
        NativeBattleActionMatrix world = actor.GeometryWorldMatrix ?? throw new NotSupportedException("Native Flare requires its target world matrix.");
        actor = NativeBattleBodyBoneMatrices.Compose5095B0(actor, world);
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(actor, source).Select(matrix => NativeBattleGfMatrices.Compose56C2F0(view, matrix)).ToImmutableArray();
        ImmutableArray<NativeBattleRawMesh> geometry = DiablosActorGeometry(resource, actor);
        uint mask = actor.SequenceState!.MeshMask124;
        ushort capacity = NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Native Flare requires its loaded vertex capacity.");
        uint workAddress = checked(0x196B018u - (uint)(capacity * 8));
        short zsf3 = NativeEffectMemory!.ScriptedGraphics!.Zsf3 ?? throw new NotSupportedException("Native Flare requires its retained ZSF3.");
        short zsf4 = NativeEffectMemory.ScriptedGraphics.Zsf4 ?? throw new NotSupportedException("Native Flare requires its retained ZSF4.");
        short extent = 0;
        for (int mesh = 0; mesh < geometry.Length; mesh++)
        {
            if ((mask & (1u << (mesh & 31))) != 0)
            {
                NativeBattleModelTextureBinding texture = resource.LinkedGeometryUsesBodySkeleton == true && mesh == 1 ? actor.LinkedTextureSlots! : actor.BodyTextureSlots!;
                Draw(geometry[mesh], bones, texture, mesh);
            }
        }
        actor = NativeBattleBodyBoneMatrices.Publish508C90(actor, source);
        graphics = graphics with { Models = graphics.Models.SetItem(index, actor) };
        ImmutableArray<NativeBattleRawMesh> linked = resource.LinkedGeometry ?? throw new NotSupportedException("Native Flare requires explicit linked geometry presence.");
        if (!linked.IsEmpty && resource.LinkedGeometryUsesBodySkeleton != true)
        {
            NativeBattlePoseSource linkedSource = resource.LinkedPoseSource ?? throw new NotSupportedException("Native Flare requires its linked skeleton.");
            NativeBattleDecodedPose pose = NativeBattlePoseMatrices.ReadLinkedDecodedPose(actor.SequenceState!, linkedSource);
            ImmutableArray<NativeBattleActionMatrix> linkedBones = NativeBattlePoseMatrices.ComposeStageCamera(NativeBattlePoseMatrices.StageBoneMatrices(linkedSource, pose), world, 0).Select(matrix => NativeBattleGfMatrices.Compose56C2F0(view, matrix)).ToImmutableArray();
            for (int linkedIndex = 0; linkedIndex < linked.Length; linkedIndex++)
            {
                Draw(linked[linkedIndex], linkedBones, actor.LinkedTextureSlots!, geometry.Length + linkedIndex);
            }
        }
        maximum = extent;
        return graphics;

        void Draw(NativeBattleRawMesh mesh, ImmutableArray<NativeBattleActionMatrix> matrices, NativeBattleModelTextureBinding texture, int meshIdentity)
        {
            if (mesh.Vertices.Length > capacity)
            {
                throw new InvalidDataException("Native Flare target exceeds its allocated vertex workspace.");
            }
            if (!resource.GeometryRelocated507550)
            {
                mesh = mesh with { Polygons = mesh.Polygons.Select(face => face with { Bytes = NativeBattleRawGeometryPackets.Relocate507550(face, texture) }).ToImmutableArray() };
            }
            NativeBattleFlare124ActorDraw drawn = NativeBattleFlare124ActorPackets.Emit(mesh, matrices, projection, actor.Color40!.Value, centerX, centerY, radius, step, amplitude, phase, resources, zsf3, zsf4, NativePilotSceneIdentity(0x6BCAD0, actor.Index, meshIdentity));
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
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(NativeEffectMemory!.PrimitiveProcess!, graphics.PrimitiveFrame!.Pending, drawn.Packets);
            NativeEffectMemory = NativeEffectMemory with { PrimitiveProcess = written.Process };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
        }
    }
}
