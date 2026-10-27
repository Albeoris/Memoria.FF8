using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect30Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect30Profile;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect30BeamLower
{
    public static NativeBattleEffect30Frame Prepare(NativeBattleEffect30Frame frame, uint address, NativeBattleEffect30Resources resources, NativeBattleEffect30LowerInputs inputs)
    {
        byte caster = (frame.Effect.Parameters ?? throw new NotSupportedException("Effect30 beam requires its captured caster.")).Actor78;
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.ReadWord(0x24) == 0)
        {
            NativeBattleEffect30PointStep target = inputs.ReadBone502170(frame, task.ReadByte(0x2D), 0xF1, 0);
            frame = Write(target.Frame, address, PutXyz(Read(target.Frame, address), 0x7C, target.Position));
        }
        NativeBattleEffect30PointStep actor = inputs.ReadBone502170(frame, caster, 0xF1, 0);
        frame = actor.Frame;
        task = PutXyz(Read(frame, address), 0x74, actor.Position);
        NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(Rotation(inputs.ReadActorBasis(frame, caster)), new(0, 128, 0));
        task = AddXyz(task, 0x74, rotated);
        NativeBattleVector direction = Difference(task, 0x7C, 0x74);
        frame = Look(Write(frame, address, task), address, direction, inputs);
        return Write(frame, address, Read(frame, address).WriteWord(0x8E, unchecked((ushort)Length(direction))));
    }

    public static NativeBattleEffect30Frame Draw(NativeBattleEffect30Frame frame, uint address, NativeBattleEffect30Resources resources, NativeBattleEffect30LowerInputs inputs)
    {
        frame = RotateTask(frame, address, inputs);
        NativeBattleModulePoolTask task = Read(frame, address);
        short ratio = Signed(task, 0x8C);
        int stretch = unchecked(Signed(task, 0x8E) * ratio) / 4864;
        task = task.WriteDword(0x4C, 0x160D538).WriteDword(0x38, unchecked((uint)stretch));
        short distance = unchecked((short)(-128 - unchecked(ratio * 4864) / 4096));
        NativeBattleVector local = NativeBattleGfMatrices.RotateShort56C4F0(Rotation(TaskMatrix(task)), new(0, 0, distance));
        task = PutXyz(task, 0x84, new(unchecked((short)(Signed(task, 0x1C) + local.X)), unchecked((short)(Signed(task, 0x1E) + local.Y)), unchecked((short)(Signed(task, 0x20) + local.Z))));
        frame = DrawMesh(Write(frame, address, task), address, TaskMatrix(task), resources, inputs);
        task = Read(frame, address);
        NativeBattleVector horizontal = Difference(task, 0x7C, 0x74) with { Y = 0 };
        frame = RotateTask(Look(frame, address, horizontal, inputs), address, inputs);
        task = Read(frame, address).WriteWord(0x8E, unchecked((ushort)Length(horizontal))).WriteWord(0x1E, 0);
        stretch = unchecked(Signed(task, 0x8E) * Signed(task, 0x8C)) / 4864;
        task = task.WriteDword(0x38, unchecked((uint)stretch)).WriteDword(0x4C, 0x160DCF8);
        frame = DrawMesh(Write(frame, address, task), address, TaskMatrix(task), resources, inputs);
        task = Read(frame, address);
        if (task.ReadWord(0x8C) != 4096)
        {
            NativeBattleEffectArena saved = TaskView(task, 0x30, 16);
            var savedPointers = task.EnumerateResourcePointers().Where(entry => entry.Key >= 0x30 && entry.Key + 4 <= 0x40).ToArray();
            task = task.WriteDword(0x4C, 0x160DC50).WriteDword(0x30, 4096).WriteDword(0x34, 4096).WriteDword(0x38, 4096);
            task = WriteTaskView(task, 0x1C, TaskView(task, 0x84, 8), 8);
            NativeBattleActionMatrix camera = Rotation(inputs.ReadCamera(frame));
            short[] transposed = Enumerable.Range(0, 9).Select(index => camera.Rotation[index % 3 * 3 + index / 3]).ToArray();
            NativeBattlePackedMatrix32 billboard = NativeBattlePackedMatrix32.FromMatrix(new(transposed.ToImmutableArray(), 0, 0, 0, 4096), null);
            bool[] known = billboard.Known.ToArray();
            Array.Fill(known, false, 20, 12);
            billboard = billboard with { Known = known.ToImmutableArray() };
            frame = DrawMesh(Write(frame, address, task), address, billboard, resources, inputs);
            task = WriteTaskView(Read(frame, address), 0x30, saved, 16);
            if (savedPointers is not null)
            {
                foreach (var entry in savedPointers)
                {
                    task = task.WriteResourcePointer(entry.Key, entry.Value);
                }
            }
            frame = Write(frame, address, task);
        }
        return frame;
    }

    public static NativeBattleEffect30Frame Emit(NativeBattleEffect30Frame frame, uint address, NativeBattleEffect30Resources resources, NativeBattleEffect30LowerInputs inputs)
    {
        short age = Signed(Read(frame, address), 0x24);
        if (age > 18)
        {
            return frame;
        }
        if (age < 0)
        {
            throw new InvalidDataException("Effect30 emission age precedes its original programme.");
        }
        NativeBattleEffect30Scatter row = resources.Scatter[age];
        uint previous = frame.State.ReadDword(0x26D6A28);
        uint weights = unchecked(previous - 8);
        if (weights < NativeBattleEffectNodeContextMemory.Address || (ulong)weights + 8 > NativeBattleEffectNodeContextMemory.Address + NativeBattleEffectNodeContextMemory.Length)
        {
            throw new InvalidDataException("Effect30 interpolation scratch leaves its canonical shared context.");
        }
        frame = frame with { State = frame.State.WriteDword(0x26D6A28, weights) };
        for (int index = 0; index < row.Count; index++)
        {
            frame = frame with { Random = frame.Random.Draw(out ushort value) };
            int factor = row.Center + value % row.Span;
            NativeBattleEffectArena shared = NativeBattleEffectNodeContextMemory.Capture(frame.Memory);
            int offset = checked((int)(weights - NativeBattleEffectNodeContextMemory.Address));
            shared = shared.Write(offset, BitConverter.GetBytes(unchecked(4096 - factor))).Write(offset + 4, BitConverter.GetBytes(factor));
            frame = frame with { Memory = NativeBattleEffectNodeContextMemory.Publish(frame.Memory, shared) };
            NativeBattleEffectHierarchyAppend child = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(4, ParticleCallback, 96, address, inputs.SelectActors);
            if (child.Address == 0)
            {
                throw new NotSupportedException("Effect30 exhausted its native particle pool.");
            }
            frame = frame with { Effect = frame.Effect with { Pools = child.Hierarchy.Pools } };
            NativeBattleModulePoolTask task = Read(frame, address);
            NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), frame.Memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown());
            registers = Blend(registers, Xyz(task, 0x74), 4096 - factor, false, inputs.Colors);
            registers = Blend(registers, Xyz(task, 0x7C), factor, true, inputs.Colors);
            NativeBattleVector point = new(unchecked((short)registers.Colors.Ir[0]!.Value), unchecked((short)registers.Colors.Ir[1]!.Value), unchecked((short)registers.Colors.Ir[2]!.Value));
            frame = frame with { Memory = frame.Memory with { ScriptedColorRegisters = registers.Colors } };
            frame = Write(frame, child.Address, PutXyz(Read(frame, child.Address), 0x1C, point));
            frame = Jitter(frame, child.Address, 128);
        }
        return frame with { State = frame.State.WriteDword(0x26D6A28, previous) };
    }

    private static NativeBattleEffect30Frame DrawMesh(NativeBattleEffect30Frame frame, uint address, NativeBattlePackedMatrix32 basis, NativeBattleEffect30Resources resources, NativeBattleEffect30LowerInputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if ((task.ReadByte(0x26) & 4) != 0)
        {
            return frame;
        }
        NativeBattleActionMatrix local = Rotation(basis) with { X = Signed(task, 0x1C), Y = Signed(task, 0x1E), Z = Signed(task, 0x20) };
        local = NativeBattlePoseMatrices.Scale56BEF0(local, unchecked((int)task.ReadDword(0x30)), unchecked((int)task.ReadDword(0x34)), unchecked((int)task.ReadDword(0x38)));
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadCamera(frame).ReadMatrix(), local);
        frame = frame with { Memory = frame.Memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(frame.Memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), composed, true, true, null) } };
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(Staging(frame), 88);
        NativeBattleEffectArena temporary = NativeBattleEffectNodeContextMemory.Copy(NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 88), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 88), 0, 88);
        uint meshAddress = task.ReadDword(0x4C);
        temporary = temporary.Write(0, BitConverter.GetBytes(meshAddress));
        temporary = NativeBattleEffectNodeContextMemory.Copy(TaskView(task, 0x40, 4), 0, temporary, 8, 4).Write(12, BitConverter.GetBytes((int)Signed(task, 0x50))).Write(0x1C, BitConverter.GetBytes(0xF0u));
        NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.Draw(frame.Memory, resources.Meshes.TryGetValue(meshAddress, out ImmutableArray<byte> mesh) ? new NativeBattleStaticMeshResource(0, meshAddress, mesh) : throw new NotSupportedException("Effect30 beam selected an unsupported mesh."), composed, inputs.Projection, temporary, inputs.Colors);
        frame = frame with { Memory = drawn.Memory, Staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, drawn.Context, 88) };
        frame = inputs.DeliverPrivate(frame, drawn.Batch);
        return frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(Staging(frame), 88) };
    }

    private static NativeBattleEffect30Frame Look(NativeBattleEffect30Frame frame, uint address, NativeBattleVector direction, NativeBattleEffect30LowerInputs inputs)
    {
        NativeBattleEffect30MatrixStep result = inputs.LookBasis8DDA50(frame, TaskMatrix(Read(frame, address)), direction, new(0, 4096, 0));
        return WriteRotation(result.Frame, address, result.Matrix);
    }

    private static NativeBattleEffect30Frame RotateTask(NativeBattleEffect30Frame frame, uint address, NativeBattleEffect30LowerInputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleStatefulMatrixRotationStep result = NativeBattleStatefulMatrixRotation.Rotate(frame.Memory, Staging(frame), TaskMatrix(task), 'Y', Signed(task, 0x46));
        return WriteRotation(frame with { Memory = result.Memory, Staging = result.Staging }, address, result.Matrix);
    }

    private static NativeBattleEffect30Frame Jitter(NativeBattleEffect30Frame frame, uint address, short radius)
    {
        frame = frame with { Random = frame.Random.Draw(out ushort yaw) };
        frame = frame with { Random = frame.Random.Draw(out ushort pitch) };
        NativeBattlePackedMatrix32 identity = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
        NativeBattleStatefulMatrixRotationStep first = NativeBattleStatefulMatrixRotation.Rotate(frame.Memory, Staging(frame), identity, 'Y', (short)(yaw & 4095));
        NativeBattleStatefulMatrixRotationStep second = NativeBattleStatefulMatrixRotation.Rotate(first.Memory, first.Staging, first.Matrix, 'X', (short)(pitch & 4095));
        frame = frame with { Memory = second.Memory, Staging = second.Staging, Random = frame.Random.Draw(out ushort distance) };
        NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(Rotation(second.Matrix), new(0, 0, unchecked((short)(distance % (radius == 0 ? 1 : radius)))));
        return Write(frame, address, AddXyz(Read(frame, address), 0x1C, offset));
    }

    private static NativeBattleGeometryRegisterStep Blend(NativeBattleGeometryRegisterStep registers, NativeBattleVector point, int factor, bool accumulate, NativeBattleSummonColors constants)
    {
        registers = registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)factor) } };
        registers = NativeBattleGeometry.LoadIrColumn(registers, unchecked((ushort)point.X), unchecked((ushort)point.Y), unchecked((ushort)point.Z));
        return NativeBattleRaldo19BallRendering.Weighted(registers, accumulate, constants);
    }

    private static NativeBattleEffect30Frame WriteRotation(NativeBattleEffect30Frame frame, uint address, NativeBattlePackedMatrix32 matrix)
    {
        _ = Rotation(matrix);
        return Write(frame, address, Read(frame, address).WriteBytes(0x54, matrix.Bytes.AsSpan(0, 18)));
    }

    private static NativeBattleActionMatrix Rotation(NativeBattlePackedMatrix32 matrix)
    {
        matrix.Validate();
        if (matrix.Known.AsSpan(0, 18).Contains(false))
        {
            throw new NotSupportedException("Effect30 requires the actual rotation words.");
        }
        return new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(matrix.Bytes.AsSpan().Slice(index * 2, 2))).ToImmutableArray(), 0, 0, 0, 4096);
    }

    private static NativeBattlePackedMatrix32 TaskMatrix(NativeBattleModulePoolTask task)
    {
        NativeBattleEffectArena view = TaskView(task, 0x54, 32);
        (byte[] bytes, bool[] known) = view.ReadSnapshot(0, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }

    private static NativeBattleEffectArena TaskView(NativeBattleModulePoolTask task, int offset, int length)
    {
        task.RequirePayloadRange(offset, length);
        task.ValidateUnknownPayloadRanges();
        NativeBattleEffectArena view = NativeBattleEffectArena.Clear().Write(0, task.ReadRawPayloadSpan().Slice(offset - 12, length));
        if (task.ReadUnknownPayloadRanges() is { } ranges)
        {
            foreach (NativeBattleEffectUnknownRegion region in ranges)
            {
                int first = Math.Max(offset, region.Offset);
                int end = Math.Min(offset + length, region.Offset + region.Length);
                if (end > first)
                {
                    view = view.MarkUnknown(first - offset, end - first);
                }
            }
        }
        return view;
    }

    private static NativeBattleModulePoolTask WriteTaskView(NativeBattleModulePoolTask task, int offset, NativeBattleEffectArena view, int length)
    {
        (byte[] bytes, bool[] known) = view.ReadSnapshot(0, length);
        task = task.WriteBytes(offset, bytes);
        for (int index = 0; index < length; index++)
        {
            if (!known[index])
            {
                task = task.MarkPayloadUnknown(offset + index, 1);
            }
        }
        return task;
    }

    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleVector Xyz(NativeBattleModulePoolTask task, int offset) => new(Signed(task, offset), Signed(task, offset + 2), Signed(task, offset + 4));
    private static NativeBattleVector Difference(NativeBattleModulePoolTask task, int first, int second) => new(unchecked((short)(Signed(task, first) - Signed(task, second))), unchecked((short)(Signed(task, first + 2) - Signed(task, second + 2))), unchecked((short)(Signed(task, first + 4) - Signed(task, second + 4))));
    private static NativeBattleModulePoolTask PutXyz(NativeBattleModulePoolTask task, int offset, NativeBattleVector value) => task.WriteWord(offset, unchecked((ushort)value.X)).WriteWord(offset + 2, unchecked((ushort)value.Y)).WriteWord(offset + 4, unchecked((ushort)value.Z));
    private static NativeBattleModulePoolTask AddXyz(NativeBattleModulePoolTask task, int offset, NativeBattleVector value) => PutXyz(task, offset, new(unchecked((short)(Signed(task, offset) + value.X)), unchecked((short)(Signed(task, offset + 2) + value.Y)), unchecked((short)(Signed(task, offset + 4) + value.Z))));
    private static int Length(NativeBattleVector value) => NativeBattleRayBomb10Geometry.Sqrt56BEC0(unchecked(value.X * value.X + value.Y * value.Y + value.Z * value.Z));
    private static NativeBattleFileStaging Staging(NativeBattleEffect30Frame frame) => frame.Staging ?? throw new NotSupportedException("Effect30 requires its physical native temporary memory.");
}
