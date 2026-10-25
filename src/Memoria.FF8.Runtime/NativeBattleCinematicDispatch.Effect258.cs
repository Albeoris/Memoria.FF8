using System.Collections.Frozen;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleCinematicDispatch
{
    private static readonly FrozenDictionary<(string Table, int Index), (uint Handler, uint Canonical)> Effect258Slots = new Dictionary<(string Table, int Index), (uint Handler, uint Canonical)>
    {
        [("vm_table", 0x0)] = (0x96EE50, 0xB2FEB0),
        [("vm_table", 0x1)] = (0x96EAC0, 0xB2FB20),
        [("vm_table", 0x2)] = (0x969EF0, 0xB2AE30),
        [("vm_table", 0x3)] = (0x969F10, 0xB2AE50),
        [("vm_table", 0x4)] = (0x969F70, 0xB2AEB0),
        [("vm_table", 0x5)] = (0x96B0F0, 0xB2C030),
        [("vm_table", 0x6)] = (0x96AAD0, 0xB2BA10),
        [("vm_table", 0x8)] = (0x96AC90, 0xB2BBD0),
        [("vm_table", 0x9)] = (0x96EE20, 0xB2FE80),
        [("vm_table", 0xA)] = (0x96B540, 0xB2C480),
        [("vm_table", 0xB)] = (0x96B540, 0xB2C480),
        [("vm_table", 0xD)] = (0x96B540, 0xB2C480),
        [("vm_table", 0x10)] = (0x96B540, 0xB2C480),
        [("vm_table", 0x12)] = (0x96B540, 0xB2C480),
        [("vm_table", 0x1E)] = (0x969FC0, 0xB2AF00),
        [("vm_table", 0x1F)] = (0x96A040, 0xB2AF80),
        [("vm_table", 0x21)] = (0x96A510, 0xB2B450),
        [("vm_table", 0x22)] = (0x96A5F0, 0xB2B530),
        [("vm_table", 0x23)] = (0x96D060, 0xB2DFA0),
        [("vm_table", 0x26)] = (0x96C580, 0xB2D4C0),
        [("vm_table", 0x27)] = (0x96D600, 0xB2E540),
        [("vm_table", 0x29)] = (0x96D500, 0xB2E440),
        [("vm_table", 0x2A)] = (0x96C650, 0xB2D590),
        [("vm_table", 0x2B)] = (0x964E30, 0xB25BB0),
        [("vm_table", 0x2F)] = (0x96B990, 0xB2C8D0),
        [("vm_table", 0x32)] = (0x96C6D0, 0xB2D610),
        [("vm_table", 0x39)] = (0x96A810, 0xB2B750),
        [("vm_table", 0x3B)] = (0x96AA40, 0xB2B980),
        [("vm_table", 0x40)] = (0x96CDF0, 0xB2DD30),
        [("vm_table", 0x44)] = (0x96A270, 0xB2B1B0),
        [("vm_table", 0x47)] = (0x96AA90, 0xB2B9D0),
        [("vm_table", 0x52)] = (0x96D680, 0xB2E5C0),
        [("vm_table", 0x58)] = (0x968F70, 0xB29D70),
        [("vm_table", 0x59)] = (0x968FE0, 0xB29DE0),
        [("vm_table", 0x64)] = (0x96A1A0, 0xB2B0E0),
        [("vm_table", 0x74)] = (0x96B420, 0xB2C360),
        [("vm_table", 0x7D)] = (0x96AF50, 0xB2BE90),
        [("vm_table", 0x8B)] = (0x96C5E0, 0xB2D520),
        [("vm_table", 0xAD)] = (0x96E450, 0x96E450),
        [("vm_table", 0xB1)] = (0x96A210, 0xB2B150),
        [("vm_table", 0xE)] = (0x96B540, 0xB2C480),
        [("vm_table", 0x11)] = (0x96B540, 0xB2C480),
        [("vm_table", 0x15)] = (0x96B540, 0xB2C480),
        [("vm_table", 0x24)] = (0x96CBB0, 0xB2DAF0),
        [("vm_table", 0x3C)] = (0x96E5B0, 0xB2F3E0),
        [("vm_table", 0x4A)] = (0x96E690, 0xB2F4C0),
        [("vm_table", 0x67)] = (0x96DA40, 0xB2E950),
        [("vm_table", 0x68)] = (0x96DDA0, 0xB2ECB0),
        [("vm_table", 0xD3)] = (0x96CEF0, 0xB2DE30),
        [("vm_table", 0xD4)] = (0x96CE90, 0xB2DDD0),
        [("bone_table", 0x0)] = (0x965560, 0xB262E0),
        [("bone_table", 0x1)] = (0x965560, 0xB262E0),
        [("draw_table", 0x2)] = (0x966BA0, 0xB27050),
        [("draw_table", 0x6)] = (0x969120, 0xB29F20),
        [("draw_table", 0x1A)] = (0x965B80, 0xAEA0C0),
        [("draw_table", 0x1B)] = (0x966E20, 0xB272D0),
        [("draw_table", 0x2B)] = (0x9660C0, 0xB1B9E0),
        [("draw_table", 0x35)] = (0x9681E0, 0xAED040),
        [("prim_table", 0x11)] = (0x967CF0, 0xB28B00),
        [("prim_table", 0x7)] = (0x9679E0, 0xB27E90),
        [("pop_table", 0x0)] = (0x9696C0, 0xB2A4C0),
        [("pop_table", 0x1)] = (0x9696F0, 0xB2A4F0),
        [("pop_table", 0x2)] = (0x969710, 0xB2A510),
        [("pop_table", 0x4)] = (0x969770, 0xB2A570),
        [("pop_table", 0x5)] = (0x969830, 0xB2A630),
        [("pop_table", 0x7)] = (0x969A20, 0xB2A820),
        [("pop_table", 0xB)] = (0x969A90, 0xB2A890),
        [("pop_table", 0x10)] = (0x969BB0, 0xB2A9B0),
        [("ptype_table", 0x1)] = (0x9694E0, 0xB2A2E0),
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<uint, uint> Effect258Aliases = Effect258Slots.Values.Where(pair => pair.Handler != pair.Canonical).Distinct().ToFrozenDictionary(pair => pair.Handler, pair => pair.Canonical);

    private static uint RequireEffect258Slot(string table, int index, uint handler)
    {
        if (!Effect258Slots.TryGetValue((table, index), out var selected) || handler != selected.Handler)
        {
            throw new NotSupportedException($"Native cinematic effect 258 table {table} slot {index:X} requires its selected handler contract.");
        }
        return selected.Canonical;
    }
}
