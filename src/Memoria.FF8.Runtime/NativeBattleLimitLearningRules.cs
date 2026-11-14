namespace Memoria.FF8.Runtime;

/// <summary>48D200's durable learned-byte producers, independent of effect execution.</summary>
public static class NativeBattleLimitLearningRules
{
    public static byte ShotMask(BattleNativeAction action) => action.Command == 237 && action.ActionId < 8 ? checked((byte)(1 << action.ActionId)) : (byte)0;
    public static byte SlotsMask(BattleNativeAction action) => action.Command == 16 && action.ActionId is >= 51 and <= 56 ? checked((byte)(1 << (action.ActionId - 51))) : (byte)0;
}
