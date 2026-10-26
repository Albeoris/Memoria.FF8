using System.Collections.Frozen;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleCinematicDispatch
{
    private static readonly FrozenDictionary<(string Table, int Index), (uint Handler, uint Canonical)> Effect246Slots = new Dictionary<(string Table, int Index), (uint Handler, uint Canonical)>
    {
        [("vm_table", 0x0)] = (0x9DC150, 0xB2FEB0),
        [("vm_table", 0x1)] = (0x9DBDC0, 0xB2FB20),
        [("vm_table", 0x2)] = (0x9D7300, 0xB2AE30),
        [("vm_table", 0x3)] = (0x9D7320, 0xB2AE50),
        [("vm_table", 0x4)] = (0x9D7380, 0xB2AEB0),
        [("vm_table", 0x5)] = (0x9D8500, 0xB2C030),
        [("vm_table", 0x9)] = (0x9DC120, 0xB2FE80),
        [("vm_table", 0xA)] = (0x9D8950, 0xB2C480),
        [("vm_table", 0xC)] = (0x9D8950, 0xB2C480),
        [("vm_table", 0x10)] = (0x9D8950, 0xB2C480),
        [("vm_table", 0x11)] = (0x9D8950, 0xB2C480),
        [("vm_table", 0x12)] = (0x9D8950, 0xB2C480),
        [("vm_table", 0x15)] = (0x9D8950, 0xB2C480),
        [("vm_table", 0x1E)] = (0x9D73D0, 0xB2AF00),
        [("vm_table", 0x1F)] = (0x9D7450, 0xB2AF80),
        [("vm_table", 0x21)] = (0x9D7920, 0xB2B450),
        [("vm_table", 0x23)] = (0x9DA470, 0xB2DFA0),
        [("vm_table", 0x24)] = (0x9D9FC0, 0xB2DAF0),
        [("vm_table", 0x26)] = (0x9D9990, 0xB2D4C0),
        [("vm_table", 0x27)] = (0x9DAA10, 0xB2E540),
        [("vm_table", 0x2B)] = (0x9D3740, 0xB25BB0),
        [("vm_table", 0x32)] = (0x9D9AE0, 0xB2D610),
        [("vm_table", 0x39)] = (0x9D7C20, 0xB2B750),
        [("vm_table", 0x3B)] = (0x9D7E50, 0xB2B980),
        [("vm_table", 0x4A)] = (0x9DB990, 0xB2F4C0),
        [("vm_table", 0x52)] = (0x9DAA90, 0xB2E5C0),
        [("vm_table", 0x58)] = (0x9D6380, 0xB29D70),
        [("vm_table", 0x59)] = (0x9D63F0, 0xB29DE0),
        [("vm_table", 0x60)] = (0x9D92D0, 0xB2CE00),
        [("vm_table", 0x64)] = (0x9D75B0, 0xB2B0E0),
        [("vm_table", 0x74)] = (0x9D8830, 0xB2C360),
        [("vm_table", 0x7D)] = (0x9D8360, 0xB2BE90),
        [("vm_table", 0x8D)] = (0x9D8470, 0x9F3BF0),
        [("vm_table", 0xA1)] = (0x9D7550, 0xB2B080),
        [("vm_table", 0xB1)] = (0x9D7620, 0xB2B150),
        [("vm_table", 0xD4)] = (0x9DA2A0, 0xB2DDD0),
        [("vm_table", 0xE7)] = (0x9D8720, 0xB2C250),
        [("bone_table", 0x0)] = (0x9D3E70, 0xB262E0),
        [("bone_table", 0x1)] = (0x9D3E70, 0xB262E0),
        [("draw_table", 0x6)] = (0x9D6530, 0xB29F20),
        [("draw_table", 0x1B)] = (0x9D4AD0, 0xB272D0),
        [("prim_table", 0x8)] = (0x9D56A0, 0xB28110),
        [("pop_table", 0x0)] = (0x9D6AD0, 0xB2A4C0),
        [("pop_table", 0x1)] = (0x9D6B00, 0xB2A4F0),
        [("pop_table", 0x2)] = (0x9D6B20, 0xB2A510),
        [("pop_table", 0x4)] = (0x9D6B80, 0xB2A570),
        [("pop_table", 0x5)] = (0x9D6C40, 0xB2A630),
        [("pop_table", 0x7)] = (0x9D6E30, 0xB2A820),
        [("pop_table", 0x10)] = (0x9D6FC0, 0xB2A9B0),
        [("ptype_table", 0x1)] = (0x9D68F0, 0xB2A2E0),
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<uint, uint> Effect246Aliases = Effect246Slots.Values.Where(pair => pair.Handler != pair.Canonical).Distinct().ToFrozenDictionary(pair => pair.Handler, pair => pair.Canonical);

    private static uint RequireEffect246Slot(string table, int index, uint handler)
    {
        if (!Effect246Slots.TryGetValue((table, index), out var selected) || handler != selected.Handler)
        {
            throw new NotSupportedException($"Native cinematic effect 246 table {table} slot {index:X} requires its selected handler contract.");
        }
        return selected.Canonical;
    }
}
