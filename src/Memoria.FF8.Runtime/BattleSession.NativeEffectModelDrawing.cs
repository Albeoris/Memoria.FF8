using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsState DrawNativeEffectModelContext92(NativeBattleGraphicsState graphics, int index, NativeBattleActionMatrix matrix, NativeBattleVector pivot, NativeBattleCameraProjection projection, int shadowTableBase, byte shadowDepthShift, byte bodyDepthShift = 4, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleFileStaging staging = NativeFileStaging ?? throw new NotSupportedException("Native effect model context requires its retained physical temporary memory.");
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 92);
        staging = allocation.Staging;
        uint address = allocation.Address;
        for (int lane = 0; lane < 9; lane++)
        {
            staging = staging.Write(address + (uint)lane * 2, BitConverter.GetBytes(matrix.Rotation[lane]));
        }
        (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(matrix, pivot);
        staging = staging.Write(address + 20, BitConverter.GetBytes(matrix.X)).Write(address + 24, BitConverter.GetBytes(matrix.Y)).Write(address + 28, BitConverter.GetBytes(matrix.Z));
        staging = staging.Write(address + 32, BitConverter.GetBytes(x)).Write(address + 36, BitConverter.GetBytes(y)).Write(address + 40, BitConverter.GetBytes(z));
        ushort maximum = NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Native effect model context requires its actual CPU workspace maximum.");
        staging = staging.Write(address + 52, BitConverter.GetBytes(checked(0x196B018u - (uint)maximum * 8)));
        staging = staging.Write(address + 68, BitConverter.GetBytes((short)0)).Write(address + 70, BitConverter.GetBytes((short)0)).Write(address + 72, BitConverter.GetBytes((short)320)).Write(address + 74, BitConverter.GetBytes((short)216));
        NativeBattleGraphicsModel model = graphics.Models[index];
        staging = staging.Write(address + 76, BitConverter.GetBytes(model.Color40 ?? throw new NotSupportedException("Native effect model context requires its actual model color40."))).Write(address + 80, BitConverter.GetBytes(model.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Native effect model context requires its actual object mask124."))).Write(address + 88, new byte[4]);
        (byte[] bytes, bool[] known) = staging.ReadSnapshot(0x1D969A8, 4);
        for (int offset = 0; offset < 4; offset++)
        {
            staging = known[offset] ? staging.Write(address + 64 + (uint)offset, bytes.AsSpan(offset, 1)) : staging.MarkUnknown(address + 64 + (uint)offset, 1);
        }
        NativeFileStaging = staging;
        graphics = DrawNativeEffectModel5099D0(graphics, index, matrix, projection, shadowTableBase, shadowDepthShift, bodyDepthShift, contextMaskAddress: address + 80, pilotIdentity: pilotIdentity);
        staging = NativeFileStaging ?? throw new InvalidDataException("Native effect model context lost its physical owner during drawing.");
        NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(staging, 92);
        return graphics;
    }

    private NativeBattleGraphicsState DrawNativeEffectModel5099D0(NativeBattleGraphicsState graphics, int index, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, int shadowTableBase, byte shadowDepthShift, byte bodyDepthShift = 4, uint? privateOrderingAddress = null, short? depthCue = null, uint? contextMaskAddress = null, bool prepareModel = true, bool drawShadow = true, bool drawLinked = true, uint? maskOverride = null, NativeBattlePilotVisualIdentity? pilotIdentity = null, bool pilotScreenSpace = false, bool pilotContinuous = true)
    {
        NativeBattleGraphicsModel model = graphics.Models[index];
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, index);
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Native effect body drawing requires its actual pose source.");
        NativeBattleGraphicsDefinition rules = definition.NativeGraphics ?? throw new NotSupportedException("Native effect body drawing requires its current graphics definition.");
        NativeBattlePrimitiveCapabilities capabilities = rules.PrimitiveCapabilities ?? throw new NotSupportedException("Native effect body drawing requires its actual renderer capabilities.");
        NativeBattleSummonColors colors = rules.Rules.SummonColors ?? throw new NotSupportedException("Native effect body drawing requires its authenticated color arithmetic.");
        if (drawShadow)
        {
            graphics = SubmitNativeShadowPackets5088A0(graphics, model, projection, shadowTableBase, false, shadowDepthShift, privateOrderingAddress, pilotScreenSpace: pilotScreenSpace, pilotContinuous: pilotContinuous);
        }
        if (prepareModel)
        {
            model = NativeBattleBodyBoneMatrices.Compose5095B0(model, matrix);
        }
        graphics = graphics with { Models = graphics.Models.SetItem(index, model) };
        DrawSource(GeometryPointer(model.ActiveResourceRow100, model.ActiveStagedResourceRow100), NativeBattleBodyBoneMatrices.Require(model, source), source.BoneCount, maskOverride ?? model.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Native effect body drawing requires its current object mask."));
        model = prepareModel ? NativeBattleBodyBoneMatrices.Publish508C90(graphics.Models[index], source) : graphics.Models[index];
        graphics = graphics with { Models = graphics.Models.SetItem(index, model) };
        if (drawLinked && (model.LinkedStagedResourceRow is not null || model.LinkedTextureResourceIndex is not null))
        {
            if (contextMaskAddress is uint maskAddress)
            {
                NativeFileStaging = (NativeFileStaging ?? throw new NotSupportedException("Native linked body drawing requires its actual physical context.")).Write(maskAddress, BitConverter.GetBytes(uint.MaxValue));
            }
            NativeBattlePoseSource linkedSource = resource.LinkedPoseSource ?? throw new NotSupportedException("Native effect body drawing requires its actual independent linked pose source.");
            NativeBattleBodyBoneMatrices linkedBones = model.CurrentLinkedBoneMatrices ?? throw new NotSupportedException("Native effect body drawing requires its actually published linked matrices.");
            _ = NativeBattleBodyBoneMatrices.Require(model with { CurrentBodyBoneMatrices = linkedBones }, linkedSource);
            ImmutableArray<NativeBattleActionMatrix> prepared = NativeBattlePoseMatrices.ComposeStageCamera(linkedBones.Matrices, matrix, 0);
            model = model with { CurrentLinkedBoneMatrices = linkedBones with { Matrices = prepared } };
            graphics = graphics with { Models = graphics.Models.SetItem(index, model) };
            DrawSource(GeometryPointer(model.LinkedTextureResourceIndex, model.LinkedStagedResourceRow), prepared, linkedSource.BoneCount, uint.MaxValue, 1);
            NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Native effect body drawing requires its actual linked pose owner.");
            graphics = graphics with { Models = graphics.Models.SetItem(index, model with { CurrentLinkedBoneMatrices = linkedBones with { Matrices = NativeBattlePoseMatrices.StageBoneMatrices(linkedSource, NativeBattlePoseMatrices.ReadLinkedDecodedPose(sequence, linkedSource)) } }) };
        }
        return graphics;

        void DrawSource(NativeBattleResourcePointer pointer, ImmutableArray<NativeBattleActionMatrix> bones, int boneCount, uint mask, int layer = 0)
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
            uint color = graphics.Models[index].Color40 ?? throw new NotSupportedException("Native effect body drawing requires its actual packed model color.");
            process = process.PublishModelColor(color);
            int capacity = checked((cpu.ScratchMaximum ?? throw new NotSupportedException("Native effect body drawing requires its actual loaded projected workspace maximum.")) * 8);
            uint scratch = checked(0x196B018u - (uint)capacity);
            (byte[] retained, _) = staging.ReadSnapshot(scratch, capacity);
            NativeBattleRawModelBodyDraw drawn = NativeBattleRawModelBodyDrawing.Draw5099D0(geometry, bones, projection, color, mask, retained.ToImmutableArray(), colors, bodyDepthShift, new(0, 0, 320, 216), 17, depthCue, pilotIdentity: pilotIdentity is { } identity ? identity with { Primitive = unchecked(identity.Primitive * 397 + layer) } : null, pilotScreenSpace: pilotScreenSpace, pilotContinuous: pilotContinuous);
            if (drawn.ProjectedBytesWritten != 0)
            {
                NativeFileStaging = staging.Write(scratch, drawn.ProjectedScratch.AsSpan().Slice(0, drawn.ProjectedBytesWritten));
            }
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Native effect body drawing requires its initialized ordering table.");
            if (privateOrderingAddress is uint table)
            {
                NativeBattleRinoa89Frame capture = new(memory with { PrimitiveProcess = process }, graphics.OriginalEffect ?? throw new NotSupportedException("Native private body capture requires its actual effect owner."), NativeFileStaging!, nativeCrtRandom ?? throw new NotSupportedException("Native private capture requires its actual CRT owner."));
                NativeBattleRinoa89CaptureStep written = NativeBattleRinoa89Capture.AppendCommon(capture, primitive.Pending, drawn.Packets, table);
                NativeBattlePrimitiveProcess published = written.Frame.Memory.PrimitiveProcess!;
                NativeEffectMemory = written.Frame.Memory with { PrimitiveProcess = published.Publish50FDF0(CommonAddress(published), null) };
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            }
            else
            {
                NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, primitive.Pending, drawn.Packets);
                NativeEffectMemory = memory with { PrimitiveProcess = written.Process.Publish50FDF0(CommonAddress(written.Process), null) };
                graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            }

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
