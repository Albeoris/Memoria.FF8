using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleMegaSpark279Controller
{
    private const uint RowsBase279 = 0x20DFAB8 + 0x3E68;
    private static (NativeBattleMegaSpark279Frame Frame, uint Address) AllocateRow279(NativeBattleMegaSpark279Frame frame, NativeBattleEffectArena point)
    {
        uint hint = Dword279(ReadBytes(frame, 0x20DFAB8 + 0x7A68, 4), 0);
        uint address = hint;
        if (address != 0)
        {
            frame = WriteBytes(frame, 0x20DFAB8 + 0x7A68, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(0u)), 4);
        }
        else
        {
            for (int index = 0; index < 384; index++)
            {
                uint candidate = RowsBase279 + (uint)(index * 40);
                if (Word279(ReadBytes(frame, candidate + 6, 2), 0) == -1)
                {
                    address = candidate;
                    break;
                }
            }
        }
        if (address == 0)
        {
            return (frame, 0);
        }
        NativeBattleEffectArena row = NativeBattleEffectNodeContextMemory.Copy(point, 0, ReadBytes(frame, address, 40), 0, 8).Write(14, BitConverter.GetBytes((short)2)).Write(32, BitConverter.GetBytes((short)512)).Write(34, BitConverter.GetBytes((short)768)).Write(36, BitConverter.GetBytes(0u));
        return (WriteBytes(frame, address, row, 40), address);
    }

    public static (NativeBattleMegaSpark279Frame Frame, uint RowAddress) BirthRow67FA80(NativeBattleMegaSpark279Frame frame, NativeBattleEffectArena start8, NativeBattleEffectArena end8, NativeBattleEffectArena direction8, NativeBattleEffectArena? optional8, short zeroOrMode, short progress, short step, short wait, short chance, NativeBattleMegaSpark279Resources resources, NativeBattleMegaSpark279Inputs inputs)
    {
        (frame, uint row) = AllocateRow279(frame, start8);
        if (row == 0)
        {
            return (frame, 0);
        }
        (frame, uint address) = Append(frame, 0x25080B0, 0x67FC30);
        if (address == 0)
        {
            return (WriteBytes(frame, row + 6, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)-1)), 2), 0);
        }
        NativeBattleModulePoolTask task = Read(frame, address).WriteDword(16, row).WriteDword(20, row).WriteWord(12, 1).WriteWord(14, unchecked((ushort)wait));
        task = CopyTask(task, 24, start8, 8);
        task = CopyTask(task, 32, end8, 8);
        task = CopyTask(task, 40, direction8, 8).WriteWord(30, unchecked((ushort)progress)).WriteWord(38, unchecked((ushort)step)).WriteWord(46, unchecked((ushort)chance));
        NativeBattleEffectArena source;
        if (optional8 is null)
        {
            frame = WriteBytes(frame, row + 8, NativeBattleEffectArena.Clear().Write(0, new byte[6]), 6);
            source = NativeBattleEffectArena.Clear().MarkUnknown(0, 8);
            for (int axis = 0; axis < 3; axis++)
            {
                frame = Random279(frame, out int value);
                source = source.Write(axis * 2, BitConverter.GetBytes(unchecked((short)((value & 255) - 128))));
            }
        }
        else
        {
            source = optional8;
        }
        task = CopyTask(task, 48, source, 8).WriteWord(54, unchecked((ushort)zeroOrMode));
        return (Write(frame, address, task), row);
    }

    private static NativeBattleMegaSpark279Frame Random279(NativeBattleMegaSpark279Frame frame, out int value)
    {
        value = unchecked((int)(frame.State.ReadDword(0x2508108) * 125 + 14)) & 32767;
        return frame with { State = frame.State.WriteDword(0x2508108, (uint)value) };
    }

    public static (NativeBattleMegaSpark279Frame Frame, int Result) AdvanceRows67FC30(NativeBattleMegaSpark279Frame frame, uint address, NativeBattleMegaSpark279Resources resources, NativeBattleMegaSpark279Inputs inputs)
    {
        NativeBattleEffectArena point = NativeBattleEffectArena.Clear().MarkUnknown(0, 8);
        bool pointInitialized = false;
        for (int iteration = 0; iteration < 8; iteration++)
        {
            NativeBattleModulePoolTask task = Read(frame, address);
            if (Signed(task, 30) >= 4096)
            {
                continue;
            }
            for (int axis = 0; axis < 3; axis++)
            {
                task = task.WriteWord(24 + axis * 2, unchecked((ushort)(task.ReadWord(24 + axis * 2) + task.ReadWord(40 + axis * 2))));
            }
            task = task.WriteWord(30, unchecked((ushort)(task.ReadWord(30) + task.ReadWord(38))));
            for (int axis = 0; axis < 3; axis++)
            {
                frame = Random279(frame, out int value);
                task = task.WriteWord(40 + axis * 2, unchecked((ushort)(task.ReadWord(40 + axis * 2) + (value & 127) - 64)));
            }
            frame = Random279(frame, out int branch);
            if (branch < 4096)
            {
                int[] masks = [511, 511, 255];
                int[] centers = [256, 256, 128];
                for (int axis = 0; axis < 3; axis++)
                {
                    frame = Random279(frame, out int value);
                    task = task.WriteWord(24 + axis * 2, unchecked((ushort)(task.ReadWord(24 + axis * 2) + (value & masks[axis]) - centers[axis])));
                }
            }
            frame = Write(frame, address, task);
            int progress = Signed(task, 30);
            if (progress >= 4096)
            {
                frame = FinishRowProgramme279(frame, task, resources, inputs);
                task = Read(frame, address).WriteWord(30, 4096);
                frame = Write(frame, address, task);
                point = TaskView(task, 32, 8);
                pointInitialized = true;
            }
            else
            {
                if (!pointInitialized)
                {
                    if (inputs.ReadRowPointW is { } readPointW)
                    {
                        point = point.Write(6, BitConverter.GetBytes(readPointW(frame)));
                    }
                    pointInitialized = true;
                }
                (frame, NativeBattleEffectArena blended) = Blend279(frame, TaskView(task, 24, 8), TaskView(task, 32, 8), progress, point, inputs);
                point = blended;
            }
            (frame, uint row) = AllocateRow279(frame, point);
            if (row == 0)
            {
                frame = Write(frame, address, Read(frame, address).WriteWord(30, 4096));
                continue;
            }
            task = Read(frame, address);
            uint last = task.ReadDword(20);
            frame = WriteBytes(frame, last + 36, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(row)), 4);
            task = task.WriteDword(20, row).WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
            frame = Write(frame, address, task);
            progress = Signed(task, 30);
            int sine = resources.ReadSineWord(progress / 2);
            NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.LoadIrColumn(Registers279(frame) with { Colors = Registers279(frame).Colors with { Ir0 = unchecked((uint)sine) } }, task.ReadWord(48), task.ReadWord(50), task.ReadWord(52));
            registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, inputs.Colors);
            frame = Publish279(frame, registers);
            NativeBattleEffectArena velocity = NativeBattleEffectArena.Clear().MarkUnknown(0, 8);
            for (int axis = 0; axis < 3; axis++)
            {
                velocity = velocity.Write(axis * 2, BitConverter.GetBytes(unchecked((short)registers.Colors.Ir[axis]!.Value)));
            }
            frame = WriteBytes(frame, row + 8, velocity, 6);
            if (progress >= 3072)
            {
                continue;
            }
            frame = Random279(frame, out int choice);
            if (choice >= unchecked(Signed(task, 46) << 13))
            {
                continue;
            }
            NativeBattleEffectArena start = NativeBattleEffectArena.Clear().MarkUnknown(0, 8);
            NativeBattleEffectArena end = NativeBattleEffectArena.Clear().MarkUnknown(0, 8);
            NativeBattleEffectArena direction = NativeBattleEffectArena.Clear().MarkUnknown(0, 8);
            for (int axis = 0; axis < 3; axis++)
            {
                frame = Random279(frame, out int value);
                start = start.Write(axis * 2, BitConverter.GetBytes(unchecked((short)((value & (axis == 1 ? 2047 : 4095)) + Signed(task, 32 + axis * 2) - (axis == 1 ? 1024 : 2048)))));
            }
            for (int axis = 0; axis < 3; axis++)
            {
                frame = Random279(frame, out int value);
                direction = direction.Write(axis * 2, BitConverter.GetBytes(unchecked((short)((value & 511) + Signed(task, 40 + axis * 2) - 256))));
            }
            (frame, uint child) = BirthRow67FA80(frame, point, start, direction, TaskView(task, 48, 8), -1, (short)progress, 512, 0, -1, resources, inputs);
            if (child != 0)
            {
                frame = WriteBytes(frame, child + 8, ReadBytes(frame, row, 8), 8);
            }
            task = Read(frame, address);
            frame = Write(frame, address, task.WriteWord(46, unchecked((ushort)(task.ReadWord(46) - 1))));
        }
        NativeBattleModulePoolTask current = Read(frame, address);
        List<uint> rows = Rows279(frame, current.ReadDword(16), current.ReadWord(12));
        foreach (uint row in rows)
        {
            NativeBattleEffectArena view = ReadBytes(frame, row, 40);
            if (Word279(view, 32) > 0)
            {
                short brightness = unchecked((short)(Word279(view, 32) + Word279(view, 34)));
                view = view.Write(32, BitConverter.GetBytes(brightness));
                if (brightness <= 0)
                {
                    view = view.Write(32, BitConverter.GetBytes((short)0));
                }
                else if (Word279(view, 14) != 0 && Word279(view, 34) <= 0)
                {
                    view = view.Write(14, BitConverter.GetBytes(unchecked((short)(Word279(view, 14) - 1))));
                }
                else if (Word279(view, 34) > -384)
                {
                    view = view.Write(34, BitConverter.GetBytes(unchecked((short)(Word279(view, 34) - 320))));
                }
                frame = WriteBytes(frame, row, view, 40);
            }
        }
        foreach (uint row in rows)
        {
            frame = ProjectRow279(frame, row, 16, 20, inputs);
        }
        if (rows.Count >= 2)
        {
            frame = DrawStrip279(frame, rows, false, resources, inputs);
        }
        foreach (uint row in rows)
        {
            NativeBattleEffectArena view = ReadBytes(frame, row, 40);
            for (int axis = 0; axis < 3; axis++)
            {
                view = view.Write(axis * 2, BitConverter.GetBytes(unchecked((short)(Word279(view, axis * 2) + Word279(view, 8 + axis * 2)))));
            }
            frame = WriteBytes(frame, row, view, 40);
        }
        bool retired = false;
        while (rows.Count >= 2 && Word279(ReadBytes(frame, rows[0] + 32, 2), 0) == 0 && Word279(ReadBytes(frame, rows[1] + 32, 2), 0) == 0)
        {
            uint removed = rows[0];
            rows.RemoveAt(0);
            frame = WriteBytes(frame, 0x20DFAB8 + 0x7A68, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(removed)), 4);
            frame = WriteBytes(frame, removed + 6, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)-1)), 2);
            current = Read(frame, address).WriteDword(16, rows[0]).WriteWord(12, (ushort)rows.Count);
            frame = Write(frame, address, current);
            if (rows.Count < 2)
            {
                retired = true;
                break;
            }
        }
        return (frame, retired ? 2 : 0);
    }

    private static List<uint> Rows279(NativeBattleMegaSpark279Frame frame, uint first, int count)
    {
        List<uint> result = [];
        while (first != 0)
        {
            if (result.Count >= 384 || first < RowsBase279 || (first - RowsBase279) % 40 != 0 || first >= RowsBase279 + 384 * 40)
            {
                throw new InvalidDataException("Mega Spark linked rows require their actual current bank.");
            }
            result.Add(first);
            first = Dword279(ReadBytes(frame, first + 36, 4), 0);
        }
        if (result.Count != count)
        {
            throw new InvalidDataException("Mega Spark linked row count differs from its physical chain.");
        }
        return result;
    }

    private static NativeBattleMegaSpark279Frame FinishRowProgramme279(NativeBattleMegaSpark279Frame frame, NativeBattleModulePoolTask task, NativeBattleMegaSpark279Resources resources, NativeBattleMegaSpark279Inputs inputs)
    {
        uint programme = 0;
        uint descriptor = 0;
        int size = 0;
        int scale = 4096;
        if (Signed(task, 46) < 0)
        {
            if (Signed(task, 34) < 0)
            {
                (frame, uint sprite) = Append(frame, 0x2508080, 0x6800F0);
                if (sprite != 0)
                {
                    NativeBattleModulePoolTask target = CopyTask(Read(frame, sprite), 12, TaskView(task, 32, 8), 8).WriteWord(18, 0);
                    frame = Write(frame, sprite, target);
                }
                return frame;
            }
            descriptor = 0x25080A0;
            programme = resources.ProgrammeC.Address;
            size = 136;
        }
        else if (task.ReadWord(14) != 0)
        {
            descriptor = 0x2508090;
            programme = resources.ProgrammeD.Address;
            size = 272;
        }
        if (programme == 0)
        {
            return frame;
        }
        (frame, uint address) = Append(frame, descriptor, 0x680170);
        if (address == 0)
        {
            return frame;
        }
        if (programme == resources.ProgrammeC.Address)
        {
            frame = DrawRandom(frame, out ushort random);
            scale = (random & 4095) + 2048;
        }
        NativeBattleGfAnimationSource source = resources.ReadProgramme(programme);
        var workspace = new NativeBattleGfAnimationWorkspace(TaskView(Read(frame, address), 44, size).ReadSnapshot(0, size).Data.ToImmutableArray()).Initialize7016B0(source, size);
        NativeBattleGraphicsModel caster = inputs.ReadModel(frame, frame.State.ReadDword(0x2508078));
        NativeBattleModelTransform transform = caster.Transform ?? throw new NotSupportedException("Mega Spark programme requires its current caster transform.");
        NativeBattleActionMatrix matrix = NativeBattleFunguarLaser277.Basis50CBA0(Signed(task, 32) - transform.Position28.X, 0, Signed(task, 36) - transform.Position28.Z).Matrix;
        if (scale != 4096)
        {
            matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, scale, scale, scale);
        }
        matrix = matrix with { X = Signed(task, 32), Y = Signed(task, 34), Z = Signed(task, 36) };
        NativeBattleModulePoolTask output = CopyTask(Read(frame, address), 12, PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, null)), 32);
        output = CopyTask(output, 44, NativeBattleEffectArena.Clear().Write(0, workspace.Data.AsSpan()), size);
        return Write(frame, address, output);
    }

    private static (NativeBattleMegaSpark279Frame Frame, NativeBattleEffectArena Point) Blend279(NativeBattleMegaSpark279Frame frame, NativeBattleEffectArena first, NativeBattleEffectArena second, int ratio, NativeBattleEffectArena output, NativeBattleMegaSpark279Inputs inputs)
    {
        NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.LoadIrColumn(Registers279(frame) with { Colors = Registers279(frame).Colors with { Ir0 = unchecked((uint)(4096 - ratio)) } }, unchecked((ushort)Word279(first, 0)), unchecked((ushort)Word279(first, 2)), unchecked((ushort)Word279(first, 4)));
        registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, inputs.Colors);
        registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)ratio) } }, unchecked((ushort)Word279(second, 0)), unchecked((ushort)Word279(second, 2)), unchecked((ushort)Word279(second, 4)));
        registers = NativeBattleRaldo19BallRendering.Weighted(registers, true, inputs.Colors);
        for (int axis = 0; axis < 3; axis++)
        {
            output = output.Write(axis * 2, BitConverter.GetBytes(unchecked((short)registers.Colors.Ir[axis]!.Value)));
        }
        return (Publish279(frame, registers), output);
    }

    private static NativeBattleMegaSpark279Frame ProjectRow279(NativeBattleMegaSpark279Frame frame, uint address, int screenOffset, int depthOffset, NativeBattleMegaSpark279Inputs inputs)
    {
        NativeBattleEffectArena point = ReadBytes(frame, address, 8);
        NativeBattleGeometryRegisterStep registers = Registers279(frame);
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Mega Spark projection requires its current controls.");
        registers = NativeBattleGeometry.ProjectSingle(NativeBattleGeometry.LoadVector(registers, 0, Vector279(point), Padding279(point)), inputs.ReadProjection(frame), controls.Dqa, controls.Dqb);
        frame = Publish279(frame, registers);
        frame = WriteBytes(frame, address + (uint)screenOffset, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(registers.Geometry.Sxy[2]!.Value)), 4);
        return WriteBytes(frame, address + (uint)depthOffset, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(unchecked((short)((int)registers.Geometry.Sz[3]!.Value >> 2)))), 2);
    }
}
