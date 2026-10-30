using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleShockwavePulsar260Frame Frame, NativeBattleGraphicsState Graphics) DrawNativeShockwavePulsar260Body(NativeBattleShockwavePulsar260Frame frame, NativeBattleGraphicsState graphics, NativeBattleShockwavePulsar260DrawBody draw, NativeBattleSummonColors colors)
    {
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, draw.Actor);
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Shockwave Pulsar requires its current body skeleton.");
        NativeBattleGraphicsModel model = graphics.Models[draw.Actor];
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Shockwave Pulsar requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Shockwave Pulsar requires its current projection.");
        NativeBattleFileStaging staging = frame.Staging ?? throw new NotSupportedException("Shockwave Pulsar requires its physical temporary memory.");
        int scratchLength = checked((NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Shockwave Pulsar requires its loaded projected vertex capacity.")) * 8);
        uint scratchAddress = checked(0x196B018u - (uint)scratchLength);
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 92);
        staging = allocation.Staging;
        NativeBattleShockwavePulsar260BodyPreparationStep prepared = NativeBattleShockwavePulsar260BodyPreparation.Prepare693170(View(allocation.Address, 92), model.GeometryWorldMatrix ?? throw new NotSupportedException("Shockwave Pulsar requires its current model world matrix."), camera.CapturePackedView1D97778(), draw.Rotation8, draw.Offset8, draw.Scale, scratchAddress, View(0x1D969A8, 4), model.Color40 ?? throw new NotSupportedException("Shockwave Pulsar requires its current model color."), model.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Shockwave Pulsar requires its current object mask."));
        NativeBattleEffectArena temporary = prepared.Temporary92;
        staging = NativeBattleTemporaryMemory.PublishView(staging, allocation.Address, temporary, 92);
        model = NativeBattleBodyBoneMatrices.Compose5095B0(model, prepared.Matrix);
        DrawSource(false, NativeBattleBodyBoneMatrices.Require(model, source));
        Restore(false);
        if (model.LinkedTransform is not null)
        {
            NativeBattlePoseSource linkedSource = resource.LinkedPoseSource ?? throw new NotSupportedException("Shockwave Pulsar requires its current linked skeleton.");
            NativeBattleBodyBoneMatrices current = model.CurrentLinkedBoneMatrices ?? throw new NotSupportedException("Shockwave Pulsar requires its published linked matrices.");
            _ = NativeBattleBodyBoneMatrices.Require(model with { CurrentBodyBoneMatrices = current }, linkedSource);
            ImmutableArray<NativeBattleActionMatrix> composed = NativeBattlePoseMatrices.ComposeStageCamera(current.Matrices, prepared.Matrix, 0);
            model = model with { CurrentLinkedBoneMatrices = current with { Matrices = composed } };
            temporary = temporary.Write(0x50, BitConverter.GetBytes(uint.MaxValue));
            DrawSource(true, composed);
            Restore(true);
        }
        staging = NativeBattleTemporaryMemory.PublishView(staging, allocation.Address, temporary, 92);
        staging = NativeBattleTemporaryMemory.Release5082D0(staging, 92);
        return (frame with { Staging = staging }, graphics with { Models = graphics.Models.SetItem(draw.Actor, model) });

        NativeBattleEffectArena View(uint address, int length) => NativeShockwavePulsar260TemporaryView(staging, address, length);

        void DrawSource(bool linked, ImmutableArray<NativeBattleActionMatrix> matrices)
        {
            NativeBattleResourcePointer pointer = NativeShockwavePulsar260GeometryPointer(frame, model, linked);
            uint geometryAddress = pointer.Cpu?.PhysicalAddress ?? pointer.StagingAddress ?? throw new NotSupportedException("Shockwave Pulsar requires its physical model geometry.");
            NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Copy(temporary, 0x30, NativeBattleEffectArena.Clear().MarkUnknown(0, 44), 0, 44);
            NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Shockwave Pulsar requires its current COMMON packet process.");
            uint bank = process.PacketBank switch { "off_B6D070" => 0x1901018, "off_B6D094" => 0x199B018, _ => throw new NotSupportedException("Shockwave Pulsar requires its actual COMMON bank.") };
            uint packet = checked(bank + (uint)process.PacketBuffer * (uint)process.CommonPacketNominalCapacity + (uint)(process.ByteCursor ?? throw new NotSupportedException("Shockwave Pulsar requires its COMMON cursor.")));
            NativeBattleModelContext44Step drawn = NativeBattleModelContext44.Draw(frame.Memory, context, matrices.Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix, null)).ToImmutableArray(), (offset, length) => ReadNativeShockwavePulsar260Geometry(frame with { Staging = staging }, pointer, offset, length), geometryAddress, View(scratchAddress, scratchLength), scratchLength, projection, colors, definition.NativeGraphics?.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Shockwave Pulsar requires its renderer capability."), 17, 4, packet, pilotIdentity: draw.PilotIdentity is { } identity ? identity with { Primitive = unchecked(identity.Primitive * 2 + (linked ? 1 : 0)), ResourceGeneration = geometryAddress } : null);
            temporary = NativeBattleEffectNodeContextMemory.Copy(drawn.Context44, 0, temporary, 0x30, 44);
            staging = NativeBattleTemporaryMemory.PublishView(staging, scratchAddress, drawn.ProjectedScratch, scratchLength);
            staging = NativeBattleTemporaryMemory.PublishView(staging, allocation.Address, temporary, 92);
            NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame ?? throw new NotSupportedException("Shockwave Pulsar requires its COMMON ordering table.");
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketArena.Append(drawn.Memory.PrimitiveProcess!, primitive.Pending, drawn.Batch);
            frame = frame with { Memory = drawn.Memory with { PrimitiveProcess = written.Process } };
            graphics = graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } };
        }

        void Restore(bool linked)
        {
            NativeBattleTemporaryAllocation boneAllocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 88);
            staging = boneAllocation.Staging;
            NativeBattleEffectArena boneTemporary;
            if (linked)
            {
                NativeBattlePoseSource linkedSource = resource.LinkedPoseSource!;
                NativeBattleDecodedPose decoded = NativeBattlePoseMatrices.ReadLinkedDecodedPose(model.SequenceState ?? throw new NotSupportedException("Shockwave Pulsar requires its current linked pose."), linkedSource);
                ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattlePoseMatrices.StageBoneMatrices(linkedSource, decoded);
                boneTemporary = NativeBattleBodyBoneTemporary.Prepare508C90(linkedSource, decoded, matrices, View(boneAllocation.Address, 88));
                model = model with { CurrentLinkedBoneMatrices = model.CurrentLinkedBoneMatrices! with { Matrices = matrices } };
            }
            else
            {
                NativeBattleBodyBonePreparation restored = NativeBattleBodyBoneTemporary.Prepare508C90(model, source, View(boneAllocation.Address, 88));
                model = restored.Model;
                boneTemporary = restored.Temporary58;
            }
            staging = NativeBattleTemporaryMemory.PublishView(staging, boneAllocation.Address, boneTemporary, 88);
            staging = NativeBattleTemporaryMemory.Release5082D0(staging, 88);
        }
    }

    private (NativeBattleShockwavePulsar260Frame Frame, NativeBattleGraphicsState Graphics) CaptureNativeShockwavePulsar260Bounds(NativeBattleShockwavePulsar260Frame frame, NativeBattleGraphicsState graphics, byte actor, uint destination)
    {
        NativeBattleGraphicsModel model = graphics.Models[actor];
        NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Shockwave Pulsar bounds require their current skeleton.");
        NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new NotSupportedException("Shockwave Pulsar bounds require their current model world matrix.");
        model = NativeBattleBodyBoneMatrices.Compose5095B0(model with { GeometryWorldMatrix = world with { Y = 0 } }, world with { Y = 0 });
        short y = (model.Transform ?? throw new NotSupportedException("Shockwave Pulsar bounds require their current model position.")).Position28.Y;
        model = model with { GeometryWorldMatrix = world with { Y = y } };
        NativeBattleFileStaging staging = frame.Staging ?? throw new NotSupportedException("Shockwave Pulsar bounds require their physical temporary memory.");
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 24);
        staging = allocation.Staging;
        NativeBattleResourcePointer pointer = NativeShockwavePulsar260GeometryPointer(frame, model, false);
        NativeBattleShockwavePulsar260BoundsStep captured = NativeBattleShockwavePulsar260BoundsCapture.Capture693000(frame.Memory, NativeShockwavePulsar260TemporaryView(staging, allocation.Address, 24), NativeBattleBodyBoneMatrices.Require(model, source).Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix, null)).ToImmutableArray(), (offset, length) => ReadNativeShockwavePulsar260Geometry(frame with { Staging = staging }, pointer, offset, length), model.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Shockwave Pulsar bounds require their actual object mask."));
        staging = NativeBattleTemporaryMemory.PublishView(staging, allocation.Address, captured.Temporary24, 24);
        staging = NativeBattleTemporaryMemory.Release5082D0(staging, 24);
        NativeBattleTemporaryAllocation boneAllocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 88);
        NativeBattleBodyBonePreparation restored = NativeBattleBodyBoneTemporary.Prepare508C90(model, source, NativeShockwavePulsar260TemporaryView(boneAllocation.Staging, boneAllocation.Address, 88));
        staging = NativeBattleTemporaryMemory.PublishView(boneAllocation.Staging, boneAllocation.Address, restored.Temporary58, 88);
        staging = NativeBattleTemporaryMemory.Release5082D0(staging, 88);
        NativeBattleEffectArena limits = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(captured.Maximum)).Write(2, BitConverter.GetBytes(captured.Minimum));
        frame = NativeBattleShockwavePulsar260Controller.WriteView(frame with { Memory = captured.Memory, Staging = staging }, destination, limits, 4);
        return (frame, graphics with { Models = graphics.Models.SetItem(actor, restored.Model) });
    }

    private NativeBattleResourcePointer NativeShockwavePulsar260GeometryPointer(NativeBattleShockwavePulsar260Frame frame, NativeBattleGraphicsModel model, bool linked)
    {
        uint? staged = linked ? model.LinkedStagedResourceRow : model.ActiveStagedResourceRow100;
        int? row = linked ? model.LinkedTextureResourceIndex : model.ActiveResourceRow100;
        if (staged is uint physical)
        {
            uint address = checked(physical + 4);
            if (NativeBattleEffectTaskMemory.TryReadPointer(frame.Effect.Pools, address, out NativeBattleResourcePointer? pointer))
            {
                return pointer ?? throw new NotSupportedException("Shockwave Pulsar geometry pointer is NULL.");
            }
            return (IsNativeEffectArenaAddress(address) ? frame.Memory.Arena!.ReadResourcePointer(NativeBattleWaterExecutionFields.ArenaOffset(address, 4)) : frame.Staging!.ReadResourcePointer(address)) ?? throw new NotSupportedException("Shockwave Pulsar geometry pointer has no current owner.");
        }
        NativeBattleModelCpuResourceRow selected = (NativeModelCpuResources ?? throw new NotSupportedException("Shockwave Pulsar requires its model CPU resource rows.")).Rows[row ?? throw new NotSupportedException("Shockwave Pulsar requires its current model resource row.")];
        return new(selected.Require(16), null, selected.Lengths.TryGetValue(16, out int length) ? length : null);
    }

    private byte[] ReadNativeShockwavePulsar260Geometry(NativeBattleShockwavePulsar260Frame frame, NativeBattleResourcePointer pointer, int offset, int length)
    {
        if (pointer.Length is int available && (offset < 0 || length < 0 || offset > available - length))
        {
            throw new InvalidDataException("Shockwave Pulsar model geometry leaves its current source extent.");
        }
        if (pointer.Cpu is { } reference)
        {
            return NativeBattleModelEnemyHeap.Read(NativeModelCpuResources!, frame.Staging!, reference with { Offset = checked(reference.Offset + offset) }, length);
        }
        uint address = checked(pointer.StagingAddress!.Value + (uint)offset);
        if (NativeBattleEffectTaskMemory.TryRead(frame.Effect.Pools, address, length, out byte[] bytes))
        {
            return bytes;
        }
        return IsNativeEffectArenaAddress(address) ? frame.Memory.Arena!.Read(NativeBattleWaterExecutionFields.ArenaOffset(address, length), length) : frame.Staging!.Read(address, length);
    }

    private static NativeBattleEffectArena NativeShockwavePulsar260TemporaryView(NativeBattleFileStaging staging, uint address, int length) => NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(staging, address, length), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
}
