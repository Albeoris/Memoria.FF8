using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattlePoisonGas171Frame Frame, NativeBattleGraphicsState Graphics) DrawNativePoisonGas171Body(NativeBattlePoisonGas171Frame frame, NativeBattleGraphicsState graphics, byte actor, uint temporaryAddress, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, actor);
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Poison Gas171 target requires its current skeleton.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Poison Gas171 target requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Poison Gas171 target requires its published projection.");
        NativeBattlePackedMatrix32 packedCamera = camera.CapturePackedView1D97778();
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Poison Gas171 target requires its current CPU resource owner.");
        int scratchLength = checked((cpu.ScratchMaximum ?? throw new NotSupportedException("Poison Gas171 target requires its loaded vertex capacity.")) * 8);
        uint scratchAddress = checked(0x196B018u - (uint)scratchLength);
        NativeBattleFileStaging staging = frame.Staging ?? throw new NotSupportedException("Poison Gas171 target requires its addressed temporary owner.");
        NativeBattleEffectArena primitiveFlags = NativePoisonGas171View(staging, 0x1D969A8, 4);
        bool linked = graphics.Models[actor].LinkedTransform is not null;
        NativeBattlePoisonGas171ModelFrame body = new(frame.Memory, staging, graphics.Models[actor], NativePoisonGas171View(staging, temporaryAddress, 176));
        NativeBattlePoisonGas171ModelServices services = new(Shadow, Compose, DrawBody, Restore, DrawLinked);
        body = NativeBattlePoisonGas171ModelDrawing.DrawOuter(body, packedCamera, scratchAddress, primitiveFlags, linked, services);
        graphics = graphics with { Models = graphics.Models.SetItem(actor, body.Model) };
        return (frame with { Memory = body.Memory, Staging = NativeBattleTemporaryMemory.PublishView(body.Staging, temporaryAddress, body.Temporary176, 176) }, graphics);

        NativeBattlePoisonGas171ModelFrame Shadow(NativeBattlePoisonGas171ModelFrame current)
        {
            NativeBattleShadowDrawStep shadow = NativeBattleShadowDrawing.Prepare5088A0(current.Staging, new(current.Memory.GeometryRegisters ?? throw new NotSupportedException("Poison Gas171 shadow requires its current GTE matrix."), current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Poison Gas171 shadow requires its current GTE data.")), current.Model, projection, current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Poison Gas171 shadow requires its depth controls."), definition.NativeGraphics?.Rules.ShadowSource ?? throw new NotSupportedException("Poison Gas171 shadow requires its original template."), definition.NativeGraphics?.PrimitiveCapabilities?.GouraudAlpha ?? throw new NotSupportedException("Poison Gas171 shadow requires its primitive capabilities."), 4121, 16, pilotIdentity: pilotIdentity is { } shadowIdentity ? shadowIdentity with { Primitive = -1 } : null);
            current = current with { Memory = current.Memory with { GeometryRegisters = shadow.Registers.Geometry, ScriptedColorRegisters = shadow.Registers.Colors }, Staging = shadow.Staging };
            current = current with { Staging = NativeBattleTemporaryMemory.PublishView(current.Staging, shadow.TemporaryAddress, shadow.Temporary78, 0x78) };
            current = AppendCommon(current, shadow.Batch);
            return current with { Staging = NativeBattleTemporaryMemory.Release5082D0(current.Staging, 0x78) };
        }

        NativeBattlePoisonGas171ModelFrame Compose(NativeBattlePoisonGas171ModelFrame current, NativeBattleActionMatrix world) => current with { Model = NativeBattleBodyBoneMatrices.Compose5095B0(current.Model, world) };

        NativeBattlePoisonGas171ModelFrame DrawBody(NativeBattlePoisonGas171ModelFrame current)
        {
            NativeBattleResourcePointer pointer = NativeDeformedGeometryPointer(Bridge(current), false);
            return DrawSource(current, pointer, NativeBattleBodyBoneMatrices.Require(current.Model, source));
        }

        NativeBattlePoisonGas171ModelFrame Restore(NativeBattlePoisonGas171ModelFrame current)
        {
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging, 88);
            NativeBattleBodyBonePreparation restored = NativeBattleBodyBoneTemporary.Prepare508C90(current.Model, source, NativePoisonGas171View(allocation.Staging, allocation.Address, 88));
            NativeBattleFileStaging published = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, restored.Temporary58, 88);
            return current with { Model = restored.Model, Staging = NativeBattleTemporaryMemory.Release5082D0(published, 88) };
        }

        NativeBattlePoisonGas171ModelFrame DrawLinked(NativeBattlePoisonGas171ModelFrame current)
        {
            NativeBattlePoseSource linkedSource = resource.LinkedPoseSource ?? throw new NotSupportedException("Poison Gas171 linked target requires its current skeleton.");
            NativeBattleBodyBoneMatrices local = current.Model.CurrentLinkedBoneMatrices ?? throw new NotSupportedException("Poison Gas171 linked target requires its current bone publication.");
            _ = NativeBattleBodyBoneMatrices.Require(current.Model with { CurrentBodyBoneMatrices = local }, linkedSource);
            (byte[] matrixBytes, bool[] matrixKnown) = current.Temporary176.ReadSnapshot(0x90, 32);
            NativeBattleActionMatrix world = new NativeBattlePackedMatrix32(matrixBytes.ToImmutableArray(), matrixKnown.ToImmutableArray()).ReadMatrix();
            ImmutableArray<NativeBattleActionMatrix> composed = NativeBattlePoseMatrices.ComposeStageCamera(local.Matrices, world, 0);
            current = current with { Model = current.Model with { CurrentLinkedBoneMatrices = local with { Matrices = composed } } };
            current = DrawLinkedSource(current, NativeDeformedGeometryPointer(Bridge(current), true), composed, linkedSource.BoneCount);
            NativeBattleSequenceState sequence = current.Model.SequenceState ?? throw new NotSupportedException("Poison Gas171 linked target requires its decoded pose.");
            NativeBattleDecodedPose decoded = NativeBattlePoseMatrices.ReadLinkedDecodedPose(sequence, linkedSource);
            ImmutableArray<NativeBattleActionMatrix> restored = NativeBattlePoseMatrices.StageBoneMatrices(linkedSource, decoded);
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging, 88);
            NativeBattleEffectArena temporary = NativeBattleBodyBoneTemporary.Prepare508C90(linkedSource, decoded, restored, NativePoisonGas171View(allocation.Staging, allocation.Address, 88));
            NativeBattleFileStaging published = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, temporary, 88);
            return current with { Model = current.Model with { CurrentLinkedBoneMatrices = local with { Matrices = restored } }, Staging = NativeBattleTemporaryMemory.Release5082D0(published, 88) };
        }

        NativeBattlePoisonGas171ModelFrame DrawSource(NativeBattlePoisonGas171ModelFrame current, NativeBattleResourcePointer pointer, ImmutableArray<NativeBattleActionMatrix> matrices)
        {
            uint sourceAddress = pointer.Cpu?.PhysicalAddress ?? pointer.StagingAddress ?? throw new NotSupportedException("Poison Gas171 target requires its physical geometry address.");
            ImmutableArray<NativeBattlePackedMatrix32> bones = matrices.Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix, null)).ToImmutableArray();
            NativeBattleEffectArena scratch = NativePoisonGas171View(current.Staging, scratchAddress, scratchLength);
            NativeBattlePoisonGas171ModelStep drawn = NativeBattlePoisonGas171ModelDrawing.DrawBody(current.Memory, current.Temporary176, scratch, Read, sourceAddress, bones, packedCamera, projection, current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Poison Gas171 target requires its current depth controls."), scratchAddress, scratchLength, pilotIdentity: pilotIdentity is { } bodyIdentity ? bodyIdentity with { ResourceGeneration = sourceAddress } : null);
            current = current with { Memory = drawn.Memory, Temporary176 = drawn.Temporary176, Staging = NativeBattleTemporaryMemory.PublishView(current.Staging, scratchAddress, drawn.VertexScratch, scratchLength) };
            current = current with { Staging = NativeBattleTemporaryMemory.PublishView(current.Staging, temporaryAddress, current.Temporary176, 176) };
            return AppendCommon(current, drawn.Batch);

            byte[] Read(int offset, int length)
            {
                return ReadNativeDeformedGeometry(Bridge(current), pointer, offset, length);
            }
        }

        NativeBattlePoisonGas171ModelFrame DrawLinkedSource(NativeBattlePoisonGas171ModelFrame current, NativeBattleResourcePointer pointer, ImmutableArray<NativeBattleActionMatrix> matrices, int boneCount)
        {
            NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Poison Gas171 linked body requires its current renderer process.");
            NativeBattleGraphicsDefinition rules = definition.NativeGraphics ?? throw new NotSupportedException("Poison Gas171 linked body requires its current graphics rules.");
            NativeBattlePrimitiveCapabilities capabilities = rules.PrimitiveCapabilities ?? throw new NotSupportedException("Poison Gas171 linked body requires its actual renderer capabilities.");
            var gate = process.Enter5099D0(capabilities.HardwareEnabled ?? throw new NotSupportedException("Poison Gas171 linked body requires its current hardware capability."));
            process = gate.Process;
            if (!gate.Draw)
            {
                return current with { Memory = current.Memory with { PrimitiveProcess = process } };
            }
            ImmutableArray<NativeBattleRawMesh> geometry = NativeBattleRawGeometryReader.Read(Read, boneCount);
            uint color = current.Model.Color40 ?? throw new NotSupportedException("Poison Gas171 linked body requires its actual full color.");
            process = process.PublishModelColor(color);
            (byte[] retained, _) = current.Staging.ReadSnapshot(scratchAddress, scratchLength);
            NativeBattleRawModelBodyDraw drawn = NativeBattleRawModelBodyDrawing.Draw5099D0(geometry, matrices, projection, color, uint.MaxValue, retained.ToImmutableArray(), rules.Rules.SummonColors ?? throw new NotSupportedException("Poison Gas171 linked body requires its current color arithmetic."), 4, new(0, 0, 320, 216), 17, null, pilotIdentity: pilotIdentity is { } linkedIdentity ? linkedIdentity with { Primitive = unchecked(linkedIdentity.Primitive * 397 + 1), ResourceGeneration = pointer.Cpu?.PhysicalAddress ?? pointer.StagingAddress ?? throw new NotSupportedException("Poison Gas linked capture requires its actual source.") } : null);
            if (drawn.ProjectedBytesWritten != 0)
            {
                current = current with { Staging = current.Staging.Write(scratchAddress, drawn.ProjectedScratch.AsSpan().Slice(0, drawn.ProjectedBytesWritten)) };
            }
            current = AppendCommon(current with { Memory = current.Memory with { PrimitiveProcess = process } }, drawn.Packets);
            process = current.Memory.PrimitiveProcess!;
            uint bank = process.PacketBank switch { "off_B6D070" => 0x1901018u, "off_B6D094" => 0x199B018u, _ => throw new NotSupportedException("Poison Gas171 linked body requires its actual COMMON packet bank.") };
            uint cursor = checked(bank + (uint)process.PacketBuffer * (uint)process.CommonPacketNominalCapacity + (uint)(process.ByteCursor ?? throw new NotSupportedException("Poison Gas171 linked body requires its current COMMON cursor.")));
            return current with { Memory = current.Memory with { PrimitiveProcess = process.Publish50FDF0(cursor, null) } };

            byte[] Read(int offset, int length)
            {
                return ReadNativeDeformedGeometry(Bridge(current), pointer, offset, length);
            }
        }

        NativeDeformedBodyBridge Bridge(NativeBattlePoisonGas171ModelFrame current) => new(current.Memory, frame.Effect, frame.Random, graphics, current.Model, current.Staging, current.Temporary176, temporaryAddress);

        NativeBattlePoisonGas171ModelFrame AppendCommon(NativeBattlePoisonGas171ModelFrame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Poison Gas171 target requires its current COMMON ordering table.");
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Poison Gas171 target requires its current COMMON packet owner."), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = current.Memory with { PrimitiveProcess = written.Process } };
        }
    }

    private static NativeBattleEffectArena NativePoisonGas171View(NativeBattleFileStaging staging, uint address, int length)
    {
        return NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }

}
