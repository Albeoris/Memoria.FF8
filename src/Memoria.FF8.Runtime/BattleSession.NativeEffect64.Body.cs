using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private static int ActorNativeEffect64(uint pointer, NativeBattleGraphicsState graphics)
    {
        uint delta = unchecked(pointer - 0x1D972C0);
        if (delta % 156 != 0 || delta / 156 >= (uint)graphics.Models.Length)
        {
            throw new InvalidDataException("Effect64 requires an actual physical model row.");
        }
        return checked((int)(delta / 156));
    }

    private NativeBattlePoseSource PoseNativeEffect64(NativeBattleGraphicsState graphics, NativeBattleEffect64Frame frame, uint pointer)
    {
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect64 requires its current resource heap.");
        return NativeGraphicsPoseResource(graphics, ActorNativeEffect64(pointer, graphics)).PoseSource ?? throw new NotSupportedException("Effect64 requires its current skeleton.");
    }

    private (NativeBattleGraphicsState Graphics, NativeBattleEffect64Frame Frame) ApplyNativeEffect64Pose(NativeBattleGraphicsState graphics, NativeBattleEffect64Frame frame, uint pointer, uint offsetAddress)
    {
        int actor = ActorNativeEffect64(pointer, graphics);
        NativeBattlePoseSource pose = PoseNativeEffect64(graphics, frame, pointer);
        NativeBattleEffectArena offsets = NativeBattleEffect64Controller.ReadBytes(frame, offsetAddress, checked(pose.BoneCount * 16));
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging!, 96);
        NativeBattleEffect298PosePreparation prepared = NativeBattleEffect298Pose.Prepare662C00(graphics.Models[actor], pose, offsets, NativeDeformedBodyView(allocation.Staging, allocation.Address, 96));
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, prepared.Temporary96, 96);
        graphics = graphics with { Models = graphics.Models.SetItem(actor, prepared.Model) };
        return (graphics, frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(staging, 96) });
    }

    private (NativeBattleGraphicsState Graphics, NativeBattleEffect64Frame Frame) RestoreNativeEffect64Pose(NativeBattleGraphicsState graphics, NativeBattleEffect64Frame frame, uint pointer)
    {
        int actor = ActorNativeEffect64(pointer, graphics);
        NativeBattlePoseSource pose = PoseNativeEffect64(graphics, frame, pointer);
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging!, 88);
        NativeBattleBodyBonePreparation prepared = NativeBattleBodyBoneTemporary.Prepare508C90(graphics.Models[actor], pose, NativeDeformedBodyView(allocation.Staging, allocation.Address, 88));
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, prepared.Temporary58, 88);
        graphics = graphics with { Models = graphics.Models.SetItem(actor, prepared.Model) };
        return (graphics, frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(staging, 88) });
    }

    private (NativeBattleGraphicsState Graphics, NativeBattleEffect64Frame Frame) DrawNativeEffect64Body(NativeBattleGraphicsState graphics, NativeBattleEffect64Frame frame, uint pointer, uint offsetAddress, Func<NativeBattleEffect64Frame, NativeBattleGfGeometryBatch, uint, ushort, NativeBattleEffect64Frame> deliver)
    {
        int actor = ActorNativeEffect64(pointer, graphics);
        NativeBattlePoseSource pose = PoseNativeEffect64(graphics, frame, pointer);
        NativeBattleEffectArena offsets = NativeBattleEffect64Controller.ReadBytes(frame, offsetAddress, checked(pose.BoneCount * 16));
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Effect64 requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Effect64 requires its current projection.");
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging!, 152);
        NativeBattleEffectArena temporary = NativeDeformedBodyView(allocation.Staging, allocation.Address, 152);
        frame = frame with { Staging = allocation.Staging };
        NativeBattleShadowDrawStep shadow = NativeBattleShadowDrawing.Prepare5088A0(frame.Staging!, new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect64 shadow requires its current geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect64 shadow requires its current arithmetic registers.")), graphics.Models[actor], projection, frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect64 shadow requires its current depth controls."), definition.NativeGraphics?.Rules.ShadowSource ?? throw new NotSupportedException("Effect64 requires its original shadow template."), definition.NativeGraphics?.PrimitiveCapabilities?.GouraudAlpha ?? throw new NotSupportedException("Effect64 requires its primitive capabilities."), 4112, 16);
        frame = frame with { Memory = frame.Memory with { GeometryRegisters = shadow.Registers.Geometry, ScriptedColorRegisters = shadow.Registers.Colors }, Staging = NativeBattleTemporaryMemory.PublishView(shadow.Staging, shadow.TemporaryAddress, shadow.Temporary78, 0x78) };
        frame = deliver(frame, shadow.Batch, 0x1D8E054, 0);
        frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(frame.Staging!, 0x78) };
        (graphics, frame) = ApplyNativeEffect64Pose(graphics, frame, pointer, offsetAddress);
        NativeBattleGraphicsModel model = graphics.Models[actor];
        NativeBattleBodyBoneMatrices bones = model.CurrentBodyBoneMatrices ?? throw new NotSupportedException("Effect64 requires its just-published current offset pose.");
        bones.Validate();
        NativeBattleActionMatrix world = model.GeometryWorldMatrix ?? throw new NotSupportedException("Effect64 requires its actual current world matrix.");
        NativeBattleActionMatrix view = camera.CapturePackedView1D97778().ReadMatrix();
        ImmutableArray<NativeBattleActionMatrix>.Builder composed = ImmutableArray.CreateBuilder<NativeBattleActionMatrix>(bones.Matrices.Length);
        for (int bone = 0; bone < bones.Matrices.Length; bone++)
        {
            NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(world, bones.Matrices[bone]);
            matrix = matrix with { X = unchecked(matrix.X + Signed(bone * 16)), Y = unchecked(matrix.Y + Signed(bone * 16 + 2)), Z = unchecked(matrix.Z + Signed(bone * 16 + 4)) };
            composed.Add(NativeBattleGfMatrices.Compose56C2F0(view, matrix));
        }
        model = model with { CurrentBodyBoneMatrices = bones with { Matrices = composed.MoveToImmutable() } };
        graphics = graphics with { Models = graphics.Models.SetItem(actor, model) };
        int scratchLength = checked((NativeModelCpuResources?.ScratchMaximum ?? throw new NotSupportedException("Effect64 requires its loaded projection capacity.")) * 8);
        uint scratchAddress = checked(0x196B018u - (uint)scratchLength);
        temporary = temporary.Write(4, BitConverter.GetBytes(scratchAddress)).Write(0x4C, BitConverter.GetBytes(offsetAddress)).Write(0xC, new byte[4]).Write(0x10, BitConverter.GetBytes((short)320)).Write(0x12, BitConverter.GetBytes((short)216));
        temporary = temporary.Write(0x14, BitConverter.GetBytes(model.Color40 ?? throw new NotSupportedException("Effect64 requires its current model color."))).Write(0x18, BitConverter.GetBytes((model.SequenceState ?? throw new NotSupportedException("Effect64 requires its current model mask.")).MeshMask124));
        NativeDeformedBodyBridge bridge = new(frame.Memory, frame.Effect, frame.Random, graphics, model, frame.Staging!, temporary, allocation.Address);
        NativeBattleResourcePointer source = NativeDeformedGeometryPointer(bridge, false);
        uint address = source.Cpu?.PhysicalAddress ?? source.StagingAddress ?? throw new NotSupportedException("Effect64 requires its current addressed geometry.");
        NativeBattleEffect64BodyStep drawn = NativeBattleEffect64BodyDrawing.DrawBody(frame.Memory, temporary, NativeDeformedBodyView(frame.Staging!, scratchAddress, scratchLength), ReadGeometry, address, NativeBattleBodyBoneMatrices.Require(model, pose).Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix, null)).ToImmutableArray(), offsets, projection, frame.Memory.ScriptedGraphics!, scratchAddress, scratchLength);
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(frame.Staging!, scratchAddress, drawn.VertexScratch, scratchLength);
        staging = NativeBattleTemporaryMemory.PublishView(staging, allocation.Address, drawn.Temporary152, 152);
        frame = deliver(frame with { Memory = drawn.Memory, Staging = staging }, drawn.Batch, 0x1D8E054, 0);
        (graphics, frame) = ApplyNativeEffect64Pose(graphics, frame, pointer, offsetAddress);
        frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(frame.Staging!, 152) };
        return (graphics, frame);

        short Signed(int offset) => System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(offsets.Read(offset, 2));
        NativeBattleEffectArena ReadGeometry(uint currentAddress, int length)
        {
            long relative = (long)currentAddress - address;
            if (relative < 0 || relative > int.MaxValue)
            {
                throw new InvalidDataException("Effect64 geometry read leaves its current source.");
            }
            return NativeBattleEffectArena.Clear().Write(0, ReadNativeDeformedGeometry(bridge, source, (int)relative, length));
        }
    }

    private NativeBattleEffect64Frame ClearNativeEffect64Geometry(NativeBattleGraphicsState graphics, NativeBattleEffect64Frame frame, uint pointer)
    {
        int actor = ActorNativeEffect64(pointer, graphics);
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging ?? throw new NotSupportedException("Effect64 geometry cleanup requires its temporary memory."), 8);
        frame = frame with { Staging = allocation.Staging };
        NativeDeformedBodyBridge bridge = new(frame.Memory, frame.Effect, frame.Random, graphics, graphics.Models[actor], frame.Staging!, NativeBattleEffectArena.Clear(), allocation.Address);
        NativeBattleResourcePointer source = NativeDeformedGeometryPointer(bridge, false);
        uint address = source.Cpu?.PhysicalAddress ?? source.StagingAddress ?? throw new NotSupportedException("Effect64 cleanup requires its current addressed geometry.");
        NativeBattleEffectArena temporary = NativeBattleEffect64GeometryCleanup.Clear(address, ReadGeometry, WriteWord, NativeDeformedBodyView(frame.Staging!, allocation.Address, 8));
        NativeBattleFileStaging staging = NativeBattleTemporaryMemory.PublishView(frame.Staging!, allocation.Address, temporary, 8);
        return frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(staging, 8) };

        NativeBattleEffectArena ReadGeometry(uint currentAddress, int length)
        {
            long relative = (long)currentAddress - address;
            if (relative < 0 || relative > int.MaxValue)
            {
                throw new InvalidDataException("Effect64 cleanup leaves its current geometry source.");
            }
            return NativeBattleEffectArena.Clear().Write(0, ReadNativeDeformedGeometry(bridge with { Memory = frame.Memory, Staging = frame.Staging! }, source, (int)relative, length));
        }
        void WriteWord(uint destination, ushort value)
        {
            _ = ReadGeometry(destination, 2);
            byte[] bytes = BitConverter.GetBytes(value);
            if (source.Cpu is { Bank: < 3 } cpuReference)
            {
                NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Effect64 cleanup lost its current CPU resource owner.");
                int relative = checked((int)(destination - address));
                NativeBattleModelCpuBank bank = cpu.Cpu.Banks[cpuReference.Bank].Write(checked(cpuReference.Offset + relative), bytes);
                NativeModelCpuResources = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(cpuReference.Bank, bank) } };
            }
            else if (IsNativeEffectArenaAddress(destination))
            {
                frame = NativeBattleEffect64Controller.WriteBytes(frame, destination, NativeBattleEffectArena.Clear().Write(0, bytes), 2);
            }
            else
            {
                frame = frame with { Staging = frame.Staging!.Write(destination, bytes) };
            }
        }
    }
}
