using System.Collections.Frozen;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleCinematicDispatch
{
    private static readonly FrozenDictionary<(string Table, int Index), (uint Handler, uint Canonical)> Effect254Slots = new Dictionary<(string Table, int Index), (uint Handler, uint Canonical)>
    {
        [("vm_table", 0x0)] = (0x9978D0, 0xB2FEB0),
        [("vm_table", 0x1)] = (0x997540, 0xB2FB20),
        [("vm_table", 0x2)] = (0x9929A0, 0xB2AE30),
        [("vm_table", 0x3)] = (0x9929C0, 0xB2AE50),
        [("vm_table", 0x4)] = (0x992A20, 0xB2AEB0),
        [("vm_table", 0x5)] = (0x993BA0, 0xB2C030),
        [("vm_table", 0x9)] = (0x9978A0, 0xB2FE80),
        [("vm_table", 0xA)] = (0x993FF0, 0xB2C480),
        [("vm_table", 0xB)] = (0x993FF0, 0xB2C480),
        [("vm_table", 0xC)] = (0x993FF0, 0xB2C480),
        [("vm_table", 0xD)] = (0x993FF0, 0xB2C480),
        [("vm_table", 0xE)] = (0x993FF0, 0xB2C480),
        [("vm_table", 0x10)] = (0x993FF0, 0xB2C480),
        [("vm_table", 0x11)] = (0x993FF0, 0xB2C480),
        [("vm_table", 0x12)] = (0x993FF0, 0xB2C480),
        [("vm_table", 0x20)] = (0x995E60, 0xB2E2F0),
        [("vm_table", 0x21)] = (0x992FC0, 0xB2B450),
        [("vm_table", 0x22)] = (0x9930A0, 0xB2B530),
        [("vm_table", 0x23)] = (0x995B10, 0xB2DFA0),
        [("vm_table", 0x24)] = (0x995660, 0xB2DAF0),
        [("vm_table", 0x26)] = (0x995030, 0xB2D4C0),
        [("vm_table", 0x27)] = (0x9960B0, 0xB2E540),
        [("vm_table", 0x2A)] = (0x995100, 0xB2D590),
        [("vm_table", 0x2B)] = (0x98D5E0, 0xB25BB0),
        [("vm_table", 0x2F)] = (0x994440, 0xB2C8D0),
        [("vm_table", 0x32)] = (0x995180, 0xB2D610),
        [("vm_table", 0x33)] = (0x992B90, 0xB2B020),
        [("vm_table", 0x39)] = (0x9932C0, 0xB2B750),
        [("vm_table", 0x3B)] = (0x9934F0, 0xB2B980),
        [("vm_table", 0x3C)] = (0x997030, 0xB2F3E0),
        [("vm_table", 0x40)] = (0x9958A0, 0xB2DD30),
        [("vm_table", 0x44)] = (0x992D20, 0xB2B1B0),
        [("vm_table", 0x51)] = (0x9956B0, 0xB2DB40),
        [("vm_table", 0x52)] = (0x996130, 0xB2E5C0),
        [("vm_table", 0x58)] = (0x991A20, 0xB29D70),
        [("vm_table", 0x59)] = (0x991A90, 0xB29DE0),
        [("vm_table", 0x5E)] = (0x9943E0, 0xB2C870),
        [("vm_table", 0x61)] = (0x994830, 0xB2CCC0),
        [("vm_table", 0x62)] = (0x994830, 0xB2CCC0),
        [("vm_table", 0x64)] = (0x992C50, 0xB2B0E0),
        [("vm_table", 0x67)] = (0x9964C0, 0xB2E950),
        [("vm_table", 0x68)] = (0x996820, 0xB2ECB0),
        [("vm_table", 0x74)] = (0x993ED0, 0xB2C360),
        [("vm_table", 0x7D)] = (0x993A00, 0xB2BE90),
        [("vm_table", 0x8B)] = (0x995090, 0xB2D520),
        [("vm_table", 0xAC)] = (0x994EB0, 0xB2D340),
        [("vm_table", 0xAD)] = (0x996ED0, 0x996ED0),
        [("vm_table", 0xB1)] = (0x992CC0, 0xB2B150),
        [("vm_table", 0xD0)] = (0x995770, 0xB2DC00),
        [("vm_table", 0xD3)] = (0x9959A0, 0xB2DE30),
        [("vm_table", 0x10C)] = (0x993C70, 0xB2C100),
        [("vm_table", 0x10D)] = (0x993D40, 0xB2C1D0),
        [("vm_table", 0x116)] = (0x996680, 0xB2EB10),
        [("bone_table", 0x0)] = (0x98DD10, 0xB262E0),
        [("bone_table", 0x1)] = (0x98DD10, 0xB262E0),
        [("draw_table", 0x2)] = (0x98F890, 0xB27050),
        [("draw_table", 0x6)] = (0x991BD0, 0xB29F20),
        [("draw_table", 0x25)] = (0x98E320, 0xB1A130),
        [("prim_table", 0x7)] = (0x9907C0, 0xB27E90),
        [("prim_table", 0x11)] = (0x990AD0, 0xB28B00),
        [("prim_table", 0x12)] = (0x990DA0, 0xB28DD0),
        [("pop_table", 0x0)] = (0x992170, 0xB2A4C0),
        [("pop_table", 0x1)] = (0x9921A0, 0xB2A4F0),
        [("pop_table", 0x4)] = (0x992220, 0xB2A570),
        [("pop_table", 0x5)] = (0x9922E0, 0xB2A630),
        [("pop_table", 0x7)] = (0x9924D0, 0xB2A820),
        [("pop_table", 0x13)] = (0x992760, 0xB2AAB0),
        [("ptype_table", 0x1)] = (0x991F90, 0xB2A2E0),
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<uint, uint> Effect254Aliases = Effect254Slots.Values.Where(pair => pair.Handler != pair.Canonical).Distinct().ToFrozenDictionary(pair => pair.Handler, pair => pair.Canonical);

    private static uint RequireEffect254Slot(string table, int index, uint handler)
    {
        if (!Effect254Slots.TryGetValue((table, index), out var selected) || handler != selected.Handler)
        {
            throw new NotSupportedException($"Native cinematic effect 254 table {table} slot {index:X} requires its selected handler contract.");
        }
        return selected.Canonical;
    }
}
