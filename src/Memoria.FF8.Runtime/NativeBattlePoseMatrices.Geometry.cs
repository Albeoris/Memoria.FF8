using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattlePoseMatrices
{
    /// <summary>502D40/5095B0 camera-compose each original508C90 bone matrix before509B30 publishes GTE registers.</summary>
    public static ImmutableArray<NativeBattleActionMatrix> GeometryMatrices(NativeBattleGraphicsModel model, NativeBattlePoseSource source, NativeBattleActionMatrix camera)
    {
        camera.Validate();
        ushort flags2 = model.Flags2 ?? throw new NotSupportedException("Native502D40 requires its actual model flagsWORD2.");
        if ((flags2 & 1) != 0)
        {
            throw new NotSupportedException("Native502D40 billboard geometry requires its original50CDC0 angle owner.");
        }
        NativeBattleModelTransform transform = model.Transform ?? throw new NotSupportedException("Native502D40 requires its actual model transform.");
        NativeBattleSequenceState state = model.SequenceState ?? throw new NotSupportedException("Native502D40 requires its decoded pose owner.");
        NativeBattleDecodedPose pose = source.Sample(state.Pose ?? throw new NotSupportedException("Native geometry has no selected pose."), state.PoseFrame114 ?? throw new NotSupportedException("Native geometry has no decoded frame."), state.PoseFlags109);
        Matrix[] bones = BoneMatrices(source, pose);
        Matrix world = ModelMatrix(model, transform, bones[0]);
        Matrix view = new(camera.Rotation.ToArray(), camera.X, camera.Y, camera.Z);
        Matrix modelView = Compose(view, world);
        return bones.Select(bone => Compose(modelView, bone)).Select(matrix => new NativeBattleActionMatrix(matrix.Rotation.ToImmutableArray(), matrix.X, matrix.Y, matrix.Z, 4096)).ToImmutableArray();
    }
}
