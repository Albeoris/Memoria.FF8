using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState DrawNativeSleep145Tint(NativeBattleGraphicsState graphics, int target, NativeBattleVector center, int fade, NativeBattlePilotVisualIdentity identity)
    {
        NativeBattleCameraProjection projection = NativeCamera!.Projection!;
        NativeBattleActionMatrix view = projection.View!;
        NativeBattleSummonColors colors = definition.NativeGraphics!.Rules.SummonColors!;
        NativeBattleGraphicsModel actor = graphics.Models[target];
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, target);
        if ((actor.Flags0 & 0x20) == 0)
        {
            graphics = SubmitNativeShadowPackets5088A0(graphics, actor, projection, 4121, false);
        }
        NativeBattlePoseSource source = resource.PoseSource!;
        ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattlePoseMatrices.ModelGroupMatrices50F190(actor, source, NativeBattleCuragaMatrices.Identity8DD770());
        ImmutableArray<NativeBattleRawMesh> body = resource.Geometry!.Value;
        ImmutableArray<NativeBattleRawMesh> linked = resource.LinkedGeometry!.Value;
        bool shared = !linked.IsEmpty && (resource.LinkedGeometryUsesBodySkeleton ?? throw new NotSupportedException("Native Sleep requires its actual linked model ownership."));
        if (shared)
        {
            body = body.SetItem(1, linked.Single());
        }
        uint mask = actor.SequenceState!.MeshMask124;
        ushort maximum = NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Native Sleep tint requires its actual scratch maximum.");
        uint address = checked(0x196B018u - (uint)(maximum * 8));
        uint tint = colors.Interpolate(0x08B0A0, fade, 0);
        short zsf3 = NativeEffectMemory!.ScriptedGraphics!.Zsf3 ?? throw new NotSupportedException("Native Sleep requires its current ZSF3.");
        short zsf4 = NativeEffectMemory.ScriptedGraphics.Zsf4 ?? throw new NotSupportedException("Native Sleep requires its current ZSF4.");
        for (int index = 0; index < body.Length; index++)
        {
            if ((mask & (1u << (index & 31))) == 0)
            {
                continue;
            }
            NativeBattleRawMesh mesh = body[index];
            byte[] output = (NativeFileStaging ?? throw new NotSupportedException("Native Sleep tint requires its actual shared projection bytes.")).Read(address, checked(mesh.Vertices.Length * 8));
            for (int vertex = 0; vertex < mesh.Vertices.Length; vertex++)
            {
                NativeBattleRawVertex raw = mesh.Vertices[vertex];
                SleepPutVector(output, vertex * 8, NativeBattleGfMatrices.Transform460840(matrices[raw.Bone], raw.Position));
            }
            NativeFileStaging = NativeFileStaging!.Write(address, output);
            NativeBattleModelTextureBinding texture = shared && index == 1 ? actor.LinkedTextureSlots! : actor.BodyTextureSlots!;
            ImmutableArray<NativeBattleRawPolygon> polygons = resource.GeometryRelocated507550 ? mesh.Polygons : mesh.Polygons.Select(polygon => polygon with { Bytes = NativeBattleRawGeometryPackets.Relocate507550(polygon, texture) }).ToImmutableArray();
            Common(NativeBattleActorTintPackets.Emit(polygons, output.ToImmutableArray(), view, projection, actor.Color40!.Value, tint, colors, (positions, factors) => NativeBattleSleep145.WriteTintFactors619B70(positions, factors, center), zsf3, zsf4, identity with { Primitive = unchecked(identity.Primitive * 397 + index) }));
        }
        actor = NativeBattleBodyBoneMatrices.Publish508C90(actor, source);
        graphics = graphics with { Models = graphics.Models.SetItem(target, actor) };
        if (!shared && !linked.IsEmpty && (actor.Flags0 & 0x200) == 0 && ResetAndEnterNativeModelRenderer5099D0(actor.Color40!.Value))
        {
            ImmutableArray<NativeBattleActionMatrix> bones = NativeBattlePoseMatrices.LinkedGeometryMatrices502D40(actor, resource.LinkedPoseSource!, view);
            ImmutableArray<NativeBattleRawMesh> geometry = linked.Select(mesh => resource.GeometryRelocated507550 ? mesh : mesh with { Polygons = mesh.Polygons.Select(polygon => polygon with { Bytes = NativeBattleRawGeometryPackets.Relocate507550(polygon, actor.LinkedTextureSlots!) }).ToImmutableArray() }).ToImmutableArray();
            ImmutableArray<NativeBattleGeometryPrimitive> primitives = NativeBattleRawGeometryPackets.Emit5099D0(geometry, bones, projection, actor.Color40!.Value, uint.MaxValue, null, 17, 4, null, colors, PublishNativeBodyProjectionScratch, pilotIdentity: identity with { Primitive = unchecked(identity.Primitive * 397 + 256) });
            Common(NativeActorPrimitiveBatch(primitives));
        }
        return graphics;

        void Common(NativeBattleGfGeometryBatch batch)
        {
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(NativeEffectMemory!.PrimitiveProcess!, graphics.PrimitiveFrame!.Pending, batch);
            NativeEffectMemory = NativeEffectMemory with { PrimitiveProcess = written.Process };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
        }
    }
}
