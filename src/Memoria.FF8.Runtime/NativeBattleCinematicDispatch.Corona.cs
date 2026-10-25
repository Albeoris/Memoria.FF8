using System.Collections.Frozen;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleCinematicDispatch
{
    private static readonly FrozenDictionary<(string Table, int Index), (uint Handler, uint Canonical)> CoronaSlots = new Dictionary<(string Table, int Index), (uint Handler, uint Canonical)>
    {
        [("vm_table", 0x0)] = (0x9ABBB0, 0xB2FEB0),
        [("vm_table", 0x1)] = (0x9AB820, 0xB2FB20),
        [("vm_table", 0x2)] = (0x9A6D60, 0xB2AE30),
        [("vm_table", 0x5)] = (0x9A7F60, 0xB2C030),
        [("vm_table", 0x9)] = (0x9ABB80, 0xB2FE80),
        [("vm_table", 0xA)] = (0x9A83B0, 0xB2C480),
        [("vm_table", 0xB)] = (0x9A83B0, 0xB2C480),
        [("vm_table", 0xD)] = (0x9A83B0, 0xB2C480),
        [("vm_table", 0xE)] = (0x9A83B0, 0xB2C480),
        [("vm_table", 0xF)] = (0x9A83B0, 0xB2C480),
        [("vm_table", 0x11)] = (0x9A83B0, 0xB2C480),
        [("vm_table", 0x12)] = (0x9A83B0, 0xB2C480),
        [("vm_table", 0x15)] = (0x9A83B0, 0xB2C480),
        [("vm_table", 0x16)] = (0x9A83B0, 0xB2C480),
        [("vm_table", 0x1E)] = (0x9A6E30, 0xB2AF00),
        [("vm_table", 0x1F)] = (0x9A6EB0, 0xB2AF80),
        [("vm_table", 0x21)] = (0x9A7380, 0xB2B450),
        [("vm_table", 0x22)] = (0x9A7460, 0xB2B530),
        [("vm_table", 0x23)] = (0x9A9ED0, 0xB2DFA0),
        [("vm_table", 0x24)] = (0x9A9A20, 0xB2DAF0),
        [("vm_table", 0x26)] = (0x9A93F0, 0xB2D4C0),
        [("vm_table", 0x27)] = (0x9AA470, 0xB2E540),
        [("vm_table", 0x2B)] = (0x9A2CA0, 0xB25BB0),
        [("vm_table", 0x2F)] = (0x9A8800, 0xB2C8D0),
        [("vm_table", 0x32)] = (0x9A9540, 0xB2D610),
        [("vm_table", 0x33)] = (0x9A6F50, 0xB2B020),
        [("vm_table", 0x39)] = (0x9A7680, 0xB2B750),
        [("vm_table", 0x3B)] = (0x9A78B0, 0xB2B980),
        [("vm_table", 0x3C)] = (0x9AB310, 0xB2F3E0),
        [("vm_table", 0x3E)] = (0x9AB460, 0xB2F530),
        [("vm_table", 0x40)] = (0x9A9C60, 0xB2DD30),
        [("vm_table", 0x4D)] = (0x9A3A60, 0xB26DF0),
        [("vm_table", 0x51)] = (0x9A9A70, 0xB2DB40),
        [("vm_table", 0x52)] = (0x9AA4F0, 0xB2E5C0),
        [("vm_table", 0x58)] = (0x9A5DE0, 0xB29D70),
        [("vm_table", 0x59)] = (0x9A5E50, 0xB29DE0),
        [("vm_table", 0x5B)] = (0x9AAFC0, 0xB2F090),
        [("vm_table", 0x64)] = (0x9A7010, 0xB2B0E0),
        [("vm_table", 0x67)] = (0x9AA880, 0xB2E950),
        [("vm_table", 0x68)] = (0x9AABE0, 0xB2ECB0),
        [("vm_table", 0x72)] = (0x9A8750, 0xB2C820),
        [("vm_table", 0x74)] = (0x9A8290, 0xB2C360),
        [("vm_table", 0x7D)] = (0x9A7DC0, 0xB2BE90),
        [("vm_table", 0x8B)] = (0x9A9450, 0xB2D520),
        [("vm_table", 0xB1)] = (0x9A7080, 0xB2B150),
        [("vm_table", 0xBC)] = (0x9A9B90, 0xB2DC60),
        [("vm_table", 0xD3)] = (0x9A9D60, 0xB2DE30),
        [("vm_table", 0x104)] = (0x9AACE0, 0xB2EDB0),
        [("vm_table", 0x105)] = (0x9AAD70, 0xB2EE40),
        [("vm_table", 0x116)] = (0x9AAA40, 0xB2EB10),
        [("bone_table", 0x0)] = (0x9A33D0, 0xB262E0),
        [("bone_table", 0x1)] = (0x9A33D0, 0xB262E0),
        [("draw_table", 0x2)] = (0x9A3CC0, 0xB27050),
        [("draw_table", 0x4)] = (0x9A3A90, 0xB26E20),
        [("draw_table", 0x6)] = (0x9A5F90, 0xB29F20),
        [("pop_table", 0x7)] = (0x9A6890, 0xB2A820),
        [("pop_table", 0x0)] = (0x9A6530, 0xB2A4C0),
        [("pop_table", 0x1)] = (0x9A6560, 0xB2A4F0),
        [("pop_table", 0x4)] = (0x9A65E0, 0xB2A570),
        [("pop_table", 0xB)] = (0x9A6900, 0xB2A890),
        [("pop_table", 0xC)] = (0x9A6940, 0xB2A8D0),
        [("pop_table", 0x10)] = (0x9A6A20, 0xB2A9B0),
        [("pop_table", 0x2)] = (0x9A6580, 0xB2A510),
        [("prim_table", 0x7)] = (0x9A4BF0, 0xB27E90),
        [("prim_table", 0x11)] = (0x9A5160, 0xB28B00),
        [("prim_table", 0x10)] = (0x9A4EF0, 0xB105B0),
        [("ptype_table", 0x1)] = (0x9A6350, 0xB2A2E0),
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<uint, uint> CoronaAliases = CoronaSlots.Values.Distinct().ToFrozenDictionary(pair => pair.Handler, pair => pair.Canonical);

    private static uint RequireCoronaSlot(string table, int index, uint handler)
    {
        if (!CoronaSlots.TryGetValue((table, index), out var selected) || handler != selected.Handler)
        {
            throw new NotSupportedException($"Native cinematic effect 252 table {table} slot {index:X} requires its selected handler contract.");
        }
        return selected.Canonical;
    }
}
