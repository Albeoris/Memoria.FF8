using System.Collections.Immutable;
using System.Security.Cryptography;

namespace Memoria.FF8.Runtime;

/// <summary>The current mutable publication of one physical body's rig matrices, whether local or composed.</summary>
public sealed record NativeBattleBodyBoneMatrices(string SkeletonSha256, ImmutableArray<NativeBattleActionMatrix> Matrices)
{
    public void Validate()
    {
        if (SkeletonSha256 is not { Length: 64 } || !SkeletonSha256.All(Uri.IsHexDigit) || Matrices.IsDefaultOrEmpty || Matrices.Length > 128 || Matrices.Any(matrix => matrix is null))
        {
            throw new InvalidDataException("Native body bone matrices require their actual rig identity and complete current publication.");
        }
        foreach (NativeBattleActionMatrix matrix in Matrices)
        {
            matrix.Validate();
        }
    }

    public static NativeBattleGraphicsModel Publish508C90(NativeBattleGraphicsModel model, NativeBattlePoseSource source)
    {
        source.Validate();
        NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Native508C90 requires its actual decoded body pose owner.");
        NativeBattleDecodedPose pose = NativeBattlePoseMatrices.ReadDecodedPose(sequence, source);
        ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattlePoseMatrices.StageBoneMatrices(source, pose);
        NativeBattleBodyBoneMatrices publication = new(Identity(source), matrices);
        publication.Validate();
        return model with { CurrentBodyBoneMatrices = publication };
    }

    public static NativeBattleGraphicsModel Compose5095B0(NativeBattleGraphicsModel model, NativeBattleActionMatrix basis)
    {
        NativeBattleBodyBoneMatrices current = model.CurrentBodyBoneMatrices ?? throw new NotSupportedException("Native5095B0 requires the retained current body matrices; missing publications are not reconstructed.");
        current.Validate();
        ImmutableArray<NativeBattleActionMatrix> composed = NativeBattlePoseMatrices.ComposeStageCamera(current.Matrices, basis, 0);
        return model with { CurrentBodyBoneMatrices = current with { Matrices = composed } };
    }

    public static ImmutableArray<NativeBattleActionMatrix> Require(NativeBattleGraphicsModel model, NativeBattlePoseSource source)
    {
        NativeBattleBodyBoneMatrices current = model.CurrentBodyBoneMatrices ?? throw new NotSupportedException("Native body matrix access requires its actual retained publication; missing imported matrices are unknown.");
        current.Validate();
        source.Validate();
        if (current.Matrices.Length != source.BoneCount || !StringComparer.OrdinalIgnoreCase.Equals(current.SkeletonSha256, Identity(source)))
        {
            throw new NotSupportedException("Native current body matrices belong to a different physical rig source.");
        }
        return current.Matrices;
    }

    private static string Identity(NativeBattlePoseSource source) => Convert.ToHexString(SHA256.HashData(source.Skeleton.AsSpan()));
}
