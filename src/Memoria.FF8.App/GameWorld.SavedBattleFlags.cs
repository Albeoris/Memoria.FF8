using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private NativeSavedBattleFlags? ProjectSavedBattleFlagEffect(BattleResult result)
    {
        if (result.NativeScriptEffects.SavedBattleFlagsBefore is not byte before)
        {
            return null;
        }
        NativeSavedBattleFlags current = session.State.SavedBattleFlags ?? throw new InvalidDataException("Battle saved flag effects require their current full canonical BYTE owner.");
        current.Validate();
        if (current.EffectiveKnownMask != byte.MaxValue || current.Value != before)
        {
            throw new InvalidDataException("Battle saved flag effect before value disagrees with its current canonical owner.");
        }
        return current with { Value = result.NativeScriptEffects.SavedBattleFlagsAfter!.Value };
    }
}
