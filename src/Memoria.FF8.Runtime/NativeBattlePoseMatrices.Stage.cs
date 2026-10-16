using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattlePoseMatrices
{
    public static ImmutableArray<NativeBattleActionMatrix> StageBoneMatrices(NativeBattlePoseSource source, NativeBattleDecodedPose pose) => BoneMatrices(source, pose).Select(matrix => new NativeBattleActionMatrix(matrix.Rotation.ToImmutableArray(), matrix.X, matrix.Y, matrix.Z, 4096)).ToImmutableArray();

    /// <summary>500FD0/5095B0 overwrites each retained stage matrix, including when508F90 is frozen.</summary>
    public static ImmutableArray<NativeBattleActionMatrix> ComposeStageCamera(ImmutableArray<NativeBattleActionMatrix> bones, NativeBattleActionMatrix camera, short groupAngle)
    {
        Matrix view = new(camera.Rotation.ToArray(), camera.X, camera.Y, camera.Z);
        if (groupAngle != 0)
        {
            view = Multiply(view, Rotation(new(0, groupAngle, 0))) with { X = camera.X, Y = camera.Y, Z = camera.Z };
        }
        return bones.Select(bone => Compose(view, new(bone.Rotation.ToArray(), bone.X, bone.Y, bone.Z))).Select(matrix => new NativeBattleActionMatrix(matrix.Rotation.ToImmutableArray(), matrix.X, matrix.Y, matrix.Z, 4096)).ToImmutableArray();
    }
}
