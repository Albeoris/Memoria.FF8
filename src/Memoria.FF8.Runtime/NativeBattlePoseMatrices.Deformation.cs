using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattlePoseMatrices
{
    public static NativeBattleActionMatrix ReadDeformationBone50F2E0(NativeBattleGraphicsModel model, NativeBattlePoseSource source, byte selected)
    {
        NativeBattleSequenceState state = model.SequenceState ?? throw new NotSupportedException("Native50F2E0 requires its decoded pose owner.");
        Matrix[] bones = BoneMatrices(source, ReadDecodedPose(state, source));
        Matrix bone = selected < bones.Length ? bones[selected] : throw new InvalidDataException("Native50F2E0 selected bone exceeds its actual skeleton.");
        return new(bone.Rotation.ToImmutableArray(), bone.X, bone.Y, bone.Z, 4096);
    }

    public static ImmutableArray<NativeBattleActionMatrix> DeformedGroupMatrices50F2E0(NativeBattleGraphicsModel model, NativeBattlePoseSource source, NativeBattleActionMatrix camera, byte selected, NativeBattleActionMatrix retained)
    {
        NativeBattleSequenceState state = model.SequenceState ?? throw new NotSupportedException("Native50F2E0 requires its decoded pose owner.");
        NativeBattleDecodedPose pose = ReadDecodedPose(state, source);
        Matrix[] bones = BoneMatrices(source, pose);
        if (selected >= bones.Length)
        {
            throw new InvalidDataException("Native50F2E0 selected bone exceeds its actual skeleton.");
        }
        bones[selected] = new(retained.Rotation.ToArray(), retained.X, retained.Y, retained.Z);
        for (int bone = selected + 1; bone < bones.Length; bone++)
        {
            short parent = ReadWord(source.Skeleton, 16 + bone * 48);
            if (parent < 0 || parent >= bone)
            {
                throw new InvalidDataException("Native50F2E0 descendant requires its earlier parent bone.");
            }
            bones[bone] = Compose(bones[parent], Rotation(pose.Bones[bone].Rotation) with { Z = ReadWord(source.Skeleton, 18 + parent * 48) });
        }
        NativeBattleActionMatrix retainedWorld = model.GeometryWorldMatrix ?? throw new NotSupportedException("Native50F2E0 requires its retained model matrix.");
        Matrix world = new(retainedWorld.Rotation.ToArray(), retainedWorld.X, retainedWorld.Y, retainedWorld.Z);
        Matrix view = new(camera.Rotation.ToArray(), camera.X, camera.Y, camera.Z);
        Matrix basis = Compose(view, world);
        return bones.Select(bone => Compose(basis, bone)).Select(matrix => new NativeBattleActionMatrix(matrix.Rotation.ToImmutableArray(), matrix.X, matrix.Y, matrix.Z, 4096)).ToImmutableArray();
    }
}
