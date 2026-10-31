using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleGraphicsState Graphics, NativeBattleEffect315Frame Frame) DrawNativeEffect315Body(NativeBattleGraphicsState graphics, NativeBattleEffect315Frame frame, uint temporaryAddress, uint modelAddress)
    {
        uint delta = unchecked(modelAddress - 0x1D972C0);
        if (delta % 156 != 0 || delta / 156 >= (uint)graphics.Models.Length)
        {
            throw new InvalidDataException("Effect315 body requires its actual physical target model.");
        }
        int actor = checked((int)(delta / 156));
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, actor);
        NativeBattlePilotVisualIdentity? bodyIdentity = frame.PilotIdentity;
        int geometryPass = 0;
        NativeBattlePoseSource pose = resource.PoseSource ?? throw new NotSupportedException("Effect315 body requires its current target skeleton.");
        _ = resource.LinkedGeometry ?? throw new NotSupportedException("Effect315 body requires its observed linked geometry presence.");
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Effect315 body requires its current CPU resource owner.");
        int scratchLength = checked((cpu.ScratchMaximum ?? throw new NotSupportedException("Effect315 body requires its current renderer vertex capacity.")) * 8);
        uint scratchAddress = checked(0x196B018u - (uint)scratchLength);
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Effect315 body requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Effect315 body requires its current projection.");
        NativeBattleFileStaging initialStaging = frame.Staging ?? throw new NotSupportedException("Effect315 body requires its current temporary owner.");
        NativeBattleEffectArena primitiveFlags = NativeDeformedBodyView(initialStaging, 0x1D969A8, 4);
        NativeBattleEffect315BodyFrame body = new(frame.Memory, initialStaging, graphics.Models[actor], NativeDeformedBodyView(initialStaging, temporaryAddress, 156), CommonCursor(frame.Memory));
        NativeBattleEffect315BodyServices services = new(Shadow, Compose, DrawSource, Restore)
        {
            DrawLinkedAndDeliver = body.Model.LinkedTransform is null ? null : DrawLinked
        };
        body = NativeBattleEffect315BodyDrawing.DrawOuter(body, camera.CapturePackedView1D97778(), scratchAddress, primitiveFlags, services);
        graphics = graphics with { Models = graphics.Models.SetItem(actor, body.Model) };
        frame = frame with { Memory = body.Memory, Staging = NativeBattleTemporaryMemory.PublishView(body.Staging, temporaryAddress, body.Temporary156, 156) };
        return (graphics, frame);

        uint CommonCursor(NativeBattleEffectMemory memory) => checked((uint)(memory.PrimitiveProcess?.ByteCursor ?? throw new NotSupportedException("Effect315 body requires its selected ordinary COMMON cursor.")));

        NativeBattleEffect315BodyFrame Deliver(NativeBattleEffect315BodyFrame current, NativeBattleGfGeometryBatch batch)
        {
            batch = NativeBattleEffect32Presentation.Depths(batch);
            if (current.CommonCursor != CommonCursor(current.Memory))
            {
                throw new InvalidDataException("Effect315 body lost its selected ordinary COMMON cursor coordinate.");
            }
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect315 body requires its current ordering table.");
            NativeBattlePrimitiveProcess packetOwner = current.Memory.PrimitiveProcess!;
            frame = frame with { Effect = frame.Effect with { PresentationPackets = NativeBattleEffectPresentationPackets.Record(frame.Effect.PresentationPackets, packetOwner.PacketBank!, packetOwner.PacketBuffer, packetOwner.ByteCursor!.Value, batch) } };
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketDelivery.Deliver(current.Memory.PrimitiveProcess!, primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            NativeBattleEffectMemory memory = current.Memory with { PrimitiveProcess = written.Process };
            return current with { Memory = memory, CommonCursor = CommonCursor(memory) };
        }

        NativeBattleEffect315BodyFrame Shadow(NativeBattleEffect315BodyFrame current)
        {
            var shadowSource = definition.NativeGraphics?.Rules.ShadowSource ?? throw new NotSupportedException("Effect315 shadow requires its original template.");
            NativeBattleShadowDrawStep shadow = NativeBattleShadowDrawing.Prepare5088A0(current.Staging, new(current.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect315 shadow requires its current GTE matrix."), current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect315 shadow requires its current GTE arithmetic.")), current.Model, projection, current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect315 shadow requires its depth controls."), shadowSource, definition.NativeGraphics?.PrimitiveCapabilities?.GouraudAlpha ?? throw new NotSupportedException("Effect315 shadow requires its actual primitive capabilities."), 4121, 16, bodyIdentity is { } shadowOwner ? shadowOwner with { ResourceGeneration = NativeBattlePilotVisualLifetimes.EpochFor(shadowSource), Primitive = unchecked(shadowOwner.Primitive * 397 + 0x5088) } : null);
            current = current with { Memory = current.Memory with { GeometryRegisters = shadow.Registers.Geometry, ScriptedColorRegisters = shadow.Registers.Colors }, Staging = NativeBattleTemporaryMemory.PublishView(shadow.Staging, shadow.TemporaryAddress, shadow.Temporary78, 120) };
            current = Deliver(current, shadow.Batch);
            return current with { Staging = NativeBattleTemporaryMemory.Release5082D0(current.Staging, 120) };
        }

        NativeBattleEffect315BodyFrame Compose(NativeBattleEffect315BodyFrame current, NativeBattlePackedMatrix32 basis) => current with { Model = NativeBattleBodyBoneMatrices.Compose5095B0(current.Model, basis.ReadMatrix()) };

        NativeBattleEffect315BodyFrame Restore(NativeBattleEffect315BodyFrame current)
        {
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging, 88);
            NativeBattleBodyBonePreparation restored = NativeBattleBodyBoneTemporary.Prepare508C90(current.Model, pose, NativeDeformedBodyView(allocation.Staging, allocation.Address, 88));
            NativeBattleFileStaging published = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, restored.Temporary58, 88);
            return current with { Model = restored.Model, Staging = NativeBattleTemporaryMemory.Release5082D0(published, 88) };
        }

        NativeBattleEffect315BodyFrame DrawSource(NativeBattleEffect315BodyFrame current)
        {
            return DrawGeometry(current, NativeDeformedGeometryPointer(Bridge(current), false), NativeBattleBodyBoneMatrices.Require(current.Model, pose));
        }

        NativeBattleEffect315BodyFrame DrawLinked(NativeBattleEffect315BodyFrame current, NativeBattlePackedMatrix32 basis)
        {
            NativeBattlePoseSource linkedPose = resource.LinkedPoseSource ?? throw new NotSupportedException("Effect315 linked body requires its current skeleton.");
            NativeBattleBodyBoneMatrices local = current.Model.CurrentLinkedBoneMatrices ?? throw new NotSupportedException("Effect315 linked body requires its current bone publication.");
            _ = NativeBattleBodyBoneMatrices.Require(current.Model with { CurrentBodyBoneMatrices = local }, linkedPose);
            ImmutableArray<NativeBattleActionMatrix> composed = NativeBattlePoseMatrices.ComposeStageCamera(local.Matrices, basis.ReadMatrix(), 0);
            current = current with { Model = current.Model with { CurrentLinkedBoneMatrices = local with { Matrices = composed } } };
            current = DrawGeometry(current, NativeDeformedGeometryPointer(Bridge(current), true), composed);
            NativeBattleSequenceState sequence = current.Model.SequenceState ?? throw new NotSupportedException("Effect315 linked body requires its actual decoded pose.");
            NativeBattleDecodedPose decoded = NativeBattlePoseMatrices.ReadLinkedDecodedPose(sequence, linkedPose);
            ImmutableArray<NativeBattleActionMatrix> restored = NativeBattlePoseMatrices.StageBoneMatrices(linkedPose, decoded);
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging, 88);
            NativeBattleEffectArena temporary = NativeBattleBodyBoneTemporary.Prepare508C90(linkedPose, decoded, restored, NativeDeformedBodyView(allocation.Staging, allocation.Address, 88));
            NativeBattleFileStaging published = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, temporary, 88);
            return current with { Model = current.Model with { CurrentLinkedBoneMatrices = local with { Matrices = restored } }, Staging = NativeBattleTemporaryMemory.Release5082D0(published, 88) };
        }

        NativeBattleEffect315BodyFrame DrawGeometry(NativeBattleEffect315BodyFrame current, NativeBattleResourcePointer pointer, ImmutableArray<NativeBattleActionMatrix> matrices)
        {
            uint sourceAddress = pointer.Cpu?.PhysicalAddress ?? pointer.StagingAddress ?? throw new NotSupportedException("Effect315 body requires its addressed current geometry.");
            NativeBattlePilotVisualIdentity? geometryIdentity = NativeBattleEffect32Presentation.Resource(bodyIdentity is { } owner ? owner with { ResourceGeneration = NativeBattleEffect32Presentation.ResourceEpoch(pointer) } : null, sourceAddress, geometryPass++);
            ImmutableArray<NativeBattlePackedMatrix32> bones = matrices.Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix, null)).ToImmutableArray();
            NativeBattleEffectArena scratch = NativeDeformedBodyView(current.Staging, scratchAddress, scratchLength);
            NativeBattleEffect315BodyDrawingStep drawn = NativeBattleEffect315BodyDrawing.DrawBodyWithPresentation(current.Memory, current.Temporary156, ReadGeometry, sourceAddress, bones, camera.CapturePackedView1D97778(), projection, current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect315 body requires its current projection controls."), scratch, scratchAddress, scratchLength, pilotIdentity: geometryIdentity);
            current = current with { Memory = drawn.Memory, Temporary156 = drawn.Temporary156, Staging = NativeBattleTemporaryMemory.PublishView(current.Staging, scratchAddress, drawn.Scratch, scratchLength) };
            uint start = current.CommonCursor;
            int allocated = 0;
            foreach (NativeBattleGfGeometryAttempt attempt in drawn.Batch.Attempts)
            {
                if (attempt.ByteOffsetRelative != allocated)
                {
                    throw new InvalidDataException("Effect315 body packet order lost its current ordinary cursor.");
                }
                if (attempt.Primitive is not null)
                {
                    allocated = checked(allocated + attempt.PacketLength);
                }
            }
            if (allocated != drawn.Batch.AllocationBytes)
            {
                throw new InvalidDataException("Effect315 body packet allocation differs from its ordinary cursor advance.");
            }
            current = Deliver(current, drawn.Batch);
            if (current.CommonCursor != start + drawn.Batch.AllocationBytes)
            {
                throw new InvalidDataException("Effect315 body packet allocation differs from its ordinary cursor advance.");
            }
            return current with { Staging = NativeBattleTemporaryMemory.PublishView(current.Staging, temporaryAddress, current.Temporary156, 156) };

            NativeBattleEffectArena ReadGeometry(uint address, int length)
            {
                long offset = (long)address - sourceAddress;
                if (offset < 0 || offset > int.MaxValue)
                {
                    throw new InvalidDataException("Effect315 body read leaves its current geometry.");
                }
                return NativeBattleEffectArena.Clear().Write(0, ReadNativeDeformedGeometry(Bridge(current), pointer, (int)offset, length));
            }
        }

        NativeDeformedBodyBridge Bridge(NativeBattleEffect315BodyFrame current) => new(current.Memory, frame.Effect, frame.Random, graphics, current.Model, current.Staging, current.Temporary156, temporaryAddress);
    }
}
