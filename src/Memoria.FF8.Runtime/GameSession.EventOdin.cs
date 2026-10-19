namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public void RestoreNativeSavedBattleFlags(NativeSavedBattleFlags flags)
    {
        flags.Validate();
        GameState candidate = State with { SavedBattleFlags = flags };
        ValidateNativeSavedBattleFlagsCheckpoint(candidate);
        State = candidate;
    }

    public void SetNativeOdin()
    {
        NativeSavedBattleFlags flags = State.SavedBattleFlags ?? throw new NotSupportedException("SETODIN requires the authentic saved battle flag BYTE.");
        flags.Validate();
        if (flags.EffectiveKnownMask != byte.MaxValue)
        {
            throw new NotSupportedException("SETODIN requires the complete saved battle flag BYTE; missing historical bits are not inferred.");
        }
        GameState candidate = State with { SavedBattleFlags = flags with { Value = (byte)(flags.Value | 2) } };
        ValidateNativeSavedBattleFlagsCheckpoint(candidate);
        State = candidate;
    }
}
