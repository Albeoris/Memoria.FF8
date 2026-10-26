using System.Collections.Frozen;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleCinematicDispatch
{
    private static readonly FrozenDictionary<(string Table, int Index), (uint Handler, uint Canonical)> Effect243Slots = new Dictionary<(string Table, int Index), (uint Handler, uint Canonical)>
    {
        [("vm_table", 0x0)] = (0x9F78D0, 0xB2FEB0),
        [("vm_table", 0x1)] = (0x9F7540, 0xB2FB20),
        [("vm_table", 0x2)] = (0x9F2A80, 0xB2AE30),
        [("vm_table", 0x3)] = (0x9F2AA0, 0xB2AE50),
        [("vm_table", 0x4)] = (0x9F2B00, 0xB2AEB0),
        [("vm_table", 0x5)] = (0x9F3C80, 0xB2C030),
        [("vm_table", 0x9)] = (0x9F78A0, 0xB2FE80),
        [("vm_table", 0xA)] = (0x9F40D0, 0xB2C480),
        [("vm_table", 0xC)] = (0x9F40D0, 0xB2C480),
        [("vm_table", 0x10)] = (0x9F40D0, 0xB2C480),
        [("vm_table", 0x11)] = (0x9F40D0, 0xB2C480),
        [("vm_table", 0x12)] = (0x9F40D0, 0xB2C480),
        [("vm_table", 0x15)] = (0x9F40D0, 0xB2C480),
        [("vm_table", 0x1E)] = (0x9F2B50, 0xB2AF00),
        [("vm_table", 0x1F)] = (0x9F2BD0, 0xB2AF80),
        [("vm_table", 0x21)] = (0x9F30A0, 0xB2B450),
        [("vm_table", 0x23)] = (0x9F5BF0, 0xB2DFA0),
        [("vm_table", 0x24)] = (0x9F5740, 0xB2DAF0),
        [("vm_table", 0x26)] = (0x9F5110, 0xB2D4C0),
        [("vm_table", 0x27)] = (0x9F6190, 0xB2E540),
        [("vm_table", 0x2B)] = (0x9EEEC0, 0xB25BB0),
        [("vm_table", 0x32)] = (0x9F5260, 0xB2D610),
        [("vm_table", 0x39)] = (0x9F33A0, 0xB2B750),
        [("vm_table", 0x3B)] = (0x9F35D0, 0xB2B980),
        [("vm_table", 0x4A)] = (0x9F7110, 0xB2F4C0),
        [("vm_table", 0x52)] = (0x9F6210, 0xB2E5C0),
        [("vm_table", 0x58)] = (0x9F1B00, 0xB29D70),
        [("vm_table", 0x59)] = (0x9F1B70, 0xB29DE0),
        [("vm_table", 0x60)] = (0x9F4A50, 0xB2CE00),
        [("vm_table", 0x64)] = (0x9F2D30, 0xB2B0E0),
        [("vm_table", 0x74)] = (0x9F3FB0, 0xB2C360),
        [("vm_table", 0x7D)] = (0x9F3AE0, 0xB2BE90),
        [("vm_table", 0x8D)] = (0x9F3BF0, 0x9F3BF0),
        [("vm_table", 0xA1)] = (0x9F2CD0, 0xB2B080),
        [("vm_table", 0xB1)] = (0x9F2DA0, 0xB2B150),
        [("vm_table", 0xD4)] = (0x9F5A20, 0xB2DDD0),
        [("vm_table", 0xE7)] = (0x9F3EA0, 0xB2C250),
        [("bone_table", 0x0)] = (0x9EF5F0, 0xB262E0),
        [("bone_table", 0x1)] = (0x9EF5F0, 0xB262E0),
        [("draw_table", 0x6)] = (0x9F1CB0, 0xB29F20),
        [("draw_table", 0x1B)] = (0x9F0250, 0xB272D0),
        [("prim_table", 0x8)] = (0x9F0E20, 0xB28110),
        [("pop_table", 0x0)] = (0x9F2250, 0xB2A4C0),
        [("pop_table", 0x1)] = (0x9F2280, 0xB2A4F0),
        [("pop_table", 0x2)] = (0x9F22A0, 0xB2A510),
        [("pop_table", 0x4)] = (0x9F2300, 0xB2A570),
        [("pop_table", 0x5)] = (0x9F23C0, 0xB2A630),
        [("pop_table", 0x7)] = (0x9F25B0, 0xB2A820),
        [("pop_table", 0x10)] = (0x9F2740, 0xB2A9B0),
        [("ptype_table", 0x1)] = (0x9F2070, 0xB2A2E0),
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<uint, uint> Effect243Aliases = Effect243Slots.Values.Distinct().ToFrozenDictionary(pair => pair.Handler, pair => pair.Canonical);

    private static uint RequireEffect243Slot(string table, int index, uint handler)
    {
        if (!Effect243Slots.TryGetValue((table, index), out var selected) || handler != selected.Handler)
        {
            throw new NotSupportedException($"Native cinematic effect 243 table {table} slot {index:X} requires its selected handler contract.");
        }
        return selected.Canonical;
    }
}
