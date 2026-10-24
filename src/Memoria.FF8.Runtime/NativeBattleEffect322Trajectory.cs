using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect322Trajectory
{
    public static NativeBattleEffect322PointStep Sample(NativeBattleEffect322Frame frame, NativeBattleEffectArena controls64, int segment, int parameter, NativeBattleEffectArena retained8, NativeBattleSummonColors colors)
    {
        if ((uint)segment > 3)
        {
            throw new NotSupportedException("The trajectory requires one of its four produced control segments.");
        }
        NativeBattleVector a = Point(controls64, segment * 8);
        NativeBattleVector b = Point(controls64, (segment + 1) * 8);
        NativeBattleVector c = Point(controls64, (segment + 2) * 8);
        NativeBattleVector d = Point(controls64, (segment + 3) * 8);
        (frame, int length) = Distance(frame, b, c);
        NativeBattleVector delta = Difference(b, a);
        (frame, int firstLength) = Distance(frame, delta, new(0, 0, 0));
        NativeBattleVector first = b;
        if (firstLength > 0)
        {
            NativeBattleVector candidate = AddTangent(b, delta, length, firstLength);
            NativeBattleWeightedPointsStep blended = NativeBattleWeightedPoints56CB90.Blend(frame.Memory, b, candidate, unchecked((short)(4096 - parameter)), unchecked((short)parameter), colors);
            frame = frame with { Memory = blended.Memory };
            first = blended.Position;
        }
        delta = Difference(c, d);
        (frame, int secondLength) = Distance(frame, delta, new(0, 0, 0));
        NativeBattleVector second = c;
        if (secondLength > 0)
        {
            NativeBattleVector candidate = AddTangent(c, delta, length, secondLength);
            NativeBattleWeightedPointsStep blended = NativeBattleWeightedPoints56CB90.Blend(frame.Memory, c, candidate, unchecked((short)parameter), unchecked((short)(4096 - parameter)), colors);
            frame = frame with { Memory = blended.Memory };
            second = blended.Position;
        }
        NativeBattleWeightedPointsStep result = NativeBattleWeightedPoints56CB90.Blend(frame.Memory, first, second, unchecked((short)(4096 - parameter)), unchecked((short)parameter), colors);
        NativeBattleEffectArena output = retained8.Write(0, BitConverter.GetBytes(result.Position.X)).Write(2, BitConverter.GetBytes(result.Position.Y)).Write(4, BitConverter.GetBytes(result.Position.Z));
        return new(frame with { Memory = result.Memory }, output);
    }

    private static (NativeBattleEffect322Frame Frame, int Length) Distance(NativeBattleEffect322Frame frame, NativeBattleVector first, NativeBattleVector second)
    {
        NativeBattleVector delta = Difference(first, second);
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), frame.Memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown());
        registers = NativeBattleGeometry.LoadIrColumn(registers, unchecked((ushort)delta.X), unchecked((ushort)delta.Y), unchecked((ushort)delta.Z));
        registers = NativeBattleReflectBeam135Geometry.Square45F930(registers);
        int length = NativeBattleRayBomb10Geometry.Sqrt56BEC0(unchecked(registers.Colors.Mac[0]!.Value + registers.Colors.Mac[1]!.Value + registers.Colors.Mac[2]!.Value));
        return (frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } }, length);
    }

    private static NativeBattleVector AddTangent(NativeBattleVector origin, NativeBattleVector delta, int length, int divisor)
    {
        return new(unchecked((short)(unchecked(delta.X * length) / divisor + origin.X)), unchecked((short)(unchecked(delta.Y * length) / divisor + origin.Y)), unchecked((short)(unchecked(delta.Z * length) / divisor + origin.Z)));
    }

    private static NativeBattleVector Difference(NativeBattleVector first, NativeBattleVector second) => new(unchecked((short)(first.X - second.X)), unchecked((short)(first.Y - second.Y)), unchecked((short)(first.Z - second.Z)));
    private static NativeBattleVector Point(NativeBattleEffectArena controls, int offset) => new(BinaryPrimitives.ReadInt16LittleEndian(controls.Read(offset, 2)), BinaryPrimitives.ReadInt16LittleEndian(controls.Read(offset + 2, 2)), BinaryPrimitives.ReadInt16LittleEndian(controls.Read(offset + 4, 2)));
}
