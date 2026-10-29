namespace Memoria.FF8.GameUi;

public static class NativeMenuUseFeedback
{
    public static MenuSound Magic(int attackType, bool changed)
    {
        if (!changed)
        {
            return MenuSound.Error;
        }
        return attackType switch
        {
            3 => MenuSound.Recovery,
            5 or 6 => MenuSound.StatusRecovery,
            _ => throw new NotSupportedException($"Field magic attack type {attackType} has no supported menu feedback.")
        };
    }

    public static MenuSound? Item(int type, bool changed)
    {
        // Native4F81F0 case17 classifies ordinary item effects through the original type table.
        return type switch
        {
            0 or 3 or 6 => changed ? MenuSound.Recovery : MenuSound.Error,
            1 or 2 or 4 or 5 or 16 or 17 or 20 or 21 => changed ? MenuSound.StatusRecovery : MenuSound.Error,
            19 => changed ? MenuSound.ItemUsed : MenuSound.Error,
            _ => null
        };
    }
}
