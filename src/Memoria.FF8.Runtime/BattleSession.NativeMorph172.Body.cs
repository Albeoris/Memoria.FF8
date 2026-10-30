using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleMorph172Frame Frame, NativeBattleGraphicsState Graphics) DrawNativeMorph172Body(NativeBattleMorph172Frame frame, NativeBattleGraphicsState graphics, byte actor, uint temporaryAddress, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, actor);
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Morph172 target requires its current skeleton.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Morph172 target requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Morph172 target requires its published projection.");
        NativeBattlePackedMatrix32 packedCamera = camera.CapturePackedView1D97778();
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Morph172 target requires its current CPU resource owner.");
        int scratchLength = checked((cpu.ScratchMaximum ?? throw new NotSupportedException("Morph172 target requires its loaded vertex capacity.")) * 8);
        uint scratchAddress = checked(0x196B018u - (uint)scratchLength);
        NativeBattleFileStaging staging = frame.Staging ?? throw new NotSupportedException("Morph172 target requires its addressed temporary owner.");
        NativeBattleEffectArena primitiveFlags = NativeMorph172View(staging, 0x1D969A8, 4);
        bool linked = graphics.Models[actor].LinkedTransform is not null;
        NativeBattleMorph172ModelFrame body = new(frame.Memory, staging, graphics.Models[actor], NativeMorph172View(staging, temporaryAddress, 220));
        NativeBattleMorph172ModelServices services = new(Shadow, Compose, DrawBody, Restore, DrawLinked);
        body = NativeBattleMorph172ModelDrawing.DrawOuter(body, packedCamera, scratchAddress, primitiveFlags, linked, services);
        graphics = graphics with { Models = graphics.Models.SetItem(actor, body.Model) };
        return (frame with { Memory = body.Memory, Staging = NativeBattleTemporaryMemory.PublishView(body.Staging, temporaryAddress, body.Temporary220, 220) }, graphics);

        NativeBattleMorph172ModelFrame Shadow(NativeBattleMorph172ModelFrame current)
        {
            NativeBattleShadowDrawStep shadow = NativeBattleShadowDrawing.Prepare5088A0(current.Staging, new(current.Memory.GeometryRegisters ?? throw new NotSupportedException("Morph172 shadow requires its current GTE matrix."), current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Morph172 shadow requires its current GTE data.")), current.Model, projection, current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Morph172 shadow requires its depth controls."), definition.NativeGraphics?.Rules.ShadowSource ?? throw new NotSupportedException("Morph172 shadow requires its original template."), definition.NativeGraphics?.PrimitiveCapabilities?.GouraudAlpha ?? throw new NotSupportedException("Morph172 shadow requires its primitive capabilities."), 4121, 16, NativeBattleMorph172Presentation.Domain(pilotIdentity, uint.MaxValue, 2));
            current = current with { Memory = current.Memory with { GeometryRegisters = shadow.Registers.Geometry, ScriptedColorRegisters = shadow.Registers.Colors }, Staging = shadow.Staging };
            current = current with { Staging = NativeBattleTemporaryMemory.PublishView(current.Staging, shadow.TemporaryAddress, shadow.Temporary78, 0x78) };
            current = AppendCommon(current, shadow.Batch);
            return current with { Staging = NativeBattleTemporaryMemory.Release5082D0(current.Staging, 0x78) };
        }

        NativeBattleMorph172ModelFrame Compose(NativeBattleMorph172ModelFrame current, NativeBattleActionMatrix world) => current with { Model = NativeBattleBodyBoneMatrices.Compose5095B0(current.Model, world) };

        NativeBattleMorph172ModelFrame DrawBody(NativeBattleMorph172ModelFrame current)
        {
            NativeBattleResourcePointer pointer = NativeDeformedGeometryPointer(Bridge(current), false);
            return DrawSource(current, pointer, NativeBattleBodyBoneMatrices.Require(current.Model, source));
        }

        NativeBattleMorph172ModelFrame Restore(NativeBattleMorph172ModelFrame current)
        {
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging, 88);
            NativeBattleBodyBonePreparation restored = NativeBattleBodyBoneTemporary.Prepare508C90(current.Model, source, NativeMorph172View(allocation.Staging, allocation.Address, 88));
            NativeBattleFileStaging published = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, restored.Temporary58, 88);
            return current with { Model = restored.Model, Staging = NativeBattleTemporaryMemory.Release5082D0(published, 88) };
        }

        NativeBattleMorph172ModelFrame DrawLinked(NativeBattleMorph172ModelFrame current)
        {
            NativeBattlePoseSource linkedSource = resource.LinkedPoseSource ?? throw new NotSupportedException("Morph172 linked target requires its current skeleton.");
            NativeBattleBodyBoneMatrices local = current.Model.CurrentLinkedBoneMatrices ?? throw new NotSupportedException("Morph172 linked target requires its current bone publication.");
            _ = NativeBattleBodyBoneMatrices.Require(current.Model with { CurrentBodyBoneMatrices = local }, linkedSource);
            (byte[] matrixBytes, bool[] matrixKnown) = current.Temporary220.ReadSnapshot(0xBC, 32);
            NativeBattleActionMatrix world = new NativeBattlePackedMatrix32(matrixBytes.ToImmutableArray(), matrixKnown.ToImmutableArray()).ReadMatrix();
            ImmutableArray<NativeBattleActionMatrix> composed = NativeBattlePoseMatrices.ComposeStageCamera(local.Matrices, world, 0);
            current = current with { Model = current.Model with { CurrentLinkedBoneMatrices = local with { Matrices = composed } } };
            current = DrawLinkedSource(current, NativeDeformedGeometryPointer(Bridge(current), true), composed, linkedSource.BoneCount);
            NativeBattleSequenceState sequence = current.Model.SequenceState ?? throw new NotSupportedException("Morph172 linked target requires its decoded pose.");
            NativeBattleDecodedPose decoded = NativeBattlePoseMatrices.ReadLinkedDecodedPose(sequence, linkedSource);
            ImmutableArray<NativeBattleActionMatrix> restored = NativeBattlePoseMatrices.StageBoneMatrices(linkedSource, decoded);
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging, 88);
            NativeBattleEffectArena temporary = NativeBattleBodyBoneTemporary.Prepare508C90(linkedSource, decoded, restored, NativeMorph172View(allocation.Staging, allocation.Address, 88));
            NativeBattleFileStaging published = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, temporary, 88);
            return current with { Model = current.Model with { CurrentLinkedBoneMatrices = local with { Matrices = restored } }, Staging = NativeBattleTemporaryMemory.Release5082D0(published, 88) };
        }

        NativeBattleMorph172ModelFrame DrawSource(NativeBattleMorph172ModelFrame current, NativeBattleResourcePointer pointer, ImmutableArray<NativeBattleActionMatrix> matrices)
        {
            uint sourceAddress = pointer.Cpu?.PhysicalAddress ?? pointer.StagingAddress ?? throw new NotSupportedException("Morph172 target requires its physical geometry address.");
            ImmutableArray<NativeBattlePackedMatrix32> bones = matrices.Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix, null)).ToImmutableArray();
            NativeBattleEffectArena scratch = NativeMorph172View(current.Staging, scratchAddress, scratchLength);
            NativeBattleMorph172ModelStep drawn = NativeBattleMorph172ModelDrawing.DrawBody(current.Memory, current.Temporary220, scratch, Read, sourceAddress, bones, packedCamera, projection, current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Morph172 target requires its current depth controls."), scratchAddress, scratchLength, definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Morph172 body requires its current color arithmetic."), pilotIdentity: NativeBattleMorph172Presentation.Domain(pilotIdentity, sourceAddress, 0));
            current = current with { Memory = drawn.Memory, Temporary220 = drawn.Temporary220, Staging = NativeBattleTemporaryMemory.PublishView(current.Staging, scratchAddress, drawn.VertexScratch, scratchLength) };
            current = current with { Staging = NativeBattleTemporaryMemory.PublishView(current.Staging, temporaryAddress, current.Temporary220, 220) };
            return AppendCommon(current, drawn.Batch);

            byte[] Read(int offset, int length)
            {
                return ReadNativeDeformedGeometry(Bridge(current), pointer, offset, length);
            }
        }

        NativeBattleMorph172ModelFrame DrawLinkedSource(NativeBattleMorph172ModelFrame current, NativeBattleResourcePointer pointer, ImmutableArray<NativeBattleActionMatrix> matrices, int boneCount)
        {
            NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Morph172 linked body requires its current renderer process.");
            NativeBattleGraphicsDefinition rules = definition.NativeGraphics ?? throw new NotSupportedException("Morph172 linked body requires its current graphics rules.");
            NativeBattlePrimitiveCapabilities capabilities = rules.PrimitiveCapabilities ?? throw new NotSupportedException("Morph172 linked body requires its actual renderer capabilities.");
            var gate = process.Enter5099D0(capabilities.HardwareEnabled ?? throw new NotSupportedException("Morph172 linked body requires its current hardware capability."));
            process = gate.Process;
            if (!gate.Draw)
            {
                return current with { Memory = current.Memory with { PrimitiveProcess = process } };
            }
            ImmutableArray<NativeBattleRawMesh> geometry = NativeBattleRawGeometryReader.Read(Read, boneCount);
            uint color = current.Model.Color40 ?? throw new NotSupportedException("Morph172 linked body requires its actual full color.");
            process = process.PublishModelColor(color);
            (byte[] retained, _) = current.Staging.ReadSnapshot(scratchAddress, scratchLength);
            NativeBattleRawModelBodyDraw drawn = NativeBattleRawModelBodyDrawing.Draw5099D0(geometry, matrices, projection, color, uint.MaxValue, retained.ToImmutableArray(), rules.Rules.SummonColors ?? throw new NotSupportedException("Morph172 linked body requires its current color arithmetic."), 4, new(0, 0, 320, 216), 17, null, pilotIdentity: pilotIdentity.HasValue ? NativeBattleMorph172Presentation.Domain(pilotIdentity, pointer.Cpu?.PhysicalAddress ?? pointer.StagingAddress ?? throw new NotSupportedException("Morph172 capture requires its actual linked source."), 1) : null);
            if (drawn.ProjectedBytesWritten != 0)
            {
                current = current with { Staging = current.Staging.Write(scratchAddress, drawn.ProjectedScratch.AsSpan().Slice(0, drawn.ProjectedBytesWritten)) };
            }
            current = AppendCommon(current with { Memory = current.Memory with { PrimitiveProcess = process } }, drawn.Packets);
            process = current.Memory.PrimitiveProcess!;
            uint bank = process.PacketBank switch { "off_B6D070" => 0x1901018u, "off_B6D094" => 0x199B018u, _ => throw new NotSupportedException("Morph172 linked body requires its actual COMMON packet bank.") };
            uint cursor = checked(bank + (uint)process.PacketBuffer * (uint)process.CommonPacketNominalCapacity + (uint)(process.ByteCursor ?? throw new NotSupportedException("Morph172 linked body requires its current COMMON cursor.")));
            return current with { Memory = current.Memory with { PrimitiveProcess = process.Publish50FDF0(cursor, null) } };

            byte[] Read(int offset, int length)
            {
                return ReadNativeDeformedGeometry(Bridge(current), pointer, offset, length);
            }
        }

        NativeDeformedBodyBridge Bridge(NativeBattleMorph172ModelFrame current) => new(current.Memory, frame.Effect, frame.Random, graphics, current.Model, current.Staging, current.Temporary220, temporaryAddress);

        NativeBattleMorph172ModelFrame AppendCommon(NativeBattleMorph172ModelFrame current, NativeBattleGfGeometryBatch batch)
        {
            batch = pilotIdentity.HasValue ? NativeBattleMorph172Presentation.PreserveNativeDepths(batch) : batch;
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Morph172 target requires its current COMMON ordering table.");
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Morph172 target requires its current COMMON packet owner."), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = current.Memory with { PrimitiveProcess = written.Process } };
        }
    }

    private static NativeBattleEffectArena NativeMorph172View(NativeBattleFileStaging staging, uint address, int length)
    {
        return NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }

}
