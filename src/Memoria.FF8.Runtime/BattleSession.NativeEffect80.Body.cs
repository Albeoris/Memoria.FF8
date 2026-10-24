using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect80Profile;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (NativeBattleGraphicsState Graphics, NativeBattleEffect80Frame Frame) DrawNativeEffect80Body(NativeBattleGraphicsState graphics, NativeBattleEffect80Frame frame, uint bodyAddress, Func<NativeBattleEffect80Frame, NativeBattleGfGeometryBatch, uint, ushort, NativeBattleEffect80Frame> deliver)
    {
        NativeBattleEffectArena context = NativeBattleEffect80Controller.ReadBytes(frame, bodyAddress, BodyContextLength);
        uint descriptorAddress = BinaryPrimitives.ReadUInt32LittleEndian(context.Read(0x60, 4));
        NativeBattleEffectArena descriptor = NativeBattleEffect80Controller.ReadBytes(frame, descriptorAddress, DescriptorLength);
        if (descriptorAddress != DescriptorAddress || BinaryPrimitives.ReadUInt32LittleEndian(descriptor.Read(4, 4)) != RowsAddress)
        {
            throw new InvalidDataException("Effect80 body requires its actual descriptor and projected row owners.");
        }
        uint modelAddress = BinaryPrimitives.ReadUInt32LittleEndian(context.Read(0x64, 4));
        uint delta = unchecked(modelAddress - 0x1D972C0);
        if (delta % 156 != 0 || delta / 156 >= (uint)graphics.Models.Length)
        {
            throw new InvalidDataException("Effect80 body requires its captured physical model.");
        }
        int actor = checked((int)(delta / 156));
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, actor);
        NativeBattlePoseSource pose = resource.PoseSource ?? throw new NotSupportedException("Effect80 requires its current target skeleton.");
        _ = resource.LinkedGeometry ?? throw new NotSupportedException("Effect80 requires observed linked geometry presence.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Effect80 requires its camera.");
        NativeBattleActionMatrix world = graphics.Models[actor].GeometryWorldMatrix ?? throw new NotSupportedException("Effect80 requires its current target world matrix.");
        NativeBattlePackedMatrix32 packedWorld = NativeBattlePackedMatrix32.FromMatrix(world, null);
        NativeBattleEffectArena worldBytes = NativeBattleEffectArena.Clear().MarkUnknown(0, 32).Write(0, packedWorld.Bytes.AsSpan());
        worldBytes = worldBytes.MarkUnknown(18, 2);
        context = NativeBattleEffectNodeContextMemory.Copy(worldBytes, 0, context, 0x20, 32);
        frame = Restore(frame);
        NativeBattlePackedMatrix32 view = camera.CapturePackedView1D97778();
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect80 requires its current geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect80 requires its current arithmetic registers."));
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, view.ReadMatrix(), true, false, view.Known[18] && view.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(view.Bytes.AsSpan().Slice(18, 2)) : null) };
        for (int column = 0; column < 3; column++)
        {
            registers = NativeBattleGeometry.LoadIrColumn(registers, unchecked((ushort)world.Rotation[column]), unchecked((ushort)world.Rotation[3 + column]), unchecked((ushort)world.Rotation[6 + column]));
            registers = NativeBattleGeometry.Transform(registers, false, true);
            for (int row = 0; row < 3; row++)
            {
                context = context.Write(0x40 + (row * 3 + column) * 2, BitConverter.GetBytes(unchecked((short)(registers.Colors.Ir[row] ?? throw new NotSupportedException("Effect80 matrix multiplication requires current IR.")))));
            }
        }
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, view.ReadMatrix(), false, true, null) };
        registers = NativeBattleGeometry.LoadVector(registers, 0, new(unchecked((short)world.X), unchecked((short)world.Y), unchecked((short)world.Z)), null);
        registers = NativeBattleGeometry.Transform(registers);
        for (int row = 0; row < 3; row++)
        {
            context = context.Write(0x54 + row * 4, BitConverter.GetBytes(registers.Colors.Mac[row] ?? throw new NotSupportedException("Effect80 matrix translation requires full current MAC.")));
        }
        frame = frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
        frame = NativeBattleEffect80Controller.WriteBytes(frame, bodyAddress, context, BodyContextLength);
        Draw(false, NativeBattleBodyBoneMatrices.Require(graphics.Models[actor], pose));
        if (graphics.Models[actor].LinkedTransform is not null)
        {
            Draw(true, NativeBattleBodyBoneMatrices.Require(graphics.Models[actor], pose));
        }
        frame = Restore(frame);
        return (graphics, frame);

        NativeBattleEffect80Frame Restore(NativeBattleEffect80Frame current)
        {
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(current.Staging!, 88);
            NativeBattleBodyBonePreparation restored = NativeBattleBodyBoneTemporary.Prepare508C90(graphics.Models[actor], pose, NativeDeformedBodyView(allocation.Staging, allocation.Address, 88));
            graphics = graphics with { Models = graphics.Models.SetItem(actor, restored.Model) };
            NativeBattleFileStaging staged = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, restored.Temporary58, 88);
            return current with { Staging = NativeBattleTemporaryMemory.Release5082D0(staged, 88) };
        }
        void Draw(bool linked, ImmutableArray<NativeBattleActionMatrix> matrices)
        {
            NativeBattleTemporaryAllocation allocated = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging!, 168);
            NativeBattleEffectArena temporary = NativeDeformedBodyView(allocated.Staging, allocated.Address, 168);
            uint color = graphics.Models[actor].Color40 ?? throw new NotSupportedException("Effect80 requires its actual caster color.");
            uint mask = graphics.Models[actor].SequenceState?.MeshMask124 ?? throw new NotSupportedException("Effect80 requires its actual caster object mask.");
            NativeDeformedBodyBridge bridge = new(frame.Memory, frame.Effect, frame.Random, graphics, graphics.Models[actor], allocated.Staging, temporary, allocated.Address);
            NativeBattleResourcePointer pointer = NativeDeformedGeometryPointer(bridge, linked);
            uint sourceAddress = pointer.Cpu?.PhysicalAddress ?? pointer.StagingAddress ?? throw new NotSupportedException("Effect80 requires addressed caster geometry.");
            NativeBattleEffect79BodyDrawingStep drawn = NativeBattleEffect79BodyDrawing.Draw(frame.Memory, context, temporary, ReadSource, sourceAddress, matrices.Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix, null)).ToImmutableArray(), color, mask, camera.Projection ?? throw new NotSupportedException("Effect80 requires its projection."), frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect80 requires depth controls."), frame.State.ReadView(RowsAddress, RowsLength), RowsLength, definition.NativeGraphics!.Rules.SummonColors ?? throw new NotSupportedException("Effect80 requires its arithmetic constants."), depthFieldOffset: 0xC0);
            NativeBattleFileStaging staged = NativeBattleTemporaryMemory.PublishView(allocated.Staging, allocated.Address, drawn.Temporary168, 168);
            frame = frame with { Memory = drawn.Memory, State = frame.State.WriteView(RowsAddress, drawn.Rows, RowsLength), Staging = NativeBattleTemporaryMemory.Release5082D0(staged, 168) };
            frame = deliver(frame, drawn.Batch, 0x1D8E054, 0);

            NativeBattleEffectArena ReadSource(uint address, int length)
            {
                long offset = (long)address - sourceAddress;
                if (offset < 0 || offset > int.MaxValue)
                {
                    throw new InvalidDataException("Effect80 target geometry read leaves its current source.");
                }
                return NativeBattleEffectArena.Clear().Write(0, ReadNativeDeformedGeometry(bridge, pointer, (int)offset, length));
            }
        }
    }
    private NativeBattleEffect80Frame ObserveNativeEffect80Anchor(NativeBattleGraphicsState graphics, NativeBattleEffect80Frame frame, uint taskAddress, uint contextAddress)
    {
        int actor = NativeBattleEffect80Controller.Read(frame, taskAddress).ReadByte(0x2D);
        NativeBattleGraphicsModel model = graphics.Models[actor];
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Effect80 anchor requires its current caster transform.");
        NativeBattlePoseSource pose = NativeGraphicsPoseResource(graphics, actor).PoseSource ?? throw new NotSupportedException("Effect80 anchor requires its current caster skeleton.");
        NativeBattleGraphicsModel temporary = model with { Transform = transform with { Position28 = new(0, 0, 0) }, GeometryWorldMatrix = NativeBattleCuragaMatrices.Identity8DD770() };
        NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(temporary, pose, 0xF1, 0);
        frame = ObserveNativeEffect80BoneAnchor(frame, producer);
        NativeBattleEffectMemory observed = frame.Memory;
        NativeBattleVector point = producer.Enabled ? new(unchecked((short)observed.ScriptedColorRegisters!.Mac[0]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[1]!.Value), unchecked((short)observed.ScriptedColorRegisters!.Mac[2]!.Value)) : producer.DisabledPosition;
        NativeBattleEffectArena destination = NativeBattleEffect80Controller.ReadBytes(frame, contextAddress + 0x70, 8);
        destination = destination.Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));
        return NativeBattleEffect80Controller.WriteBytes(frame with { Memory = observed }, contextAddress + 0x70, destination, 8);
    }

    private NativeBattleEffect80Frame ObserveNativeEffect80BoneAnchor(NativeBattleEffect80Frame frame, NativeBattleBoneAnchorProducer producer)
    {
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect80 bone observation requires its current temporary memory.");
        NativeBattleEffectMemory observed = ObserveNativeBoneAnchor(frame.Memory, producer);
        return frame with { Memory = observed, Staging = NativeFileStaging };
    }

}
