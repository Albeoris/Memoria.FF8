using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect140Controller;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleEffect140Frame DrawNativeEffect140OwnedModel(NativeBattleEffect140Frame frame, NativeBattleEffect140DrawOwnedModel draw, NativeBattleEffect140Resources resources, NativeBattleEffect140Inputs inputs)
    {
        NativeBattleEffect140OwnedModelSnapshot snapshot = NativeBattleEffect140OwnedModel.ReadCurrent(frame, draw.ModelAddress, resources);
        int scratchLength = checked(snapshot.Geometry.Max(mesh => mesh.Vertices.Length) * 8);
        if (draw.ScratchAddress != frame.State.ReadDword(0x2517B28) + 0x878 || scratchLength > 0x2EE0)
        {
            throw new InvalidDataException("Effect140 body requires its dedicated current projected rows before the first packet bank.");
        }
        (frame, uint temporaryAddress) = Allocate(frame, draw.Custom ? 96 : 76);
        NativeBattleEffectArena temporary = ReadBytes(frame, temporaryAddress, draw.Custom ? 96 : 76);
        NativeBattlePackedMatrix32 world = CaptureMatrix(ReadBytes(frame, draw.ModelAddress + 0x40, 32));
        NativeBattlePackedMatrix32 view = inputs.ReadView(frame);
        NativeBattleActionMatrix basis = draw.Custom ? world.ReadMatrix() : NativeBattlePoseMatrices.Compose56C2F0(view.ReadMatrix(), world.ReadMatrix());
        ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattlePoseMatrices.ComposeStageCamera(snapshot.Bones.Select(bone => bone.ReadMatrix()).ToImmutableArray(), basis, 0);
        NativeBattleFileStaging staging = NativeBattleEffectOwnedModelRendering.PublishMatrices(RequireStaging(frame), resources.Model, matrices, index => snapshot.Bones[index].Known[18] && snapshot.Bones[index].Known[19] ? BinaryPrimitives.ReadUInt16LittleEndian(snapshot.Bones[index].Bytes.AsSpan().Slice(18, 2)) : null);
        frame = frame with { Staging = staging };
        uint color = BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(frame, draw.ModelAddress + 0x28, 4).Read(0, 4));
        uint mask = BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(frame, draw.ModelAddress + 0x7C, 4).Read(0, 4));
        NativeBattleEffectArena scratch = ReadBytes(frame, draw.ScratchAddress, scratchLength);
        NativeBattleGfGeometryBatch batch;
        if (draw.Custom)
        {
            temporary = temporary.Write(4, BitConverter.GetBytes(draw.ScratchAddress)).Write(0xC, new byte[4]).Write(0x10, BitConverter.GetBytes((short)320)).Write(0x12, BitConverter.GetBytes((short)216)).Write(0x14, BitConverter.GetBytes(color)).Write(0x18, BitConverter.GetBytes(mask)).Write(0x4C, BitConverter.GetBytes(draw.FarInput)).Write(0x58, new byte[4]).MarkUnknown(0x54, 4);
            NativeBattleEffect69CasterDrawingStep rendered = NativeBattleEffect69CasterDrawing.Draw(frame.Memory, temporary, (address, length) => ReadBytes(frame, address, length), resources.Model.Address + (uint)resources.Model.Section(1), NativeBattleEffectOwnedModelRendering.CaptureBones(staging, resources.Model), view, inputs.ReadProjection(frame), frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect140 body requires its current GTE controls."), scratch, scratchLength, pilotIdentity: draw.PilotIdentity);
            frame = frame with { Memory = rendered.Memory };
            temporary = rendered.Temporary96;
            scratch = rendered.Scratch;
            batch = rendered.Batch;
        }
        else
        {
            temporary = NativeBattleEffectOwnedModelRendering.StoreMatrix(temporary, 0, NativeBattlePackedMatrix32.FromMatrix(basis, null));
            temporary = temporary.Write(0x24, BitConverter.GetBytes(draw.ScratchAddress)).Write(0x34, new byte[4]).Write(0x38, BitConverter.GetBytes((short)320)).Write(0x3A, BitConverter.GetBytes((short)216)).Write(0x3C, BitConverter.GetBytes(color)).Write(0x40, BitConverter.GetBytes(mask)).Write(0x44, new byte[2]).Write(0x48, new byte[4]);
            temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(RequireStaging(frame), 0x1D969A8, 4), 0, temporary, 0x30, 4);
            NativeBattlePrimitiveProcess process = frame.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect140 body requires its current renderer process.");
            uint geometry = resources.Model.Address + (uint)resources.Model.Section(1);
            NativeBattleEffectArena context = NativeBattleEffectNodeContextMemory.Copy(temporary, 0x20, NativeBattleEffectArena.Clear().MarkUnknown(0, 44), 0, 44);
            NativeBattleModelContext44Step rendered = NativeBattleModelContext44.Draw(frame.Memory, context, NativeBattleEffectOwnedModelRendering.CaptureBones(staging, resources.Model), (offset, length) => ReadBytes(frame, checked(geometry + (uint)offset), length).Read(0, length), geometry, scratch, scratchLength, inputs.ReadProjection(frame), inputs.Colors, definition.NativeGraphics?.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Effect140 body requires its actual hardware capability."), 17, 4, CurrentPacketAddress(process), pilotIdentity: draw.PilotIdentity);
            temporary = NativeBattleEffectNodeContextMemory.Copy(rendered.Context44, 0, temporary, 0x20, 44);
            frame = frame with { Memory = rendered.Memory };
            scratch = rendered.ProjectedScratch;
            batch = rendered.Batch;
        }
        frame = WriteBytes(frame, temporaryAddress, temporary, draw.Custom ? 96 : 76);
        frame = WriteBytes(frame, draw.ScratchAddress, scratch, scratchLength);
        frame = inputs.Execute(frame, new NativeBattleEffect140DrawBatch(batch, 0x1D8E054));
        (frame, uint restoreAddress) = Allocate(frame, 88);
        NativeBattleEffectArena skeleton = NativeBattleArenaPose.RestoreBoneMatrices(ReadBytes(frame, resources.Model.Address + (uint)resources.Model.Section(0), resources.Model.PoseSource.Skeleton.Length), 0, snapshot.Pose);
        ImmutableArray<NativeBattleActionMatrix> local = Enumerable.Range(0, snapshot.Pose.Bones.Length).Select(index => NativeBattleArenaPose.ReadMatrix(skeleton, 32 + index * 48)).ToImmutableArray();
        NativeBattleEffectArena restored = NativeBattleBodyBoneTemporary.Prepare508C90(resources.Model.PoseSource, snapshot.Pose, local, ReadBytes(frame, restoreAddress, 88));
        frame = WriteBytes(frame, resources.Model.Address + (uint)resources.Model.Section(0), skeleton, resources.Model.PoseSource.Skeleton.Length);
        frame = WriteBytes(frame, restoreAddress, restored, 88);
        return Release(Release(frame, 88), draw.Custom ? 96 : 76);

        static NativeBattlePackedMatrix32 CaptureMatrix(NativeBattleEffectArena bytes)
        {
            (byte[] data, bool[] known) = bytes.ReadSnapshot(0, 32);
            return new(data.ToImmutableArray(), known.ToImmutableArray());
        }

        static uint CurrentPacketAddress(NativeBattlePrimitiveProcess process)
        {
            uint bank = process.PacketBank switch
            {
                "off_B6D070" => 0x1901018,
                "off_B6D094" => 0x199B018,
                _ => throw new NotSupportedException("Effect140 body requires its current COMMON packet bank.")
            };
            return checked(bank + (uint)process.PacketBuffer * (uint)process.CommonPacketNominalCapacity + (uint)(process.ByteCursor ?? throw new NotSupportedException("Effect140 body requires its current packet cursor.")));
        }
    }
}
