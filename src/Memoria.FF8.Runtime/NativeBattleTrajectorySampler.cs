using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleTrajectoryStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Output8);

public static class NativeBattleTrajectorySampler
{
    public static NativeBattleTrajectoryStep Sample(NativeBattleEffectMemory memory, NativeBattleEffectArena controls, int pointCount, int segment, int parameter, NativeBattleEffectArena retained8, NativeBattleSummonColors colors)
    {
        if (pointCount < 4 || segment < 0 || segment > pointCount - 4)
        {
            throw new NotSupportedException("The trajectory requires a segment within its produced control points.");
        }
        NativeBattleVector a = Point(controls, segment * 8);
        NativeBattleVector b = Point(controls, (segment + 1) * 8);
        NativeBattleVector c = Point(controls, (segment + 2) * 8);
        NativeBattleVector d = Point(controls, (segment + 3) * 8);
        (memory, int length) = Distance(memory, b, c);
        NativeBattleVector delta = Difference(b, a);
        (memory, int firstLength) = Distance(memory, delta, new(0, 0, 0));
        NativeBattleVector first = b;
        if (firstLength > 0)
        {
            NativeBattleVector candidate = AddTangent(b, delta, length, firstLength);
            NativeBattleWeightedPointsStep blended = NativeBattleWeightedPoints56CB90.Blend(memory, b, candidate, unchecked((short)(4096 - parameter)), unchecked((short)parameter), colors);
            memory = blended.Memory;
            first = blended.Position;
        }
        delta = Difference(c, d);
        (memory, int secondLength) = Distance(memory, delta, new(0, 0, 0));
        NativeBattleVector second = c;
        if (secondLength > 0)
        {
            NativeBattleVector candidate = AddTangent(c, delta, length, secondLength);
            NativeBattleWeightedPointsStep blended = NativeBattleWeightedPoints56CB90.Blend(memory, c, candidate, unchecked((short)parameter), unchecked((short)(4096 - parameter)), colors);
            memory = blended.Memory;
            second = blended.Position;
        }
        NativeBattleWeightedPointsStep result = NativeBattleWeightedPoints56CB90.Blend(memory, first, second, unchecked((short)(4096 - parameter)), unchecked((short)parameter), colors);
        NativeBattleEffectArena output = retained8.Write(0, BitConverter.GetBytes(result.Position.X)).Write(2, BitConverter.GetBytes(result.Position.Y)).Write(4, BitConverter.GetBytes(result.Position.Z));
        return new(result.Memory, output);
    }

    private static (NativeBattleEffectMemory Memory, int Length) Distance(NativeBattleEffectMemory memory, NativeBattleVector first, NativeBattleVector second)
    {
        NativeBattleVector delta = Difference(first, second);
        NativeBattleGeometryRegisterStep registers = new(memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown());
        registers = NativeBattleGeometry.LoadIrColumn(registers, unchecked((ushort)delta.X), unchecked((ushort)delta.Y), unchecked((ushort)delta.Z));
        registers = NativeBattleReflectBeam135Geometry.Square45F930(registers);
        int length = NativeBattleRayBomb10Geometry.Sqrt56BEC0(unchecked(registers.Colors.Mac[0]!.Value + registers.Colors.Mac[1]!.Value + registers.Colors.Mac[2]!.Value));
        return (memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, length);
    }

    private static NativeBattleVector AddTangent(NativeBattleVector origin, NativeBattleVector delta, int length, int divisor)
    {
        return new(unchecked((short)(unchecked(delta.X * length) / divisor + origin.X)), unchecked((short)(unchecked(delta.Y * length) / divisor + origin.Y)), unchecked((short)(unchecked(delta.Z * length) / divisor + origin.Z)));
    }

    private static NativeBattleVector Difference(NativeBattleVector first, NativeBattleVector second) => new(unchecked((short)(first.X - second.X)), unchecked((short)(first.Y - second.Y)), unchecked((short)(first.Z - second.Z)));
    private static NativeBattleVector Point(NativeBattleEffectArena controls, int offset) => new(BinaryPrimitives.ReadInt16LittleEndian(controls.Read(offset, 2)), BinaryPrimitives.ReadInt16LittleEndian(controls.Read(offset + 2, 2)), BinaryPrimitives.ReadInt16LittleEndian(controls.Read(offset + 4, 2)));
}
