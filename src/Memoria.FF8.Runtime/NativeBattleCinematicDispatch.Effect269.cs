using System.Collections.Frozen;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleCinematicDispatch
{
    private static readonly FrozenDictionary<(string Table, int Index), (uint Handler, uint Canonical)> Effect269Slots = new Dictionary<(string Table, int Index), (uint Handler, uint Canonical)>
    {
        [("bone_table", 0x0)] = (0x8F7D80, 0xB262E0),
        [("bone_table", 0x1)] = (0x8F7D80, 0xB262E0),
        [("bone_table", 0x3)] = (0x8F7E90, 0xB263F0),
        [("draw_table", 0x2)] = (0x8FBCA0, 0xB27050),
        [("draw_table", 0x4)] = (0x8FBA70, 0xB26E20),
        [("draw_table", 0x5)] = (0x8FE270, 0xB298D0),
        [("draw_table", 0x6)] = (0x8FE8C0, 0xB29F20),
        [("draw_table", 0x16)] = (0x8F88D0, 0xB4BBE0),
        [("pop_table", 0x0)] = (0x8FEE60, 0xB2A4C0),
        [("pop_table", 0x1)] = (0x8FEE90, 0xB2A4F0),
        [("pop_table", 0x4)] = (0x8FEF10, 0xB2A570),
        [("pop_table", 0x5)] = (0x8FEFD0, 0xB2A630),
        [("pop_table", 0x7)] = (0x8FF1C0, 0xB2A820),
        [("pop_table", 0x9)] = (0x8FF0F0, 0xB2A750),
        [("pop_table", 0xA)] = (0x8FF160, 0xB2A7C0),
        [("prim_table", 0x7)] = (0x8FC9F0, 0xB27E90),
        [("prim_table", 0x10)] = (0x8FD230, 0xB105B0),
        [("prim_table", 0x11)] = (0x8FD4A0, 0xB28B00),
        [("ptype_table", 0x1)] = (0x8FEC80, 0xB2A2E0),
        [("ptype_table", 0x2)] = (0x8FECD0, 0xB2A330),
        [("spr_table", 0x9)] = (0x8FE1D0, 0xB29830),
        [("vm_table", 0x0)] = (0x9045C0, 0xB2FEB0),
        [("vm_table", 0x1)] = (0x904230, 0xB2FB20),
        [("vm_table", 0x2)] = (0x8FF690, 0xB2AE30),
        [("vm_table", 0x3)] = (0x8FF6B0, 0xB2AE50),
        [("vm_table", 0x4)] = (0x8FF710, 0xB2AEB0),
        [("vm_table", 0x5)] = (0x9008F0, 0xB2C030),
        [("vm_table", 0x6)] = (0x9002D0, 0xB2BA10),
        [("vm_table", 0x8)] = (0x900490, 0xB2BBD0),
        [("vm_table", 0x9)] = (0x904590, 0xB2FE80),
        [("vm_table", 0xA)] = (0x900D40, 0xB2C480),
        [("vm_table", 0xB)] = (0x900D40, 0xB2C480),
        [("vm_table", 0xD)] = (0x900D40, 0xB2C480),
        [("vm_table", 0xE)] = (0x900D40, 0xB2C480),
        [("vm_table", 0x10)] = (0x900D40, 0xB2C480),
        [("vm_table", 0x11)] = (0x900D40, 0xB2C480),
        [("vm_table", 0x12)] = (0x900D40, 0xB2C480),
        [("vm_table", 0x13)] = (0x900D40, 0xB2C480),
        [("vm_table", 0x1E)] = (0x8FF760, 0xB2AF00),
        [("vm_table", 0x1F)] = (0x8FF7E0, 0xB2AF80),
        [("vm_table", 0x20)] = (0x902BB0, 0xB2E2F0),
        [("vm_table", 0x21)] = (0x8FFD10, 0xB2B450),
        [("vm_table", 0x22)] = (0x8FFDF0, 0xB2B530),
        [("vm_table", 0x23)] = (0x902860, 0xB2DFA0),
        [("vm_table", 0x24)] = (0x9023B0, 0xB2DAF0),
        [("vm_table", 0x25)] = (0x902360, 0xB2DAA0),
        [("vm_table", 0x26)] = (0x901D80, 0xB2D4C0),
        [("vm_table", 0x27)] = (0x902E00, 0xB2E540),
        [("vm_table", 0x29)] = (0x902D00, 0xB2E440),
        [("vm_table", 0x2A)] = (0x901E50, 0xB2D590),
        [("vm_table", 0x2B)] = (0x8F7650, 0xB25BB0),
        [("vm_table", 0x2F)] = (0x901190, 0xB2C8D0),
        [("vm_table", 0x32)] = (0x901ED0, 0xB2D610),
        [("vm_table", 0x33)] = (0x8FF880, 0xB2B020),
        [("vm_table", 0x35)] = (0x902F80, 0xA996A0),
        [("vm_table", 0x37)] = (0x900CC0, 0xB2C400),
        [("vm_table", 0x39)] = (0x900010, 0xB2B750),
        [("vm_table", 0x3B)] = (0x900240, 0xB2B980),
        [("vm_table", 0x3C)] = (0x903D20, 0xB2F3E0),
        [("vm_table", 0x40)] = (0x9025F0, 0xB2DD30),
        [("vm_table", 0x43)] = (0x8FFA80, 0xB2B220),
        [("vm_table", 0x44)] = (0x8FFA10, 0xB2B1B0),
        [("vm_table", 0x45)] = (0x900540, 0xB2BC80),
        [("vm_table", 0x4D)] = (0x8FBA40, 0xB26DF0),
        [("vm_table", 0x52)] = (0x902E80, 0xB2E5C0),
        [("vm_table", 0x53)] = (0x8F77B0, 0xB4AAC0),
        [("vm_table", 0x58)] = (0x8FE710, 0xB29D70),
        [("vm_table", 0x59)] = (0x8FE780, 0xB29DE0),
        [("vm_table", 0x5A)] = (0x902190, 0xB2D8D0),
        [("vm_table", 0x5B)] = (0x9039D0, 0xB2F090),
        [("vm_table", 0x5F)] = (0x901040, 0xB2C780),
        [("vm_table", 0x61)] = (0x901580, 0xB2CCC0),
        [("vm_table", 0x64)] = (0x8FF940, 0xB2B0E0),
        [("vm_table", 0x67)] = (0x903290, 0xB2E950),
        [("vm_table", 0x68)] = (0x9035F0, 0xB2ECB0),
        [("vm_table", 0x7D)] = (0x900750, 0xB2BE90),
        [("vm_table", 0x8B)] = (0x901DE0, 0xB2D520),
        [("vm_table", 0xB1)] = (0x8FF9B0, 0xB2B150),
        [("vm_table", 0xD0)] = (0x9024C0, 0xB2DC00),
        [("vm_table", 0xD3)] = (0x9026F0, 0xB2DE30),
        [("vm_table", 0xE7)] = (0x900B10, 0xB2C250),
        [("vm_table", 0x13D)] = (0x8FFE90, 0xB2B5D0),
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<uint, uint> Effect269Aliases = Effect269Slots.Values.Distinct().ToFrozenDictionary(pair => pair.Handler, pair => pair.Canonical);

    private static uint RequireEffect269Slot(string table, int index, uint handler)
    {
        if (!Effect269Slots.TryGetValue((table, index), out var selected) || handler != selected.Handler)
        {
            throw new NotSupportedException($"Native cinematic effect 269 table {table} slot {index:X} requires its selected handler contract.");
        }
        return selected.Canonical;
    }
}
