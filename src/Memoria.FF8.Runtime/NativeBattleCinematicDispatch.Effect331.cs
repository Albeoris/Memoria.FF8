using System.Collections.Frozen;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleCinematicDispatch
{
    private static readonly FrozenDictionary<(string Table, int Index), (uint Handler, uint Canonical)> Effect331Slots = new Dictionary<(string Table, int Index), (uint Handler, uint Canonical)>
    {
        [("bone_table", 0x0)] = (0x8E0B00, 0xB262E0),
        [("bone_table", 0x1)] = (0x8E0B00, 0xB262E0),
        [("bone_table", 0x3)] = (0x8E0C10, 0xB263F0),
        [("draw_table", 0x5)] = (0x8E3480, 0xB298D0),
        [("draw_table", 0x6)] = (0x8E3AD0, 0xB29F20),
        [("draw_table", 0x1B)] = (0x8E1DC0, 0xB272D0),
        [("draw_table", 0x26)] = (0x8E0FD0, 0xAF59D0),
        [("pop_table", 0x0)] = (0x8E4070, 0xB2A4C0),
        [("pop_table", 0x1)] = (0x8E40A0, 0xB2A4F0),
        [("pop_table", 0x2)] = (0x8E40C0, 0xB2A510),
        [("pop_table", 0x4)] = (0x8E4120, 0xB2A570),
        [("pop_table", 0x5)] = (0x8E41E0, 0xB2A630),
        [("pop_table", 0x7)] = (0x8E43D0, 0xB2A820),
        [("pop_table", 0x10)] = (0x8E4560, 0xB2A9B0),
        [("prim_table", 0x8)] = (0x8E2990, 0xB28110),
        [("prim_table", 0x9)] = (0x8E2C30, 0xB283B0),
        [("ptype_table", 0x1)] = (0x8E3E90, 0xB2A2E0),
        [("spr_table", 0x9)] = (0x8E33E0, 0xB29830),
        [("vm_table", 0x0)] = (0x8E97F0, 0xB2FEB0),
        [("vm_table", 0x1)] = (0x8E9460, 0xB2FB20),
        [("vm_table", 0x2)] = (0x8E48A0, 0xB2AE30),
        [("vm_table", 0x5)] = (0x8E5AC0, 0xB2C030),
        [("vm_table", 0x9)] = (0x8E97C0, 0xB2FE80),
        [("vm_table", 0xA)] = (0x8E5F10, 0xB2C480),
        [("vm_table", 0xB)] = (0x8E5F10, 0xB2C480),
        [("vm_table", 0xC)] = (0x8E5F10, 0xB2C480),
        [("vm_table", 0xD)] = (0x8E5F10, 0xB2C480),
        [("vm_table", 0x10)] = (0x8E5F10, 0xB2C480),
        [("vm_table", 0x11)] = (0x8E5F10, 0xB2C480),
        [("vm_table", 0x12)] = (0x8E5F10, 0xB2C480),
        [("vm_table", 0x15)] = (0x8E5F10, 0xB2C480),
        [("vm_table", 0x16)] = (0x8E5F10, 0xB2C480),
        [("vm_table", 0x1E)] = (0x8E4970, 0xB2AF00),
        [("vm_table", 0x1F)] = (0x8E49F0, 0xB2AF80),
        [("vm_table", 0x21)] = (0x8E4EE0, 0xB2B450),
        [("vm_table", 0x22)] = (0x8E4FC0, 0xB2B530),
        [("vm_table", 0x23)] = (0x8E7A30, 0xB2DFA0),
        [("vm_table", 0x24)] = (0x8E7580, 0xB2DAF0),
        [("vm_table", 0x25)] = (0x8E7530, 0xB2DAA0),
        [("vm_table", 0x26)] = (0x8E6F50, 0xB2D4C0),
        [("vm_table", 0x27)] = (0x8E7FD0, 0xB2E540),
        [("vm_table", 0x2A)] = (0x8E7020, 0xB2D590),
        [("vm_table", 0x2B)] = (0x8E03D0, 0xB25BB0),
        [("vm_table", 0x2F)] = (0x8E6360, 0xB2C8D0),
        [("vm_table", 0x32)] = (0x8E70A0, 0xB2D610),
        [("vm_table", 0x33)] = (0x8E4A90, 0xB2B020),
        [("vm_table", 0x37)] = (0x8E5E90, 0xB2C400),
        [("vm_table", 0x39)] = (0x8E51E0, 0xB2B750),
        [("vm_table", 0x44)] = (0x8E4C40, 0xB2B1B0),
        [("vm_table", 0x45)] = (0x8E5710, 0xB2BC80),
        [("vm_table", 0x47)] = (0x8E5460, 0xB2B9D0),
        [("vm_table", 0x4A)] = (0x8E9030, 0xB2F4C0),
        [("vm_table", 0x52)] = (0x8E8050, 0xB2E5C0),
        [("vm_table", 0x58)] = (0x8E3920, 0xB29D70),
        [("vm_table", 0x59)] = (0x8E3990, 0xB29DE0),
        [("vm_table", 0x5A)] = (0x8E7360, 0xB2D8D0),
        [("vm_table", 0x5D)] = (0x8E7280, 0xB2D7F0),
        [("vm_table", 0x5E)] = (0x8E6300, 0xB2C870),
        [("vm_table", 0x60)] = (0x8E6890, 0xB2CE00),
        [("vm_table", 0x64)] = (0x8E4B70, 0xB2B0E0),
        [("vm_table", 0x74)] = (0x8E5DF0, 0xB2C360),
        [("vm_table", 0x7D)] = (0x8E5920, 0xB2BE90),
        [("vm_table", 0x8B)] = (0x8E6FB0, 0xB2D520),
        [("vm_table", 0xAD)] = (0x8E8DF0, 0xA8EA50),
        [("vm_table", 0xB1)] = (0x8E4BE0, 0xB2B150),
        [("vm_table", 0xB5)] = (0x8E4B30, 0x8E4B30),
        [("vm_table", 0xD4)] = (0x8E7860, 0xB2DDD0),
        [("vm_table", 0xE7)] = (0x8E5CE0, 0xB2C250),
        [("vm_table", 0xF2)] = (0x8E6660, 0xB2CBD0),
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<uint, uint> Effect331Aliases = Effect331Slots.Values.Distinct().ToFrozenDictionary(pair => pair.Handler, pair => pair.Canonical);

    private static uint RequireEffect331Slot(string table, int index, uint handler)
    {
        if (!Effect331Slots.TryGetValue((table, index), out var selected) || handler != selected.Handler)
        {
            throw new NotSupportedException($"Native cinematic effect 331 table {table} slot {index:X} requires its selected handler contract.");
        }
        return selected.Canonical;
    }
}
