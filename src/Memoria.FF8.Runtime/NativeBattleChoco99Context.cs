namespace Memoria.FF8.Runtime;

public static class NativeBattleChoco99Context
{
    public static NativeBattleChoco97Context Clear718670() => NativeBattleChoco97Context.Clear72A350();

    public static NativeBattleChoco97Context Initialize718670(Func<int, NativeBattleGraphicsModel> readModel)
    {
        NativeBattleChoco97Context context = NativeBattleChoco97Context.Initialize72A350(readModel);
        return unchecked((short)context.Word(8)) <= 0 ? context.WriteWord(8, 0) : context;
    }
}
