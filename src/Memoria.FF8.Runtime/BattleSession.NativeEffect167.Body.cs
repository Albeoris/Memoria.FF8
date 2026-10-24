using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleGraphicsState Graphics, NativeBattleEffect167Frame Frame) DrawNativeEffect167Body(NativeBattleGraphicsState graphics, NativeBattleEffect167Frame frame, uint temporaryAddress, int actor, NativeBattlePackedMatrix32 offsetWorld, NativeBattleEffect167Resources resources)
    {
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, actor);
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Effect167 body requires its current primary skeleton.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Effect167 body requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Effect167 body requires its current projection.");
        NativeBattleFileStaging staging = frame.Staging ?? throw new NotSupportedException("Effect167 body requires its current addressed memory.");
        NativeDeformedBodyBridge current = new(frame.Memory, frame.Effect, frame.Random, graphics, graphics.Models[actor], staging, NativeDeformedBodyView(staging, temporaryAddress, 464), temporaryAddress);
        int scratchLength = checked((NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Effect167 body requires its retained projected capacity.")) * 8);
        uint scratchAddress = checked(0x196B018u - (uint)scratchLength);
        if ((current.Model.Flags0 & 0x20) == 0)
        {
            NativeBattleShadowDrawStep shadow = NativeBattleShadowDrawing.Prepare5088A0(current.Staging, new(current.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect167 shadow requires its current GTE matrix."), current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect167 shadow requires its current arithmetic.")), current.Model, projection, current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect167 shadow requires its depth controls."), definition.NativeGraphics?.Rules.ShadowSource ?? throw new NotSupportedException("Effect167 shadow requires its original source."), definition.NativeGraphics?.PrimitiveCapabilities?.GouraudAlpha ?? throw new NotSupportedException("Effect167 shadow requires its current renderer capability."), 4121, 16);
            current = current with { Memory = current.Memory with { GeometryRegisters = shadow.Registers.Geometry, ScriptedColorRegisters = shadow.Registers.Colors }, Staging = NativeBattleTemporaryMemory.PublishView(shadow.Staging, shadow.TemporaryAddress, shadow.Temporary78, 120) };
            current = Deliver(current, shadow.Batch);
            current = current with { Staging = NativeBattleTemporaryMemory.Release5082D0(current.Staging, 120) };
        }
        NativeBattleActionMatrix view = camera.CapturePackedView1D97778().ReadMatrix();
        NativeBattleActionMatrix world = current.Model.GeometryWorldMatrix ?? throw new NotSupportedException("Effect167 body requires its current world matrix.");
        NativeBattleEffectArena context = PutMatrix(current.Temporary, 0x190, NativeBattleGfMatrices.Compose56C2F0(view, world));
        context = PutMatrix(context, 0x1B0, NativeBattleGfMatrices.Compose56C2F0(view, offsetWorld.ReadMatrix()));
        context = context.Write(4, BitConverter.GetBytes(scratchAddress)).Write(0x10, BitConverter.GetBytes((current.Model.SequenceState ?? throw new NotSupportedException("Effect167 body requires its current object mask.")).MeshMask124)).Write(0x18, BitConverter.GetBytes(current.Model.Color40 ?? throw new NotSupportedException("Effect167 body requires its current packed color.")));
        current = Draw(current with { Temporary = context }, false, NativeBattleBodyBoneMatrices.Require(current.Model, source));
        current = RestoreNativeDeformedBody(current, source, false);
        _ = resource.LinkedGeometry ?? throw new NotSupportedException("Effect167 body requires its observed linked geometry presence.");
        if (current.Model.LinkedTransform is { } linked && (current.Model.Flags0 & 0x200) == 0)
        {
            NativeBattlePoseSource linkedSource = resource.LinkedPoseSource ?? throw new NotSupportedException("Effect167 linked body requires its current skeleton.");
            NativeBattleBodyBoneMatrices linkedBones = current.Model.CurrentLinkedBoneMatrices ?? throw new NotSupportedException("Effect167 linked body requires its current bone publication.");
            _ = NativeBattleBodyBoneMatrices.Require(current.Model with { CurrentBodyBoneMatrices = linkedBones }, linkedSource);
            NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(linked.Rotation) with { X = linked.Position.X, Y = linked.Position.Y, Z = linked.Position.Z };
            context = PutMatrix(current.Temporary, 0x190, NativeBattleGfMatrices.Compose56C2F0(ReadMatrix(current.Temporary, 0x190), local));
            context = PutMatrix(context, 0x1B0, NativeBattleGfMatrices.Compose56C2F0(ReadMatrix(current.Temporary, 0x1B0), local)).Write(0x10, BitConverter.GetBytes(uint.MaxValue));
            current = Draw(current with { Temporary = context }, true, linkedBones.Matrices);
            current = RestoreNativeDeformedBody(current, linkedSource, true);
        }
        current = current with { Staging = NativeBattleTemporaryMemory.PublishView(current.Staging, temporaryAddress, current.Temporary, 464) };
        return (current.Graphics with { Models = current.Graphics.Models.SetItem(actor, current.Model) }, frame with { Memory = current.Memory, Staging = current.Staging });

        NativeDeformedBodyBridge Deliver(NativeDeformedBodyBridge value, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = value.Graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect167 body requires its current ordering table.");
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketDelivery.Deliver(value.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect167 body requires its ordinary packet cursor."), primitive.Pending, batch);
            return value with { Memory = value.Memory with { PrimitiveProcess = written.Process }, Graphics = value.Graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } } };
        }

        NativeDeformedBodyBridge Draw(NativeDeformedBodyBridge value, bool linked, ImmutableArray<NativeBattleActionMatrix> matrices)
        {
            NativeBattleResourcePointer pointer = NativeDeformedGeometryPointer(value, linked);
            uint geometryAddress = pointer.Cpu?.PhysicalAddress ?? pointer.StagingAddress ?? throw new NotSupportedException("Effect167 body requires its current addressed geometry.");
            int start = value.Memory.PrimitiveProcess?.ByteCursor ?? throw new NotSupportedException("Effect167 body requires its actual ordinary COMMON cursor.");
            NativeBattleEffect167BodyStep drawn = NativeBattleEffect167BodyDrawing.Draw(value.Memory, value.Temporary, temporaryAddress, ReadGeometry, geometryAddress, matrices.Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix, null)).ToImmutableArray(), NativeDeformedBodyView(value.Staging, scratchAddress, scratchLength), scratchLength, projection, resources.Mapping, start, 17, 2);
            value = value with { Memory = drawn.Memory, Temporary = drawn.Temporary464, Staging = NativeBattleTemporaryMemory.PublishView(value.Staging, scratchAddress, drawn.Scratch, scratchLength) };
            foreach (NativeBattleGfGeometryAttempt attempt in drawn.Batch.Attempts)
            {
                if (value.Memory.PrimitiveProcess!.ByteCursor != start + attempt.ByteOffsetRelative)
                {
                    throw new InvalidDataException("Effect167 clipped packet order lost its current ordinary cursor.");
                }
                value = Deliver(value, new([attempt with { ByteOffsetRelative = 0 }], attempt.Primitive is null ? 0 : attempt.PacketLength));
            }
            if (value.Memory.PrimitiveProcess!.ByteCursor != start + drawn.Batch.AllocationBytes)
            {
                throw new InvalidDataException("Effect167 body packet allocation differs from its ordinary cursor advance.");
            }
            return value with { Staging = NativeBattleTemporaryMemory.PublishView(value.Staging, temporaryAddress, value.Temporary, 464) };

            NativeBattleEffectArena ReadGeometry(uint address, int count)
            {
                long offset = (long)address - geometryAddress;
                if (offset < 0 || offset > int.MaxValue)
                {
                    throw new InvalidDataException("Effect167 body read leaves its current geometry.");
                }
                return NativeBattleEffectArena.Clear().Write(0, ReadNativeDeformedGeometry(value, pointer, (int)offset, count));
            }
        }

        static NativeBattleEffectArena PutMatrix(NativeBattleEffectArena destination, int offset, NativeBattleActionMatrix matrix)
        {
            NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(matrix, null);
            destination = destination.Write(offset, packed.Bytes.AsSpan(0, 18));
            return destination.Write(offset + 20, packed.Bytes.AsSpan(20, 12));
        }

        static NativeBattleActionMatrix ReadMatrix(NativeBattleEffectArena value, int offset)
        {
            (byte[] bytes, bool[] known) = value.ReadSnapshot(offset, 32);
            return new NativeBattlePackedMatrix32(bytes.ToImmutableArray(), known.ToImmutableArray()).ReadMatrix();
        }
    }
}
