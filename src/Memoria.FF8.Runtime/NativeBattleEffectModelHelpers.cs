namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffectLocalBounds(NativeBattleVector Minimum, NativeBattleVector Maximum);

/// <summary>The shared8DC610/8DC740 anchors and8DC870 local posed-skeleton bounds.</summary>
public static class NativeBattleEffectModelHelpers
{
    public static NativeBattleModulePoolTask UpdateTarget8DC610(NativeBattleModulePoolTask task, IReadOnlyList<NativeBattleGraphicsModel> models, Func<int, NativeBattlePoseSource> readSource, Action<NativeBattleBoneAnchorProducer>? observeAnchor = null)
    {
        return WriteAnchors(task, models, task.ReadByte(0x2D), readSource, observeAnchor);
    }

    public static NativeBattleModulePoolTask UpdateCaster8DC740(NativeBattleModulePoolTask task, NativeBattleEffectParameters parameters, IReadOnlyList<NativeBattleGraphicsModel> models, Func<int, NativeBattlePoseSource> readSource, Action<NativeBattleBoneAnchorProducer>? observeAnchor = null)
    {
        return WriteAnchors(task, models, parameters.Actor78, readSource, observeAnchor);
    }

    public static NativeBattleModulePoolTask UpdateBounds8DC870(NativeBattleModulePoolTask task, NativeBattlePoseSource source, NativeBattleDecodedPose pose)
    {
        NativeBattleEffectLocalBounds bounds = NativeBattlePoseMatrices.LocalBounds8DC870(source, pose);
        return WriteBounds(task, bounds);
    }

    public static NativeBattleModulePoolTask UpdateBounds8DC870(NativeBattleModulePoolTask task, NativeBattleGraphicsModel model, NativeBattlePoseSource source)
    {
        NativeBattleEffectLocalBounds bounds = NativeBattlePoseMatrices.LocalBounds8DC870(model, source);
        return WriteBounds(task, bounds);
    }

    private static NativeBattleModulePoolTask WriteBounds(NativeBattleModulePoolTask task, NativeBattleEffectLocalBounds bounds)
    {
        //Only these six WORDs have semantic consumers. Adjacent copied scratch padding is not an invented coordinate.
        return task.WriteWord(0x48, unchecked((ushort)bounds.Minimum.X)).WriteWord(0x4A, unchecked((ushort)bounds.Minimum.Y)).WriteWord(0x4C, unchecked((ushort)bounds.Minimum.Z)).WriteWord(0x50, unchecked((ushort)bounds.Maximum.X)).WriteWord(0x52, unchecked((ushort)bounds.Maximum.Y)).WriteWord(0x54, unchecked((ushort)bounds.Maximum.Z));
    }

    private static NativeBattleModulePoolTask WriteAnchors(NativeBattleModulePoolTask task, IReadOnlyList<NativeBattleGraphicsModel> models, int index, Func<int, NativeBattlePoseSource> readSource, Action<NativeBattleBoneAnchorProducer>? observeAnchor)
    {
        if ((uint)index >= models.Count)
        {
            throw new InvalidDataException("Native8DC610/740 actor exceeds the physical model bank.");
        }
        NativeBattleGraphicsModel model = models[index];
        if ((model.Flags0 & 2) == 0)
        {
            return task;
        }
        NativeBattlePoseSource source = readSource(index);
        NativeBattleVector first = ReadAnchor(240);
        NativeBattleVector second = ReadAnchor(241);
        short angle = (model.Transform ?? throw new NotSupportedException("Native8DC610/740 requires modelWORD36.")).Angle36;
        return task.WriteWord(0x30, unchecked((ushort)second.X)).WriteWord(0x32, unchecked((ushort)angle)).WriteWord(0x34, unchecked((ushort)second.Z)).WriteWord(0x38, unchecked((ushort)((first.X + second.X) / 2))).WriteWord(0x3A, unchecked((ushort)((first.Y + second.Y) / 2))).WriteWord(0x3C, unchecked((ushort)((first.Z + second.Z) / 2))).WriteWord(0x40, unchecked((ushort)first.X)).WriteWord(0x42, unchecked((ushort)first.Y)).WriteWord(0x44, unchecked((ushort)first.Z));

        NativeBattleVector ReadAnchor(byte selector)
        {
            NativeBattleVector result = NativeBattlePoseMatrices.ReadBone(model, source, selector, 0);
            if (observeAnchor is not null)
            {
                observeAnchor(NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source, selector, 0));
            }
            return result;
        }
    }
}

public static partial class NativeBattlePoseMatrices
{
    public static NativeBattleEffectLocalBounds LocalBounds8DC870(NativeBattlePoseSource source, NativeBattleDecodedPose pose)
    {
        source.Validate();
        if (pose.Bones.IsDefault || pose.Bones.Length != source.BoneCount)
        {
            throw new InvalidDataException("Native8DC870 requires every posed original skeleton matrix.");
        }
        return LocalBounds8DC870(source, BoneMatrices(source, pose));
    }

    public static NativeBattleEffectLocalBounds LocalBounds8DC870(NativeBattleGraphicsModel model, NativeBattlePoseSource source)
    {
        Matrix[] matrices = NativeBattleBodyBoneMatrices.Require(model, source).Select(matrix => new Matrix(matrix.Rotation.ToArray(), matrix.X, matrix.Y, matrix.Z)).ToArray();
        return LocalBounds8DC870(source, matrices);
    }

    private static NativeBattleEffectLocalBounds LocalBounds8DC870(NativeBattlePoseSource source, Matrix[] matrices)
    {
        short minX = unchecked((short)matrices[0].X);
        short minY = unchecked((short)matrices[0].Y);
        short minZ = unchecked((short)matrices[0].Z);
        short maxX = minX;
        short maxY = minY;
        short maxZ = minZ;
        for (int bone = 1; bone < matrices.Length; bone++)
        {
            Matrix matrix = matrices[bone];
            short length = ReadWord(source.Skeleton, 18 + 48 * bone);
            //NEG AX wraps before460840 transforms the signed local vector.
            (int x, int y, int z) = ApplyRounded(matrix, 0, 0, unchecked((short)-length));
            Include(unchecked(x + matrix.X), ref minX, ref maxX);
            Include(unchecked(y + matrix.Y), ref minY, ref maxY);
            Include(unchecked(z + matrix.Z), ref minZ, ref maxZ);
        }
        return new(new(minX, minY, minZ), new(maxX, maxY, maxZ));
    }

    private static void Include(int value, ref short minimum, ref short maximum)
    {
        if (value < minimum)
        {
            minimum = unchecked((short)value);
        }
        else if (value > maximum)
        {
            maximum = unchecked((short)value);
        }
    }
}
