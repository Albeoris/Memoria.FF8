namespace Memoria.FF7.Formats;

public sealed record Ff7BattleActionTitle(byte Command, ushort Argument);
public sealed record Ff7BattlePrimaryActionTitle(Ff7BattleActionTitle Title, byte Remaining) : Ff7BattlePrimaryCallback;

public static partial class Ff7BattlePrimaryEffects
{
    public static Ff7BattlePrimaryState ScheduleActionTitle(Ff7BattlePrimaryState state, byte command, ushort argument, byte messageSpeed)
    {
        PrimaryPass pass = new(state, null);
        pass.Allocate(new Ff7BattlePrimaryActionTitle(new(command, argument), (byte)((messageSpeed >> 2) + 4)));
        return pass.Finish(state.Tick, preserveReceipts: true);
    }

    private static Ff7BattlePrimaryCallback? ExecuteActionTitle(Ff7BattlePrimaryActionTitle callback, PrimaryPass pass)
    {
        if (callback.Remaining == 0)
        {
            return null;
        }
        if (callback.Remaining > 67)
        {
            throw new InvalidDataException("FFVII action title has an invalid native duration.");
        }
        pass.ActionTitle = callback.Title;
        return callback with { Remaining = (byte)(callback.Remaining - 1) };
    }
}
