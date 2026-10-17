namespace Memoria.FF8.Runtime;

public static partial class NativeBattlePoseMatrices
{
    public static NativeBattleDecodedPose ReadDecodedPose(NativeBattleSequenceState state, NativeBattlePoseSource source)
    {
        return state.DecodedPose ?? source.Sample(state.Pose ?? throw new NotSupportedException("Native model pose selector is not initialized."), state.PoseFrame114 ?? throw new NotSupportedException("Native model pose frame is not initialized."), state.PoseFlags109);
    }

    public static NativeBattleDecodedPose ReadLinkedDecodedPose(NativeBattleSequenceState state, NativeBattlePoseSource source)
    {
        NativeBattlePoseProgress progress = state.LinkedPose ?? throw new NotSupportedException("Native linked model requires its actual pose progress.");
        return state.LinkedDecodedPose ?? source.Sample(progress.Pose, progress.Frame, progress.Flags);
    }
}
