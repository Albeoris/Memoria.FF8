namespace Memoria.FF8.Runtime;

public sealed partial class NativeBattleModelBoundary
{
    internal NativeBattleModelBoundary UpdateUnboundPartyFlags0(ushort andMask, ushort orMask)
    {
        var updated = cells;
        for (byte offset = 0; offset < CellCount; offset++)
        {
            NativeBattleModelSlot slot = NativeBattleModelSlot.Decode(offset);
            Cell cell = cells[offset];
            if (slot.Side != BattleSide.Allies || cell.Owner is not null || cell.Retained is not { } retained)
            {
                continue;
            }
            ushort flags = (ushort)((retained.Flags0 & andMask) | orMask);
            if (flags != retained.Flags0)
            {
                updated = updated.SetItem(offset, cell with { Retained = retained with { Flags0 = flags } });
            }
        }
        return updated == cells ? this : new(updated);
    }

    internal NativeBattleModelBoundary UpdateUnboundColor44(uint color)
    {
        var updated = cells;
        for (int offset = 0; offset < cells.Length; offset++)
        {
            Cell cell = cells[offset];
            if (cell.Owner is null && cell.Retained is { } model && model.Color44 != color)
            {
                updated = updated.SetItem(offset, cell with { Retained = model with { Color44 = color } });
            }
        }
        return updated == cells ? this : new(updated);
    }
}
