using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleModelTransform(NativeBattleVector Initial20, NativeBattleVector Position28, short Vertical34, short Angle36, short Scale38)
{
    public NativeBattleVector Scale148 { get; init; } = new(4096, 4096, 4096);
    public short Scale154 { get; init; } = 4096;
}

public readonly record struct NativeBattleModelScaleVector(int X, int Y, int Z);

public sealed record NativeBattleBoneAnchorProducer(NativeBattleActionMatrix? Matrix, short Distance, NativeBattleVector DisabledPosition)
{
    public bool Enabled => Matrix is not null;
}

/// <summary>508C90 bone matrices and502170/571400 model anchors, with native WORD stores and biased-double matrix conversion.</summary>
public static partial class NativeBattlePoseMatrices
{
    private sealed record Matrix(short[] Rotation, int X = 0, int Y = 0, int Z = 0);

    public static ImmutableArray<short> RotationWords(NativeBattleVector rotation) => Rotation(rotation).Rotation.ToImmutableArray();

    public static NativeBattleVector RotateVector(NativeBattleVector rotation, NativeBattleVector vector)
    {
        (int x, int y, int z) = ApplyRounded(Rotation(rotation), vector.X, vector.Y, vector.Z);
        return new(unchecked((short)x), unchecked((short)y), unchecked((short)z));
    }

    public static NativeBattleVector TransformCameraPoint(NativeBattleActionMatrix source, NativeBattleVector local, bool mirror)
    {
        source.Validate();
        if (source.Scale == 0)
        {
            return local with { Z = mirror ? unchecked((short)-local.Z) : local.Z };
        }
        Matrix matrix = Scale(new(source.Rotation.ToArray(), source.X, source.Y, source.Z), [source.Scale, source.Scale, source.Scale]);
        short x = mirror ? unchecked((short)-local.X) : local.X;
        (int dx, int dy, int dz) = ApplyRounded(matrix, x, local.Y, local.Z);
        return new(unchecked((short)(matrix.X + dx)), unchecked((short)(matrix.Y + dy)), unchecked((short)(matrix.Z + dz)));
    }

    public static NativeBattleModuleAnchor ReadAnchor(NativeBattleGraphicsModel model, NativeBattlePoseSource source)
    {
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native571400 requires the model's original position/scale WORD owners.");
        NativeBattleVector first = ReadBone(model, source, 240, 0);
        NativeBattleVector second = ReadBone(model, source, 241, 0);
        //571400 averages signed WORDs with IDIV-style truncation;570CC0 then replaces W with WORD38's scale.
        return new(unchecked((short)((first.X + second.X) / 2)), unchecked((short)((first.Y + second.Y) / 2)), unchecked((short)((first.Z + second.Z) / 2)), unchecked((short)(transform.Scale38 * 2000 >> 12)));
    }

    public static NativeBattleVector ReadBone(NativeBattleGraphicsModel model, NativeBattlePoseSource source, byte selector, short fraction)
    {
        NativeBattleBoneAnchorProducer producer = ResolveBoneAnchor502170(model, source, selector, fraction);
        if (!producer.Enabled)
        {
            return producer.DisabledPosition;
        }
        NativeBattleFullMacVertex mac = NativeBattleGfMatrices.TransformMac460840(producer.Matrix!, new(0, 0, producer.Distance));
        return new(unchecked((short)mac.X), unchecked((short)mac.Y), unchecked((short)mac.Z));
    }

    public static NativeBattleBoneAnchorProducer ResolveBoneAnchor502170(NativeBattleGraphicsModel model, NativeBattlePoseSource source, byte selector, short fraction)
    {
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native502170 requires its model transform owner.");
        if ((model.Flags0 & 2) == 0)
        {
            return new(null, 0, transform.Position28);
        }
        if (model.ModelId4 == 0x8F)
        {
            throw new NotSupportedException("Native model8F bone selectors require its B8B6EC source table.");
        }
        NativeBattleSequenceState state = model.SequenceState ?? throw new NotSupportedException("Native502170 requires a decoded model pose.");
        ImmutableArray<NativeBattleActionMatrix> bones = NativeBattleBodyBoneMatrices.Require(model, source);
        int bone = selector;
        if (selector >= 240)
        {
            int offset = selector - 236;
            if (offset >= 16)
            {
                throw new NotSupportedException("Native selector exceeds its skeleton header aliases.");
            }
            bone = offset == 4 ? state.Geometry4 ?? source.Skeleton[offset] : offset == 5 ? state.Geometry5 ?? source.Skeleton[offset] : source.Skeleton[offset];
            if ((bone & 0x80) != 0)
            {
                fraction = 4096;
                bone &= 0x7F;
            }
        }
        if (bone >= bones.Length)
        {
            throw new InvalidDataException("Native502170 bone alias exceeds its original skeleton.");
        }
        NativeBattleActionMatrix cached = model.GeometryWorldMatrix ?? throw new NotSupportedException("Native502170 requires its retained502AB0 model matrix.");
        Matrix world = new(cached.Rotation.ToArray(), cached.X, cached.Y, cached.Z);
        NativeBattleActionMatrix retained = bones[bone];
        Matrix selected = Compose(world, new(retained.Rotation.ToArray(), retained.X, retained.Y, retained.Z));
        short parent = ReadWord(source.Skeleton, 16 + bone * 48);
        int distance = parent < 0 ? 0 : fraction * ReadWord(source.Skeleton, 18 + bone * 48) >> 12;
        return new(new(selected.Rotation.ToImmutableArray(), selected.X, selected.Y, selected.Z, 4096), unchecked((short)distance), transform.Position28);
    }

    public static NativeBattleVector TransformCameraPoint(NativeBattleGraphicsModel model, NativeBattlePoseSource source, byte lowNibble, NativeBattleVector local, bool mirror)
    {
        if (lowNibble > 15)
        {
            throw new ArgumentOutOfRangeException(nameof(lowNibble));
        }
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native actor-relative camera requires its position/scale WORD owners.");
        NativeBattleActionMatrix cached = model.GeometryWorldMatrix ?? throw new NotSupportedException("Native actor-relative camera requires its retained502AB0 model matrix.");
        Matrix matrix = new(cached.Rotation.ToArray(), cached.X, cached.Y, cached.Z);
        if (lowNibble != 15)
        {
            NativeBattleVector bone = ReadBone(model, source, (byte)(0xF0 | lowNibble), 2048);
            matrix = matrix with { X = bone.X, Y = bone.Y, Z = bone.Z };
        }
        int scale = unchecked(2 * transform.Scale38 * transform.Scale154) >> 12;
        matrix = Scale(matrix, [scale, scale, scale]);
        short x = mirror ? unchecked((short)-local.X) : local.X;
        (int dx, int dy, int dz) = ApplyRounded(matrix, x, local.Y, local.Z);
        return new(unchecked((short)(matrix.X + dx)), unchecked((short)(matrix.Y + dy)), unchecked((short)(matrix.Z + dz)));
    }

    private static Matrix[] BoneMatrices(NativeBattlePoseSource source, NativeBattleDecodedPose pose)
    {
        ImmutableArray<NativeBattleRigBone> bones = Enumerable.Range(0, source.BoneCount).Select(index => new NativeBattleRigBone(ReadWord(source.Skeleton, 16 + index * 48), ReadWord(source.Skeleton, 18 + index * 48))).ToImmutableArray();
        return BoneMatrices(ReadWord(source.Skeleton, 2), bones, pose);
    }

    private static Matrix[] BoneMatrices(short skeletonScale, ImmutableArray<NativeBattleRigBone> bones, NativeBattleDecodedPose pose)
    {
        if (bones.IsDefaultOrEmpty || bones.Length != pose.Bones.Length || bones.Where((bone, index) => bone.Parent < -1 || bone.Parent >= index).Any())
        {
            throw new InvalidDataException("Native508C90 requires its actual earlier-parent rig records and decoded pose.");
        }
        Matrix[] result = new Matrix[bones.Length];
        Matrix[] unscaled = new Matrix[result.Length];
        int[][] scales = new int[result.Length][];
        for (int bone = 0; bone < result.Length; bone++)
        {
            Matrix local = Rotation(pose.Bones[bone].Rotation);
            int parent = bones[bone].Parent;
            NativeBattleVector scale = pose.Bones[bone].Scale;
            if (parent < 0)
            {
                unscaled[bone] = Scale(local, [skeletonScale, skeletonScale, skeletonScale]);
                scales[bone] = [4 * scale.X, 4 * scale.Y, 4 * scale.Z];
                Matrix root = pose.HasScale ? Scale(unscaled[bone], scales[bone]) : unscaled[bone];
                result[bone] = root with { X = skeletonScale * pose.Translation.X >> 8, Y = skeletonScale * pose.Translation.Y >> 8, Z = skeletonScale * pose.Translation.Z >> 8 };
            }
            else
            {
                int length = bones[parent].Length;
                if (pose.HasScale)
                {
                    unscaled[bone] = Multiply(unscaled[parent], local);
                    scales[bone] = [scales[parent][0] * scale.X >> 10, scales[parent][1] * scale.Y >> 10, scales[parent][2] * scale.Z >> 10];
                    (int x, int y, int z) = ApplyRounded(result[parent], 0, 0, length);
                    result[bone] = Scale(unscaled[bone], scales[bone]) with { X = unchecked(x + result[parent].X), Y = unchecked(y + result[parent].Y), Z = unchecked(z + result[parent].Z) };
                }
                else
                {
                    result[bone] = Compose(result[parent], local with { Z = length });
                }
            }
        }
        return result;
    }

    private static Matrix ModelMatrix(NativeBattleGraphicsModel model, NativeBattleModelTransform transform, Matrix root, NativeBattleModelScaleVector? pointedScale)
    {
        Matrix rotation = Rotation(new(model.Rotation12 ?? throw new NotSupportedException("Native model lacks rotationWORD12."), model.Rotation14 ?? throw new NotSupportedException("Native model lacks rotationWORD14."), model.Rotation16 ?? throw new NotSupportedException("Native model lacks rotationWORD16.")));
        NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Native502AB0 requires its resource-header displacement WORDs.");
        //502AB0 rotates the resource-local displacement before model scaling and root-pivot correction.
        var (offsetX, offsetY, offsetZ) = ApplyRounded(rotation, sequence.Header36, sequence.Header38, sequence.Header40);
        int[] scale = [transform.Scale154 * transform.Scale148.X >> 12, transform.Scale154 * transform.Scale148.Y >> 12, transform.Scale154 * transform.Scale148.Z >> 12];
        var (beforeX, _, beforeZ) = ApplyRounded(rotation, root.X, root.Y, root.Z);
        Matrix scaled = pointedScale is { } vector ? Scale(rotation, [vector.X, vector.Y, vector.Z]) : rotation;
        scaled = Scale(scaled, scale);
        var (afterX, _, afterZ) = ApplyRounded(scaled, root.X, root.Y, root.Z);
        return scaled with { X = unchecked(transform.Position28.X + offsetX + beforeX - afterX), Y = unchecked(transform.Position28.Y + transform.Vertical34 + offsetY), Z = unchecked(transform.Position28.Z + offsetZ + beforeZ - afterZ) };
    }

    private static Matrix Rotation(NativeBattleVector rotation)
    {
        short cx = unchecked((short)NativeBattleCameraMath.Cosine(rotation.X));
        short sx = unchecked((short)NativeBattleCameraMath.Sine(rotation.X));
        short cy = unchecked((short)NativeBattleCameraMath.Cosine(rotation.Y));
        short sy = unchecked((short)NativeBattleCameraMath.Sine(rotation.Y));
        short cz = unchecked((short)NativeBattleCameraMath.Cosine(rotation.Z));
        short sz = unchecked((short)NativeBattleCameraMath.Sine(rotation.Z));
        Matrix z = new([cz, unchecked((short)-sz), 0, sz, cz, 0, 0, 0, 4096]);
        Matrix x = new([4096, 0, 0, 0, cx, unchecked((short)-sx), 0, sx, cx]);
        Matrix y = new([cy, 0, sy, 0, 4096, 0, unchecked((short)-sy), 0, cy]);
        return Multiply(y, Multiply(x, z));
    }

    private static Matrix Scale(Matrix matrix, int[] scale)
    {
        return matrix with { Rotation = matrix.Rotation.Select((value, index) => unchecked((short)(scale[index % 3] * value >> 12))).ToArray() };
    }

    private static Matrix Multiply(Matrix left, Matrix right)
    {
        short[] result = new short[9];
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                double value = 0;
                for (int inner = 0; inner < 3; inner++)
                {
                    value += left.Rotation[row * 3 + inner] / 4096d * right.Rotation[inner * 3 + column];
                }
                result[row * 3 + column] = unchecked((short)ConvertRounded(value));
            }
        }
        return new(result);
    }

    private static Matrix Compose(Matrix left, Matrix right)
    {
        (int x, int y, int z) = ApplyRounded(left, right.X, right.Y, right.Z);
        return Multiply(left, right) with { X = unchecked(x + left.X), Y = unchecked(y + left.Y), Z = unchecked(z + left.Z) };
    }

    private static (int X, int Y, int Z) ApplyRounded(Matrix matrix, int x, int y, int z)
    {
        int Row(int row) => ConvertRounded(matrix.Rotation[row * 3 + 2] * (z / 4096d) + matrix.Rotation[row * 3 + 1] * (y / 4096d) + matrix.Rotation[row * 3] * (x / 4096d));
        return (Row(0), Row(1), Row(2));
    }

    private static int ConvertRounded(double value) => unchecked((int)((uint)BitConverter.DoubleToInt64Bits(value + 4503601774854144d) + 0x80000000u));
    private static short ReadWord(ImmutableArray<byte> data, int offset) => BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan().Slice(offset));
}
