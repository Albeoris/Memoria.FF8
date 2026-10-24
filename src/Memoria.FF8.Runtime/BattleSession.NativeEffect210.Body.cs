using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleEffect210Frame DrawNativeEffect210Body(ref NativeBattleGraphicsState graphics, NativeBattleEffect210Frame frame, NativeBattleEffect210DrawModel9960 operation)
    {
        NativeFileStaging = frame.Staging ?? throw new NotSupportedException("Effect210 body requires its current temporary memory.");
        long actorOffset = (long)operation.ModelAddress - 0x1D972C0;
        if (actorOffset < 0 || actorOffset % 156 != 0 || actorOffset / 156 >= graphics.Models.Length)
        {
            throw new InvalidDataException("Effect210 body requires its actual physical model pointer.");
        }
        byte actor = checked((byte)(actorOffset / 156));
        NativeBattleGraphicsResource resource = NativeGraphicsPoseResource(graphics, actor);
        NativeBattlePoseSource source = resource.PoseSource ?? throw new NotSupportedException("Effect210 body requires its current skeleton.");
        NativeBattleCameraState camera = NativeCamera ?? throw new NotSupportedException("Effect210 body requires its current camera.");
        NativeBattleCameraProjection projection = camera.Projection ?? throw new NotSupportedException("Effect210 body requires its current projection.");
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new NotSupportedException("Effect210 body requires its current CPU resources.");
        int capacity = checked((cpu.ScratchMaximum ?? throw new NotSupportedException("Effect210 body requires its actual projected capacity.")) * 8);
        uint scratch = checked(0x196B018u - (uint)capacity);
        NativeDeformedBodyBridge body = AllocateNativeDeformedBody(frame.Memory, frame.Effect, frame.Random, frame.Staging ?? throw new NotSupportedException("Effect210 body requires its current temporary memory."), graphics, actor, 92);
        NativeBattleVector angles = Vector(operation.Rotation8);
        NativeBattleVector pivot = Vector(operation.Pivot8);
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(angles) with { X = pivot.X, Y = pivot.Y, Z = pivot.Z };
        NativeBattleActionMatrix world = body.Model.GeometryWorldMatrix ?? throw new NotSupportedException("Effect210 body requires its actual world matrix.");
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(camera.CapturePackedView1D97778().ReadMatrix(), NativeBattleGfMatrices.Compose56C2F0(world, local));
        (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(matrix, pivot);
        matrix = matrix with { X = unchecked(matrix.X - x), Y = unchecked(matrix.Y - y), Z = unchecked(matrix.Z - z) };
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(matrix, null);
        body = body with { Temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleEffect210Controller.PackedArena(packed), 0, body.Temporary, 0, 32) };
        body = body with { Temporary = body.Temporary.Write(0x20, BitConverter.GetBytes(x)).Write(0x24, BitConverter.GetBytes(y)).Write(0x28, BitConverter.GetBytes(z)) };
        body = body with { Model = NativeBattleBodyBoneMatrices.Compose5095B0(body.Model, matrix) };
        uint color = body.Model.Color40 ?? throw new NotSupportedException("Effect210 body requires its actual model color.");
        uint mask = body.Model.SequenceState?.MeshMask124 ?? throw new NotSupportedException("Effect210 body requires its actual mesh mask.");
        body = body with { Temporary = body.Temporary.Write(0x34, BitConverter.GetBytes(scratch)).Write(0x44, new byte[4]).Write(0x48, BitConverter.GetBytes((short)320)).Write(0x4A, BitConverter.GetBytes((short)216)).Write(0x4C, BitConverter.GetBytes(color)).Write(0x50, BitConverter.GetBytes(mask)).Write(0x58, new byte[4]) };
        body = body with { Temporary = NativeBattleEffectNodeContextMemory.Copy(NativeDeformedBodyView(body.Staging, 0x1D969A8, 4), 0, body.Temporary, 0x40, 4) };
        body = Draw(body, false, NativeBattleBodyBoneMatrices.Require(body.Model, source));
        body = RestoreNativeDeformedBody(body, source, false);
        _ = resource.LinkedGeometry ?? throw new NotSupportedException("Effect210 body requires its observed linked geometry presence.");
        if (body.Model.LinkedTransform is not null)
        {
            NativeBattlePoseSource linkedSource = resource.LinkedPoseSource ?? throw new NotSupportedException("Effect210 linked body requires its current skeleton.");
            body = body with { Temporary = body.Temporary.Write(0x50, BitConverter.GetBytes(uint.MaxValue)) };
            var composed = ComposeNativeDeformedLinked(body, linkedSource, matrix);
            body = Draw(composed.Bridge, true, composed.Matrices);
            body = RestoreNativeDeformedBody(body, linkedSource, true);
        }
        body = FinishNativeDeformedBody(body, actor, 92);
        graphics = body.Graphics;
        return frame with { Memory = body.Memory, Staging = body.Staging };

        NativeDeformedBodyBridge Draw(NativeDeformedBodyBridge current, bool linked, ImmutableArray<NativeBattleActionMatrix> matrices)
        {
            current = current with { Staging = NativeBattleTemporaryMemory.PublishView(current.Staging, current.TemporaryAddress, current.Temporary, 92) };
            NativeBattlePrimitiveProcess process = current.Memory.PrimitiveProcess ?? throw new NotSupportedException("Effect210 body requires its current COMMON process.");
            NativeBattleResourcePointer pointer = NativeDeformedGeometryPointer(current, linked);
            uint address = pointer.Cpu?.PhysicalAddress ?? pointer.StagingAddress ?? throw new NotSupportedException("Effect210 body requires its physical geometry address.");
            NativeBattleSummonColors colors = definition.NativeGraphics?.Rules.SummonColors ?? throw new NotSupportedException("Effect210 body requires its color arithmetic.");
            uint bank = process.PacketBank switch { "off_B6D070" => 0x1901018, "off_B6D094" => 0x199B018, _ => throw new NotSupportedException("Effect210 body requires its actual COMMON packet bank.") };
            uint cursor = checked(bank + (uint)process.PacketBuffer * (uint)process.CommonPacketNominalCapacity + (uint)(process.ByteCursor ?? throw new NotSupportedException("Effect210 body requires its current COMMON cursor.")));
            NativeBattleEffectArena context44 = NativeBattleEffectNodeContextMemory.Copy(current.Temporary, 0x30, NativeBattleEffectArena.Clear().MarkUnknown(0, 44), 0, 44);
            NativeBattleModelContext44Step drawn = NativeBattleModelContext44.Draw(current.Memory, context44, matrices.Select(matrix => NativeBattlePackedMatrix32.FromMatrix(matrix, null)).ToImmutableArray(), (offset, length) => ReadNativeDeformedGeometry(current, pointer, offset, length), address, NativeDeformedBodyView(current.Staging, scratch, capacity), capacity, projection, colors, definition.NativeGraphics?.PrimitiveCapabilities?.HardwareEnabled ?? throw new NotSupportedException("Effect210 body requires its renderer capability."), 17, 4, cursor);
            current = current with { Memory = drawn.Memory, Temporary = NativeBattleEffectNodeContextMemory.Copy(drawn.Context44, 0, current.Temporary, 0x30, 44), Staging = NativeBattleTemporaryMemory.PublishView(current.Staging, scratch, drawn.ProjectedScratch, capacity) };
            NativeBattlePrimitiveFrame primitive = current.Graphics.PrimitiveFrame ?? throw new NotSupportedException("Effect210 body requires its COMMON ordering table.");
            NativeBattleCommonPacketWrite written = NativeBattleCommonPacketDelivery.Deliver(current.Memory.PrimitiveProcess!, primitive.Pending, drawn.Batch);
            current = current with { Memory = current.Memory with { PrimitiveProcess = written.Process }, Graphics = current.Graphics with { PrimitiveFrame = primitive with { Pending = written.Ordering } } };
            return current with { Staging = NativeBattleTemporaryMemory.PublishView(current.Staging, current.TemporaryAddress, current.Temporary, 92) };
        }

        static NativeBattleVector Vector(NativeBattleEffectArena bytes) => new(BinaryPrimitives.ReadInt16LittleEndian(bytes.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.Read(4, 2)));
    }
}
