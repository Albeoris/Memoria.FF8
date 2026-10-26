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
        return model with { CurrentBodyBoneMatrices = ReadLoadedSkeleton(source) };
    }

    public static NativeBattleGraphicsModel PublishLoadedSkeletons(NativeBattleGraphicsModel model, NativeBattlePoseSource bodySource, NativeBattlePoseSource? linkedSource)
    {
        model = PublishLoadedSkeleton(model, bodySource);
        return model with { CurrentLinkedBoneMatrices = linkedSource is null ? null : ReadLoadedSkeleton(linkedSource) };
    }

    public static NativeBattleGraphicsModel PublishLinked508C90(NativeBattleGraphicsModel model, NativeBattlePoseSource source)
    {
        source.Validate();
        NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Native linked508C90 requires its actual decoded linked pose owner.");
        NativeBattleDecodedPose pose = NativeBattlePoseMatrices.ReadLinkedDecodedPose(sequence, source);
        NativeBattleBodyBoneMatrices publication = new(Identity(source), NativeBattlePoseMatrices.StageBoneMatrices(source, pose));
        publication.Validate();
        return model with { CurrentLinkedBoneMatrices = publication };
    }

    public static NativeBattleGraphicsModel Publish508F60(NativeBattleGraphicsModel model, NativeBattlePoseSource bodySource, NativeBattlePoseSource? linkedSource)
    {
        model = Publish508C90(model, bodySource);
        return linkedSource is null ? model : PublishLinked508C90(model, linkedSource);
    }

    public static NativeBattleBodyBoneMatrices ReadLoadedSkeleton(NativeBattlePoseSource source)
    {
        source.Validate();
        ImmutableArray<NativeBattleActionMatrix>.Builder matrices = ImmutableArray.CreateBuilder<NativeBattleActionMatrix>(source.BoneCount);
        for (int bone = 0; bone < source.BoneCount; bone++)
        {
            int offset = 32 + bone * 48;
            ImmutableArray<short> rotation = Enumerable.Range(0, 9).Select(lane => BinaryPrimitives.ReadInt16LittleEndian(source.Skeleton.AsSpan().Slice(offset + lane * 2, 2))).ToImmutableArray();
            matrices.Add(new(rotation, BinaryPrimitives.ReadInt32LittleEndian(source.Skeleton.AsSpan().Slice(offset + 20, 4)), BinaryPrimitives.ReadInt32LittleEndian(source.Skeleton.AsSpan().Slice(offset + 24, 4)), BinaryPrimitives.ReadInt32LittleEndian(source.Skeleton.AsSpan().Slice(offset + 28, 4)), 4096));
        }
        NativeBattleBodyBoneMatrices publication = new(Identity(source), matrices.MoveToImmutable());
        publication.Validate();
        return publication;
    }

    public static NativeBattleGraphicsModel Publish508C90(NativeBattleGraphicsModel model, NativeBattlePoseSource source)
    {
        NativeBattleDecodedPose pose = ReadCurrentPose508C90(model, source);
        ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattlePoseMatrices.StageBoneMatrices(source, pose);
        NativeBattleBodyBoneMatrices publication = new(Identity(source), matrices);
        publication.Validate();
        return model with { CurrentBodyBoneMatrices = publication };
    }

    public static NativeBattleDecodedPose ReadCurrentPose508C90(NativeBattleGraphicsModel model, NativeBattlePoseSource source)
    {
        source.Validate();
        NativeBattleSequenceState sequence = model.SequenceState ?? throw new NotSupportedException("Native508C90 requires its actual current body owner.");
        if (sequence.DecodedPose is not null || sequence.Pose is not null)
        {
            return NativeBattlePoseMatrices.ReadDecodedPose(sequence, source);
        }

        //508C90 consumes current skeleton fields even before an animation has been selected.
        NativeBattleEffectArena skeleton = NativeBattleEffectArena.Clear().Write(0, source.Skeleton.AsSpan());
        if (!source.SkeletonKnownBytes.IsDefaultOrEmpty)
        {
            for (int offset = 0; offset < source.SkeletonKnownBytes.Length; offset++)
            {
                if (!source.SkeletonKnownBytes[offset])
                {
                    skeleton = skeleton.MarkUnknown(offset, 1);
                }
            }
        }
        return NativeBattleArenaPose.ReadCurrentPose(skeleton, 0, source.BoneCount);
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
        return RequirePublication(current, source);
    }

    public static ImmutableArray<NativeBattleActionMatrix> RequirePublication(NativeBattleBodyBoneMatrices current, NativeBattlePoseSource source)
    {
        current.Validate();
        source.Validate();
        bool identityMatches = StringComparer.OrdinalIgnoreCase.Equals(current.SkeletonSha256, Identity(source));
        if (!identityMatches && (source.SkeletonKnownBytes.IsDefaultOrEmpty || source.SkeletonKnownBytes.All(value => value)))
        {
            identityMatches = StringComparer.OrdinalIgnoreCase.Equals(current.SkeletonSha256, Convert.ToHexString(SHA256.HashData(source.Skeleton.AsSpan())));
        }
        if (current.Matrices.Length != source.BoneCount || !identityMatches)
        {
            throw new NotSupportedException("Native current body matrices belong to a different physical rig source.");
        }
        return current.Matrices;
    }

    public static string Identity(NativeBattlePoseSource source)
    {
        source.Validate();
        byte[] rig = new byte[checked(16 + source.BoneCount * 16)];
        source.Skeleton.AsSpan().Slice(0, 16).CopyTo(rig);
        for (int bone = 0; bone < source.BoneCount; bone++)
        {
            source.Skeleton.AsSpan().Slice(16 + bone * 48, 16).CopyTo(rig.AsSpan(16 + bone * 16));
        }
        return Convert.ToHexString(SHA256.HashData(rig));
    }
}
