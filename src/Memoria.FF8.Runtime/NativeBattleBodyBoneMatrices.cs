using System.Collections.Immutable;
using System.Buffers.Binary;
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

    public static NativeBattleGraphicsModel PublishLoadedSkeleton(NativeBattleGraphicsModel model, NativeBattlePoseSource source)
    {
        source.Validate();
        ImmutableArray<NativeBattleActionMatrix>.Builder matrices = ImmutableArray.CreateBuilder<NativeBattleActionMatrix>(source.BoneCount);
        for (int bone = 0; bone < source.BoneCount; bone++)
        {
            int offset = 24 + bone * 48;
            ImmutableArray<short> rotation = Enumerable.Range(0, 9).Select(lane => BinaryPrimitives.ReadInt16LittleEndian(source.Skeleton.AsSpan().Slice(offset + lane * 2, 2))).ToImmutableArray();
            matrices.Add(new(rotation, BinaryPrimitives.ReadInt32LittleEndian(source.Skeleton.AsSpan().Slice(offset + 20, 4)), BinaryPrimitives.ReadInt32LittleEndian(source.Skeleton.AsSpan().Slice(offset + 24, 4)), BinaryPrimitives.ReadInt32LittleEndian(source.Skeleton.AsSpan().Slice(offset + 28, 4)), 4096));
        }
        NativeBattleBodyBoneMatrices publication = new(Identity(source), matrices.MoveToImmutable());
        publication.Validate();
        return model with { CurrentBodyBoneMatrices = publication };
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
