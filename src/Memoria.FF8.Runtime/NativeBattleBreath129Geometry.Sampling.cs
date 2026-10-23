using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBreath129SampleStep(NativeBattleBreath129Frame Frame, NativeBattleEffectArena Point8, NativeBattleEffectArena Normal8);

public static partial class NativeBattleBreath129Geometry
{
    public static NativeBattleBreath129SampleStep Sample6B6FE0(NativeBattleBreath129Frame frame, NativeBattleBreath129BodySource body, uint triangles, int frames, int progress, NativeBattleBreath129Resources resources, NativeBattleBreath129Inputs inputs) => Sample6B6FE0(frame, body, triangles, frames, progress, UnknownSample(), UnknownSample(), resources, inputs);

    public static NativeBattleBreath129SampleStep Sample6B6FE0(NativeBattleBreath129Frame frame, NativeBattleBreath129BodySource body, uint triangles, int frames, int progress, NativeBattleEffectArena point8, NativeBattleEffectArena normal8, NativeBattleBreath129Resources resources, NativeBattleBreath129Inputs inputs)
    {
        int phase = unchecked((frames - 1) * progress);
        uint selected = unchecked(triangles + (uint)((phase >> 12) * 24));
        int ratio = phase & 4095;
        if (ratio == 0)
        {
            return SampleTriangle6B7170(frame, body, selected, point8, normal8, resources);
        }
        NativeBattleBreath129SampleStep first = SampleTriangle6B7170(frame, body, selected, UnknownSample(), UnknownSample(), resources);
        NativeBattleBreath129SampleStep second = SampleTriangle6B7170(first.Frame, body, unchecked(selected + 24), UnknownSample(), UnknownSample(), resources);
        (frame, NativeBattleEffectArena point) = BlendSample(second.Frame, first.Point8, second.Point8, ratio, inputs);
        (frame, NativeBattleEffectArena normal) = BlendSample(frame, first.Normal8, second.Normal8, ratio, inputs);
        int[] lanes = SampleLanes(normal);
        (frame, int length) = SampleLength(frame, lanes, resources, true);
        for (int lane = 0; lane < 3; lane++)
        {
            normal8 = normal8.Write(lane * 2, BitConverter.GetBytes(unchecked((short)(unchecked(lanes[lane] << 12) / length))));
        }
        return new(frame, NativeBattleEffectNodeContextMemory.Copy(point, 0, point8, 0, 6), normal8);
    }

    public static NativeBattleBreath129SampleStep SampleTriangle6B7170(NativeBattleBreath129Frame frame, NativeBattleBreath129BodySource body, uint triangle, NativeBattleBreath129Resources resources) => SampleTriangle6B7170(frame, body, triangle, UnknownSample(), UnknownSample(), resources);

    public static NativeBattleBreath129SampleStep SampleTriangle6B7170(NativeBattleBreath129Frame frame, NativeBattleBreath129BodySource body, uint triangle, NativeBattleEffectArena point8, NativeBattleEffectArena normal8, NativeBattleBreath129Resources resources)
    {
        NativeBattleEffectArena source = triangle >= NativeBattleBreath129Profile.ControlTrianglesAddress && (ulong)triangle + 24 <= NativeBattleBreath129Profile.ControlTrianglesAddress + 0x6F0 ? resources.ReadView(triangle, 24) : Get(frame, triangle, 24);
        int[][] points = new int[3][];
        for (int corner = 0; corner < 3; corner++)
        {
            NativeBattleEffectArena local = NativeBattleEffectNodeContextMemory.Copy(source, corner * 8, UnknownSample(), 0, 8);
            short bone = BinaryPrimitives.ReadInt16LittleEndian(local.Read(6, 2));
            if ((uint)bone >= body.BoneMatrices.Length)
            {
                throw new InvalidDataException("Breath sampling leaves its actual current bone matrix bank.");
            }
            frame = Load(frame, body.BoneMatrices[bone]);
            NativeBattleGeometryRegisterStep transformed = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(Registers(frame), 0, Vector(local), bone));
            frame = Publish(frame, transformed);
            points[corner] = transformed.Colors.Ir.Select(value => (int)unchecked((short)(value ?? throw new NotSupportedException("Breath sampling requires transformed corner coordinates.")))).ToArray();
        }
        int[] a = Enumerable.Range(0, 3).Select(lane => points[0][lane] - points[1][lane]).ToArray();
        int[] b = Enumerable.Range(0, 3).Select(lane => points[2][lane] - points[0][lane]).ToArray();
        NativeBattleGeometryRegisterStep registers = Registers(frame);
        byte?[] rotation = registers.Geometry.Rotation20.ToArray();
        for (int lane = 0; lane < 3; lane++)
        {
            byte[] bytes = BitConverter.GetBytes(a[lane]);
            for (int index = 0; index < 4; index++)
            {
                rotation[lane * 8 + index] = bytes[index];
            }
        }
        registers = registers with { Geometry = registers.Geometry with { Rotation20 = rotation.ToImmutableArray() }, Colors = registers.Colors with { Ir = b.Select(value => (int?)value).ToImmutableArray() } };
        registers = NativeBattleGeometry.OuterProduct45E670(registers);
        int[] cross = registers.Colors.Mac.Select(value => value ?? throw new NotSupportedException("Breath sampling requires its current cross product.")).ToArray();
        int maximum = NativeAbs(cross[0]);
        maximum = Math.Max(maximum, NativeAbs(cross[1]));
        maximum = Math.Max(maximum, NativeAbs(cross[2]));
        registers = registers with { Geometry = NativeBattleSonic139Preprocessing.PublishLeading45DCA0(registers.Geometry, unchecked((uint)maximum)) };
        int leading = checked((int)(registers.Geometry.Lzcr ?? throw new NotSupportedException("Breath sampling requires its leading sign count.")));
        for (int lane = 0; lane < 3; lane++)
        {
            int sum = points[0][lane] + points[1][lane] + points[2][lane];
            point8 = point8.Write(lane * 2, BitConverter.GetBytes(unchecked((short)(unchecked(sum * 21845) >> 16))));
            if (leading < 18)
            {
                cross[lane] >>= 18 - leading;
            }
        }
        (frame, int length) = SampleLength(Publish(frame, registers), cross, resources);
        for (int lane = 0; lane < 3; lane++)
        {
            normal8 = normal8.Write(lane * 2, BitConverter.GetBytes(unchecked((short)(unchecked(cross[lane] << 12) / length))));
        }
        return new(frame, point8, normal8);
    }

    private static NativeBattleEffectArena UnknownSample() => NativeBattleEffectArena.Clear().MarkUnknown(0, 8);
    private static int[] SampleLanes(NativeBattleEffectArena point) => Enumerable.Range(0, 3).Select(lane => (int)BinaryPrimitives.ReadInt16LittleEndian(point.Read(lane * 2, 2))).ToArray();
    private static int NativeAbs(int value) => value < 0 ? unchecked(-value) : value;

    private static (NativeBattleBreath129Frame Frame, int Length) SampleLength(NativeBattleBreath129Frame frame, int[] lanes, NativeBattleBreath129Resources resources, bool wordInput = false)
    {
        NativeBattleGeometryRegisterStep registers = Registers(frame);
        registers = wordInput ? NativeBattleGeometry.LoadIrColumn(registers, unchecked((ushort)lanes[0]), unchecked((ushort)lanes[1]), unchecked((ushort)lanes[2])) : registers with { Colors = registers.Colors with { Ir = lanes.Select(value => (int?)value).ToImmutableArray() } };
        registers = NativeBattleReflectBeam135Geometry.Square45F930(registers);
        int sum = unchecked(registers.Colors.Mac[0]!.Value + registers.Colors.Mac[1]!.Value + registers.Colors.Mac[2]!.Value);
        registers = registers with { Geometry = NativeBattleSonic139Preprocessing.PublishLeading45DCA0(registers.Geometry, unchecked((uint)sum)) };
        return (Publish(frame, registers), NativeBattleQuezacotlRibbonMath.SqrtQ12(sum, resources.SqrtWords256) >> 12);
    }

    private static (NativeBattleBreath129Frame Frame, NativeBattleEffectArena Point) BlendSample(NativeBattleBreath129Frame frame, NativeBattleEffectArena first, NativeBattleEffectArena second, int ratio, NativeBattleBreath129Inputs inputs)
    {
        NativeBattleGeometryRegisterStep registers = Registers(frame);
        int[] firstLanes = SampleLanes(first);
        int[] secondLanes = SampleLanes(second);
        registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = (uint)(4096 - ratio) } }, unchecked((ushort)firstLanes[0]), unchecked((ushort)firstLanes[1]), unchecked((ushort)firstLanes[2]));
        registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, inputs.Colors);
        registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = (uint)ratio } }, unchecked((ushort)secondLanes[0]), unchecked((ushort)secondLanes[1]), unchecked((ushort)secondLanes[2]));
        registers = NativeBattleRaldo19BallRendering.Weighted(registers, true, inputs.Colors);
        NativeBattleEffectArena point = UnknownSample();
        for (int lane = 0; lane < 3; lane++)
        {
            point = point.Write(lane * 2, BitConverter.GetBytes(unchecked((short)registers.Colors.Ir[lane]!.Value)));
        }
        return (Publish(frame, registers), point);
    }
}
