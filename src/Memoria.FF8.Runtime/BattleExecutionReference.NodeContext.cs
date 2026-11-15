using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal abstract partial record BattleExecutionReference
{
    internal sealed record NodeContext(BattleModelOwner Source, ImmutableArray<NativeBattleEffectNodeRecipient> Recipients) : BattleExecutionReference
    {
        internal NativeBattleEffectNodeRecipient ReadRecipient(int position)
        {
            if ((uint)position >= (uint)Recipients.Length)
            {
                throw new InvalidDataException("Effect node recipient position leaves its captured contact list.");
            }
            return Recipients[position];
        }
    }
}

internal readonly record struct NativeBattleEffectNodeVector(uint X, uint Y, uint Z, uint Padding, byte KnownPaddingBytes = 0xF)
{
    internal bool IsPaddingByteKnown(int index) => (KnownPaddingBytes & (1 << index)) != 0;
}

internal sealed record NativeBattleEffectNodeRecipient(BattleModelOwner Owner, BattleContactOccurrence Occurrence, NativeBattleEffectNodeVector Heading, NativeBattleEffectNodeVector Ground, NativeBattleEffectNodeVector BoneF1, NativeBattleEffectNodeVector BoneF0, NativeBattleEffectNodeVector UpperBound)
{
    internal NativeBattleEffectNodeVector ReadAnchor(int selector) => selector switch
    {
        0 => Ground,
        1 => BoneF1,
        2 => BoneF0,
        3 => UpperBound,
        _ => throw new ArgumentOutOfRangeException(nameof(selector))
    };
}
