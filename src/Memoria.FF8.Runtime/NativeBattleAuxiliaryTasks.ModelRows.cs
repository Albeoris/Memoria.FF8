namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleAuxiliaryState
{
    private static bool IsAuxiliaryModelBound(int model, BattleModelRoster? roster)
    {
        roster?.Validate();
        return (uint)model < (uint)(roster?.ActorCount ?? NativeBattleGraphicsModelAddress.NativeRowCount) &&
            (roster is null || roster.Rows[model] is not null);
    }

    private static bool TryReadAuxiliaryModel(uint address, BattleModelRoster? roster, out int model)
    {
        roster?.Validate();
        return NativeBattleGraphicsModelAddress.TryDecode(address, out model, roster?.ActorCount ?? NativeBattleGraphicsModelAddress.NativeRowCount) &&
            (roster is null || roster.Rows[model] is not null);
    }
}
