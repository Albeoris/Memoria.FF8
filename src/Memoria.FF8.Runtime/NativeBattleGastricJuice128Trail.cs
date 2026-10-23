using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleGastricJuice128Profile;
using static Memoria.FF8.Runtime.NativeBattleGastricJuice128Controller;

namespace Memoria.FF8.Runtime;

public static class NativeBattleGastricJuice128Trail
{
    public static (NativeBattleGastricJuice128Frame Frame, int Result) Advance6B8DC0(NativeBattleGastricJuice128Frame frame, uint address, NativeBattleGastricJuice128Resources resources, NativeBattleGastricJuice128Inputs inputs)
    {
        if (Read(frame, address).Callback != 0x6B8DC0)
        {
            throw new NotSupportedException("Gastric Juice reached an unsupported trail callback.");
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
                    (frame, uint child) = Append(frame, 3, 0x6B9030);
                    if (child != 0)
                    {
                        frame = Write(frame, child, WritePoint(Read(frame, child).WriteDword(12, 0), point).WriteWord(0x16, unchecked((ushort)(task.ReadWord(0x4C) << 4))));
                    }
                }
                (frame, uint next) = AllocatePoint(frame, PointView(point), Signed(task, 0x4C));
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
                    color = unchecked((short)(color - (color > 1024 ? 128 : 64)));
                    frame = PointWord(frame, cursor + 0x20, Math.Max((short)0, color));
                }
                cursor = PointDword(frame, cursor + 0x24);
            }
        }
        NativeBattleModulePoolTask current = Read(frame, address);
        short count = Signed(current, 14);
        uint head = current.ReadDword(0x10);
        frame = ProjectPoints(frame, head, inputs);
        if (count >= 2)
        {
            (frame, NativeBattleGfGeometryBatch batch) = DrawRibbon6B90B0(frame, head, count, resources);
            frame = inputs.Execute(frame, new NativeBattleGastricJuice128DeliverRibbonBatch(batch));
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

    private static NativeBattleGastricJuice128Frame Interpolate(NativeBattleGastricJuice128Frame frame, NativeBattleVector first, NativeBattleVector second, int firstFactor, int secondFactor, NativeBattleGastricJuice128Inputs inputs, out NativeBattleVector point)
    {
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Gastric Juice requires its current geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Gastric Juice requires its current arithmetic registers."));
        registers = registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)firstFactor), Ir = [(int)unchecked((ushort)first.X), (int)unchecked((ushort)first.Y), (int)unchecked((ushort)first.Z)] } };
        NativeBattleSummonColors colors = inputs.Colors;
        registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, colors);
        registers = registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)secondFactor), Ir = [(int)unchecked((ushort)second.X), (int)unchecked((ushort)second.Y), (int)unchecked((ushort)second.Z)] } };
        registers = NativeBattleRaldo19BallRendering.Weighted(registers, true, colors);
        point = new(unchecked((short)registers.Colors.Ir[0]!.Value), unchecked((short)registers.Colors.Ir[1]!.Value), unchecked((short)registers.Colors.Ir[2]!.Value));
        return frame with { Memory = frame.Memory with { ScriptedColorRegisters = registers.Colors } };
    }
    private static void RequirePoint(NativeBattleGastricJuice128Frame frame, uint address, int visited)
    {
        uint first = Working(frame) + PointsOffset;
        if (visited >= PointCount || address < first || address - first >= PointCount * PointStride || (address - first) % PointStride != 0)
        {
            throw new InvalidDataException("Gastric Juice chain leaves its actual retained point bank.");
        }
    }
    private static short PointSigned(NativeBattleGastricJuice128Frame frame, uint address) => BinaryPrimitives.ReadInt16LittleEndian(Arena(frame).Read(Offset(address, 2), 2));
    private static uint PointDword(NativeBattleGastricJuice128Frame frame, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Arena(frame).Read(Offset(address, 4), 4));
    private static NativeBattleGastricJuice128Frame PointWord(NativeBattleGastricJuice128Frame frame, uint address, short value) => WriteArena(frame, address, BitConverter.GetBytes(value));
    private static NativeBattleGastricJuice128Frame PointVector(NativeBattleGastricJuice128Frame frame, uint address, NativeBattleVector point) => PointWord(PointWord(PointWord(frame, address, point.X), address + 2, point.Y), address + 4, point.Z);

    public static (NativeBattleGastricJuice128Frame Frame, NativeBattleGfGeometryBatch Batch) DrawRibbon6B90B0(NativeBattleGastricJuice128Frame frame, uint head, short count, NativeBattleGastricJuice128Resources resources)
    {
        uint working = Working(frame);
        NativeBattleRibbonBank bank = new(checked(working + PointsOffset), Offset(checked(working + PointsOffset), checked(PointCount * PointStride)), PointCount);
        NativeBattleRibbonPacketPolicy policy = new(NativeBattleRibbonWidthPolicy.Projected, 3, 17, 0x3D14E048, 0x00BAE078, 0xE048, 0xE078);
        var result = NativeBattleRibbonPackets.Draw(Arena(frame), head, count, bank, resources.DistanceTable, policy);
        return (frame with { Memory = frame.Memory with { Arena = result.Arena } }, result.Batch);
    }

    private static NativeBattleGastricJuice128Frame ProjectPoints(NativeBattleGastricJuice128Frame frame, uint head, NativeBattleGastricJuice128Inputs inputs)
    {
        frame = LoadMatrix(frame, inputs.ReadView(frame));
        NativeBattleScriptedGraphicsMemory controls = frame.Memory.ScriptedGraphics ?? throw new NotSupportedException("Gastric Juice requires its current projection controls.");
        for (uint point = head, visited = 0; point != 0; visited++)
        {
            RequirePoint(frame, point, checked((int)visited));
            NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Gastric Juice requires its current geometry registers."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Gastric Juice requires its current arithmetic registers."));
            registers = NativeBattleGeometry.LoadVector(registers, 0, new(PointSigned(frame, point), PointSigned(frame, point + 2), PointSigned(frame, point + 4)), PointSigned(frame, point + 6));
            registers = NativeBattleGeometry.ProjectSingle(registers, inputs.ReadProjection(frame), controls.Dqa, controls.Dqb);
            frame = frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
            frame = WriteArena(frame, point + 0x10, BitConverter.GetBytes(registers.Geometry.Sxy[2] ?? throw new NotSupportedException("Gastric Juice requires its actual SXY output.")));
            frame = WriteArena(frame, point + 0x14, BitConverter.GetBytes(unchecked((uint)(unchecked((int)(registers.Geometry.Sz[3] ?? throw new NotSupportedException("Gastric Juice requires its actual SZ output."))) >> 2))));
            point = PointDword(frame, point + 0x24);
        }
        return frame;
    }

    private static NativeBattleModulePoolTask WritePoint(NativeBattleModulePoolTask task, NativeBattleVector point) => task.WriteWord(0x10, unchecked((ushort)point.X)).WriteWord(0x12, unchecked((ushort)point.Y)).WriteWord(0x14, unchecked((ushort)point.Z));
    private static uint Working(NativeBattleGastricJuice128Frame frame) => frame.State.ReadDword(WorkingGlobal);
    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static int Offset(uint address, int length) => NativeBattleWaterExecutionFields.ArenaOffset(address, length);
}
