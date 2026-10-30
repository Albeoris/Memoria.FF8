using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleAcid317Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleAcid317Controller
{
    private static NativeBattleAcid317Frame StartTrail(NativeBattleAcid317Frame frame, NativeBattleVector source, NativeBattleVector destination, short tag, short progress, short increment, short scale, int angle, bool capturePresentation = false)
    {
        (frame, uint head) = AllocatePoint(frame, source, tag, capturePresentation);
        if (head == 0)
        {
            return frame;
        }
        (frame, uint address) = Append(frame, 2, 0x660AD0);
        if (address == 0)
        {
            return PointWord(frame, head + 6, -1);
        }
        NativeBattleModulePoolTask task = Read(frame, address).WriteBytes(12, new byte[68]).WriteWord(12, 0).WriteWord(14, 1).WriteDword(0x10, head).WriteDword(0x14, head).WriteWord(0x18, unchecked((ushort)source.X)).WriteWord(0x1A, unchecked((ushort)source.Y)).WriteWord(0x1C, unchecked((ushort)source.Z)).WriteWord(0x1E, unchecked((ushort)progress)).WriteWord(0x20, unchecked((ushort)destination.X)).WriteWord(0x22, unchecked((ushort)destination.Y)).WriteWord(0x24, unchecked((ushort)destination.Z)).WriteWord(0x26, unchecked((ushort)increment)).WriteWord(0x48, unchecked((ushort)tag)).WriteWord(0x4A, unchecked((ushort)angle)).WriteWord(0x4C, unchecked((ushort)scale));
        NativeBattleActionMatrix basis;
        if (source == destination)
        {
            basis = new(new short[9].ToImmutableArray(), 0, 0, 0, 4096);
        }
        else
        {
            NativeBattleActionMatrix transposed = NativeBattleActionGeometryRules.CaptureMatrix(source, destination, new(0, 0, 0));
            basis = transposed with { Rotation = [transposed.Rotation[0], transposed.Rotation[3], transposed.Rotation[6], transposed.Rotation[1], transposed.Rotation[4], transposed.Rotation[7], transposed.Rotation[2], transposed.Rotation[5], transposed.Rotation[8]] };
        }
        for (int index = 0; index < 9; index++)
        {
            task = task.WriteWord(0x28 + index * 2, unchecked((ushort)basis.Rotation[index]));
        }
        frame = Write(frame, address, task);
        frame = WriteArena(frame, head + 8, BitConverter.GetBytes(frame.State.ReadDword(0x25054D8)));
        return WriteArena(frame, head + 12, BitConverter.GetBytes(frame.State.ReadDword(0x25054DC)));
    }

    private static (NativeBattleAcid317Frame Frame, uint Address) AllocatePoint(NativeBattleAcid317Frame frame, NativeBattleVector position, short tag, bool capturePresentation)
    {
        for (int index = 0; index < PointCount; index++)
        {
            uint address = Working(frame) + PointsOffset + (uint)(index * PointStride);
            if (PointSigned(frame, address + 6) == -1)
            {
                frame = PointVector(frame, address, position);
                frame = PointWord(PointWord(PointWord(frame, address + 6, tag), address + 14, 10), address + 0x20, 2048);
                if (capturePresentation)
                {
                    NativeBattlePilotVisualLifetimes.Allocated(frame.Memory.Arena ?? throw new NotSupportedException("Acid point birth requires its current native arena."), Working(frame) + PointsOffset, index);
                }
                return (WriteArena(frame, address + 0x24, BitConverter.GetBytes(0u)), address);
            }
        }
        return (frame, 0);
    }

    private static (NativeBattleAcid317Frame Frame, int Result) Trail(NativeBattleAcid317Frame frame, uint address, NativeBattleAcid317Resources resources, NativeBattleAcid317Inputs inputs)
    {
        if (Read(frame, address).Callback != 0x660AD0)
        {
            throw new NotSupportedException("Acid reached an unsupported trail callback.");
        }
        for (int pass = 0; pass < 2; pass++)
        {
            NativeBattleModulePoolTask task = Read(frame, address);
            if (Signed(task, 0x1E) < 4096)
            {
                short progress = unchecked((short)(task.ReadWord(0x1E) + task.ReadWord(0x26)));
                frame = Write(frame, address, task.WriteWord(0x1E, unchecked((ushort)progress)));
                NativeBattleVector source = new(Signed(task, 0x18), Signed(task, 0x1A), Signed(task, 0x1C));
                NativeBattleVector destination = new(Signed(task, 0x20), Signed(task, 0x22), Signed(task, 0x24));
                frame = Interpolate(frame, source, destination, 4096 - progress, progress, inputs, out NativeBattleVector point);
                int sine = NativeBattleCameraMath.Sine(progress >> 1);
                point = point with { Y = unchecked((short)(point.Y - (unchecked((short)sine) >> 2))) };
                short radius = unchecked((short)(unchecked((short)sine) >> 4));
                int angle = Signed(task, 0x4A);
                NativeBattleVector radial = new(unchecked((short)(unchecked(NativeBattleCameraMath.Sine(angle) * radius) >> 12)), unchecked((short)(unchecked(NativeBattleCameraMath.Cosine(angle) * radius) >> 12)), 0);
                NativeBattleActionMatrix basis = new(Enumerable.Range(0, 9).Select(index => Signed(task, 0x28 + index * 2)).ToImmutableArray(), 0, 0, 0, 4096);
                NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(basis, radial);
                point = new(unchecked((short)(point.X + rotated.X)), unchecked((short)(point.Y + rotated.Y)), unchecked((short)(point.Z + rotated.Z)));
                frame = Write(frame, address, Read(frame, address).WriteWord(0x4A, unchecked((ushort)(task.ReadWord(0x4A) + 256))));
                if (progress >= 4096 && Signed(task, 0x48) >= 0)
                {
                    (frame, uint child) = Append(frame, 3, 0x660D40);
                    if (child != 0)
                    {
                        frame = Write(frame, child, Position(Read(frame, child).WriteDword(12, 0), point).WriteWord(0x16, unchecked((ushort)(task.ReadWord(0x4C) << 4))));
                    }
                }
                (frame, uint next) = AllocatePoint(frame, point, Signed(task, 0x4C), inputs.CapturePresentation);
                if (next == 0)
                {
                    frame = Write(frame, address, Read(frame, address).WriteWord(0x1E, 4096));
                }
                else
                {
                    task = Read(frame, address);
                    frame = WriteArena(frame, task.ReadDword(0x14) + 0x24, BitConverter.GetBytes(next));
                    frame = Write(frame, address, task.WriteDword(0x14, next).WriteWord(14, unchecked((ushort)(task.ReadWord(14) + 1))));
                    frame = PointWord(PointWord(PointWord(frame, next + 8, unchecked((short)(rotated.X >> 3))), next + 10, unchecked((short)(rotated.Y >> 3))), next + 12, unchecked((short)(rotated.Z >> 3)));
                }
            }
            uint cursor = Read(frame, address).ReadDword(0x10);
            for (int visited = 0; cursor != 0; visited++)
            {
                RequirePoint(frame, cursor, visited);
                short color = PointSigned(frame, cursor + 0x20);
                if (color > 0)
                {
                    color = unchecked((short)(color - (color > 1024 ? 256 : 128)));
                    frame = PointWord(frame, cursor + 0x20, Math.Max((short)0, color));
                }
                cursor = PointDword(frame, cursor + 0x24);
            }
        }
        NativeBattleModulePoolTask current = Read(frame, address);
        short count = Signed(current, 14);
        uint head = current.ReadDword(0x10);
        frame = inputs.Execute(frame, new NativeBattleAcid317ProjectPoints(head, count) { PilotIdentity = inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(317, current, 2) : null });
        if (count >= 2)
        {
            frame = inputs.Execute(frame, new NativeBattleAcid317DrawTrail(head, count) { PilotIdentity = inputs.CapturePresentation ? NativeBattlePilotVisualLifetimes.Identity(317, current, 2) : null });
        }
        for (uint cursor = head, visited = 0; cursor != 0; visited++)
        {
            RequirePoint(frame, cursor, checked((int)visited));
            frame = PointVector(frame, cursor, new(unchecked((short)(PointSigned(frame, cursor) + PointSigned(frame, cursor + 8))), unchecked((short)(PointSigned(frame, cursor + 2) + PointSigned(frame, cursor + 10))), unchecked((short)(PointSigned(frame, cursor + 4) + PointSigned(frame, cursor + 12)))));
            cursor = PointDword(frame, cursor + 0x24);
        }
        uint first = Read(frame, address).ReadDword(0x10);
        while (PointSigned(frame, first + 0x20) == 0)
        {
            uint next = PointDword(frame, first + 0x24);
            RequirePoint(frame, next, 0);
            if (PointSigned(frame, next + 0x20) != 0)
            {
                break;
            }
            frame = PointWord(frame, first + 6, -1);
            current = Read(frame, address);
            count = unchecked((short)(current.ReadWord(14) - 1));
            frame = Write(frame, address, current.WriteWord(14, unchecked((ushort)count)).WriteDword(0x10, next));
            if (count < 2)
            {
                return (frame, 2);
            }
            first = next;
        }
        return (frame, 0);
    }

    private static NativeBattleAcid317Frame Interpolate(NativeBattleAcid317Frame frame, NativeBattleVector first, NativeBattleVector second, int firstFactor, int secondFactor, NativeBattleAcid317Inputs inputs, out NativeBattleVector point)
    {
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), frame.Memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown());
        registers = registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)firstFactor), Ir = [(int)unchecked((ushort)first.X), (int)unchecked((ushort)first.Y), (int)unchecked((ushort)first.Z)] } };
        NativeBattleSummonColors colors = inputs.Colors ?? throw new NotSupportedException("Acid requires its actual weighted interpolation constants.");
        registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, colors);
        registers = registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)secondFactor), Ir = [(int)unchecked((ushort)second.X), (int)unchecked((ushort)second.Y), (int)unchecked((ushort)second.Z)] } };
        registers = NativeBattleRaldo19BallRendering.Weighted(registers, true, colors);
        point = new(unchecked((short)registers.Colors.Ir[0]!.Value), unchecked((short)registers.Colors.Ir[1]!.Value), unchecked((short)registers.Colors.Ir[2]!.Value));
        return frame with { Memory = frame.Memory with { ScriptedColorRegisters = registers.Colors } };
    }
    private static void RequirePoint(NativeBattleAcid317Frame frame, uint address, int visited)
    {
        uint first = Working(frame) + PointsOffset;
        if (visited >= PointCount || address < first || address - first >= PointCount * PointStride || (address - first) % PointStride != 0)
        {
            throw new InvalidDataException("Acid chain leaves its actual retained point bank.");
        }
    }
    private static short PointSigned(NativeBattleAcid317Frame frame, uint address) => BinaryPrimitives.ReadInt16LittleEndian(Arena(frame).Read(Offset(address, 2), 2));
    private static uint PointDword(NativeBattleAcid317Frame frame, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Arena(frame).Read(Offset(address, 4), 4));
    private static NativeBattleAcid317Frame PointWord(NativeBattleAcid317Frame frame, uint address, short value) => WriteArena(frame, address, BitConverter.GetBytes(value));
    private static NativeBattleAcid317Frame PointVector(NativeBattleAcid317Frame frame, uint address, NativeBattleVector point) => PointWord(PointWord(PointWord(frame, address, point.X), address + 2, point.Y), address + 4, point.Z);
}
