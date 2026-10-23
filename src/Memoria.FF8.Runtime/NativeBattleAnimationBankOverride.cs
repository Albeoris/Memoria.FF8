using System.Collections.Immutable;
using System.Security.Cryptography;

namespace Memoria.FF8.Runtime;

/// <summary>An explicit owner-scoped source bank replacement, resolved before native pose decoding.</summary>
public sealed record NativeBattleAnimationBankOverride(string Bank, uint? SourceDirectory, bool Linked, string SourceFingerprint, ImmutableArray<byte> Animations);

public sealed partial record NativeBattleGraphicsResource
{
    public ImmutableArray<NativeBattleAnimationBankOverride> AnimationBankOverrides { get; init; } = [];

    public NativeBattleGraphicsResource ApplyAnimationBankOverrides(string? bank, uint? directory = null)
    {
        NativeBattlePoseSource? body = Replace(PoseSource, false);
        NativeBattlePoseSource? weapon = Replace(LinkedPoseSource, true);
        return this with { PoseSource = body, LinkedPoseSource = weapon, AnimationFrames = body is null ? AnimationFrames : NativeBattlePoseDecoder.ReadFrameCounts(body.Animations.AsSpan()), LinkedAnimationFrames = weapon is null ? LinkedAnimationFrames : NativeBattlePoseDecoder.ReadFrameCounts(weapon.Animations.AsSpan()) };

        NativeBattlePoseSource? Replace(NativeBattlePoseSource? source, bool linked)
        {
            NativeBattleAnimationBankOverride[] matches = AnimationBankOverrides.Where(item => item.Linked == linked && (bank is not null ? item.Bank == bank : directory is not null && item.SourceDirectory == directory)).ToArray();
            if (matches.Length == 0 || source is null)
            {
                return source;
            }
            if (matches.Length != 1)
            {
                throw new InvalidDataException("The active model has duplicate overrides for an explicitly bound animation bank.");
            }
            NativeBattleAnimationBankOverride replacement = matches[0];
            string fingerprint = Convert.ToHexString(SHA256.HashData(source.Animations.AsSpan()));
            if (fingerprint != replacement.SourceFingerprint)
            {
                if (source.Animations.AsSpan().SequenceEqual(replacement.Animations.AsSpan()))
                {
                    return source;
                }
                throw new InvalidDataException("An animation bank override no longer matches its bound source revision.");
            }
            NativeBattlePoseSource result = source with { Animations = replacement.Animations };
            result.Validate();
            return result;
        }
    }
}
