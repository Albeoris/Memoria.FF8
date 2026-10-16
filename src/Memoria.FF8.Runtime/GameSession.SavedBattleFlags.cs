namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public void SetNativeWitch(NativeSavedBattleFlagFieldWrite write)
    {
        NativeSavedBattleFlags? flags = State.SavedBattleFlags;
        if (flags is null)
        {
            NativeCharacterVitals vitals = State.CharacterVitals ?? throw new NotSupportedException("Native SETWITCH requires its saved flag owner or the independently known dream bit0 alias.");
            flags = new NativeSavedBattleFlags(vitals.InitOutSha256, (byte)((vitals.DreamEnabled ? 1 : 0) | (write.RawOperand != 0 ? 0x20 : 0)), 0x21, WitchFieldWrite: write);
        }
        GameState candidate = State with { SavedBattleFlags = flags.WithWitch(write) };
        ValidateNativeSavedBattleFlagsCheckpoint(candidate);
        State = candidate;
    }

    public void SetNativeSavedBattleFlagBit4(NativeSavedBattleFlagFieldWrite write)
    {
        NativeCharacterVitals vitals = State.CharacterVitals ?? throw new NotSupportedException("Native529EB0 requires the independently known dream bit0 alias.");
        NativeSavedBattleFlags flags = State.SavedBattleFlags ?? new NativeSavedBattleFlags(vitals.InitOutSha256, (byte)((vitals.DreamEnabled ? 1 : 0) | (write.RawOperand != 0 ? 0x10 : 0)), 0x11, write);
        GameState candidate = State with { SavedBattleFlags = flags.WithFieldBit4(write) };
        ValidateNativeSavedBattleFlagsCheckpoint(candidate);
        State = candidate;
    }

    public void InitializeNewGameSavedBattleFlags(NativeSavedBattleFlags flags)
    {
        flags.Validate();
        if (flags.Value != 0 || flags.KnownMask is not null || flags.FieldWrite is not null || flags.WitchFieldWrite is not null || State.SavedBattleFlags is not null || State.Scene != SceneKind.Field || State.FieldId != 74 || State.EventState is not null || State.SavedWorld?.Origin.Kind != NativeSavedWorldOriginKind.NativeNewGame || State.CharacterVitals?.DreamEnabled == true)
        {
            throw new NotSupportedException("Saved battle flag initialization requires the untouched authentic New Game clear.");
        }
        State = State with { SavedBattleFlags = flags };
    }

    private NativeSavedBattleFlags? SynchronizeSavedBattleFlags(NativeCharacterVitals? vitals)
    {
        NativeSavedBattleFlags? flags = State.SavedBattleFlags;
        if (flags is null || vitals is null)
        {
            return flags;
        }
        flags.Validate();
        if (flags.InitOutSha256 != vitals.InitOutSha256)
        {
            throw new InvalidDataException("Saved battle flags and character records have different native initialization provenance.");
        }
        return flags.WithDreamEnabled(vitals.DreamEnabled);
    }

    private static void ValidateNativeSavedBattleFlagsCheckpoint(GameState state)
    {
        state.SavedBattleFlags?.Validate();
        state.LimitProgress?.Validate();
        if (state.SavedBattleFlags is { } flags && state.CharacterVitals is { } vitals && (flags.InitOutSha256 != vitals.InitOutSha256 || ((flags.Value & 1) != 0) != vitals.DreamEnabled))
        {
            throw new InvalidDataException("Saved battle flags disagree with the canonical DreamEnabled bit0 alias.");
        }
        if (state.LimitProgress is { } progress && state.CharacterVitals is { } characters && progress.InitOutSha256 != characters.InitOutSha256)
        {
            throw new InvalidDataException("Saved Limit progression and characters have different native initialization provenance.");
        }
    }
}
