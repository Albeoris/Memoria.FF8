using System.Collections.Frozen;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleCinematicDispatch
{
    private static readonly FrozenDictionary<(string Table, int Index), (uint Handler, uint Canonical)> Effect240Slots = new Dictionary<(string Table, int Index), (uint Handler, uint Canonical)>
    {
        [("vm_table", 0x0)] = (0xA155F0, 0xB2FEB0),
        [("vm_table", 0x1)] = (0xA15260, 0xB2FB20),
        [("vm_table", 0x2)] = (0xA107A0, 0xB2AE30),
        [("vm_table", 0x5)] = (0xA119A0, 0xB2C030),
        [("vm_table", 0x9)] = (0xA155C0, 0xB2FE80),
        [("vm_table", 0xA)] = (0xA11DF0, 0xB2C480),
        [("vm_table", 0xD)] = (0xA11DF0, 0xB2C480),
        [("vm_table", 0xE)] = (0xA11DF0, 0xB2C480),
        [("vm_table", 0xF)] = (0xA11DF0, 0xB2C480),
        [("vm_table", 0x10)] = (0xA11DF0, 0xB2C480),
        [("vm_table", 0x11)] = (0xA11DF0, 0xB2C480),
        [("vm_table", 0x12)] = (0xA11DF0, 0xB2C480),
        [("vm_table", 0x15)] = (0xA11DF0, 0xB2C480),
        [("vm_table", 0x1E)] = (0xA10870, 0xB2AF00),
        [("vm_table", 0x1F)] = (0xA108F0, 0xB2AF80),
        [("vm_table", 0x21)] = (0xA10DC0, 0xB2B450),
        [("vm_table", 0x22)] = (0xA10EA0, 0xB2B530),
        [("vm_table", 0x23)] = (0xA13910, 0xB2DFA0),
        [("vm_table", 0x24)] = (0xA13460, 0xB2DAF0),
        [("vm_table", 0x27)] = (0xA13EB0, 0xB2E540),
        [("vm_table", 0x2B)] = (0xA0B790, 0xB25BB0),
        [("vm_table", 0x2F)] = (0xA12240, 0xB2C8D0),
        [("vm_table", 0x32)] = (0xA12F80, 0xB2D610),
        [("vm_table", 0x33)] = (0xA10990, 0xB2B020),
        [("vm_table", 0x39)] = (0xA110C0, 0xB2B750),
        [("vm_table", 0x3B)] = (0xA112F0, 0xB2B980),
        [("vm_table", 0x3C)] = (0xA14D50, 0xB2F3E0),
        [("vm_table", 0x40)] = (0xA136A0, 0xB2DD30),
        [("vm_table", 0x44)] = (0xA10B20, 0xB2B1B0),
        [("vm_table", 0x47)] = (0xA11340, 0xB2B9D0),
        [("vm_table", 0x4A)] = (0xA14E30, 0xB2F4C0),
        [("vm_table", 0x52)] = (0xA13F30, 0xB2E5C0),
        [("vm_table", 0x58)] = (0xA0F820, 0xB29D70),
        [("vm_table", 0x59)] = (0xA0F890, 0xB29DE0),
        [("vm_table", 0x5E)] = (0xA121E0, 0xB2C870),
        [("vm_table", 0x64)] = (0xA10A50, 0xB2B0E0),
        [("vm_table", 0x67)] = (0xA142C0, 0xB2E950),
        [("vm_table", 0x68)] = (0xA14620, 0xB2ECB0),
        [("vm_table", 0x6E)] = (0xA128E0, 0xB2CF70),
        [("vm_table", 0x72)] = (0xA12190, 0xB2C820),
        [("vm_table", 0x74)] = (0xA11CD0, 0xB2C360),
        [("vm_table", 0x7D)] = (0xA11800, 0xB2BE90),
        [("vm_table", 0x8B)] = (0xA12E90, 0xB2D520),
        [("vm_table", 0xB1)] = (0xA10AC0, 0xB2B150),
        [("pop_table", 0x0)] = (0xA0FF70, 0xB2A4C0),
        [("pop_table", 0x1)] = (0xA0FFA0, 0xB2A4F0),
        [("pop_table", 0x2)] = (0xA0FFC0, 0xB2A510),
        [("pop_table", 0x4)] = (0xA10020, 0xB2A570),
        [("pop_table", 0x5)] = (0xA100E0, 0xB2A630),
        [("pop_table", 0x7)] = (0xA102D0, 0xB2A820),
        [("pop_table", 0x9)] = (0xA10200, 0xB2A750),
        [("pop_table", 0xA)] = (0xA10270, 0xB2A7C0),
        [("pop_table", 0x10)] = (0xA10460, 0xB2A9B0),
        [("bone_table", 0x0)] = (0xA0BEC0, 0xB262E0),
        [("bone_table", 0x1)] = (0xA0BEC0, 0xB262E0),
        [("draw_table", 0x2)] = (0xA0CB20, 0xB27050),
        [("draw_table", 0x6)] = (0xA0F9D0, 0xB29F20),
        [("draw_table", 0x1B)] = (0xA0CDA0, 0xB272D0),
        [("draw_table", 0x22)] = (0xA0E3D0, 0xA0E3D0),
        [("prim_table", 0x8)] = (0xA0D970, 0xB28110),
        [("prim_table", 0x9)] = (0xA0DC10, 0xB283B0),
        [("prim_table", 0x12)] = (0xA0DF50, 0xB28DD0),
        [("ptype_table", 0x1)] = (0xA0FD90, 0xB2A2E0),
        [("ptype_table", 0x2)] = (0xA0FDE0, 0xB2A330),
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<uint, uint> Effect240Aliases = Effect240Slots.Values.Where(pair => pair.Handler != pair.Canonical).Distinct().ToFrozenDictionary(pair => pair.Handler, pair => pair.Canonical);

    private static uint RequireEffect240Slot(string table, int index, uint handler)
    {
        if (!Effect240Slots.TryGetValue((table, index), out var selected) || handler != selected.Handler)
        {
            throw new NotSupportedException($"Native cinematic effect 240 table {table} slot {index:X} requires its selected handler contract.");
        }
        return selected.Canonical;
    }
}
