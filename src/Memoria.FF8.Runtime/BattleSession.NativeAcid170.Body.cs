using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleAcid170Frame Frame, NativeBattleGraphicsState Graphics) DrawNativeAcid170Body(NativeBattleAcid170Frame frame, NativeBattleGraphicsState graphics, byte actor, uint temporaryAddress)
    {
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, actor);
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Acid170 target requires its current skeleton.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Acid170 target requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Acid170 target requires its published projection.");
        NativeBattlePackedMatrix32 packedCamera = camera.CapturePackedView1D97778();
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Acid170 target requires its current CPU resource owner.");
        int scratchLength = checked((cpu.ScratchMaximum ?? throw new NotSupportedException("Acid170 target requires its loaded vertex capacity.")) * 8);
        uint scratchAddress = checked(0x196B018u - (uint)scratchLength);
        NativeBattleFileStaging staging = frame.Staging ?? throw new NotSupportedException("Acid170 target requires its addressed temporary owner.");
        NativeBattleEffectArena primitiveFlags = NativeAcid170View(staging, 0x1D969A8, 4);
        bool linked = graphics.Models[actor].LinkedTransform is not null;
        NativeBattleAcid170ModelFrame body = new(frame.Memory, staging, graphics.Models[actor], NativeAcid170View(staging, temporaryAddress, 172));
        NativeBattleAcid170ModelServices services = new(Shadow, Compose, DrawBody, Restore, DrawLinked);
        body = NativeBattleAcid170ModelDrawing.DrawOuter(body, packedCamera, scratchAddress, primitiveFlags, linked, services);
        graphics = graphics with { Models = graphics.Models.SetItem(actor, body.Model) };
        return (frame with { Memory = body.Memory, Staging = NativeBattleTemporaryMemory.PublishView(body.Staging, temporaryAddress, body.Temporary172, 172) }, graphics);

        NativeBattleAcid170ModelFrame Shadow(NativeBattleAcid170ModelFrame current)
        {
            NativeBattleShadowDrawStep shadow = NativeBattleShadowDrawing.Prepare5088A0(current.Staging, new(current.Memory.GeometryRegisters ?? throw new NotSupportedException("Acid170 shadow requires its current GTE matrix."), current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Acid170 shadow requires its current GTE data.")), current.Model, projection, current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Acid170 shadow requires its depth controls."), definition.NativeGraphics?.Rules.ShadowSource ?? throw new NotSupportedException("Acid170 shadow requires its original template."), definition.NativeGraphics?.PrimitiveCapabilities?.GouraudAlpha ?? throw new NotSupportedException("Acid170 shadow requires its primitive capabilities."), 4121, 16);
            current = current with { Memory = current.Memory with { GeometryRegisters = shadow.Registers.Geometry, ScriptedColorRegisters = shadow.Registers.Colors }, Staging = shadow.Staging };
            current = current with { Staging = NativeBattleTemporaryMemory.PublishView(current.Staging, shadow.TemporaryAddress, shadow.Temporary78, 0x78) };
            current = AppendCommon(current, shadow.Batch);
            return current with { Staging = NativeBattleTemporaryMemory.Release5082D0(current.Staging, 0x78) };
        }

        NativeBattleAcid170ModelFrame Compose(NativeBattleAcid170ModelFrame current, NativeBattleActionMatrix world) => current with { Model = NativeBattleBodyBoneMatrices.Compose5095B0(current.Model, world) };

        NativeBattleAcid170ModelFrame DrawBody(NativeBattleAcid170ModelFrame current)
        {
            NativeBattleResourcePointer pointer = NativeDeformedGeometryPointer(Bridge(current), false);
            return DrawSource(current, pointer, NativeBattleBodyBoneMatrices.Require(current.Model, source));
        }

        NativeBattleAcid170ModelFrame Restore(NativeBattleAcid170ModelFrame current)
        {
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging, 88);
            NativeBattleBodyBonePreparation restored = NativeBattleBodyBoneTemporary.Prepare508C90(current.Model, source, NativeAcid170View(allocation.Staging, allocation.Address, 88));
            NativeBattleFileStaging published = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, restored.Temporary58, 88);
            return current with { Model = restored.Model, Staging = NativeBattleTemporaryMemory.Release5082D0(published, 88) };
        }

        NativeBattleAcid170ModelFrame DrawLinked(NativeBattleAcid170ModelFrame current)
        {
            NativeBattlePoseSource linkedSource = resource.LinkedPoseSource ?? throw new NotSupportedException("Acid170 linked target requires its current skeleton.");
            NativeBattleBodyBoneMatrices local = current.Model.CurrentLinkedBoneMatrices ?? throw new NotSupportedException("Acid170 linked target requires its current bone publication.");
            _ = NativeBattleBodyBoneMatrices.Require(current.Model with { CurrentBodyBoneMatrices = local }, linkedSource);
            NativeBattleActionMatrix world = current.Model.GeometryWorldMatrix ?? throw new NotSupportedException("Acid170 linked target requires its actual world matrix.");
            ImmutableArray<NativeBattleActionMatrix> composed = NativeBattlePoseMatrices.ComposeStageCamera(local.Matrices, world, 0);
            current = current with { Model = current.Model with { CurrentLinkedBoneMatrices = local with { Matrices = composed } } };
            current = DrawSource(current, NativeDeformedGeometryPointer(Bridge(current), true), composed);
            NativeBattleSequenceState sequence = current.Model.SequenceState ?? throw new NotSupportedException("Acid170 linked target requires its decoded pose.");
            NativeBattleDecodedPose decoded = NativeBattlePoseMatrices.ReadLinkedDecodedPose(sequence, linkedSource);
            ImmutableArray<NativeBattleActionMatrix> restored = NativeBattlePoseMatrices.StageBoneMatrices(linkedSource, decoded);
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging, 88);
            NativeBattleEffectArena temporary = NativeBattleBodyBoneTemporary.Prepare508C90(linkedSource, decoded, restored, NativeAcid170View(allocation.Staging, allocation.Address, 88));
            NativeBattleFileStaging published = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, temporary, 88);
            return current with { Model = current.Model with { CurrentLinkedBoneMatrices = local with { Matrices = restored } }, Staging = NativeBattleTemporaryMemory.Release5082D0(published, 88) };
        }

        NativeBattleAcid170ModelFrame DrawSource(NativeBattleAcid170ModelFrame current, NativeBattleResourcePointer pointer, ImmutableArray<NativeBattleActionMatrix> matrices)
        {
            uint sourceAddress = pointer.Cpu?.PhysicalAddress ?? pointer.StagingAddress ?? throw new NotSupportedException("Acid170 target requires its physical geometry address.");
            ImmutableArray<NativeBattlePackedMatrix32> bones = matrices.Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix, null)).ToImmutableArray();
            NativeBattleEffectArena scratch = NativeAcid170View(current.Staging, scratchAddress, scratchLength);
            NativeBattleAcid170ModelStep drawn = NativeBattleAcid170ModelDrawing.DrawBody(current.Memory, current.Temporary172, scratch, Read, sourceAddress, bones, packedCamera, projection, current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Acid170 target requires its current depth controls."), scratchAddress, scratchLength);
            current = current with { Memory = drawn.Memory, Temporary172 = drawn.Temporary172, Staging = NativeBattleTemporaryMemory.PublishView(current.Staging, scratchAddress, drawn.VertexScratch, scratchLength) };
            current = current with { Staging = NativeBattleTemporaryMemory.PublishView(current.Staging, temporaryAddress, current.Temporary172, 172) };
            return AppendCommon(current, drawn.Batch);

            byte[] Read(int offset, int length)
            {
                return ReadNativeDeformedGeometry(Bridge(current), pointer, offset, length);
            }
        }

        NativeDeformedBodyBridge Bridge(NativeBattleAcid170ModelFrame current) => new(current.Memory, frame.Effect, frame.Random, graphics, current.Model, current.Staging, current.Temporary172, temporaryAddress);

        NativeBattleAcid170ModelFrame AppendCommon(NativeBattleAcid170ModelFrame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Acid170 target requires its current COMMON ordering table.");
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Acid170 target requires its current COMMON packet owner."), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = current.Memory with { PrimitiveProcess = written.Process } };
        }
    }

    private static NativeBattleEffectArena NativeAcid170View(NativeBattleFileStaging staging, uint address, int length)
    {
        return NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }

}
