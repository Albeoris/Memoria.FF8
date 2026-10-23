using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleGraphicsState Graphics, NativeBattleEffect283Frame Frame, short MaximumDeviation) DrawNativeEffect283Body(NativeBattleGraphicsState graphics, NativeBattleEffect283Frame frame, uint modelAddress, short centerX, short centerY, short threshold, int bankIndex, int fade, NativeBattleEffect283Resources resources)
    {
        uint delta = unchecked(modelAddress - 0x1D972C0);
        if (delta % 156 != 0 || delta / 156 >= (uint)graphics.Models.Length)
        {
            throw new InvalidDataException("Effect283 body requires its actual physical target model.");
        }
        int actor = checked((int)(delta / 156));
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, actor);
        NativeBattlePoseSource pose = resource.PoseSource ?? throw new NotSupportedException("Effect283 body requires its current target skeleton.");
        _ = resource.LinkedGeometry ?? throw new NotSupportedException("Effect283 body requires its observed linked geometry presence.");
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Effect283 body requires its current CPU resource owner.");
        int scratchLength = checked((cpu.ScratchMaximum ?? throw new NotSupportedException("Effect283 body requires its current renderer vertex capacity.")) * 8);
        uint scratchAddress = checked(0x196B018u - (uint)scratchLength);
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Effect283 body requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Effect283 body requires its current projection.");
        ImmutableArray<byte> distance = Enumerable.Range(0, NativeBattleEffect283Profile.DistanceLength).Select(resources.ReadDistance).ToImmutableArray();
        ImmutableArray<ushort> factors = Enumerable.Range(0, 128).Select(index => resources.ReadWarpFactor(bankIndex, index)).ToImmutableArray();
        uint bankAddress = checked(NativeBattleEffect283Profile.WarpTablesAddress + (uint)((bankIndex - 1) * 256));
        NativeBattleTemporaryAllocation allocated = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging ?? throw new NotSupportedException("Effect283 body requires its current temporary owner."), 124);
        NativeBattleEffect283BodyFrame body = new(frame.Memory, allocated.Staging, graphics.Models[actor], NativeDeformedBodyView(allocated.Staging, allocated.Address, 124), CommonCursor(frame.Memory));
        NativeBattleEffect283BodyServices services = new(Shadow, Compose, DrawSource, Restore)
        {
            DrawLinkedAndDeliver = body.Model.LinkedTransform is null ? null : DrawLinked
        };
        body = NativeBattleEffect283BodyDrawing.DrawOuter(body, camera.CapturePackedView1D97778(), centerX, centerY, threshold, bankAddress, fade, scratchAddress, definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect283 body requires its current color constants."), services);
        graphics = graphics with { Models = graphics.Models.SetItem(actor, body.Model) };
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(body.Staging, allocated.Address, body.Temporary124, 124);
        short maximum = BinaryPrimitives.ReadInt16LittleEndian(body.Temporary124.Read(0x76, 2));
        frame = frame with { Memory = body.Memory, Staging = NativeBattleTemporaryMemory.Release5082D0(staging, 124) };
        return (graphics, frame, maximum);

        uint CommonCursor(NativeBattleEffectMemory memory) => checked((uint)(memory.PrimitiveProcess?.ByteCursor ?? throw new NotSupportedException("Effect283 body requires its selected ordinary COMMON cursor.")));

        NativeBattleEffect283BodyFrame Deliver(NativeBattleEffect283BodyFrame current, NativeBattleGfGeometryBatch batch)
        {
            if (current.CommonCursor != CommonCursor(current.Memory))
            {
                throw new InvalidDataException("Effect283 body lost its selected ordinary COMMON cursor coordinate.");
            }
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect283 body requires its current ordering table.");
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketDelivery.Deliver(current.Memory.PrimitiveProcess!, primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            NativeBattleEffectMemory memory = current.Memory with { PrimitiveProcess = written.Process };
            return current with { Memory = memory, CommonCursor = CommonCursor(memory) };
        }

        NativeBattleEffect283BodyFrame Shadow(NativeBattleEffect283BodyFrame current)
        {
            NativeBattleShadowDrawStep shadow = NativeBattleShadowDrawing.Prepare5088A0(current.Staging, new(current.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect283 shadow requires its current GTE matrix."), current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect283 shadow requires its current GTE arithmetic.")), current.Model, projection, current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect283 shadow requires its depth controls."), definition.NativeGraphics?.Rules.ShadowSource ?? throw new NotSupportedException("Effect283 shadow requires its original template."), definition.NativeGraphics?.PrimitiveCapabilities?.GouraudAlpha ?? throw new NotSupportedException("Effect283 shadow requires its actual primitive capabilities."), 4112, 16);
            current = current with { Memory = current.Memory with { GeometryRegisters = shadow.Registers.Geometry, ScriptedColorRegisters = shadow.Registers.Colors }, Staging = NativeBattleTemporaryMemory.PublishView(shadow.Staging, shadow.TemporaryAddress, shadow.Temporary78, 120) };
            current = Deliver(current, shadow.Batch);
            return current with { Staging = NativeBattleTemporaryMemory.Release5082D0(current.Staging, 120) };
        }

        NativeBattleEffect283BodyFrame Compose(NativeBattleEffect283BodyFrame current, NativeBattlePackedMatrix32 basis) => current with { Model = NativeBattleBodyBoneMatrices.Compose5095B0(current.Model, basis.ReadMatrix()) };

        NativeBattleEffect283BodyFrame Restore(NativeBattleEffect283BodyFrame current)
        {
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging, 88);
            NativeBattleBodyBonePreparation restored = NativeBattleBodyBoneTemporary.Prepare508C90(current.Model, pose, NativeDeformedBodyView(allocation.Staging, allocation.Address, 88));
            NativeBattleFileStaging published = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, restored.Temporary58, 88);
            return current with { Model = restored.Model, Staging = NativeBattleTemporaryMemory.Release5082D0(published, 88) };
        }

        NativeBattleEffect283BodyFrame DrawSource(NativeBattleEffect283BodyFrame current)
        {
            return DrawGeometry(current, NativeDeformedGeometryPointer(Bridge(current), false), NativeBattleBodyBoneMatrices.Require(current.Model, pose));
        }

        NativeBattleEffect283BodyFrame DrawLinked(NativeBattleEffect283BodyFrame current, NativeBattlePackedMatrix32 basis)
        {
            NativeBattlePoseSource linkedPose = resource.LinkedPoseSource ?? throw new NotSupportedException("Effect283 linked body requires its current skeleton.");
            NativeBattleBodyBoneMatrices local = current.Model.CurrentLinkedBoneMatrices ?? throw new NotSupportedException("Effect283 linked body requires its current bone publication.");
            _ = NativeBattleBodyBoneMatrices.Require(current.Model with { CurrentBodyBoneMatrices = local }, linkedPose);
            ImmutableArray<NativeBattleActionMatrix> composed = NativeBattlePoseMatrices.ComposeStageCamera(local.Matrices, basis.ReadMatrix(), 0);
            current = current with { Model = current.Model with { CurrentLinkedBoneMatrices = local with { Matrices = composed } } };
            current = DrawGeometry(current, NativeDeformedGeometryPointer(Bridge(current), true), composed);
            NativeBattleSequenceState sequence = current.Model.SequenceState ?? throw new NotSupportedException("Effect283 linked body requires its actual decoded pose.");
            NativeBattleDecodedPose decoded = NativeBattlePoseMatrices.ReadLinkedDecodedPose(sequence, linkedPose);
            ImmutableArray<NativeBattleActionMatrix> restored = NativeBattlePoseMatrices.StageBoneMatrices(linkedPose, decoded);
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging, 88);
            NativeBattleEffectArena temporary = NativeBattleBodyBoneTemporary.Prepare508C90(linkedPose, decoded, restored, NativeDeformedBodyView(allocation.Staging, allocation.Address, 88));
            NativeBattleFileStaging published = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, temporary, 88);
            return current with { Model = current.Model with { CurrentLinkedBoneMatrices = local with { Matrices = restored } }, Staging = NativeBattleTemporaryMemory.Release5082D0(published, 88) };
        }

        NativeBattleEffect283BodyFrame DrawGeometry(NativeBattleEffect283BodyFrame current, NativeBattleResourcePointer pointer, ImmutableArray<NativeBattleActionMatrix> matrices)
        {
            uint sourceAddress = pointer.Cpu?.PhysicalAddress ?? pointer.StagingAddress ?? throw new NotSupportedException("Effect283 body requires its addressed current geometry.");
            ImmutableArray<NativeBattlePackedMatrix32> bones = matrices.Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix, null)).ToImmutableArray();
            NativeBattleEffectArena scratch = NativeDeformedBodyView(current.Staging, scratchAddress, scratchLength);
            NativeBattleEffect283BodyDrawingStep drawn = NativeBattleEffect283BodyDrawing.DrawBody(current.Memory, current.Temporary124, ReadGeometry, sourceAddress, bones, projection, current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect283 body requires its current projection controls."), scratch, scratchAddress, scratchLength, distance, factors);
            current = current with { Memory = drawn.Memory, Temporary124 = drawn.Temporary124, Staging = NativeBattleTemporaryMemory.PublishView(current.Staging, scratchAddress, drawn.Scratch, scratchLength) };
            uint start = current.CommonCursor;
            foreach (NativeBattleEffect283BodyWrite write in drawn.Batch.Writes)
            {
                if (write.Bank == NativeBattleEffect283BodyBank.ProjectionScratch)
                {
                    continue;
                }
                NativeBattleGfGeometryAttempt attempt = write.Attempt;
                if (current.CommonCursor != start + attempt.ByteOffsetRelative)
                {
                    throw new InvalidDataException("Effect283 body packet order lost its current ordinary cursor.");
                }
                current = Deliver(current, new([attempt with { ByteOffsetRelative = 0 }], attempt.Primitive is null ? 0 : attempt.PacketLength));
            }
            if (current.CommonCursor != start + drawn.Batch.CommonAllocationBytes)
            {
                throw new InvalidDataException("Effect283 body packet allocation differs from its ordinary cursor advance.");
            }
            return current with { Staging = NativeBattleTemporaryMemory.PublishView(current.Staging, allocated.Address, current.Temporary124, 124) };

            NativeBattleEffectArena ReadGeometry(uint address, int length)
            {
                long offset = (long)address - sourceAddress;
                if (offset < 0 || offset > int.MaxValue)
                {
                    throw new InvalidDataException("Effect283 body read leaves its current geometry.");
                }
                return NativeBattleEffectArena.Clear().Write(0, ReadNativeDeformedGeometry(Bridge(current), pointer, (int)offset, length));
            }
        }

        NativeDeformedBodyBridge Bridge(NativeBattleEffect283BodyFrame current) => new(current.Memory, frame.Effect, nativeCrtRandom ?? throw new NotSupportedException("Effect283 body source access requires the current CRT owner."), graphics, current.Model, current.Staging, current.Temporary124, allocated.Address);
    }
}
