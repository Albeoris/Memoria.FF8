using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShadowSource(NativeBattleActionMatrix OffsetMatrix, NativeBattleVector RowScale, ImmutableArray<NativeBattleVector> Vertices)
{
    public void Validate()
    {
        OffsetMatrix.Validate();
        if (Vertices.IsDefault || Vertices.Length != 10)
        {
            throw new InvalidDataException("Native5088A0 requires its authenticated ten-vector template.");
        }
    }
}
public sealed record NativeBattleShadowGeometry(NativeBattleActionMatrix CameraMatrix, ImmutableArray<NativeBattleVector> Vertices);

public static partial class NativeBattlePoseMatrices
{
    public static NativeBattleGraphicsModel PublishShadowBounds5086F0(NativeBattleGraphicsModel model, NativeBattlePoseSource source)
    {
        NativeBattleSequenceState state = model.SequenceState ?? throw new NotSupportedException("Native5086F0 requires its actual resource header and pose owner.");
        ushort flags2 = model.Flags2 ?? throw new NotSupportedException("Native5086F0 requires modelWORD2.");
        byte sequence = model.Sequence ?? throw new NotSupportedException("Native5086F0 requires its resource header's current sequenceBYTE0.");
        NativeBattleDecodedPose pose = source.Sample(state.Pose ?? throw new NotSupportedException("Native5086F0 has no selected pose."), state.PoseFrame114 ?? throw new NotSupportedException("Native5086F0 has no decoded pose frame."), state.PoseFlags109);
        NativeBattleEffectLocalBounds bounds = ShadowBounds5086F0(source, pose);
        short? minimum = model.MinimumY146;
        if ((flags2 & 8) == 0 && sequence == state.Default2)
        {
            //An unknown retained minimum stays unknown; its earlier standing-pose samples cannot be reconstructed here.
            minimum = minimum is short value ? Math.Min(value, bounds.Minimum.Y) : bounds.Minimum.Y == short.MinValue ? short.MinValue : null;
        }
        return model with { GeometryBounds52 = bounds, MinimumY146 = minimum };
    }

    /// <summary>5086F0's signedWORD model bounds, including the root's500-unit X/Z margin and positive bone-length vector.</summary>
    public static NativeBattleEffectLocalBounds ShadowBounds5086F0(NativeBattlePoseSource source, NativeBattleDecodedPose pose)
    {
        source.Validate();
        if (pose.Bones.IsDefault || pose.Bones.Length != source.BoneCount)
        {
            throw new InvalidDataException("Native5086F0 requires every actual posed bone matrix.");
        }
        Matrix[] matrices = BoneMatrices(source, pose);
        short minX = unchecked((short)(matrices[0].X - 500));
        short maxX = unchecked((short)(matrices[0].X + 500));
        short minY = unchecked((short)matrices[0].Y);
        short maxY = minY;
        short minZ = unchecked((short)(matrices[0].Z - 500));
        short maxZ = unchecked((short)(matrices[0].Z + 500));
        for (int bone = 1; bone < matrices.Length; bone++)
        {
            Matrix matrix = matrices[bone];
            short length = ReadWord(source.Skeleton, 18 + 48 * bone);
            (int x, int y, int z) = ApplyRounded(matrix, 0, 0, length);
            Include(unchecked(x + matrix.X), ref minX, ref maxX);
            Include(unchecked(y + matrix.Y), ref minY, ref maxY);
            Include(unchecked(z + matrix.Z), ref minZ, ref maxZ);
        }
        return new(new(minX, minY, minZ), new(maxX, maxY, maxZ));
    }

    public static NativeBattleShadowGeometry ShadowGeometry5088A0(NativeBattleGraphicsModel model, NativeBattleEffectLocalBounds bounds, NativeBattleActionMatrix camera, NativeBattleShadowSource source, bool gouraudShadows)
    {
        NativeBattleActionMatrix previous = model.GeometryWorldMatrix ?? throw new NotSupportedException("Native5088A0 requires the retained model matrix at+40h.");
        short groundY = (model.Transform ?? throw new NotSupportedException("Native5088A0 requires modelWORD36.")).Angle36;
        return ShadowGeometry5088A0(previous, groundY, bounds, camera, source, gouraudShadows);
    }

    public static NativeBattleShadowGeometry ShadowGeometry5088A0(NativeBattleActionMatrix previous, short groundY, NativeBattleEffectLocalBounds bounds, NativeBattleActionMatrix camera, NativeBattleShadowSource source, bool gouraudShadows)
    {
        source.Validate();
        camera.Validate();
        previous.Validate();
        short centerX = unchecked((short)((bounds.Minimum.X + bounds.Maximum.X) / 2));
        short centerZ = unchecked((short)((bounds.Minimum.Z + bounds.Maximum.Z) / 2));
        short radiusX = unchecked((short)(bounds.Maximum.X - centerX));
        short radiusZ = unchecked((short)(bounds.Maximum.Z - centerZ));
        if (!gouraudShadows)
        {
            radiusX = unchecked((short)(radiusX >> 1));
            radiusZ = unchecked((short)(radiusZ >> 1));
        }
        Matrix offset = new(source.OffsetMatrix.Rotation.ToArray(), centerX, source.OffsetMatrix.Y, centerZ);
        Matrix world = new(previous.Rotation.ToArray(), previous.X, groundY, previous.Z);
        Matrix centered = Compose(world, offset);
        int[] rowScale = [source.RowScale.X, source.RowScale.Y, source.RowScale.Z];
        centered = centered with { Rotation = centered.Rotation.Select((value, index) => unchecked((short)(value * rowScale[index / 3] >> 12))).ToArray() };
        Matrix result = Compose(new(camera.Rotation.ToArray(), camera.X, camera.Y, camera.Z), centered);
        int sine = NativeBattleCameraMath.Sine(512);
        short diagonalX = unchecked((short)(sine * radiusX >> 12));
        short diagonalZ = unchecked((short)(sine * radiusZ >> 12));
        NativeBattleVector[] vertices = source.Vertices.ToArray();
        vertices[1] = vertices[1] with { Z = unchecked((short)-radiusZ) };
        vertices[2] = vertices[2] with { X = unchecked((short)-diagonalX), Z = unchecked((short)-diagonalZ) };
        vertices[3] = vertices[3] with { X = unchecked((short)-radiusX) };
        vertices[4] = vertices[4] with { X = unchecked((short)-diagonalX), Z = diagonalZ };
        vertices[5] = vertices[5] with { Z = radiusZ };
        vertices[6] = vertices[6] with { X = diagonalX, Z = diagonalZ };
        vertices[7] = vertices[7] with { X = radiusX };
        vertices[8] = vertices[8] with { X = diagonalX, Z = unchecked((short)-diagonalZ) };
        vertices[9] = vertices[9] with { Z = unchecked((short)-radiusZ) };
        return new(new(result.Rotation.ToImmutableArray(), result.X, result.Y, result.Z, 4096), vertices.ToImmutableArray());
    }
}
