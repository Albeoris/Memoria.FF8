using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleAdditional181Frame Frame, NativeBattleGraphicsState Graphics) DrawNativeAdditional181Body(NativeBattleAdditional181Frame frame, NativeBattleGraphicsState graphics, byte actor, uint temporaryAddress, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, actor);
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Additional effect181 target requires its current skeleton.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Additional effect181 target requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Additional effect181 target requires its published projection.");
        NativeBattlePackedMatrix32 packedCamera = camera.CapturePackedView1D97778();
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Additional effect181 target requires its current CPU resource owner.");
        int scratchLength = checked((cpu.ScratchMaximum ?? throw new NotSupportedException("Additional effect181 target requires its loaded vertex capacity.")) * 8);
        uint scratchAddress = checked(0x196B018u - (uint)scratchLength);
        NativeBattleFileStaging staging = frame.Staging ?? throw new NotSupportedException("Additional effect181 target requires its addressed temporary owner.");
        NativeBattleEffectArena primitiveFlags = NativeAdditional181View(staging, 0x1D969A8, 4);
        _ = resource.LinkedGeometry ?? throw new NotSupportedException("Additional effect181 target requires its observed linked geometry presence.");
        bool linked = graphics.Models[actor].LinkedTransform is not null;
        NativeBattleAdditional181ModelFrame body = new(frame.Memory, staging, graphics.Models[actor], NativeAdditional181View(staging, temporaryAddress, 128));
        NativeBattleAdditional181ModelServices services = new(Shadow, Compose, DrawBody, Restore, DrawLinked);
        body = NativeBattleAdditional181ModelDrawing.DrawOuter(body, packedCamera, scratchAddress, primitiveFlags, linked, services);
        graphics = graphics with { Models = graphics.Models.SetItem(actor, body.Model) };
        return (frame with { Memory = body.Memory, Staging = NativeBattleTemporaryMemory.PublishView(body.Staging, temporaryAddress, body.Temporary128, 128) }, graphics);

        NativeBattleAdditional181ModelFrame Shadow(NativeBattleAdditional181ModelFrame current)
        {
            NativeBattleShadowDrawStep shadow = NativeBattleShadowDrawing.Prepare5088A0(current.Staging, new(current.Memory.GeometryRegisters ?? throw new NotSupportedException("Additional effect181 shadow requires its current GTE matrix."), current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Additional effect181 shadow requires its current GTE data.")), current.Model, projection, current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Additional effect181 shadow requires its depth controls."), definition.NativeGraphics?.Rules.ShadowSource ?? throw new NotSupportedException("Additional effect181 shadow requires its original template."), definition.NativeGraphics?.PrimitiveCapabilities?.GouraudAlpha ?? throw new NotSupportedException("Additional effect181 shadow requires its primitive capabilities."), 4121, 16, pilotIdentity is { } shadowIdentity ? shadowIdentity with { Primitive = -1 } : null);
            current = current with { Memory = current.Memory with { GeometryRegisters = shadow.Registers.Geometry, ScriptedColorRegisters = shadow.Registers.Colors }, Staging = shadow.Staging };
            current = current with { Staging = NativeBattleTemporaryMemory.PublishView(current.Staging, shadow.TemporaryAddress, shadow.Temporary78, 0x78) };
            current = AppendCommon(current, shadow.Batch);
            return current with { Staging = NativeBattleTemporaryMemory.Release5082D0(current.Staging, 0x78) };
        }

        NativeBattleAdditional181ModelFrame Compose(NativeBattleAdditional181ModelFrame current, NativeBattleActionMatrix world) => current with { Model = NativeBattleBodyBoneMatrices.Compose5095B0(current.Model, world) };

        NativeBattleAdditional181ModelFrame DrawBody(NativeBattleAdditional181ModelFrame current)
        {
            NativeBattleResourcePointer pointer = NativeDeformedGeometryPointer(Bridge(current), false);
            return DrawSource(current, pointer, NativeBattleBodyBoneMatrices.Require(current.Model, source));
        }

        NativeBattleAdditional181ModelFrame Restore(NativeBattleAdditional181ModelFrame current)
        {
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging, 88);
            NativeBattleBodyBonePreparation restored = NativeBattleBodyBoneTemporary.Prepare508C90(current.Model, source, NativeAdditional181View(allocation.Staging, allocation.Address, 88));
            NativeBattleFileStaging published = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, restored.Temporary58, 88);
            return current with { Model = restored.Model, Staging = NativeBattleTemporaryMemory.Release5082D0(published, 88) };
        }

        NativeBattleAdditional181ModelFrame DrawLinked(NativeBattleAdditional181ModelFrame current)
        {
            NativeBattlePoseSource linkedSource = resource.LinkedPoseSource ?? throw new NotSupportedException("Additional effect181 linked target requires its current skeleton.");
            NativeBattleBodyBoneMatrices local = current.Model.CurrentLinkedBoneMatrices ?? throw new NotSupportedException("Additional effect181 linked target requires its current bone publication.");
            _ = NativeBattleBodyBoneMatrices.Require(current.Model with { CurrentBodyBoneMatrices = local }, linkedSource);
            NativeBattleActionMatrix world = current.Model.GeometryWorldMatrix ?? throw new NotSupportedException("Additional effect181 linked target requires its actual world matrix.");
            ImmutableArray<NativeBattleActionMatrix> composed = NativeBattlePoseMatrices.ComposeStageCamera(local.Matrices, world, 0);
            current = current with { Model = current.Model with { CurrentLinkedBoneMatrices = local with { Matrices = composed } } };
            current = DrawSource(current, NativeDeformedGeometryPointer(Bridge(current), true), composed);
            NativeBattleSequenceState sequence = current.Model.SequenceState ?? throw new NotSupportedException("Additional effect181 linked target requires its decoded pose.");
            NativeBattleDecodedPose decoded = NativeBattlePoseMatrices.ReadLinkedDecodedPose(sequence, linkedSource);
            ImmutableArray<NativeBattleActionMatrix> restored = NativeBattlePoseMatrices.StageBoneMatrices(linkedSource, decoded);
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging, 88);
            NativeBattleEffectArena temporary = NativeBattleBodyBoneTemporary.Prepare508C90(linkedSource, decoded, restored, NativeAdditional181View(allocation.Staging, allocation.Address, 88));
            NativeBattleFileStaging published = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, temporary, 88);
            return current with { Model = current.Model with { CurrentLinkedBoneMatrices = local with { Matrices = restored } }, Staging = NativeBattleTemporaryMemory.Release5082D0(published, 88) };
        }

        NativeBattleAdditional181ModelFrame DrawSource(NativeBattleAdditional181ModelFrame current, NativeBattleResourcePointer pointer, ImmutableArray<NativeBattleActionMatrix> matrices)
        {
            uint sourceAddress = pointer.Cpu?.PhysicalAddress ?? pointer.StagingAddress ?? throw new NotSupportedException("Additional effect181 target requires its physical geometry address.");
            ImmutableArray<NativeBattlePackedMatrix32> bones = matrices.Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix, null)).ToImmutableArray();
            NativeBattleEffectArena scratch = NativeAdditional181View(current.Staging, scratchAddress, scratchLength);
            NativeBattleAdditional181ModelStep drawn = NativeBattleAdditional181ModelDrawing.DrawBody(current.Memory, current.Temporary128, scratch, Read, sourceAddress, bones, packedCamera, projection, current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Additional effect181 target requires its current depth controls."), scratchAddress, scratchLength, pilotIdentity: pilotIdentity is { } bodyIdentity ? bodyIdentity with { ResourceGeneration = sourceAddress } : null);
            current = current with { Memory = drawn.Memory, Temporary128 = drawn.Temporary128, Staging = NativeBattleTemporaryMemory.PublishView(current.Staging, scratchAddress, drawn.VertexScratch, scratchLength) };
            current = current with { Staging = NativeBattleTemporaryMemory.PublishView(current.Staging, temporaryAddress, current.Temporary128, 128) };
            return AppendCommon(current, drawn.Batch);

            byte[] Read(int offset, int length)
            {
                return ReadNativeDeformedGeometry(Bridge(current), pointer, offset, length);
            }
        }

        NativeDeformedBodyBridge Bridge(NativeBattleAdditional181ModelFrame current) => new(current.Memory, frame.Effect, frame.Random, graphics, current.Model, current.Staging, current.Temporary128, temporaryAddress);

        NativeBattleAdditional181ModelFrame AppendCommon(NativeBattleAdditional181ModelFrame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Additional effect181 target requires its current COMMON ordering table.");
            NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Additional effect181 target requires its current COMMON packet owner.");
            frame = frame with { Effect = frame.Effect with { PresentationPackets = NativeBattleEffectPresentationPackets.Record(frame.Effect.PresentationPackets, process.PacketBank!, process.PacketBuffer, process.ByteCursor!.Value, batch) } };
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(process, primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = current.Memory with { PrimitiveProcess = written.Process } };
        }
    }

    private static NativeBattleEffectArena NativeAdditional181View(NativeBattleFileStaging staging, uint address, int length)
    {
        return NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }

}
