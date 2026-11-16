namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public NativeSavedBattleFlags PrepareDiagnosticAutomaticGuardian(bool odin)
    {
        if (liveBattle is not null || State.Battle is not null)
        {
            throw new InvalidOperationException("Diagnostic automatic support must be prepared before battle allocation.");
        }

        NativeSavedBattleFlags before = State.SavedBattleFlags ?? throw new NotSupportedException("Diagnostic automatic support requires the initialized saved flag owner.");
        before.Validate();
        if ((before.EffectiveKnownMask & 0x0A) != 0x0A)
        {
            throw new NotSupportedException("Diagnostic automatic support requires known Odin and Gilgamesh unlock bits.");
        }

        byte unlock = odin ? (byte)2 : (byte)8;
        NativeSavedBattleFlags prepared = before with { Value = (byte)((before.Value & ~0x0A) | unlock) };
        GameState candidate = State with { SavedBattleFlags = prepared };
        ValidateNativeSavedBattleFlagsCheckpoint(candidate);
        State = candidate;
        return prepared;
    }
}
