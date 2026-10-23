using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect83Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private static NativeBattleEffect83Frame CaptureNativeEffect83Target(NativeBattleEffect83Frame frame, uint taskAddress, NativeBattleGraphicsState graphics)
    {
        NativeBattleModulePoolTask task = NativeBattleEffect83Controller.Read(frame, taskAddress);
        byte actor = task.ReadByte(0x2D);
        if (actor >= graphics.Models.Length)
        {
            throw new InvalidDataException("Effect83 target capture leaves its physical model bank.");
        }
        uint body = checked(taskAddress + 0x6C);
        frame = NativeBattleEffect83Controller.ClearBytes(frame, body, 88);
        NativeBattleEffectArena context = NativeBattleEffect83Controller.ReadBytes(frame, body, 88);
        context = context.Write(0x40, BitConverter.GetBytes(TransformedAddress)).Write(0x44, BitConverter.GetBytes(DescriptorAddress)).Write(0x48, BitConverter.GetBytes(checked(0x1D972C0u + (uint)actor * 156))).Write(0x4C, BitConverter.GetBytes(0x25C9F74u)).Write(0x50, BitConverter.GetBytes(0x25C9F74u));
        frame = NativeBattleEffect83Controller.WriteBytes(frame, body, context, 88);
        NativeBattleEffect83State state = frame.State.WriteDword(DescriptorAddress + 4, TransformedAddress).WriteWord(DescriptorAddress + 0x14, 0).WriteWord(DescriptorAddress + 0x16, 0).WriteWord(DescriptorAddress + 0x18, 320).WriteWord(DescriptorAddress + 0x1A, 0).Write(DescriptorAddress + 0x1C, [128, 128, 128]).WriteDword(DescriptorAddress + 0x20, uint.MaxValue).WriteWord(DescriptorAddress + 0x24, 0).WriteDword(0x25CF3F0, 0x209FAB8);
        NativeBattleGuardianWorkBuffer work = frame.Memory.GuardianWorkBuffer ?? throw new NotSupportedException("Effect83 requires its separately retained work buffer.");
        return frame with { State = state, Memory = frame.Memory with { GuardianWorkBuffer = work.Write(0, new byte[0x7D00]) } };
    }

    private (NativeBattleGraphicsState Graphics, NativeBattleEffect83Frame Frame) DrawNativeEffect83Target(NativeBattleGraphicsState graphics, NativeBattleEffect83Frame frame, uint bodyAddress, Func<NativeBattleEffect83Frame, NativeBattleGfGeometryBatch, uint, ushort, NativeBattleEffect83Frame> deliver)
    {
        NativeBattleEffectArena context = NativeBattleEffect83Controller.ReadBytes(frame, bodyAddress, 88);
        uint modelAddress = BinaryPrimitives.ReadUInt32LittleEndian(context.Read(0x48, 4));
        uint delta = unchecked(modelAddress - 0x1D972C0);
        if (delta % 156 != 0 || delta / 156 >= (uint)graphics.Models.Length)
        {
            throw new InvalidDataException("Effect83 body requires its captured physical model.");
        }
        int actor = checked((int)(delta / 156));
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, actor);
        NativeBattlePoseSource pose = resource.PoseSource ?? throw new NotSupportedException("Effect83 requires its current target skeleton.");
        _ = resource.LinkedGeometry ?? throw new NotSupportedException("Effect83 requires observed linked geometry presence.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Effect83 requires its camera.");
        NativeBattleActionMatrix world = graphics.Models[actor].GeometryWorldMatrix ?? throw new NotSupportedException("Effect83 requires its current target world matrix.");
        NativeBattlePackedMatrix32 packedWorld = NativeBattlePackedMatrix32.FromMatrix(world, null);
        NativeBattleEffectArena worldBytes = NativeBattleEffectArena.Clear().MarkUnknown(0, 32).Write(0, packedWorld.Bytes.AsSpan());
        worldBytes = worldBytes.MarkUnknown(18, 2);
        context = NativeBattleEffectNodeContextMemory.Copy(worldBytes, 0, context, 0, 32);
        frame = Restore(frame);
        NativeBattlePackedMatrix32 view = camera.CapturePackedView1D97778();
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect83 requires its current geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect83 requires its current arithmetic registers."));
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, view.ReadMatrix(), true, false, view.Known[18] && view.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(view.Bytes.AsSpan().Slice(18, 2)) : null) };
        for (int column = 0; column < 3; column++)
        {
            registers = NativeBattleGeometry.LoadIrColumn(registers, unchecked((ushort)world.Rotation[column]), unchecked((ushort)world.Rotation[3 + column]), unchecked((ushort)world.Rotation[6 + column]));
            registers = NativeBattleGeometry.Transform(registers, false, true);
            for (int row = 0; row < 3; row++)
            {
                context = context.Write(0x20 + (row * 3 + column) * 2, BitConverter.GetBytes(unchecked((short)(registers.Colors.Ir[row] ?? throw new NotSupportedException("Effect83 matrix multiplication requires current IR.")))));
            }
        }
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, view.ReadMatrix(), false, true, null) };
        registers = NativeBattleGeometry.LoadVector(registers, 0, new(unchecked((short)world.X), unchecked((short)world.Y), unchecked((short)world.Z)), null);
        registers = NativeBattleGeometry.Transform(registers);
        for (int row = 0; row < 3; row++)
        {
            context = context.Write(0x34 + row * 4, BitConverter.GetBytes(registers.Colors.Mac[row] ?? throw new NotSupportedException("Effect83 matrix translation requires full current MAC.")));
        }
        frame = frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
        frame = NativeBattleEffect83Controller.WriteBytes(frame, bodyAddress, context, 88);
        Draw(false, NativeBattleBodyBoneMatrices.Require(graphics.Models[actor], pose));
        if (graphics.Models[actor].LinkedTransform is not null)
        {
            NativeBattlePoseSource linkedPose = resource.LinkedPoseSource ?? throw new NotSupportedException("Effect83 linked target requires its skeleton.");
            NativeBattleBodyBoneMatrices linked = graphics.Models[actor].CurrentLinkedBoneMatrices ?? throw new NotSupportedException("Effect83 linked target requires its current bones.");
            ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattleBodyBoneMatrices.Require(graphics.Models[actor] with { CurrentBodyBoneMatrices = linked }, linkedPose);
            Draw(true, matrices);
        }
        frame = Restore(frame);
        return (graphics, frame);

        NativeBattleEffect83Frame Restore(NativeBattleEffect83Frame current)
        {
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging!, 88);
            NativeBattleBodyBonePreparation restored = NativeBattleBodyBoneTemporary.Prepare508C90(graphics.Models[actor], pose, NativeDeformedBodyView(allocation.Staging, allocation.Address, 88));
            graphics = graphics with { Models = graphics.Models.SetItem(actor, restored.Model) };
            NativeBattleFileStaging staged = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, restored.Temporary58, 88);
            return current with { Staging = NativeBattleTemporaryMemory.Release5082D0(staged, 88) };
        }
        void Draw(bool linked, ImmutableArray<NativeBattleActionMatrix> matrices)
        {
            NativeBattleTemporaryAllocation allocated = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging!, 148);
            NativeBattleEffectArena temporary = NativeDeformedBodyView(allocated.Staging, allocated.Address, 148);
            uint color = graphics.Models[actor].Color40 ?? throw new NotSupportedException("Effect83 requires its actual target color.");
            uint mask = graphics.Models[actor].SequenceState?.MeshMask124 ?? throw new NotSupportedException("Effect83 requires its actual target object mask.");
            temporary = temporary.Write(0x48, BitConverter.GetBytes((color & 0xFFFFFF) | 0x24000000)).Write(0x4C, BitConverter.GetBytes((color & 0xFFFFFF) | 0x2C000000)).Write(0x50, BitConverter.GetBytes(mask)).Write(0x90, context.Read(0x54, 2));
            NativeDeformedBodyBridge bridge = new(frame.Memory, frame.Effect, frame.Random, graphics, graphics.Models[actor], allocated.Staging, temporary, allocated.Address);
            NativeBattleResourcePointer pointer = NativeDeformedGeometryPointer(bridge, linked);
            uint sourceAddress = pointer.Cpu?.PhysicalAddress ?? pointer.StagingAddress ?? throw new NotSupportedException("Effect83 requires addressed target geometry.");
            (byte[] bytes, bool[] known) = context.ReadSnapshot(0x20, 32);
            NativeBattlePackedMatrix32 basis = new(bytes.ToImmutableArray(), known.ToImmutableArray());
            uint faceAddress = frame.State.ReadDword(0x25CF3F0);
            int workOffset = checked((int)(faceAddress - 0x209FAB8));
            NativeBattleGuardianWorkBuffer work = frame.Memory.GuardianWorkBuffer ?? throw new NotSupportedException("Effect83 requires its current face records.");
            NativeBattleEffectArena faces = NativeBattleEffectArena.Clear().Write(0, work.Read(workOffset, 0x7D00));
            NativeBattleEffect83SpiralDrawingStep drawn = NativeBattleEffect83SpiralDrawing.Draw(frame.Memory, temporary, ReadSource, sourceAddress, matrices.Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix, null)).ToImmutableArray(), basis, camera.Projection ?? throw new NotSupportedException("Effect83 requires its projection."), frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect83 requires its depth controls."), frame.State.ReadView(TransformedAddress, TransformedLength), TransformedLength, faces);
            NativeBattleFileStaging staged = NativeBattleTemporaryMemory.PublishView(allocated.Staging, allocated.Address, drawn.Temporary148, 148);
            frame = frame with { Memory = drawn.Memory with { GuardianWorkBuffer = work.Write(workOffset, drawn.Faces.Read(0, 0x7D00)) }, State = frame.State.WriteView(TransformedAddress, drawn.Transformed, TransformedLength), Staging = NativeBattleTemporaryMemory.Release5082D0(staged, 148) };
            frame = deliver(frame, drawn.Batch, 0x25C9F74, 0);

            NativeBattleEffectArena ReadSource(uint address, int length)
            {
                long offset = (long)address - sourceAddress;
                if (offset < 0 || offset > int.MaxValue)
                {
                    throw new InvalidDataException("Effect83 target geometry read leaves its current source.");
                }
                return NativeBattleEffectArena.Clear().Write(0, ReadNativeDeformedGeometry(bridge, pointer, (int)offset, length));
            }
        }
    }
}
