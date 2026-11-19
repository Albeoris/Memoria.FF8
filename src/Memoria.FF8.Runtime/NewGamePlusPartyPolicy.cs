namespace Memoria.FF8.Runtime;

public static class NewGamePlusPartyPolicy
{
    public static byte EffectiveMenuSelection(GameState state)
    {
        byte native = state.WorldControls?.Menu209 ?? 0;
        return state.Scene == SceneKind.WorldMap && state.NewGamePlus?.AllowWorldMapPartyChanges == true ? (byte)(native | 2) : native;
    }
}
