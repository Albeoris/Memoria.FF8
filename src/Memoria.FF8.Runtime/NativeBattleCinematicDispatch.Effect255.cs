using System.Collections.Frozen;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleCinematicDispatch
{
    private static readonly FrozenDictionary<(string Table, int Index), (uint Handler, uint Canonical)> Effect255Slots = new Dictionary<(string Table, int Index), (uint Handler, uint Canonical)>
    {
        [("vm_table", 0x0)] = (0x98CD70, 0xB2FEB0),
        [("vm_table", 0x1)] = (0x98C9E0, 0xB2FB20),
        [("vm_table", 0x2)] = (0x987E40, 0xB2AE30),
        [("vm_table", 0x3)] = (0x987E60, 0xB2AE50),
        [("vm_table", 0x4)] = (0x987EC0, 0xB2AEB0),
        [("vm_table", 0x5)] = (0x989040, 0xB2C030),
        [("vm_table", 0x9)] = (0x98CD40, 0xB2FE80),
        [("vm_table", 0xA)] = (0x989490, 0xB2C480),
        [("vm_table", 0xB)] = (0x989490, 0xB2C480),
        [("vm_table", 0xC)] = (0x989490, 0xB2C480),
        [("vm_table", 0xD)] = (0x989490, 0xB2C480),
        [("vm_table", 0xE)] = (0x989490, 0xB2C480),
        [("vm_table", 0x10)] = (0x989490, 0xB2C480),
        [("vm_table", 0x11)] = (0x989490, 0xB2C480),
        [("vm_table", 0x12)] = (0x989490, 0xB2C480),
        [("vm_table", 0x20)] = (0x98B300, 0xB2E2F0),
        [("vm_table", 0x21)] = (0x988460, 0xB2B450),
        [("vm_table", 0x22)] = (0x988540, 0xB2B530),
        [("vm_table", 0x23)] = (0x98AFB0, 0xB2DFA0),
        [("vm_table", 0x24)] = (0x98AB00, 0xB2DAF0),
        [("vm_table", 0x26)] = (0x98A4D0, 0xB2D4C0),
        [("vm_table", 0x27)] = (0x98B550, 0xB2E540),
        [("vm_table", 0x2A)] = (0x98A5A0, 0xB2D590),
        [("vm_table", 0x2B)] = (0x982A80, 0xB25BB0),
        [("vm_table", 0x2F)] = (0x9898E0, 0xB2C8D0),
        [("vm_table", 0x32)] = (0x98A620, 0xB2D610),
        [("vm_table", 0x33)] = (0x988030, 0xB2B020),
        [("vm_table", 0x39)] = (0x988760, 0xB2B750),
        [("vm_table", 0x3B)] = (0x988990, 0xB2B980),
        [("vm_table", 0x3C)] = (0x98C4D0, 0xB2F3E0),
        [("vm_table", 0x40)] = (0x98AD40, 0xB2DD30),
        [("vm_table", 0x44)] = (0x9881C0, 0xB2B1B0),
        [("vm_table", 0x51)] = (0x98AB50, 0xB2DB40),
        [("vm_table", 0x52)] = (0x98B5D0, 0xB2E5C0),
        [("vm_table", 0x58)] = (0x986EC0, 0xB29D70),
        [("vm_table", 0x59)] = (0x986F30, 0xB29DE0),
        [("vm_table", 0x5E)] = (0x989880, 0xB2C870),
        [("vm_table", 0x61)] = (0x989CD0, 0xB2CCC0),
        [("vm_table", 0x62)] = (0x989CD0, 0xB2CCC0),
        [("vm_table", 0x64)] = (0x9880F0, 0xB2B0E0),
        [("vm_table", 0x67)] = (0x98B960, 0xB2E950),
        [("vm_table", 0x68)] = (0x98BCC0, 0xB2ECB0),
        [("vm_table", 0x74)] = (0x989370, 0xB2C360),
        [("vm_table", 0x7D)] = (0x988EA0, 0xB2BE90),
        [("vm_table", 0x8B)] = (0x98A530, 0xB2D520),
        [("vm_table", 0xAC)] = (0x98A350, 0xB2D340),
        [("vm_table", 0xAD)] = (0x98C370, 0x98C370),
        [("vm_table", 0xB1)] = (0x988160, 0xB2B150),
        [("vm_table", 0xD0)] = (0x98AC10, 0xB2DC00),
        [("vm_table", 0xD3)] = (0x98AE40, 0xB2DE30),
        [("vm_table", 0x10C)] = (0x989110, 0xB2C100),
        [("vm_table", 0x10D)] = (0x9891E0, 0xB2C1D0),
        [("vm_table", 0x116)] = (0x98BB20, 0xB2EB10),
        [("bone_table", 0x0)] = (0x9831B0, 0xB262E0),
        [("bone_table", 0x1)] = (0x9831B0, 0xB262E0),
        [("draw_table", 0x2)] = (0x984D30, 0xB27050),
        [("draw_table", 0x6)] = (0x987070, 0xB29F20),
        [("draw_table", 0x25)] = (0x9837C0, 0xB1A130),
        [("prim_table", 0x7)] = (0x985C60, 0xB27E90),
        [("prim_table", 0x11)] = (0x985F70, 0xB28B00),
        [("prim_table", 0x12)] = (0x986240, 0xB28DD0),
        [("pop_table", 0x0)] = (0x987610, 0xB2A4C0),
        [("pop_table", 0x1)] = (0x987640, 0xB2A4F0),
        [("pop_table", 0x4)] = (0x9876C0, 0xB2A570),
        [("pop_table", 0x5)] = (0x987780, 0xB2A630),
        [("pop_table", 0x7)] = (0x987970, 0xB2A820),
        [("pop_table", 0x13)] = (0x987C00, 0xB2AAB0),
        [("ptype_table", 0x1)] = (0x987430, 0xB2A2E0),
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<uint, uint> Effect255Aliases = Effect255Slots.Values.Where(pair => pair.Handler != pair.Canonical).Distinct().ToFrozenDictionary(pair => pair.Handler, pair => pair.Canonical);

    private static uint RequireEffect255Slot(string table, int index, uint handler)
    {
        if (!Effect255Slots.TryGetValue((table, index), out var selected) || handler != selected.Handler)
        {
            throw new NotSupportedException($"Native cinematic effect 255 table {table} slot {index:X} requires its selected handler contract.");
        }
        return selected.Canonical;
    }
}
