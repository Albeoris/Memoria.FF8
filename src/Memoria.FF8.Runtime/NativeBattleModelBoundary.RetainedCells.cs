namespace Memoria.FF8.Runtime;

public sealed partial class NativeBattleModelBoundary
{
    internal NativeBattleModelBoundary WriteUnboundCell(NativeBattleModelSlot slot, NativeBattleGraphicsModel state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Cell cell = cells[slot.Offset];
        if (cell.Owner is not null)
        {
            throw new InvalidOperationException("A bound native cell must be updated through its live model owner.");
        }
        return new(cells.SetItem(slot.Offset, cell with { Retained = state }));
    }
}
