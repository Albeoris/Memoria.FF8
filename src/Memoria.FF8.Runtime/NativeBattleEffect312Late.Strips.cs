using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect312Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect312Late
{
    private static NativeBattleEffect312Frame ZeroViewTranslation(NativeBattleEffect312Frame frame, NativeBattleEffect312Inputs inputs)
    {
        NativeBattlePackedMatrix32 view = inputs.ReadView(frame);
        NativeBattleEffectArena bytes = PackedBytes(view);
        bytes = Put(Put(Put(bytes, 20, 0), 24, 0), 28, 0);
        (byte[] data, bool[] known) = bytes.ReadSnapshot(0, 32);
        return LoadMatrix(frame, new(data.ToImmutableArray(), known.ToImmutableArray()));
    }

    private static NativeBattleGeometryRegisterStep LoadPoint(NativeBattleGeometryRegisterStep step, int slot, NativeBattleEffectArena bytes, int offset) => NativeBattleGeometry.LoadVector(step, slot, Vector(bytes, offset), Padding(bytes, offset + 6));

    private sealed class StripWrites(int offset)
    {
        private readonly byte[] bytes = new byte[36];
        private readonly NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[36];

        public void Put(int at, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at), value);
            Array.Fill(mask, NativeBattleGfPacketByteState.Known, at, 4);
        }

        public NativeBattleGfGeometryAttempt Attempt(NativeBattleGeometryPrimitive? primitive) => new(offset, bytes.ToImmutableArray(), mask.ToImmutableArray(), primitive);
        public ImmutableArray<uint> Words() => Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray();
    }

    private static NativeBattleEffect312Step History(NativeBattleEffect312Frame frame, uint address, NativeBattleEffect312Resources resources, NativeBattleEffect312Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        uint group = unchecked(0x223F090u + (uint)(TaskWord(task, 14) * 820));
        (frame, uint temporary) = Allocate(frame, 64);
        int age = TaskWord(task, 12);
        int ring = age % 3;
        uint history = unchecked(group + 0x1B8u + (uint)(ring * 20));
        frame = WriteBytes(frame, history, Put(ReadBytes(frame, history, 20), 0, 1), 4);
        frame = WriteBytes(frame, history + 4, ReadBytes(frame, 0x2240EC8, 8), 8);
        frame = WriteBytes(frame, history + 12, ReadBytes(frame, unchecked(group + 0x38u + (uint)(TaskWord(task, 24) * 8)), 8), 8);
        int requested = age < 21 ? 3 : 24 - age;
        int count = 0;
        while (count < requested)
        {
            uint row = unchecked(group + 0x1B8u + (uint)(ring * 20));
            if (frame.State.ReadDword(row) == 0)
            {
                break;
            }
            frame = WriteBytes(frame, group + 0x1F4u + (uint)(count * 8), ReadBytes(frame, row + 4, 8), 8);
            frame = WriteBytes(frame, group + 0x214u + (uint)(count * 8), ReadBytes(frame, row + 12, 8), 8);
            ring--;
            if (ring < 0)
            {
                ring = 2;
            }
            count++;
        }
        if (count > 1)
        {
            (frame, uint workAddress) = Allocate(frame, 400);
            NativeBattleEffectArena work = ReadBytes(frame, workAddress, 400);
            for (int curve = 0; curve < 2; curve++)
            {
                uint source = group + (curve == 0 ? 0x1F4u : 0x214u);
                uint output = group + (curve == 0 ? 0x234u : 0x2B4u);
                work = NativeBattleGilgameshSwordTrailCurve.Prepare571620(count, ReadBytes(frame, source, count * 8), 0, work);
                NativeBattleEffectArena points = ReadBytes(frame, output, 128);
                for (int index = 0; index < 16; index++)
                {
                    points = NativeBattleGilgameshSwordTrailCurve.Evaluate571690(count, work, index * 4096 / 15, points, index * 8);
                }
                frame = WriteBytes(frame, output, points, 128);
            }
            frame = WriteBytes(frame, workAddress, work, 400);
            frame = Release(frame, 400);
            frame = ZeroViewTranslation(frame, inputs);
            NativeBattleEffectArena scratch = ReadBytes(frame, temporary, 64);
            uint color = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(unchecked(0xCDF670u + (uint)(age % 12 * 4)), 4)) | 0x3A000000;
            scratch = Put(Put(scratch, 20, color), 28, color);
            NativeBattleGeometryRegisterStep registers = Registers(frame);
            NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect312 strips require current projection controls.");
            NativeBattleModelRendererRegisters far = frame.Memory.PrimitiveProcess?.ModelRendererRegisters ?? throw new NotSupportedException("Effect312 strips require current far-color controls.");
            var attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
            int allocated = 0;
            for (int segment = 0; segment < 14; segment++)
            {
                StripWrites first = new(allocated);
                first.Put(0, 0x08000000);
                scratch = Put(scratch, 16, Dword(scratch, 20));
                registers = LoadPoint(registers, 0, ReadBytes(frame, group + 0x234u + (uint)(segment * 8), 8), 0);
                registers = LoadPoint(registers, 1, ReadBytes(frame, group + 0x23Cu + (uint)(segment * 8), 8), 0);
                registers = LoadPoint(registers, 2, ReadBytes(frame, group + 0x2B4u + (uint)(segment * 8), 8), 0);
                registers = NativeBattleGeometry.ProjectTriple(registers, inputs.ReadProjection(frame), controls.Dqa, controls.Dqb);
                uint flags = registers.Colors.Flags!.Value;
                scratch = Put(scratch, 12, flags);
                if ((flags & 0x60000) != 0)
                {
                    attempts.Add(first.Attempt(null));
                    continue;
                }
                uint[] screens = [registers.Geometry.Sxy[0]!.Value, registers.Geometry.Sxy[1]!.Value, registers.Geometry.Sxy[2]!.Value, 0];
                registers = LoadPoint(registers, 0, ReadBytes(frame, group + 0x2BCu + (uint)(segment * 8), 8), 0);
                registers = NativeBattleGeometry.ProjectSingle(registers, inputs.ReadProjection(frame), controls.Dqa, controls.Dqb);
                screens[3] = registers.Geometry.Sxy[2]!.Value;
                registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Effect312 strips require current ZSF4."));
                uint depth = registers.Geometry.Otz!.Value;
                scratch = Put(scratch, 8, depth);
                NativeBattleScriptedDpcsResult shaded = NativeBattleScriptedDpcs.Transform45F270(registers.Colors, Dword(scratch, 28), unchecked((uint)(segment * 4096 / 14)), far, inputs.Colors);
                registers = registers with { Colors = shaded.Registers };
                scratch = Put(scratch, 20, shaded.PackedRgb);
                StripWrites second = new(allocated + 36);
                second.Put(0, 0x08000000);
                uint[] firstColors = [Dword(scratch, 16), Dword(scratch, 20), Dword(scratch, 16), Dword(scratch, 20)];
                uint[] secondColors = [0x3A000000, 0x3A000000, Dword(scratch, 16), Dword(scratch, 20)];
                for (int corner = 0; corner < 4; corner++)
                {
                    first.Put(4 + corner * 8, firstColors[corner]);
                    first.Put(8 + corner * 8, screens[corner]);
                    second.Put(4 + corner * 8, secondColors[corner]);
                    second.Put(8 + corner * 8, screens[corner]);
                }
                int bucket = unchecked(17 + ((int)depth >> 2));
                ImmutableArray<int> payload = registers.Geometry.Sz.Select(value => unchecked((int)value!.Value)).ToImmutableArray();
                attempts.Add(first.Attempt(new(bucket, first.Words(), payload)));
                attempts.Add(second.Attempt(new(bucket, second.Words(), payload)));
                allocated += 72;
            }
            frame = WriteBytes(Publish(frame, registers), temporary, scratch, 64);
            frame = inputs.Execute(frame, new NativeBattleEffect312DrawBatch(new(attempts.ToImmutable(), allocated)));
            frame = frame with { State = frame.State.WriteDword(0x2240EDC, unchecked(frame.State.ReadDword(0x2240EDC) + 36)) };
        }
        frame = Release(frame, 64);
        if (Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        int step = TaskWord(task, 26);
        int cursor = unchecked((short)(TaskWord(task, 24) + step));
        task = TaskPut(TaskPut(TaskPut(task, 24, cursor), 26, step > 2 ? step - 1 : step), 12, age + 1);
        return new(Write(frame, address, task), cursor >= 48 ? 2 : 0);
    }

    private static NativeBattleEffect312Step Wide(NativeBattleEffect312Frame frame, uint address, NativeBattleEffect312Resources resources, NativeBattleEffect312Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        uint group = unchecked(0x223F090u + (uint)(TaskWord(task, 14) * 820));
        (frame, uint temporary) = Allocate(frame, 144);
        int count = TaskWord(task, 24) + 1;
        int age = TaskWord(task, 12);
        if (count >= 2)
        {
            NativeBattleEffectArena scratch = ReadBytes(frame, temporary, 144);
            scratch = PutWord(PutWord(PutWord(scratch, 48, 90), 50, 0), 52, 0);
            scratch = scratch.Write(64, resources.ReadStatic(0xCDF660, 16));
            for (int index = 0; index < count; index++)
            {
                NativeBattleEffectArena center = ReadBytes(frame, group + 0x38u + (uint)(index * 8), 8);
                NativeBattleEffectArena other = ReadBytes(frame, group + 0x38u + (uint)((index == 0 ? 1 : index - 1) * 8), 8);
                scratch = CopyBytes(center, 0, scratch, 32, 8);
                scratch = CopyBytes(other, 0, scratch, 40, 8);
                int dx = index == 0 ? Word(other, 0) - Word(center, 0) : Word(center, 0) - Word(other, 0);
                int dz = index == 0 ? Word(other, 4) - Word(center, 4) : Word(center, 4) - Word(other, 4);
                var direction = NativeBattleActionGeometryRules.NormalizeVector56BC50(dx, 0, dz);
                scratch = Put(Put(Put(scratch, 80, unchecked((uint)direction.X)), 84, unchecked((uint)direction.Y)), 88, unchecked((uint)direction.Z));
                NativeBattleModuleAxisAngle axis = NativeBattleModuleAlignment.Between571480(unchecked((int)Dword(scratch, 64)), unchecked((int)Dword(scratch, 68)), unchecked((int)Dword(scratch, 72)), direction.X, direction.Y, direction.Z);
                scratch = Put(Put(Put(scratch, 96, unchecked((uint)axis.Axis.X)), 100, unchecked((uint)axis.Axis.Y)), 104, unchecked((uint)axis.Axis.Z));
                ImmutableArray<short> rotation = NativeBattleModuleAlignment.Rotation5714F0(axis.Angle, axis.Axis);
                for (int element = 0; element < 9; element++)
                {
                    scratch = PutWord(scratch, 112 + element * 2, rotation[element]);
                }
                NativeBattleVector width = NativeBattleGfMatrices.RotateShort56C4F0(new(rotation, 0, 0, 0, 4096), new(90, 0, 0));
                scratch = PutWord(PutWord(PutWord(scratch, 56, width.X), 58, width.Y), 60, width.Z);
                uint rowAddress = 0x2240A30u + (uint)(index * 24);
                NativeBattleEffectArena row = CopyBytes(center, 0, ReadBytes(frame, rowAddress, 24), 0, 8);
                row = PutWord(PutWord(PutWord(PutWord(PutWord(PutWord(row, 8, Word(center, 0) + width.X), 10, 0), 12, Word(center, 4) + width.Z), 16, Word(center, 0) - width.X), 18, 0), 20, Word(center, 4) - width.Z);
                frame = WriteBytes(frame, rowAddress, row, 24);
            }
            frame = ZeroViewTranslation(frame, inputs);
            scratch = Put(Put(Put(Put(scratch, 16, 0x3A202080), 20, 0x3A040430), 24, 0x3A04040E), 28, 0x3A000000);
            NativeBattleGeometryRegisterStep registers = Registers(frame);
            NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Effect312 strips require current projection controls.");
            if (age >= 24)
            {
                NativeBattleModelRendererRegisters far = frame.Memory.PrimitiveProcess?.ModelRendererRegisters ?? throw new NotSupportedException("Effect312 strips require current far-color controls.");
                NativeBattleScriptedColorRegisters loaded = NativeBattleScriptedDpct.LoadColors45E120(registers.Colors, Dword(scratch, 16), Dword(scratch, 20), Dword(scratch, 24)) with { Ir0 = unchecked((uint)((age - 24) * 341)) };
                registers = registers with { Colors = NativeBattleScriptedDpct.Transform45F4C0(loaded, far, inputs.Colors) };
                for (int index = 0; index < 3; index++)
                {
                    scratch = Put(scratch, 16 + index * 4, registers.Colors.Rgb[index]!.Value | (index < 2 ? 0x3A000000u : 0));
                }
            }
            int allocated = 0;
            var attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
            for (int segment = 0; segment < count - 1; segment++)
            {
                StripWrites first = new(allocated);
                StripWrites second = new(allocated + 36);
                first.Put(0, 0x08000000);
                second.Put(0, 0x08000000);
                uint row = 0x2240A30u + (uint)(segment * 24);
                registers = LoadPoint(registers, 0, ReadBytes(frame, row + 24, 8), 0);
                registers = LoadPoint(registers, 1, ReadBytes(frame, row + 8, 8), 0);
                registers = LoadPoint(registers, 2, ReadBytes(frame, row + 16, 8), 0);
                registers = NativeBattleGeometry.ProjectTriple(registers, inputs.ReadProjection(frame), controls.Dqa, controls.Dqb);
                uint flags = registers.Colors.Flags!.Value;
                scratch = Put(scratch, 12, flags);
                if ((flags & 0x60000) != 0)
                {
                    attempts.Add(first.Attempt(null));
                    attempts.Add(second.Attempt(null));
                    continue;
                }
                uint[] q = registers.Geometry.Sxy.Select(value => value!.Value).ToArray();
                registers = NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Effect312 strips require current ZSF3."));
                uint depth = registers.Geometry.Otz!.Value;
                scratch = Put(scratch, 8, depth);
                registers = LoadPoint(registers, 0, ReadBytes(frame, row, 8), 0);
                registers = LoadPoint(registers, 1, ReadBytes(frame, row + 32, 8), 0);
                registers = LoadPoint(registers, 2, ReadBytes(frame, row + 40, 8), 0);
                registers = NativeBattleGeometry.ProjectTriple(registers, inputs.ReadProjection(frame), controls.Dqa, controls.Dqb);
                uint[] r = registers.Geometry.Sxy.Select(value => value!.Value).ToArray();
                uint[] firstScreens = [r[1], q[0], q[1], r[0]];
                uint[] secondScreens = [q[0], r[2], r[0], q[2]];
                int[] firstIndices = segment == 0 ? [20, 16, 28, 24] : segment == count - 2 ? [28, 24, 20, 16] : [20, 16, 20, 16];
                int[] secondIndices = segment == 0 ? [16, 20, 24, 28] : segment == count - 2 ? [24, 28, 16, 20] : [16, 20, 16, 20];
                for (int corner = 0; corner < 4; corner++)
                {
                    first.Put(4 + corner * 8, Dword(scratch, firstIndices[corner]));
                    first.Put(8 + corner * 8, firstScreens[corner]);
                    second.Put(4 + corner * 8, Dword(scratch, secondIndices[corner]));
                    second.Put(8 + corner * 8, secondScreens[corner]);
                }
                int bucket = unchecked(17 + ((int)depth >> 2));
                ImmutableArray<int> payload = registers.Geometry.Sz.Select(value => unchecked((int)value!.Value)).ToImmutableArray();
                attempts.Add(first.Attempt(new(bucket, first.Words(), payload)));
                attempts.Add(second.Attempt(new(bucket, second.Words(), payload)));
                allocated += 72;
            }
            frame = WriteBytes(Publish(frame, registers), temporary, scratch, 144);
            frame = inputs.Execute(frame, new NativeBattleEffect312DrawBatch(new(attempts.ToImmutable(), allocated)));
            frame = frame with { State = frame.State.WriteDword(0x2240EDC, unchecked(frame.State.ReadDword(0x2240EDC) + 36)) };
        }
        frame = Release(frame, 144);
        if (Paused(frame, inputs))
        {
            return new(frame, 0);
        }
        int step = TaskWord(task, 26);
        int cursor = unchecked((short)(TaskWord(task, 24) + step));
        if (cursor >= 48)
        {
            cursor = 47;
        }
        age = unchecked((short)(age + 1));
        task = TaskPut(TaskPut(TaskPut(task, 24, cursor), 26, step > 2 ? step - 1 : step), 12, age);
        return new(Write(frame, address, task), age >= 36 ? 2 : 0);
    }
}
