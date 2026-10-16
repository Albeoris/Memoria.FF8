namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleUiRandomState? nativeBattleUiRandom;

    private void RestoreNativeBattleUiRandom(NativeBattleUiRandomState? state)
    {
        if ((state is null) != (definition.NativeBattleUiRandom is null) || state?.State >= 0x8000)
        {
            throw new InvalidDataException("Saved native battle UI random state contradicts its battle-entry owner.");
        }
        nativeBattleUiRandom = state;
    }
}
