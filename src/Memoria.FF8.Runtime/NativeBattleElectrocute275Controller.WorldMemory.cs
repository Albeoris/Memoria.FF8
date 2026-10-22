using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleElectrocute275Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleElectrocute275Controller
{
    private static (NativeBattleElectrocute275Frame Frame, uint Head) StartWorld(NativeBattleElectrocute275Frame frame, NativeBattleVector velocity, NativeBattleVector source, NativeBattleVector target, NativeBattleVector? drift, short actor, short progress, short increment, short scale, short textureV, short branches)
    {
        NativeBattleCpuTrailAddressStep step = NativeBattleCpuTrails.StartWorld(CpuTrailFrame(frame), CpuTrailLayout(frame), velocity, source, target, drift, actor, progress, increment, scale, textureV, branches);
        return (WithCpuTrail(frame, step.Frame), step.Address);
    }

    private static (NativeBattleElectrocute275Frame Frame, uint Address) AllocateWorldPoint(NativeBattleElectrocute275Frame frame, NativeBattleVector position, short scale)
    {
        NativeBattleCpuTrailAddressStep step = NativeBattleCpuTrails.AllocateWorldPoint(CpuTrailFrame(frame), CpuTrailLayout(frame), position, scale);
        return (WithCpuTrail(frame, step.Frame), step.Address);
    }

    private static NativeBattleElectrocute275Frame ClearHeadVelocity(NativeBattleElectrocute275Frame frame, uint head)
    {
        if (head == 0)
        {
            return frame;
        }
        frame = WriteArena(frame, head + 8, BitConverter.GetBytes(frame.State.ReadDword(0x2508408)));
        return WriteArena(frame, head + 12, BitConverter.GetBytes(frame.State.ReadDword(0x250840C)));
    }
    private static NativeBattleModulePoolTask Vector(NativeBattleModulePoolTask task, int offset, NativeBattleVector value) => task.WriteWord(offset, unchecked((ushort)value.X)).WriteWord(offset + 2, unchecked((ushort)value.Y)).WriteWord(offset + 4, unchecked((ushort)value.Z));
    private static NativeBattleVector Vector(NativeBattleModulePoolTask task, int offset) => new(Signed(task, offset), Signed(task, offset + 2), Signed(task, offset + 4));
    private static short PointSigned(NativeBattleElectrocute275Frame frame, uint address) => BinaryPrimitives.ReadInt16LittleEndian(Arena(frame).Read(Offset(address, 2), 2));
    private static uint PointDword(NativeBattleElectrocute275Frame frame, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Arena(frame).Read(Offset(address, 4), 4));
    private static NativeBattleElectrocute275Frame PointWord(NativeBattleElectrocute275Frame frame, uint address, short value) => WriteArena(frame, address, BitConverter.GetBytes(value));
    private static NativeBattleElectrocute275Frame PointVector(NativeBattleElectrocute275Frame frame, uint address, NativeBattleVector value) => PointWord(PointWord(PointWord(frame, address, value.X), address + 2, value.Y), address + 4, value.Z);
    private static void RequireWorldPoint(NativeBattleElectrocute275Frame frame, uint address, int visited)
    {
        uint first = Working(frame) + WorldPointsOffset;
        if (visited >= PointCount || address < first || address - first >= PointCount * PointStride || (address - first) % PointStride != 0)
        {
            throw new InvalidDataException("Electrocute world trail leaves its original point bank.");
        }
    }
    private static NativeBattleElectrocute275Frame WeightedBlend(NativeBattleElectrocute275Frame frame, NativeBattleVector first, NativeBattleVector second, int firstFactor, int secondFactor, NativeBattleElectrocute275Inputs inputs, out NativeBattleVector result)
    {
        NativeBattleGeometryRegisterStep step = new(frame.Memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), frame.Memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown());
        NativeBattleSummonColors constants = inputs.Colors ?? throw new NotSupportedException("Electrocute requires its actual GTE interpolation constants.");
        step = step with { Colors = step.Colors with { Ir0 = unchecked((uint)firstFactor), Ir = [(int)unchecked((ushort)first.X), (int)unchecked((ushort)first.Y), (int)unchecked((ushort)first.Z)] } };
        step = NativeBattleRaldo19BallRendering.Weighted(step, false, constants);
        step = step with { Colors = step.Colors with { Ir0 = unchecked((uint)secondFactor), Ir = [(int)unchecked((ushort)second.X), (int)unchecked((ushort)second.Y), (int)unchecked((ushort)second.Z)] } };
        step = NativeBattleRaldo19BallRendering.Weighted(step, true, constants);
        result = new(unchecked((short)step.Colors.Ir[0]!.Value), unchecked((short)step.Colors.Ir[1]!.Value), unchecked((short)step.Colors.Ir[2]!.Value));
        return frame with { Memory = frame.Memory with { ScriptedColorRegisters = step.Colors } };
    }
}
