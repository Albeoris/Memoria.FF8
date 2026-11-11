namespace Memoria.FF7.Formats;

public sealed record Ff7BattleBossDeathShakeCallback(Ff7BattleBossDeathShakeState State) : Ff7BattleSecondaryCallback;
public sealed record Ff7BattleActorScreenPulse(byte Red, byte Green, byte Blue);

public static partial class Ff7BattleSecondaryEffects
{
    public static Ff7BattleSecondaryState ScheduleBossDeathShake(Ff7BattleSecondaryState state, int target, short baseZ)
    {
        ValidateActor(target);
        return Schedule(state, new Ff7BattleBossDeathShakeCallback(new(target, baseZ)));
    }
}
