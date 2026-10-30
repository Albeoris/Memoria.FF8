using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleGraphicsState Graphics, NativeBattleEffect70Frame Frame) DrawNativeEffect70Actor(NativeBattleGraphicsState graphics, NativeBattleEffect70Frame frame, NativeBattleEffect70DrawActor693170 draw, NativeBattleSummonColors colors, Func<NativeBattleEffect70Frame, NativeBattleGfGeometryBatch, NativeBattleEffect70Frame> deliver)
    {
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, draw.Actor);
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Effect70 body requires its current skeleton.");
        NativeBattleGraphicsModel model = graphics.Models[draw.Actor];
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Effect70 body requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Effect70 body requires its current projection.");
        int scratchLength = checked((NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Effect70 body requires its loaded projected capacity.")) * 8);
        uint scratchAddress = checked(0x196B018u - (uint)scratchLength);
        (frame, uint temporaryAddress) = NativeBattleEffect70Controller.Allocate(frame, 92);
        NativeBattleShockwavePulsar260BodyPreparationStep prepared = NativeBattleShockwavePulsar260BodyPreparation.Prepare693170(View(temporaryAddress, 92), model.GeometryWorldMatrix ?? throw new NotSupportedException("Effect70 body requires its current world matrix."), camera.CapturePackedView1D97778(), draw.Rotation8, draw.Offset8, draw.Scale, scratchAddress, View(0x1D969A8, 4), model.Color40 ?? throw new NotSupportedException("Effect70 body requires its current color."), model.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Effect70 body requires its current object mask."));
        NativeBattleEffectArena temporary = prepared.Temporary92;
        frame = NativeBattleEffect70Controller.WriteBytes(frame, temporaryAddress, temporary, 92);
        model = NativeBattleBodyBoneMatrices.Compose5095B0(model, prepared.Matrix);
        DrawSource(false, NativeBattleBodyBoneMatrices.Require(model, source));
        Restore(false);
        if (model.LinkedTransform is not null)
        {
            NativeBattlePoseSource linkedSource = resource.LinkedPoseSource ?? throw new NotSupportedException("Effect70 body requires its current linked skeleton.");
            NativeBattleBodyBoneMatrices current = model.CurrentLinkedBoneMatrices ?? throw new NotSupportedException("Effect70 body requires its current linked matrices.");
            _ = NativeBattleBodyBoneMatrices.Require(model with { CurrentBodyBoneMatrices = current }, linkedSource);
            ImmutableArray<NativeBattleActionMatrix> composed = NativeBattlePoseMatrices.ComposeStageCamera(current.Matrices, prepared.Matrix, 0);
            model = model with { CurrentLinkedBoneMatrices = current with { Matrices = composed } };
            temporary = temporary.Write(0x50, BitConverter.GetBytes(uint.MaxValue));
            DrawSource(true, composed);
            Restore(true);
        }
        frame = NativeBattleEffect70Controller.WriteBytes(frame, temporaryAddress, temporary, 92);
        frame = NativeBattleEffect70Controller.Release(frame, 92);
        return (graphics with { Models = graphics.Models.SetItem(draw.Actor, model) }, frame);

        NativeBattleEffectArena View(uint address, int length) => NativeBattleEffect70Controller.ReadBytes(frame, address, length);

        void DrawSource(bool linked, ImmutableArray<NativeBattleActionMatrix> matrices)
        {
            NativeDeformedBodyBridge bridge = new(frame.Memory, frame.Effect, frame.Random, graphics, model, frame.Staging!, temporary, temporaryAddress);
            NativeBattleResourcePointer pointer = NativeDeformedGeometryPointer(bridge, linked);
            uint geometryAddress = pointer.Cpu?.PhysicalAddress ?? pointer.StagingAddress ?? throw new NotSupportedException("Effect70 body requires current addressed geometry.");
            NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Copy(temporary, 0x30, NativeBattleEffectArena.Clear().MarkUnknown(0, 44), 0, 44);
            NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect70 body requires its current COMMON packet process.");
            uint bank = process.PacketBank switch { "off_B6D070" => 0x1901018, "off_B6D094" => 0x199B018, _ => throw new NotSupportedException("Effect70 body requires its actual COMMON bank.") };
            uint packet = checked(bank + (uint)process.PacketBuffer * (uint)process.CommonPacketNominalCapacity + (uint)(process.ByteCursor ?? throw new NotSupportedException("Effect70 body requires its current COMMON cursor.")));
            NativeBattleModelContext44Step drawn = NativeBattleModelContext44.Draw(frame.Memory, context, matrices.Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix, null)).ToImmutableArray(), (offset, length) => ReadNativeDeformedGeometry(bridge, pointer, offset, length), geometryAddress, View(scratchAddress, scratchLength), scratchLength, projection, colors, definition.NativeGraphics?.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Effect70 body requires its renderer capability."), 17, 4, packet, pilotIdentity: draw.PilotIdentity is { } identity ? identity with { Primitive = unchecked(identity.Primitive * 397 + (linked ? 1 : 0)), ResourceGeneration = geometryAddress } : null);
            temporary = NativeBattleEffectNodeContextMemory.Copy(drawn.Context44, 0, temporary, 0x30, 44);
            frame = frame with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(frame.Staging!, scratchAddress, drawn.ProjectedScratch, scratchLength) };
            frame = NativeBattleEffect70Controller.WriteBytes(frame, temporaryAddress, temporary, 92);
            frame = deliver(frame, drawn.Batch);
        }

        void Restore(bool linked)
        {
            (frame, uint boneTemporaryAddress) = NativeBattleEffect70Controller.Allocate(frame, 88);
            NativeBattleEffectArena boneTemporary;
            if (linked)
            {
                NativeBattlePoseSource linkedSource = resource.LinkedPoseSource!;
                NativeBattleDecodedPose decoded = NativeBattlePoseMatrices.ReadLinkedDecodedPose(model.SequenceState!, linkedSource);
                ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattlePoseMatrices.StageBoneMatrices(linkedSource, decoded);
                boneTemporary = NativeBattleBodyBoneTemporary.Prepare508C90(linkedSource, decoded, matrices, View(boneTemporaryAddress, 88));
                model = model with { CurrentLinkedBoneMatrices = model.CurrentLinkedBoneMatrices! with { Matrices = matrices } };
            }
            else
            {
                NativeBattleBodyBonePreparation restored = NativeBattleBodyBoneTemporary.Prepare508C90(model, source, View(boneTemporaryAddress, 88));
                model = restored.Model;
                boneTemporary = restored.Temporary58;
            }
            frame = NativeBattleEffect70Controller.WriteBytes(frame, boneTemporaryAddress, boneTemporary, 88);
            frame = NativeBattleEffect70Controller.Release(frame, 88);
        }
    }

    private (NativeBattleGraphicsState Graphics, NativeBattleEffect70Frame Frame) CaptureNativeEffect70Bounds(NativeBattleGraphicsState graphics, NativeBattleEffect70Frame frame, NativeBattleEffect70CaptureBounds6DFD90 capture)
    {
        NativeBattleGraphicsModel model = graphics.Models[capture.Actor];
        NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, capture.Actor).PoseSource ?? throw new NotSupportedException("Effect70 bounds require their current skeleton.");
        model = NativeBattleBodyBoneMatrices.Compose5095B0(model, model.GeometryWorldMatrix ?? throw new NotSupportedException("Effect70 bounds require their current world matrix."));
        (frame, uint temporaryAddress) = NativeBattleEffect70Controller.Allocate(frame, 24);
        NativeDeformedBodyBridge bridge = new(frame.Memory, frame.Effect, frame.Random, graphics, model, frame.Staging!, NativeBattleEffect70Controller.ReadBytes(frame, temporaryAddress, 24), temporaryAddress);
        NativeBattleResourcePointer pointer = NativeDeformedGeometryPointer(bridge, false);
        NativeBattleEffect70BoundsStep captured = NativeBattleEffect70BoundsCapture.Capture6DFD90(frame.Memory, bridge.Temporary, NativeBattleBodyBoneMatrices.Require(model, source).Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix, null)).ToImmutableArray(), (offset, length) => ReadNativeDeformedGeometry(bridge, pointer, offset, length), model.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Effect70 bounds require their current object mask."), NativeCamera?.Projection ?? throw new NotSupportedException("Effect70 bounds require their current projection."));
        frame = NativeBattleEffect70Controller.WriteBytes(frame with { Memory = captured.Memory }, temporaryAddress, captured.Temporary24, 24);
        frame = NativeBattleEffect70Controller.Release(frame, 24);
        (frame, uint boneTemporaryAddress) = NativeBattleEffect70Controller.Allocate(frame, 88);
        NativeBattleBodyBonePreparation restored = NativeBattleBodyBoneTemporary.Prepare508C90(model, source, NativeBattleEffect70Controller.ReadBytes(frame, boneTemporaryAddress, 88));
        frame = NativeBattleEffect70Controller.WriteBytes(frame, boneTemporaryAddress, restored.Temporary58, 88);
        frame = NativeBattleEffect70Controller.Release(frame, 88);
        NativeBattleEffectArena limits = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(captured.Maximum)).Write(2, BitConverter.GetBytes(captured.Minimum));
        frame = NativeBattleEffect70Controller.WriteBytes(frame, capture.Destination, limits, 4);
        return (graphics with { Models = graphics.Models.SetItem(capture.Actor, restored.Model) }, frame);
    }
}
