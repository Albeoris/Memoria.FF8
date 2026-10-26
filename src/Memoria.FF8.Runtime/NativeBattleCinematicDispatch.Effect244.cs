using System.Collections.Frozen;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleCinematicDispatch
{
    private static readonly FrozenDictionary<(string Table, int Index), (uint Handler, uint Canonical)> Effect244Slots = new Dictionary<(string Table, int Index), (uint Handler, uint Canonical)>
    {
        [("vm_table", 0x0)] = (0x9EE650, 0xB2FEB0),
        [("vm_table", 0x1)] = (0x9EE2C0, 0xB2FB20),
        [("vm_table", 0x2)] = (0x9E9800, 0xB2AE30),
        [("vm_table", 0x3)] = (0x9E9820, 0xB2AE50),
        [("vm_table", 0x4)] = (0x9E9880, 0xB2AEB0),
        [("vm_table", 0x5)] = (0x9EAA00, 0xB2C030),
        [("vm_table", 0x9)] = (0x9EE620, 0xB2FE80),
        [("vm_table", 0xA)] = (0x9EAE50, 0xB2C480),
        [("vm_table", 0xC)] = (0x9EAE50, 0xB2C480),
        [("vm_table", 0x10)] = (0x9EAE50, 0xB2C480),
        [("vm_table", 0x11)] = (0x9EAE50, 0xB2C480),
        [("vm_table", 0x12)] = (0x9EAE50, 0xB2C480),
        [("vm_table", 0x15)] = (0x9EAE50, 0xB2C480),
        [("vm_table", 0x1E)] = (0x9E98D0, 0xB2AF00),
        [("vm_table", 0x1F)] = (0x9E9950, 0xB2AF80),
        [("vm_table", 0x21)] = (0x9E9E20, 0xB2B450),
        [("vm_table", 0x23)] = (0x9EC970, 0xB2DFA0),
        [("vm_table", 0x24)] = (0x9EC4C0, 0xB2DAF0),
        [("vm_table", 0x26)] = (0x9EBE90, 0xB2D4C0),
        [("vm_table", 0x27)] = (0x9ECF10, 0xB2E540),
        [("vm_table", 0x2B)] = (0x9E5C40, 0xB25BB0),
        [("vm_table", 0x32)] = (0x9EBFE0, 0xB2D610),
        [("vm_table", 0x39)] = (0x9EA120, 0xB2B750),
        [("vm_table", 0x3B)] = (0x9EA350, 0xB2B980),
        [("vm_table", 0x4A)] = (0x9EDE90, 0xB2F4C0),
        [("vm_table", 0x52)] = (0x9ECF90, 0xB2E5C0),
        [("vm_table", 0x58)] = (0x9E8880, 0xB29D70),
        [("vm_table", 0x59)] = (0x9E88F0, 0xB29DE0),
        [("vm_table", 0x60)] = (0x9EB7D0, 0xB2CE00),
        [("vm_table", 0x64)] = (0x9E9AB0, 0xB2B0E0),
        [("vm_table", 0x74)] = (0x9EAD30, 0xB2C360),
        [("vm_table", 0x7D)] = (0x9EA860, 0xB2BE90),
        [("vm_table", 0x8D)] = (0x9EA970, 0x9F3BF0),
        [("vm_table", 0xA1)] = (0x9E9A50, 0xB2B080),
        [("vm_table", 0xB1)] = (0x9E9B20, 0xB2B150),
        [("vm_table", 0xD4)] = (0x9EC7A0, 0xB2DDD0),
        [("vm_table", 0xE7)] = (0x9EAC20, 0xB2C250),
        [("bone_table", 0x0)] = (0x9E6370, 0xB262E0),
        [("bone_table", 0x1)] = (0x9E6370, 0xB262E0),
        [("draw_table", 0x6)] = (0x9E8A30, 0xB29F20),
        [("draw_table", 0x1B)] = (0x9E6FD0, 0xB272D0),
        [("prim_table", 0x8)] = (0x9E7BA0, 0xB28110),
        [("pop_table", 0x0)] = (0x9E8FD0, 0xB2A4C0),
        [("pop_table", 0x1)] = (0x9E9000, 0xB2A4F0),
        [("pop_table", 0x2)] = (0x9E9020, 0xB2A510),
        [("pop_table", 0x4)] = (0x9E9080, 0xB2A570),
        [("pop_table", 0x5)] = (0x9E9140, 0xB2A630),
        [("pop_table", 0x7)] = (0x9E9330, 0xB2A820),
        [("pop_table", 0x10)] = (0x9E94C0, 0xB2A9B0),
        [("ptype_table", 0x1)] = (0x9E8DF0, 0xB2A2E0),
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<uint, uint> Effect244Aliases = Effect244Slots.Values.Where(pair => pair.Handler != pair.Canonical).Distinct().ToFrozenDictionary(pair => pair.Handler, pair => pair.Canonical);

    private static uint RequireEffect244Slot(string table, int index, uint handler)
    {
        if (!Effect244Slots.TryGetValue((table, index), out var selected) || handler != selected.Handler)
        {
            throw new NotSupportedException($"Native cinematic effect 244 table {table} slot {index:X} requires its selected handler contract.");
        }
        return selected.Canonical;
    }
}
