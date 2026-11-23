namespace Memoria.FF7.Formats;

public sealed record Ff7BattleBossDeathShakeCallback(Ff7BattleBossDeathShakeState State) : Ff7BattleSecondaryCallback;
public sealed record Ff7BattleScreenFadeCallback : Ff7BattleSecondaryCallback
{
    internal const int RampEndAge = 15;
    public const int HoldAge = 16;
}

public sealed record Ff7BattleActorScreenPulse(byte Red, byte Green, byte Blue)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public byte? Opacity { get; init; }
}

public static partial class Ff7BattleSecondaryEffects
{
    public static Ff7BattleSecondaryState ScheduleBossDeathShake(Ff7BattleSecondaryState state, int target, short baseZ)
    {
        ValidateActor(target);
        return Schedule(state, new Ff7BattleBossDeathShakeCallback(new(target, baseZ)));
    }
}
