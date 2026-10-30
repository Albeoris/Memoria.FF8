using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState DrawNativeBombActor(NativeBattleGraphicsState graphics, int index, uint overlayColor, int scroll, bool textured, NativeBattlePilotVisualIdentity? pilotIdentity = null, Action<ImmutableArray<NativeBattlePrimitiveReference>>? recordPresentation = null)
    {
        NativeBattleGraphicsModel actor = graphics.Models[index];
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, index);
        NativeBattleCameraProjection projection = NativeCamera!.Projection!;
        NativeBattleActionMatrix view = projection.View!;
        if ((actor.Flags0 & 0x20) == 0)
        {
            NativeBattlePrimitiveProcess process = NativeEffectMemory!.PrimitiveProcess!;
            int start = process.ByteCursor!.Value;
            graphics = SubmitNativeShadowPackets5088A0(graphics, actor, projection, 4121, false, callerPilotIdentity: pilotIdentity is { } shadow ? shadow with { Primitive = unchecked(shadow.Primitive * 65537 + 0x10000), ResourceGeneration = NativeBattlePilotVisualLifetimes.EpochFor(resource) } : null);
            int end = NativeEffectMemory!.PrimitiveProcess!.ByteCursor!.Value;
            recordPresentation?.Invoke(graphics.PrimitiveFrame!.Pending.Packets.Select(packet => packet.Reference).Where(reference => reference.Bank == process.PacketBank && reference.Buffer == process.PacketBuffer && reference.ByteOffset >= start && reference.ByteOffset < end).ToImmutableArray());
        }
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Native Bomb actor drawing requires its original rig.");
        NativeBattleActionMatrix world = actor.GeometryWorldMatrix ?? throw new NotSupportedException("Native Bomb actor drawing requires its retained world matrix.");
        actor = NativeBattleBodyBoneMatrices.Compose5095B0(actor, world);
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(actor, source);
        ImmutableArray<NativeBattleRawMesh> geometry = DiablosActorGeometry(resource, actor);
        uint mask = actor.SequenceState!.MeshMask124;
        ushort maximum = NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Native Bomb actor drawing requires its loaded scratch maximum.");
        uint address = checked(0x196B018u - (uint)(maximum * 8));
        short zsf3 = NativeEffectMemory!.ScriptedGraphics!.Zsf3 ?? throw new NotSupportedException("Native Bomb actor drawing requires ZSF3.");
        short zsf4 = NativeEffectMemory.ScriptedGraphics.Zsf4 ?? throw new NotSupportedException("Native Bomb actor drawing requires ZSF4.");
        for (int mesh = 0; mesh < geometry.Length; mesh++)
        {
            if ((mask & (1u << (mesh & 31))) == 0)
            {
                continue;
            }
            NativeBattleModelTextureBinding texture = resource.LinkedGeometryUsesBodySkeleton == true && mesh == 1 ? actor.LinkedTextureSlots! : actor.BodyTextureSlots!;
            Draw(geometry[mesh], bones, texture, true, mesh);
        }
        actor = NativeBattleBodyBoneMatrices.Publish508C90(actor, source);
        graphics = graphics with { Models = graphics.Models.SetItem(index, actor) };
        ImmutableArray<NativeBattleRawMesh> linked = resource.LinkedGeometry ?? throw new NotSupportedException("Native Bomb actor drawing requires its actual linked geometry presence.");
        if (!linked.IsEmpty && (actor.Flags0 & 0x200) == 0 && resource.LinkedGeometryUsesBodySkeleton != true)
        {
            NativeBattlePoseSource linkedSource = resource.LinkedPoseSource ?? throw new NotSupportedException("Native Bomb actor drawing requires its actual linked rig.");
            NativeBattleDecodedPose pose = NativeBattlePoseMatrices.ReadLinkedDecodedPose(actor.SequenceState!, linkedSource);
            ImmutableArray<NativeBattleActionMatrix> linkedBones = NativeBattlePoseMatrices.ComposeStageCamera(NativeBattlePoseMatrices.StageBoneMatrices(linkedSource, pose), world, 0);
            if (textured)
            {
                if (ResetAndEnterNativeModelRenderer5099D0(actor.Color40!.Value))
                {
                    ImmutableArray<NativeBattleActionMatrix> cameraBones = linkedBones.Select(matrix => NativeBattleGfMatrices.Compose56C2F0(view, matrix)).ToImmutableArray();
                    ImmutableArray<NativeBattleGeometryPrimitive> packets = NativeBattleRawGeometryPackets.Emit5099D0(linked, cameraBones, projection, actor.Color40.Value, uint.MaxValue, null, 17, 4, resource.GeometryRelocated507550 ? null : actor.LinkedTextureSlots, definition.NativeGraphics!.Rules.SummonColors, PublishNativeBodyProjectionScratch, pilotIdentity: pilotIdentity is { } linkedIdentity ? linkedIdentity with { Primitive = unchecked(linkedIdentity.Primitive * 65537 + 0x20000), ResourceGeneration = NativeBattlePilotVisualLifetimes.EpochFor(resource) } : null);
                    Common(NativeActorPrimitiveBatch(packets));
                }
            }
            else
            {
                for (int linkedIndex = 0; linkedIndex < linked.Length; linkedIndex++)
                {
                    Draw(linked[linkedIndex], linkedBones, actor.LinkedTextureSlots!, resource.GeometryRelocated507550, 0x100 + linkedIndex);
                }
            }
        }
        return graphics;

        void Draw(NativeBattleRawMesh mesh, ImmutableArray<NativeBattleActionMatrix> matrices, NativeBattleModelTextureBinding texture, bool alreadyRelocated, int meshIndex)
        {
            if (mesh.Vertices.Length > maximum)
            {
                throw new InvalidDataException("Native Bomb world vertices exceed their current shared scratch capacity.");
            }
            byte[] output = new byte[mesh.Vertices.Length * 8];
            for (int vertex = 0; vertex < mesh.Vertices.Length; vertex++)
            {
                NativeBattleRawVertex raw = mesh.Vertices[vertex];
                NativeBattleVector point = NativeBattleGfMatrices.Transform460840(matrices[raw.Bone], raw.Position);
                BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(vertex * 8), point.X);
                BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(vertex * 8 + 2), point.Y);
                BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(vertex * 8 + 4), point.Z);
                int stored = 8;
                if (textured)
                {
                    BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(vertex * 8 + 6), unchecked((ushort)((byte)point.X | (byte)((point.Y + scroll) / 3) << 8)));
                }
                NativeFileStaging = (NativeFileStaging ?? throw new NotSupportedException("Native Bomb actor drawing requires its canonical shared vertex owner.")).Write(address + (uint)(vertex * 8), output.AsSpan(vertex * 8, stored));
            }
            ImmutableArray<NativeBattleRawPolygon> polygons = alreadyRelocated ? mesh.Polygons : mesh.Polygons.Select(polygon => polygon with { Bytes = NativeBattleRawGeometryPackets.Relocate507550(polygon, texture) }).ToImmutableArray();
            Common(NativeBattleBombActorPackets.Emit(polygons, output.ToImmutableArray(), view, projection, actor.Color40!.Value, overlayColor, scroll, textured, zsf3, zsf4, pilotIdentity is { } identity ? identity with { Primitive = unchecked(identity.Primitive * 65537 + meshIndex), ResourceGeneration = NativeBattlePilotVisualLifetimes.EpochFor(resource) } : null));
        }
        void Common(NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveProcess process = NativeEffectMemory!.PrimitiveProcess!;
            recordPresentation?.Invoke(NativeBattleEffectPresentationPackets.Record([], process.PacketBank!, process.PacketBuffer, process.ByteCursor!.Value, batch));
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(NativeEffectMemory!.PrimitiveProcess!, graphics.PrimitiveFrame!.Pending, batch);
            NativeEffectMemory = NativeEffectMemory with { PrimitiveProcess = written.Process };
            graphics = graphics with { PrimitiveFrame = graphics.PrimitiveFrame with { Pending = written.Ordering } };
        }
    }
}
