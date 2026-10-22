using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleDegenerator306Frame Frame, NativeBattleGraphicsState Graphics) DrawNativeDegenerator306Body(NativeBattleDegenerator306Frame frame, NativeBattleGraphicsState graphics, byte actor, uint temporaryAddress)
    {
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, actor);
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Degenerator target requires its current skeleton.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Degenerator target requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Degenerator target requires its published projection.");
        NativeBattlePackedMatrix32 packedCamera = camera.CapturePackedView1D97778();
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Degenerator target requires its current CPU resource owner.");
        int scratchLength = checked((cpu.ScratchMaximum ?? throw new NotSupportedException("Degenerator target requires its loaded vertex capacity.")) * 8);
        uint scratchAddress = checked(0x196B018u - (uint)scratchLength);
        NativeBattleFileStaging staging = frame.Staging ?? throw new NotSupportedException("Degenerator target requires its addressed temporary owner.");
        NativeBattleEffectArena primitiveFlags = NativeBattleTemporaryMemory.ReadView(staging, 0x1D969A8, 4);
        _ = resource.LinkedGeometry ?? throw new NotSupportedException("Degenerator target requires its observed linked geometry presence.");
        bool linked = graphics.Models[actor].LinkedTransform is not null;
        NativeBattleDegenerator306ModelFrame body = new(frame.Memory, staging, graphics.Models[actor], NativeBattleTemporaryMemory.ReadView(staging, temporaryAddress, 136));
        NativeBattleDegenerator306ModelServices services = new(Shadow, Compose, DrawBody, Restore, DrawLinked);
        body = NativeBattleDegenerator306ModelDrawing.DrawOuter(body, packedCamera, scratchAddress, primitiveFlags, linked, services);
        graphics = graphics with { Models = graphics.Models.SetItem(actor, body.Model) };
        return (frame with { Memory = body.Memory, Staging = NativeBattleTemporaryMemory.PublishView(body.Staging, temporaryAddress, body.Temporary136, 136) }, graphics);

        NativeBattleDegenerator306ModelFrame Shadow(NativeBattleDegenerator306ModelFrame current)
        {
            NativeBattleShadowDrawStep shadow = NativeBattleShadowDrawing.Prepare5088A0(current.Staging, new(current.Memory.GeometryRegisters ?? throw new NotSupportedException("Degenerator shadow requires its current GTE matrix."), current.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Degenerator shadow requires its current GTE data.")), current.Model, projection, current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Degenerator shadow requires its depth controls."), definition.NativeGraphics?.Rules.ShadowSource ?? throw new NotSupportedException("Degenerator shadow requires its original template."), definition.NativeGraphics?.PrimitiveCapabilities?.GouraudAlpha ?? throw new NotSupportedException("Degenerator shadow requires its primitive capabilities."), 4121, 16);
            current = current with { Memory = current.Memory with { GeometryRegisters = shadow.Registers.Geometry, ScriptedColorRegisters = shadow.Registers.Colors }, Staging = shadow.Staging };
            current = current with { Staging = NativeBattleTemporaryMemory.PublishView(current.Staging, shadow.TemporaryAddress, shadow.Temporary78, 0x78) };
            current = AppendCommon(current, shadow.Batch);
            return current with { Staging = NativeBattleTemporaryMemory.Release5082D0(current.Staging, 0x78) };
        }

        NativeBattleDegenerator306ModelFrame Compose(NativeBattleDegenerator306ModelFrame current, NativeBattleActionMatrix world) => current with { Model = NativeBattleBodyBoneMatrices.Compose5095B0(current.Model, world) };

        NativeBattleDegenerator306ModelFrame DrawBody(NativeBattleDegenerator306ModelFrame current)
        {
            NativeBattleResourcePointer pointer = GeometryPointer(current, current.Model.ActiveResourceRow100, current.Model.ActiveStagedResourceRow100);
            return DrawSource(current, pointer, NativeBattleBodyBoneMatrices.Require(current.Model, source));
        }

        NativeBattleDegenerator306ModelFrame Restore(NativeBattleDegenerator306ModelFrame current)
        {
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging, 88);
            NativeBattleBodyBonePreparation restored = NativeBattleBodyBoneTemporary.Prepare508C90(current.Model, source, NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88));
            NativeBattleFileStaging published = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, restored.Temporary58, 88);
            return current with { Model = restored.Model, Staging = NativeBattleTemporaryMemory.Release5082D0(published, 88) };
        }

        NativeBattleDegenerator306ModelFrame DrawLinked(NativeBattleDegenerator306ModelFrame current)
        {
            NativeBattlePoseSource linkedSource = resource.LinkedPoseSource ?? throw new NotSupportedException("Degenerator linked target requires its current skeleton.");
            NativeBattleBodyBoneMatrices local = current.Model.CurrentLinkedBoneMatrices ?? throw new NotSupportedException("Degenerator linked target requires its current bone publication.");
            _ = NativeBattleBodyBoneMatrices.Require(current.Model with { CurrentBodyBoneMatrices = local }, linkedSource);
            NativeBattleActionMatrix world = current.Model.GeometryWorldMatrix ?? throw new NotSupportedException("Degenerator linked target requires its actual world matrix.");
            ImmutableArray<NativeBattleActionMatrix> composed = NativeBattlePoseMatrices.ComposeStageCamera(local.Matrices, world, 0);
            current = current with { Model = current.Model with { CurrentLinkedBoneMatrices = local with { Matrices = composed } } };
            current = DrawSource(current, GeometryPointer(current, current.Model.LinkedTextureResourceIndex, current.Model.LinkedStagedResourceRow), composed);
            NativeBattleSequenceState sequence = current.Model.SequenceState ?? throw new NotSupportedException("Degenerator linked target requires its decoded pose.");
            NativeBattleDecodedPose decoded = NativeBattlePoseMatrices.ReadLinkedDecodedPose(sequence, linkedSource);
            ImmutableArray<NativeBattleActionMatrix> restored = NativeBattlePoseMatrices.StageBoneMatrices(linkedSource, decoded);
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging, 88);
            NativeBattleEffectArena temporary = NativeBattleBodyBoneTemporary.Prepare508C90(linkedSource, decoded, restored, NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88));
            NativeBattleFileStaging published = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, temporary, 88);
            return current with { Model = current.Model with { CurrentLinkedBoneMatrices = local with { Matrices = restored } }, Staging = NativeBattleTemporaryMemory.Release5082D0(published, 88) };
        }

        NativeBattleDegenerator306ModelFrame DrawSource(NativeBattleDegenerator306ModelFrame current, NativeBattleResourcePointer pointer, ImmutableArray<NativeBattleActionMatrix> matrices)
        {
            uint sourceAddress = pointer.Cpu?.PhysicalAddress ?? pointer.StagingAddress ?? throw new NotSupportedException("Degenerator target requires its physical geometry address.");
            ImmutableArray<NativeBattlePackedMatrix32> bones = matrices.Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix, null)).ToImmutableArray();
            NativeBattleEffectArena scratch = NativeBattleTemporaryMemory.ReadView(current.Staging, scratchAddress, scratchLength);
            NativeBattleDegenerator306ModelStep drawn = NativeBattleDegenerator306ModelDrawing.DrawBody(current.Memory, current.Temporary136, scratch, Read, sourceAddress, bones, packedCamera, projection, current.Memory.ScriptedGraphics ?? throw new NotSupportedException("Degenerator target requires its current depth controls."), scratchAddress, scratchLength);
            current = current with { Memory = drawn.Memory, Temporary136 = drawn.Temporary136, Staging = NativeBattleTemporaryMemory.PublishView(current.Staging, scratchAddress, drawn.VertexScratch, scratchLength) };
            current = current with { Staging = NativeBattleTemporaryMemory.PublishView(current.Staging, temporaryAddress, current.Temporary136, 136) };
            return AppendCommon(current, drawn.Batch);

            byte[] Read(int offset, int length)
            {
                if (pointer.Length is int available && (offset < 0 || length < 0 || offset > available - length))
                {
                    throw new InvalidDataException("Degenerator target geometry read leaves its current resource extent.");
                }
                if (pointer.Cpu is { } reference)
                {
                    return NativeBattleModelEnemyHeap.Read(cpu, current.Staging, reference with { Offset = checked(reference.Offset + offset) }, length);
                }
                uint address = checked(pointer.StagingAddress!.Value + (uint)offset);
                if (NativeBattleEffectTaskMemory.TryRead(frame.Effect.Pools, address, length, out byte[] bytes))
                {
                    return bytes;
                }
                return IsNativeEffectArenaAddress(address) ? current.Memory.Arena!.Read(NativeBattleWaterExecutionFields.ArenaOffset(address, length), length) : current.Staging.Read(address, length);
            }
        }

        NativeBattleResourcePointer GeometryPointer(NativeBattleDegenerator306ModelFrame current, int? row, uint? stagedRow)
        {
            if (stagedRow is uint staged)
            {
                uint address = checked(staged + 4);
                if (NativeBattleEffectTaskMemory.TryReadPointer(frame.Effect.Pools, address, out NativeBattleResourcePointer? pointer))
                {
                    return pointer ?? throw new NotSupportedException("Degenerator target geometry pointer is NULL.");
                }
                NativeBattleResourcePointer? selected = address >= NativeBattleDegenerator306Profile.BindingSave && address < NativeBattleDegenerator306Profile.BindingSave + 32 ? frame.State.ReadResourcePointer(address) : IsNativeEffectArenaAddress(address) ? current.Memory.Arena!.ReadResourcePointer(NativeBattleWaterExecutionFields.ArenaOffset(address, 4)) : current.Staging.ReadResourcePointer(address);
                return selected ?? throw new NotSupportedException("Degenerator target geometry pointer is unobserved.");
            }
            NativeBattleModelCpuResourceRow selectedRow = cpu.Rows[row ?? throw new NotSupportedException("Degenerator target requires its actual CPU row.")];
            return new(selectedRow.Require(16), null, selectedRow.Lengths.TryGetValue(16, out int length) ? length : null);
        }

        NativeBattleDegenerator306ModelFrame AppendCommon(NativeBattleDegenerator306ModelFrame current, NativeBattleGfGeometryBatch batch)
        {
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Degenerator target requires its current COMMON ordering table.");
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Degenerator target requires its current COMMON packet owner."), primitive.Pending, batch);
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
            return current with { Memory = current.Memory with { PrimitiveProcess = written.Process } };
        }
    }
}
