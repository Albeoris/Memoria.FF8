using System.Collections.Frozen;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleCinematicDispatch
{
    private static readonly FrozenDictionary<(string Table, int Index), (uint Handler, uint Canonical)> Effect250Slots = new Dictionary<(string Table, int Index), (uint Handler, uint Canonical)>
    {
        [("vm_table", 0x0)] = (0x9B6F40, 0xB2FEB0),
        [("vm_table", 0x1)] = (0x9B6BB0, 0xB2FB20),
        [("vm_table", 0x2)] = (0x9B20F0, 0xB2AE30),
        [("vm_table", 0x5)] = (0x9B32F0, 0xB2C030),
        [("vm_table", 0x9)] = (0x9B6F10, 0xB2FE80),
        [("vm_table", 0xA)] = (0x9B3740, 0xB2C480),
        [("vm_table", 0xB)] = (0x9B3740, 0xB2C480),
        [("vm_table", 0xC)] = (0x9B3740, 0xB2C480),
        [("vm_table", 0xD)] = (0x9B3740, 0xB2C480),
        [("vm_table", 0xE)] = (0x9B3740, 0xB2C480),
        [("vm_table", 0x10)] = (0x9B3740, 0xB2C480),
        [("vm_table", 0x11)] = (0x9B3740, 0xB2C480),
        [("vm_table", 0x12)] = (0x9B3740, 0xB2C480),
        [("vm_table", 0x13)] = (0x9B3740, 0xB2C480),
        [("vm_table", 0x16)] = (0x9B3740, 0xB2C480),
        [("vm_table", 0x1A)] = (0x9B4330, 0xB2D070),
        [("vm_table", 0x1E)] = (0x9B21C0, 0xB2AF00),
        [("vm_table", 0x1F)] = (0x9B2240, 0xB2AF80),
        [("vm_table", 0x21)] = (0x9B2710, 0xB2B450),
        [("vm_table", 0x22)] = (0x9B27F0, 0xB2B530),
        [("vm_table", 0x23)] = (0x9B5260, 0xB2DFA0),
        [("vm_table", 0x24)] = (0x9B4DB0, 0xB2DAF0),
        [("vm_table", 0x26)] = (0x9B4780, 0xB2D4C0),
        [("vm_table", 0x27)] = (0x9B5800, 0xB2E540),
        [("vm_table", 0x2B)] = (0x9AC420, 0xB25BB0),
        [("vm_table", 0x2F)] = (0x9B3B90, 0xB2C8D0),
        [("vm_table", 0x32)] = (0x9B48D0, 0xB2D610),
        [("vm_table", 0x33)] = (0x9B22E0, 0xB2B020),
        [("vm_table", 0x39)] = (0x9B2A10, 0xB2B750),
        [("vm_table", 0x3B)] = (0x9B2C40, 0xB2B980),
        [("vm_table", 0x3C)] = (0x9B66A0, 0xB2F3E0),
        [("vm_table", 0x40)] = (0x9B4FF0, 0xB2DD30),
        [("vm_table", 0x43)] = (0x9B24E0, 0xB2B220),
        [("vm_table", 0x44)] = (0x9B2470, 0xB2B1B0),
        [("vm_table", 0x4D)] = (0x9AE580, 0xB26DF0),
        [("vm_table", 0x52)] = (0x9B5880, 0xB2E5C0),
        [("vm_table", 0x58)] = (0x9B1170, 0xB29D70),
        [("vm_table", 0x59)] = (0x9B11E0, 0xB29DE0),
        [("vm_table", 0x5B)] = (0x9B6350, 0xB2F090),
        [("vm_table", 0x5F)] = (0x9B3A40, 0xB2C780),
        [("vm_table", 0x64)] = (0x9B23A0, 0xB2B0E0),
        [("vm_table", 0x68)] = (0x9B5F70, 0xB2ECB0),
        [("vm_table", 0x72)] = (0x9B3AE0, 0xB2C820),
        [("vm_table", 0x74)] = (0x9B3620, 0xB2C360),
        [("vm_table", 0x7D)] = (0x9B3150, 0xB2BE90),
        [("vm_table", 0x82)] = (0x9B3BC0, 0xB2C900),
        [("vm_table", 0x8B)] = (0x9B47E0, 0xB2D520),
        [("vm_table", 0xA1)] = (0x9B2340, 0xB2B080),
        [("vm_table", 0xB1)] = (0x9B2410, 0xB2B150),
        [("vm_table", 0xB9)] = (0x9B3D40, 0xB2CA80),
        [("vm_table", 0xD3)] = (0x9B50F0, 0xB2DE30),
        [("vm_table", 0xD4)] = (0x9B5090, 0xB2DDD0),
        [("vm_table", 0x104)] = (0x9B6070, 0xB2EDB0),
        [("vm_table", 0x105)] = (0x9B6100, 0xB2EE40),
        [("bone_table", 0x0)] = (0x9ACB50, 0xB262E0),
        [("bone_table", 0x1)] = (0x9ACB50, 0xB262E0),
        [("bone_table", 0x9)] = (0x9ACE20, 0xB265B0),
        [("draw_table", 0x2)] = (0x9AE7E0, 0xB27050),
        [("draw_table", 0x4)] = (0x9AE5B0, 0xB26E20),
        [("draw_table", 0x6)] = (0x9B1320, 0xB29F20),
        [("draw_table", 0x15)] = (0x9ACF30, 0xB266C0),
        [("draw_table", 0x25)] = (0x9AD270, 0xB1A130),
        [("prim_table", 0x6)] = (0x9AF700, 0xAEB5B0),
        [("prim_table", 0x7)] = (0x9AF940, 0xB27E90),
        [("prim_table", 0x9)] = (0x9AFBD0, 0xB283B0),
        [("prim_table", 0x11)] = (0x9AFF00, 0xB28B00),
        [("prim_table", 0x12)] = (0x9B01D0, 0xB28DD0),
        [("prim_table", 0x13)] = (0x9B04B0, 0xB290B0),
        [("pop_table", 0x0)] = (0x9B18C0, 0xB2A4C0),
        [("pop_table", 0x1)] = (0x9B18F0, 0xB2A4F0),
        [("pop_table", 0x4)] = (0x9B1970, 0xB2A570),
        [("pop_table", 0x5)] = (0x9B1A30, 0xB2A630),
        [("pop_table", 0x7)] = (0x9B1C20, 0xB2A820),
        [("pop_table", 0x9)] = (0x9B1B50, 0xB2A750),
        [("pop_table", 0xA)] = (0x9B1BC0, 0xB2A7C0),
        [("pop_table", 0x13)] = (0x9B1EB0, 0xB2AAB0),
        [("ptype_table", 0x1)] = (0x9B16E0, 0xB2A2E0),
        [("ptype_table", 0x2)] = (0x9B1730, 0xB2A330),
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<uint, uint> Effect250Aliases = Effect250Slots.Values.Where(pair => pair.Handler != pair.Canonical).Distinct().ToFrozenDictionary(pair => pair.Handler, pair => pair.Canonical);

    private static uint RequireEffect250Slot(string table, int index, uint handler)
    {
        if (!Effect250Slots.TryGetValue((table, index), out var selected) || handler != selected.Handler)
        {
            throw new NotSupportedException($"Native cinematic effect 250 table {table} slot {index:X} requires its selected handler contract.");
        }
        return selected.Canonical;
    }
}
