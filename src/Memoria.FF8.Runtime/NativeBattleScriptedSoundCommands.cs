namespace Memoria.FF8.Runtime;

/// <summary>Cinematic sound-bank pause/resume over the explicitly selected native backend.</summary>
public static class NativeBattleScriptedSoundCommands
{
    public static NativeBattleWaterExecutionFrame ExecuteC3(NativeBattleWaterExecutionFrame frame, NativeGlobalSoundBackend backend)
    {
        if ((frame.ReadTemporaryWord(0x14A) & 0x1FF) != 0xC3)
        {
            throw new NotSupportedException("Native cinematic sound command requires its selected instruction.");
        }
        if (backend != NativeGlobalSoundBackend.Bufferless)
        {
            throw new NotSupportedException("Native cinematic sound-bank control requires its actual backend contract.");
        }
        // Selectors2 and3 both reach the SFX bank. With no native DirectSound device,
        // both pause and resume return before reading or changing any voice state.
        return NativeBattleScriptedCommandReads.Advance(frame, 2);
    }
}
