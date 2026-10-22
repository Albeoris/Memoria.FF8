using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private sealed record NativeAcid317BodyBridge(NativeBattleAcid317Frame Frame, NativeBattleGraphicsState Graphics, NativeBattleGraphicsModel Model, NativeBattleFileStaging Staging, NativeBattleEffectArena Temporary, uint TemporaryAddress);

    private (NativeBattleAcid317Frame Frame, NativeBattleGraphicsState Graphics) SnapshotNativeAcid317Body(NativeBattleAcid317Frame frame, NativeBattleGraphicsState graphics, byte actor, uint cacheAddress)
    {
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, actor);
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Acid snapshot requires its current skeleton.");
        NativeBattleActionMatrix world = graphics.Models[actor].GeometryWorldMatrix ?? throw new NotSupportedException("Acid snapshot requires its current world matrix.");
        NativeAcid317BodyBridge current = AllocateNativeAcid317Body(frame, graphics, actor, 12);
        current = current with { Temporary = current.Temporary.Write(8, BitConverter.GetBytes(cacheAddress)), Model = NativeBattleBodyBoneMatrices.Compose5095B0(current.Model, world) };
        current = Capture(current, false, NativeBattleBodyBoneMatrices.Require(current.Model, source));
        current = RestoreNativeAcid317Body(current, source, false);
        _ = resource.LinkedGeometry ?? throw new NotSupportedException("Acid snapshot requires its observed linked geometry presence.");
        if (current.Model.LinkedTransform is not null)
        {
            NativeBattlePoseSource linkedSource = resource.LinkedPoseSource ?? throw new NotSupportedException("Acid linked snapshot requires its current skeleton.");
            var composed = ComposeNativeAcid317Linked(current, linkedSource, world);
            current = composed.Bridge;
            current = Capture(current, true, composed.Matrices);
            current = RestoreNativeAcid317Body(current, linkedSource, true);
        }
        return FinishNativeAcid317Body(current, actor, 12);

        NativeAcid317BodyBridge Capture(NativeAcid317BodyBridge value, bool linked, ImmutableArray<NativeBattleActionMatrix> matrices)
        {
            NativeBattleResourcePointer pointer = NativeAcid317GeometryPointer(value, linked);
            uint address = pointer.Cpu?.PhysicalAddress ?? pointer.StagingAddress ?? throw new NotSupportedException("Acid snapshot requires its physical geometry address.");
            NativeBattleAcid317SnapshotStep captured = NativeBattleAcid317Snapshot.Capture(value.Frame.Memory, value.Frame.Random, value.Temporary, (offset, count) => ReadNativeAcid317Geometry(value, pointer, offset, count), address, matrices.Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix, null)).ToImmutableArray(), cacheAddress, NativeBattleAcid317Profile.BodyCacheBytes);
            return value with { Frame = value.Frame with { Memory = captured.Memory, Random = captured.Random }, Temporary = captured.Temporary12, Staging = NativeBattleTemporaryMemory.PublishView(value.Staging, value.TemporaryAddress, captured.Temporary12, 12) };
        }
    }

    private (NativeBattleAcid317Frame Frame, NativeBattleGraphicsState Graphics) DrawNativeAcid317Body(NativeBattleAcid317Frame frame, NativeBattleGraphicsState graphics, byte actor, short fraction)
    {
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, actor);
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Acid target requires its current skeleton.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Acid target requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Acid target requires its published projection.");
        NativeBattlePackedMatrix32 packedCamera = camera.CapturePackedView1D97778();
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Acid target requires its current CPU resource owner.");
        int scratchLength = checked((cpu.ScratchMaximum ?? throw new NotSupportedException("Acid target requires its loaded vertex capacity.")) * 8);
        uint scratchAddress = checked(0x196B018u - (uint)scratchLength);
        uint cacheAddress = checked(frame.State.ReadDword(NativeBattleAcid317Profile.WorkingGlobal) + NativeBattleAcid317Profile.BodyCacheOffset);
        NativeAcid317BodyBridge current = AllocateNativeAcid317Body(frame, graphics, actor, 132);
        current = current with { Temporary = current.Temporary.Write(0x80, BitConverter.GetBytes(fraction)).Write(0x2C, BitConverter.GetBytes(cacheAddress)) };
        current = current with { Staging = NativeBattleTemporaryMemory.PublishView(current.Staging, current.TemporaryAddress, current.Temporary, 132) };
        NativeBattleShadowDrawStep shadow = NativeBattleShadowDrawing.Prepare5088A0(current.Staging, new(current.Frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Acid shadow requires its current GTE matrix."), current.Frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Acid shadow requires its current arithmetic registers.")), current.Model, projection, current.Frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Acid shadow requires its current depth controls."), definition.NativeGraphics?.Rules.ShadowSource ?? throw new NotSupportedException("Acid shadow requires its original template."), definition.NativeGraphics?.PrimitiveCapabilities?.GouraudAlpha ?? throw new NotSupportedException("Acid shadow requires its primitive capabilities."), 4112, 16);
        current = current with { Frame = current.Frame with { Memory = current.Frame.Memory with { GeometryRegisters = shadow.Registers.Geometry, ScriptedColorRegisters = shadow.Registers.Colors } }, Staging = NativeBattleTemporaryMemory.PublishView(shadow.Staging, shadow.TemporaryAddress, shadow.Temporary78, 0x78) };
        current = AppendNativeAcid317Body(current, shadow.Batch);
        current = current with { Staging = NativeBattleTemporaryMemory.Release5082D0(current.Staging, 0x78) };
        NativeBattleActionMatrix world = current.Model.GeometryWorldMatrix ?? throw new NotSupportedException("Acid target requires its actual world matrix.");
        current = current with { Model = NativeBattleBodyBoneMatrices.Compose5095B0(current.Model, world) };
        current = current with
        {
            Temporary = current.Temporary.Write(0x24, BitConverter.GetBytes(scratchAddress)).Write(0x40, new byte[4]).Write(0x44, BitConverter.GetBytes((short)320)).Write(0x46, BitConverter.GetBytes((short)216)).Write(0x48, BitConverter.GetBytes(current.Model.Color40 ?? throw new NotSupportedException("Acid target requires its actual full color DWORD."))).Write(0x4C, BitConverter.GetBytes((current.Model.SequenceState ?? throw new NotSupportedException("Acid target requires its current mesh mask.")).MeshMask124))
        };
        current = DrawSource(current, false, NativeBattleBodyBoneMatrices.Require(current.Model, source));
        current = RestoreNativeAcid317Body(current, source, false);
        _ = resource.LinkedGeometry ?? throw new NotSupportedException("Acid target requires its observed linked geometry presence.");
        if (current.Model.LinkedTransform is not null)
        {
            NativeBattlePoseSource linkedSource = resource.LinkedPoseSource ?? throw new NotSupportedException("Acid linked target requires its current skeleton.");
            current = current with { Temporary = current.Temporary.Write(0x4C, BitConverter.GetBytes(uint.MaxValue)) };
            var composed = ComposeNativeAcid317Linked(current, linkedSource, world);
            current = composed.Bridge;
            current = DrawSource(current, true, composed.Matrices);
            current = RestoreNativeAcid317Body(current, linkedSource, true);
        }
        return FinishNativeAcid317Body(current, actor, 132);

        NativeAcid317BodyBridge DrawSource(NativeAcid317BodyBridge value, bool linked, ImmutableArray<NativeBattleActionMatrix> matrices)
        {
            NativeBattleResourcePointer pointer = NativeAcid317GeometryPointer(value, linked);
            uint address = pointer.Cpu?.PhysicalAddress ?? pointer.StagingAddress ?? throw new NotSupportedException("Acid target requires its physical geometry address.");
            NativeBattleAcid317ModelStep drawn = NativeBattleAcid317ModelDrawing.DrawBody(value.Frame.Memory, value.Temporary, NativeAcid317BodyView(value.Staging, scratchAddress, scratchLength), (offset, count) => ReadNativeAcid317Geometry(value, pointer, offset, count), address, matrices.Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix, null)).ToImmutableArray(), packedCamera, projection, value.Frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Acid target requires its current depth controls."), scratchAddress, scratchLength, cacheAddress, NativeBattleAcid317Profile.BodyCacheBytes);
            value = value with { Frame = value.Frame with { Memory = drawn.Memory }, Temporary = drawn.Temporary132, Staging = NativeBattleTemporaryMemory.PublishView(value.Staging, scratchAddress, drawn.VertexScratch, scratchLength) };
            value = value with { Staging = NativeBattleTemporaryMemory.PublishView(value.Staging, value.TemporaryAddress, value.Temporary, 132) };
            return AppendNativeAcid317Body(value, drawn.Batch);
        }
    }

    private static NativeAcid317BodyBridge AllocateNativeAcid317Body(NativeBattleAcid317Frame frame, NativeBattleGraphicsState graphics, byte actor, int length)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging ?? throw new NotSupportedException("Acid body requires its current addressed staging owner."), length);
        return new(frame, graphics, graphics.Models[actor], allocation.Staging, NativeAcid317BodyView(allocation.Staging, allocation.Address, length), allocation.Address);
    }

    private static (NativeBattleAcid317Frame Frame, NativeBattleGraphicsState Graphics) FinishNativeAcid317Body(NativeAcid317BodyBridge current, byte actor, int length)
    {
        NativeBattleFileStaging published = NativeBattleTemporaryMemory.PublishView(current.Staging, current.TemporaryAddress, current.Temporary, length);
        return (current.Frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(published, length) }, current.Graphics with { Models = current.Graphics.Models.SetItem(actor, current.Model) });
    }

    private static (NativeAcid317BodyBridge Bridge, ImmutableArray<NativeBattleActionMatrix> Matrices) ComposeNativeAcid317Linked(NativeAcid317BodyBridge current, NativeBattlePoseSource source, NativeBattleActionMatrix world)
    {
        NativeBattleBodyBoneMatrices local = current.Model.CurrentLinkedBoneMatrices ?? throw new NotSupportedException("Acid linked target requires its actual bone publication.");
        _ = NativeBattleBodyBoneMatrices.Require(current.Model with { CurrentBodyBoneMatrices = local }, source);
        ImmutableArray<NativeBattleActionMatrix> composed = NativeBattlePoseMatrices.ComposeStageCamera(local.Matrices, world, 0);
        return (current with { Model = current.Model with { CurrentLinkedBoneMatrices = local with { Matrices = composed } } }, composed);
    }

    private static NativeAcid317BodyBridge RestoreNativeAcid317Body(NativeAcid317BodyBridge current, NativeBattlePoseSource source, bool linked)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging, 88);
        NativeBattleEffectArena temporary = NativeAcid317BodyView(allocation.Staging, allocation.Address, 88);
        NativeBattleGraphicsModel model;
        if (linked)
        {
            NativeBattleSequenceState sequence = current.Model.SequenceState ?? throw new NotSupportedException("Acid linked target requires its decoded pose.");
            NativeBattleDecodedPose decoded = NativeBattlePoseMatrices.ReadLinkedDecodedPose(sequence, source);
            ImmutableArray<NativeBattleActionMatrix> restored = NativeBattlePoseMatrices.StageBoneMatrices(source, decoded);
            temporary = NativeBattleBodyBoneTemporary.Prepare508C90(source, decoded, restored, temporary);
            model = current.Model with { CurrentLinkedBoneMatrices = (current.Model.CurrentLinkedBoneMatrices ?? throw new NotSupportedException("Acid linked target requires its current matrix owner.")) with { Matrices = restored } };
        }
        else
        {
            NativeBattleBodyBonePreparation prepared = NativeBattleBodyBoneTemporary.Prepare508C90(current.Model, source, temporary);
            model = prepared.Model;
            temporary = prepared.Temporary58;
        }
        NativeBattleFileStaging published = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, temporary, 88);
        return current with { Model = model, Staging = NativeBattleTemporaryMemory.Release5082D0(published, 88) };
    }

    private NativeBattleResourcePointer NativeAcid317GeometryPointer(NativeAcid317BodyBridge current, bool linked)
    {
        int? row = linked ? current.Model.LinkedTextureResourceIndex : current.Model.ActiveResourceRow100;
        uint? stagedRow = linked ? current.Model.LinkedStagedResourceRow : current.Model.ActiveStagedResourceRow100;
        if (stagedRow is uint staged)
        {
            uint address = checked(staged + 4);
            if (NativeBattleEffectTaskMemory.TryReadPointer(current.Frame.Effect.Pools, address, out NativeBattleResourcePointer? pointer))
            {
                return pointer ?? throw new NotSupportedException("Acid target geometry pointer is NULL.");
            }
            NativeBattleResourcePointer? selected = IsNativeEffectArenaAddress(address) ? current.Frame.Memory.Arena!.ReadResourcePointer(NativeBattleWaterExecutionFields.ArenaOffset(address, 4)) : current.Staging.ReadResourcePointer(address);
            return selected ?? throw new NotSupportedException("Acid target geometry pointer is unobserved.");
        }
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Acid target requires its current CPU resource owner.");
        NativeBattleModelCpuResourceRow selectedRow = cpu.Rows[row ?? throw new NotSupportedException("Acid target requires its actual CPU row.")];
        return new(selectedRow.Require(16), null, selectedRow.Lengths.TryGetValue(16, out int length) ? length : null);
    }

    private byte[] ReadNativeAcid317Geometry(NativeAcid317BodyBridge current, NativeBattleResourcePointer pointer, int offset, int length)
    {
        if (offset < 0 || length < 0 || pointer.Length is int available && offset > available - length)
        {
            throw new InvalidDataException("Acid target geometry read leaves its current resource extent.");
        }
        if (pointer.Cpu is { } reference)
        {
            return NativeBattleModelEnemyHeap.Read(NativeModelCpuResources ?? throw new NotSupportedException("Acid target requires its current CPU resource owner."), current.Staging, reference with { Offset = checked(reference.Offset + offset) }, length);
        }
        uint address = checked((pointer.StagingAddress ?? throw new NotSupportedException("Acid target requires its addressed geometry source.")) + (uint)offset);
        if (NativeBattleEffectTaskMemory.TryRead(current.Frame.Effect.Pools, address, length, out byte[] bytes))
        {
            return bytes;
        }
        return IsNativeEffectArenaAddress(address) ? current.Frame.Memory.Arena!.Read(NativeBattleWaterExecutionFields.ArenaOffset(address, length), length) : current.Staging.Read(address, length);
    }

    private static NativeAcid317BodyBridge AppendNativeAcid317Body(NativeAcid317BodyBridge current, NativeBattleGfGeometryBatch batch)
    {
        NativeBattlePrimitiveFrame primitive = current.Graphics.PrimitiveFrame ?? throw new NotSupportedException("Acid target requires its current COMMON ordering table.");
        NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(current.Frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Acid target requires its current COMMON packet owner."), primitive.Pending, batch);
        return current with { Frame = current.Frame with { Memory = current.Frame.Memory with { PrimitiveProcess = written.Process } }, Graphics = current.Graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } } };
    }

    private static NativeBattleEffectArena NativeAcid317BodyView(NativeBattleFileStaging staging, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
}
