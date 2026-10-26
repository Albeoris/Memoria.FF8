using System.Collections.Frozen;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleCinematicDispatch
{
    private static readonly FrozenDictionary<(string Table, int Index), (uint Handler, uint Canonical)> Effect247Slots = new Dictionary<(string Table, int Index), (uint Handler, uint Canonical)>
    {
        [("vm_table", 0x0)] = (0x9D2ED0, 0xB2FEB0),
        [("vm_table", 0x1)] = (0x9D2B40, 0xB2FB20),
        [("vm_table", 0x2)] = (0x9CE080, 0xB2AE30),
        [("vm_table", 0x3)] = (0x9CE0A0, 0xB2AE50),
        [("vm_table", 0x4)] = (0x9CE100, 0xB2AEB0),
        [("vm_table", 0x5)] = (0x9CF280, 0xB2C030),
        [("vm_table", 0x6)] = (0x9CEC60, 0xB2BA10),
        [("vm_table", 0x8)] = (0x9CEE20, 0xB2BBD0),
        [("vm_table", 0x9)] = (0x9D2EA0, 0xB2FE80),
        [("vm_table", 0xA)] = (0x9CF6D0, 0xB2C480),
        [("vm_table", 0xC)] = (0x9CF6D0, 0xB2C480),
        [("vm_table", 0xD)] = (0x9CF6D0, 0xB2C480),
        [("vm_table", 0xE)] = (0x9CF6D0, 0xB2C480),
        [("vm_table", 0x10)] = (0x9CF6D0, 0xB2C480),
        [("vm_table", 0x11)] = (0x9CF6D0, 0xB2C480),
        [("vm_table", 0x12)] = (0x9CF6D0, 0xB2C480),
        [("vm_table", 0x15)] = (0x9CF6D0, 0xB2C480),
        [("vm_table", 0x16)] = (0x9CF6D0, 0xB2C480),
        [("vm_table", 0x1E)] = (0x9CE150, 0xB2AF00),
        [("vm_table", 0x1F)] = (0x9CE1D0, 0xB2AF80),
        [("vm_table", 0x21)] = (0x9CE6A0, 0xB2B450),
        [("vm_table", 0x23)] = (0x9D11F0, 0xB2DFA0),
        [("vm_table", 0x26)] = (0x9D0710, 0xB2D4C0),
        [("vm_table", 0x27)] = (0x9D1790, 0xB2E540),
        [("vm_table", 0x29)] = (0x9D1690, 0xB2E440),
        [("vm_table", 0x2A)] = (0x9D07E0, 0xB2D590),
        [("vm_table", 0x2B)] = (0x9C9CC0, 0xB25BB0),
        [("vm_table", 0x2F)] = (0x9CFB20, 0xB2C8D0),
        [("vm_table", 0x32)] = (0x9D0860, 0xB2D610),
        [("vm_table", 0x33)] = (0x9CE270, 0xB2B020),
        [("vm_table", 0x39)] = (0x9CE9A0, 0xB2B750),
        [("vm_table", 0x3C)] = (0x9D2630, 0xB2F3E0),
        [("vm_table", 0x40)] = (0x9D0F80, 0xB2DD30),
        [("vm_table", 0x45)] = (0x9CEED0, 0xB2BC80),
        [("vm_table", 0x48)] = (0x9CFC30, 0xB2C9E0),
        [("vm_table", 0x4A)] = (0x9D2710, 0xB2F4C0),
        [("vm_table", 0x4D)] = (0x9CAA80, 0xB26DF0),
        [("vm_table", 0x52)] = (0x9D1810, 0xB2E5C0),
        [("vm_table", 0x58)] = (0x9CD100, 0xB29D70),
        [("vm_table", 0x59)] = (0x9CD170, 0xB29DE0),
        [("vm_table", 0x5A)] = (0x9D0B20, 0xB2D8D0),
        [("vm_table", 0x5E)] = (0x9CFAC0, 0xB2C870),
        [("vm_table", 0x61)] = (0x9CFF10, 0xB2CCC0),
        [("vm_table", 0x64)] = (0x9CE330, 0xB2B0E0),
        [("vm_table", 0x67)] = (0x9D1BA0, 0xB2E950),
        [("vm_table", 0x68)] = (0x9D1F00, 0xB2ECB0),
        [("vm_table", 0x74)] = (0x9CF5B0, 0xB2C360),
        [("vm_table", 0x7D)] = (0x9CF0E0, 0xB2BE90),
        [("vm_table", 0x8B)] = (0x9D0770, 0xB2D520),
        [("vm_table", 0xA1)] = (0x9CE2D0, 0xB2B080),
        [("vm_table", 0xB1)] = (0x9CE3A0, 0xB2B150),
        [("vm_table", 0xD4)] = (0x9D1020, 0xB2DDD0),
        [("vm_table", 0xE7)] = (0x9CF4A0, 0xB2C250),
        [("vm_table", 0x10C)] = (0x9CF350, 0xB2C100),
        [("bone_table", 0x0)] = (0x9CA3F0, 0xB262E0),
        [("bone_table", 0x1)] = (0x9CA3F0, 0xB262E0),
        [("bone_table", 0x3)] = (0x9CA500, 0xB263F0),
        [("draw_table", 0x2)] = (0x9CACE0, 0xB27050),
        [("draw_table", 0x4)] = (0x9CAAB0, 0xB26E20),
        [("draw_table", 0x6)] = (0x9CD2B0, 0xB29F20),
        [("draw_table", 0x1B)] = (0x9CB050, 0xB272D0),
        [("prim_table", 0x8)] = (0x9CBE90, 0xB28110),
        [("prim_table", 0x12)] = (0x9CC480, 0xB28DD0),
        [("pop_table", 0x0)] = (0x9CD850, 0xB2A4C0),
        [("pop_table", 0x1)] = (0x9CD880, 0xB2A4F0),
        [("pop_table", 0x2)] = (0x9CD8A0, 0xB2A510),
        [("pop_table", 0x4)] = (0x9CD900, 0xB2A570),
        [("pop_table", 0x5)] = (0x9CD9C0, 0xB2A630),
        [("pop_table", 0x7)] = (0x9CDBB0, 0xB2A820),
        [("pop_table", 0x10)] = (0x9CDD40, 0xB2A9B0),
        [("pop_table", 0x13)] = (0x9CDE40, 0xB2AAB0),
        [("ptype_table", 0x1)] = (0x9CD670, 0xB2A2E0),
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<uint, uint> Effect247Aliases = Effect247Slots.Values.Where(pair => pair.Handler != pair.Canonical).Distinct().ToFrozenDictionary(pair => pair.Handler, pair => pair.Canonical);

    private static uint RequireEffect247Slot(string table, int index, uint handler)
    {
        if (!Effect247Slots.TryGetValue((table, index), out var selected) || handler != selected.Handler)
        {
            throw new NotSupportedException($"Native cinematic effect 247 table {table} slot {index:X} requires its selected handler contract.");
        }
        return selected.Canonical;
    }
}
