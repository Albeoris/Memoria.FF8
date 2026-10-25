using System.Collections.Frozen;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleCinematicDispatch
{
    private static readonly FrozenDictionary<(string Table, int Index), (uint Handler, uint Canonical)> Effect253Slots = new Dictionary<(string Table, int Index), (uint Handler, uint Canonical)>
    {
        [("vm_table", 0x0)] = (0x9A2430, 0xB2FEB0),
        [("vm_table", 0x1)] = (0x9A20A0, 0xB2FB20),
        [("vm_table", 0x2)] = (0x99D500, 0xB2AE30),
        [("vm_table", 0x3)] = (0x99D520, 0xB2AE50),
        [("vm_table", 0x4)] = (0x99D580, 0xB2AEB0),
        [("vm_table", 0x5)] = (0x99E700, 0xB2C030),
        [("vm_table", 0x9)] = (0x9A2400, 0xB2FE80),
        [("vm_table", 0xA)] = (0x99EB50, 0xB2C480),
        [("vm_table", 0xB)] = (0x99EB50, 0xB2C480),
        [("vm_table", 0xD)] = (0x99EB50, 0xB2C480),
        [("vm_table", 0xE)] = (0x99EB50, 0xB2C480),
        [("vm_table", 0x10)] = (0x99EB50, 0xB2C480),
        [("vm_table", 0x11)] = (0x99EB50, 0xB2C480),
        [("vm_table", 0x12)] = (0x99EB50, 0xB2C480),
        [("vm_table", 0x20)] = (0x9A09C0, 0xB2E2F0),
        [("vm_table", 0x21)] = (0x99DB20, 0xB2B450),
        [("vm_table", 0x22)] = (0x99DC00, 0xB2B530),
        [("vm_table", 0x23)] = (0x9A0670, 0xB2DFA0),
        [("vm_table", 0x24)] = (0x9A01C0, 0xB2DAF0),
        [("vm_table", 0x26)] = (0x99FB90, 0xB2D4C0),
        [("vm_table", 0x27)] = (0x9A0C10, 0xB2E540),
        [("vm_table", 0x2B)] = (0x998140, 0xB25BB0),
        [("vm_table", 0x2F)] = (0x99EFA0, 0xB2C8D0),
        [("vm_table", 0x32)] = (0x99FCE0, 0xB2D610),
        [("vm_table", 0x33)] = (0x99D6F0, 0xB2B020),
        [("vm_table", 0x39)] = (0x99DE20, 0xB2B750),
        [("vm_table", 0x3B)] = (0x99E050, 0xB2B980),
        [("vm_table", 0x3C)] = (0x9A1B90, 0xB2F3E0),
        [("vm_table", 0x40)] = (0x9A0400, 0xB2DD30),
        [("vm_table", 0x51)] = (0x9A0210, 0xB2DB40),
        [("vm_table", 0x52)] = (0x9A0C90, 0xB2E5C0),
        [("vm_table", 0x5E)] = (0x99EF40, 0xB2C870),
        [("vm_table", 0x62)] = (0x99F390, 0xB2CCC0),
        [("vm_table", 0x64)] = (0x99D7B0, 0xB2B0E0),
        [("vm_table", 0x68)] = (0x9A1380, 0xB2ECB0),
        [("vm_table", 0x74)] = (0x99EA30, 0xB2C360),
        [("vm_table", 0x7D)] = (0x99E560, 0xB2BE90),
        [("vm_table", 0x8B)] = (0x99FBF0, 0xB2D520),
        [("vm_table", 0xAC)] = (0x99FA10, 0xB2D340),
        [("vm_table", 0xAD)] = (0x9A1A30, 0x9A1A30),
        [("vm_table", 0xB1)] = (0x99D820, 0xB2B150),
        [("vm_table", 0xD3)] = (0x9A0500, 0xB2DE30),
        [("vm_table", 0x10D)] = (0x99E8A0, 0xB2C1D0),
        [("vm_table", 0x116)] = (0x9A11E0, 0xB2EB10),
        [("vm_table", 0xC)] = (0x99EB50, 0xB2C480),
        [("vm_table", 0x44)] = (0x99D880, 0xB2B1B0),
        [("vm_table", 0x61)] = (0x99F390, 0xB2CCC0),
        [("vm_table", 0xD0)] = (0x9A02D0, 0xB2DC00),
        [("vm_table", 0x10C)] = (0x99E7D0, 0xB2C100),
        [("vm_table", 0x2A)] = (0x99FC60, 0xB2D590),
        [("vm_table", 0x58)] = (0x99C580, 0xB29D70),
        [("vm_table", 0x59)] = (0x99C5F0, 0xB29DE0),
        [("vm_table", 0x67)] = (0x9A1020, 0xB2E950),
        [("bone_table", 0x0)] = (0x998870, 0xB262E0),
        [("bone_table", 0x1)] = (0x998870, 0xB262E0),
        [("draw_table", 0x2)] = (0x99A3F0, 0xB27050),
        [("draw_table", 0x6)] = (0x99C730, 0xB29F20),
        [("draw_table", 0x25)] = (0x998E80, 0xB1A130),
        [("prim_table", 0x7)] = (0x99B320, 0xB27E90),
        [("prim_table", 0x12)] = (0x99B900, 0xB28DD0),
        [("pop_table", 0x13)] = (0x99D2C0, 0xB2AAB0),
        [("prim_table", 0x11)] = (0x99B630, 0xB28B00),
        [("pop_table", 0x0)] = (0x99CCD0, 0xB2A4C0),
        [("pop_table", 0x1)] = (0x99CD00, 0xB2A4F0),
        [("pop_table", 0x4)] = (0x99CD80, 0xB2A570),
        [("pop_table", 0x5)] = (0x99CE40, 0xB2A630),
        [("pop_table", 0x7)] = (0x99D030, 0xB2A820),
        [("ptype_table", 0x1)] = (0x99CAF0, 0xB2A2E0),
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<uint, uint> Effect253Aliases = Effect253Slots.Values.Where(pair => pair.Handler != pair.Canonical).Distinct().ToFrozenDictionary(pair => pair.Handler, pair => pair.Canonical);

    private static uint RequireEffect253Slot(string table, int index, uint handler)
    {
        if (!Effect253Slots.TryGetValue((table, index), out var selected) || handler != selected.Handler)
        {
            throw new NotSupportedException($"Native cinematic effect 253 table {table} slot {index:X} requires its selected handler contract.");
        }
        return selected.Canonical;
    }
}
