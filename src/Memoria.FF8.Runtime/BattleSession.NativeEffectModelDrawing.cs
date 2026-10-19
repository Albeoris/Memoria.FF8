using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState DrawNativeEffectModel5099D0(NativeBattleGraphicsState graphics, int index, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, int shadowTableBase, byte shadowDepthShift)
    {
        NativeBattleGraphicsModel model = graphics.Models[index];
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, index);
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Native effect body drawing requires its actual pose source.");
        NativeBattleGraphicsDefinition rules = definition.NativeGraphics ?? throw new NotSupportedException("Native effect body drawing requires its current graphics definition.");
        NativeBattlePrimitiveCapabilities capabilities = rules.PrimitiveCapabilities ?? throw new NotSupportedException("Native effect body drawing requires its actual renderer capabilities.");
        NativeBattleSummonColors colors = rules.Rules.SummonColors ?? throw new NotSupportedException("Native effect body drawing requires its authenticated color arithmetic.");
        graphics = SubmitNativeShadowPackets5088A0(graphics, model, projection, shadowTableBase, false, shadowDepthShift);
        model = NativeBattleBodyBoneMatrices.Compose5095B0(model, matrix);
        graphics = graphics with { Models = graphics.Models.SetItem(index, model) };
        DrawSource(GeometryPointer(model.ActiveResourceRow100, model.ActiveStagedResourceRow100), NativeBattleBodyBoneMatrices.Require(model, source), source.BoneCount, model.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Native effect body drawing requires its current object mask."), resource.GeometryRelocated507550 ? null : model.BodyTextureSlots ?? throw new NotSupportedException("Native effect body drawing requires its actual body texture relocation."));
        model = NativeBattleBodyBoneMatrices.Publish508C90(graphics.Models[index], source);
        graphics = graphics with { Models = graphics.Models.SetItem(index, model) };
        ImmutableArray<NativeBattleRawMesh> linked = resource.LinkedGeometry ?? throw new NotSupportedException("Native effect body drawing requires its actual linked geometry presence.");
        if (!linked.IsEmpty && !(resource.LinkedGeometryUsesBodySkeleton ?? throw new NotSupportedException("Native effect body drawing requires its actual linked rig layout.")))
        {
            NativeBattlePoseSource linkedSource = resource.LinkedPoseSource ?? throw new NotSupportedException("Native effect body drawing requires its actual independent linked pose source.");
            NativeBattleBodyBoneMatrices linkedBones = model.CurrentLinkedBoneMatrices ?? throw new NotSupportedException("Native effect body drawing requires its actually published linked matrices.");
            _ = NativeBattleBodyBoneMatrices.Require(model with { CurrentBodyBoneMatrices = linkedBones }, linkedSource);
            ImmutableArray<NativeBattleActionMatrix> prepared = NativeBattlePoseMatrices.ComposeStageCamera(linkedBones.Matrices, matrix, 0);
            model = model with { CurrentLinkedBoneMatrices = linkedBones with { Matrices = prepared } };
            graphics = graphics with { Models = graphics.Models.SetItem(index, model) };
            DrawSource(GeometryPointer(model.LinkedTextureResourceIndex, model.LinkedStagedResourceRow), prepared, linkedSource.BoneCount, uint.MaxValue, resource.GeometryRelocated507550 ? null : model.LinkedTextureSlots ?? throw new NotSupportedException("Native effect body drawing requires its actual linked texture relocation."));
            NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Native effect body drawing requires its actual linked pose owner.");
            graphics = graphics with { Models = graphics.Models.SetItem(index, model with { CurrentLinkedBoneMatrices = linkedBones with { Matrices = NativeBattlePoseMatrices.StageBoneMatrices(linkedSource, NativeBattlePoseMatrices.ReadLinkedDecodedPose(sequence, linkedSource)) } }) };
        }
        return graphics;

        void DrawSource(NativeBattleResourcePointer pointer, ImmutableArray<NativeBattleActionMatrix> bones, int boneCount, uint mask, NativeBattleModelTextureBinding? textureBinding)
        {
            NativeBattleEffectMemory memory = NativeEffectMemory ?? throw new NotSupportedException("Native effect body drawing requires its retained packet memory.");
            NativeBattlePrimitiveProcess process = memory.PrimitiveProcess ?? throw new NotSupportedException("Native effect body drawing requires its current renderer process.");
            var gate = process.Enter5099D0(capabilities.HardwareEnabled ?? throw new NotSupportedException("Native effect body drawing requires its current hardware capability."));
            process = gate.Process;
            if (!gate.Draw)
            {
                NativeEffectMemory = memory with { PrimitiveProcess = process };
                return;
            }
            var cpu = NativeModelCpuResources ?? throw new NotSupportedException("Native effect body drawing requires its actual model CPU rows.");
            NativeBattleFileStaging staging = NativeFileStaging ?? throw new NotSupportedException("Native effect body drawing requires its canonical projected CPU memory.");
            ImmutableArray<NativeBattleRawMesh> geometry = NativeBattleRawGeometryReader.Read(Read, boneCount);
            if (textureBinding is not null)
            {
                geometry = geometry.Select(mesh => mesh with { Polygons = mesh.Polygons.Select(polygon => polygon with { Bytes = NativeBattleRawGeometryPackets.Relocate507550(polygon, textureBinding) }).ToImmutableArray() }).ToImmutableArray();
            }
            uint color = graphics.Models[index].Color40 ?? throw new NotSupportedException("Native effect body drawing requires its actual packed model color.");
            process = process.PublishModelColor(color);
            int capacity = checked((cpu.ScratchMaximum ?? throw new NotSupportedException("Native effect body drawing requires its actual loaded projected workspace maximum.")) * 8);
            uint scratch = checked(0x196B018u - (uint)capacity);
            (byte[] retained, _) = staging.ReadSnapshot(scratch, capacity);
            NativeBattleRawModelBodyDraw drawn = NativeBattleRawModelBodyDrawing.Draw5099D0(geometry, bones, projection, color, mask, retained.ToImmutableArray(), colors, 4, new(0, 0, 320, 216), 17, null);
            if (drawn.ProjectedBytesWritten != 0)
            {
                NativeFileStaging = staging.Write(scratch, drawn.ProjectedScratch.AsSpan().Slice(0, drawn.ProjectedBytesWritten));
            }
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native effect body drawing requires its initialized ordering table.");
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, primitive.Pending, drawn.Packets);
            NativeEffectMemory = memory with { PrimitiveProcess = written.Process.Publish50FDF0(CommonAddress(written.Process), null) };
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };

            byte[] Read(int offset, int length)
            {
                if (pointer.Length is int available && (offset < 0 || length < 0 || offset > available - length))
                {
                    throw new InvalidDataException("Native effect body geometry read leaves its actual source extent.");
                }
                if (pointer.Cpu is { } cpuPointer)
                {
                    return NativeBattleModelEnemyHeap.Read(cpu, staging, cpuPointer with { Offset = checked(cpuPointer.Offset + offset) }, length);
                }
                uint address = checked((pointer.StagingAddress ?? throw new NotSupportedException("Native effect body geometry requires its actual source pointer.")) + (uint)offset);
                if (graphics.OriginalEffect is { } effect && NativeBattleEffectTaskMemory.TryRead(effect.Pools, address, length, out byte[] bytes))
                {
                    return bytes;
                }
                return IsNativeEffectArenaAddress(address) ? memory.Arena!.Read(NativeBattleWaterExecutionFields.ArenaOffset(address, length), length) : staging.Read(address, length);
            }
        }

        NativeBattleResourcePointer GeometryPointer(int? row, uint? staged)
        {
            if (staged is uint physical)
            {
                uint address = checked(physical + 4);
                if (graphics.OriginalEffect is { } effect && NativeBattleEffectTaskMemory.TryReadPointer(effect.Pools, address, out NativeBattleResourcePointer? selected))
                {
                    return selected ?? throw new NotSupportedException("Native effect body geometry pointer is NULL.");
                }
                return (IsNativeEffectArenaAddress(address) ? NativeEffectMemory!.Arena!.ReadResourcePointer(NativeBattleWaterExecutionFields.ArenaOffset(address, 4)) : NativeFileStaging!.ReadResourcePointer(address)) ?? throw new NotSupportedException("Native effect body geometry pointer is unobserved.");
            }
            NativeBattleModelCpuResourceRow cpuRow = (NativeModelCpuResources ?? throw new NotSupportedException("Native effect body geometry requires its actual CPU rows.")).Rows[row ?? throw new NotSupportedException("Native effect body geometry requires its actual resource row.")];
            return new(cpuRow.Require(16), null, cpuRow.Lengths.TryGetValue(16, out int length) ? length : null);
        }
        static uint CommonAddress(NativeBattlePrimitiveProcess process)
        {
            uint bank = process.PacketBank switch { "off_B6D070" => 0x1901018, "off_B6D094" => 0x199B018, _ => throw new NotSupportedException("Native effect body drawing requires its actual common packet bank.") };
            return checked(bank + (uint)process.PacketBuffer * (uint)process.CommonPacketNominalCapacity + (uint)(process.ByteCursor ?? throw new NotSupportedException("Native effect body drawing requires its actual common cursor.")));
        }
    }
}
