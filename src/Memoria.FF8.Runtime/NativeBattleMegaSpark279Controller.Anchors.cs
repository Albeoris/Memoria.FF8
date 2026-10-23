using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleMegaSpark279Controller
{
    private static (NativeBattleMegaSpark279Frame Frame, NativeBattleEffectArena Point) BonePoint279(NativeBattleMegaSpark279Frame frame, uint row, NativeBattleEffectArena point, int bone, NativeBattleMegaSpark279Inputs inputs)
    {
        frame = LoadMatrix(frame, inputs.ReadCurrentBoneMatrix(frame, row, bone));
        NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(Registers279(frame), 0, Vector279(point), Padding279(point)));
        NativeBattleEffectArena result = NativeBattleEffectArena.Clear().MarkUnknown(0, 8);
        for (int axis = 0; axis < 3; axis++)
        {
            result = result.Write(axis * 2, BitConverter.GetBytes(unchecked((short)registers.Colors.Ir[axis]!.Value)));
        }
        return (Publish279(frame, registers), result);
    }

    private static (NativeBattleMegaSpark279Frame Frame, NativeBattleEffectArena Center, NativeBattleEffectArena Normal) Triangle704130(NativeBattleMegaSpark279Frame frame, uint row, uint source, NativeBattleMegaSpark279Resources resources, NativeBattleMegaSpark279Inputs inputs)
    {
        NativeBattleEffectArena raw = NativeBattleEffectArena.Clear().Write(0, resources.ReadStatic(source, 24).AsSpan());
        int[][] points = new int[3][];
        for (int corner = 0; corner < 3; corner++)
        {
            var point = NativeBattleEffectNodeContextMemory.Copy(raw, corner * 8, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8);
            (frame, NativeBattleEffectArena transformed) = BonePoint279(frame, row, point, Word279(point, 6), inputs);
            points[corner] = Enumerable.Range(0, 3).Select(axis => (int)Word279(transformed, axis * 2)).ToArray();
        }
        int[] a = Enumerable.Range(0, 3).Select(axis => points[0][axis] - points[1][axis]).ToArray();
        int[] b = Enumerable.Range(0, 3).Select(axis => points[2][axis] - points[0][axis]).ToArray();
        NativeBattleGeometryRegisterStep registers = Registers279(frame);
        byte?[] rotation = registers.Geometry.Rotation20.ToArray();
        for (int axis = 0; axis < 3; axis++)
        {
            byte[] bytes = BitConverter.GetBytes(a[axis]);
            for (int index = 0; index < 4; index++)
            {
                rotation[axis * 8 + index] = bytes[index];
            }
        }
        registers = registers with { Geometry = registers.Geometry with { Rotation20 = rotation.ToImmutableArray() }, Colors = registers.Colors with { Ir = b.Select(value => (int?)value).ToImmutableArray() } };
        registers = NativeBattleGeometry.OuterProduct45E670(registers);
        int[] cross = registers.Colors.Mac.Select(value => value ?? throw new NotSupportedException("Mega Spark triangle requires its current cross product.")).ToArray();
        int maximum = cross.Select(value => value < 0 ? unchecked(-value) : value).Max();
        registers = registers with { Geometry = NativeBattleSonic139Preprocessing.PublishLeading45DCA0(registers.Geometry, unchecked((uint)maximum)) };
        int leading = checked((int)registers.Geometry.Lzcr!.Value);
        NativeBattleEffectArena center = NativeBattleEffectArena.Clear().MarkUnknown(0, 8);
        for (int axis = 0; axis < 3; axis++)
        {
            center = center.Write(axis * 2, BitConverter.GetBytes(unchecked((short)(unchecked((points[0][axis] + points[1][axis] + points[2][axis]) * 21845) >> 16))));
            if (leading < 18)
            {
                cross[axis] >>= 18 - leading;
            }
        }
        (frame, int length, _) = Length279(Publish279(frame, registers), cross, resources);
        NativeBattleEffectArena normal = NativeBattleEffectArena.Clear().MarkUnknown(0, 8);
        for (int axis = 0; axis < 3; axis++)
        {
            normal = normal.Write(axis * 2, BitConverter.GetBytes(unchecked((short)(unchecked(cross[axis] << 12) / length))));
        }
        return (frame, center, normal);
    }

    private static (NativeBattleMegaSpark279Frame Frame, int Length, int RootQ12) Length279(NativeBattleMegaSpark279Frame frame, int[] lanes, NativeBattleMegaSpark279Resources resources)
    {
        NativeBattleGeometryRegisterStep registers = Registers279(frame);
        registers = registers with { Colors = registers.Colors with { Ir = lanes.Select(value => (int?)value).ToImmutableArray() } };
        registers = NativeBattleReflectBeam135Geometry.Square45F930(registers);
        int sum = unchecked(registers.Colors.Mac[0]!.Value + registers.Colors.Mac[1]!.Value + registers.Colors.Mac[2]!.Value);
        registers = registers with { Geometry = NativeBattleSonic139Preprocessing.PublishLeading45DCA0(registers.Geometry, unchecked((uint)sum)) };
        int even = checked((int)registers.Geometry.Lzcr!.Value) & ~1;
        int index = even <= 24 ? sum >> (24 - even) : unchecked(sum << (even - 24));
        int root = unchecked(resources.ReadNormalizationWord(index) << ((31 - even) >> 1));
        return (Publish279(frame, registers), root >> 12, root);
    }
}
