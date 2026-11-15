namespace Memoria.FF8.Runtime;

public sealed partial class NativeBattleModelBoundary
{
    internal NativeBattleModelBoundary ClearUnboundPartyCellFlags(NativeBattleModelSlot slot, ushort mask)
    {
        Cell cell = cells[slot.Offset];
        if (slot.Side != BattleSide.Allies || cell.Owner is not null || cell.Retained is not { } retained)
        {
            throw new InvalidDataException("Expected an unbound original party model cell.");
        }
        ushort flags = (ushort)(retained.Flags0 & ~mask);
        return flags == retained.Flags0 ? this : new(cells.SetItem(slot.Offset, cell with { Retained = retained with { Flags0 = flags } }));
    }
}
