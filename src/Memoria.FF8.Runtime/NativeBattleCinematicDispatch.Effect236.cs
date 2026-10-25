using System.Collections.Frozen;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleCinematicDispatch
{
    private static readonly FrozenDictionary<(string Table, int Index), (uint Handler, uint Canonical)> Effect236Slots = new Dictionary<(string Table, int Index), (uint Handler, uint Canonical)>
    {
        [("vm_table", 0x0)] = (0xA3DF30, 0xB2FEB0),
        [("vm_table", 0x1)] = (0xA3DBA0, 0xB2FB20),
        [("vm_table", 0x2)] = (0xA390E0, 0xB2AE30),
        [("vm_table", 0x3)] = (0xA39100, 0xB2AE50),
        [("vm_table", 0x4)] = (0xA39160, 0xB2AEB0),
        [("vm_table", 0x5)] = (0xA3A2E0, 0xB2C030),
        [("vm_table", 0x6)] = (0xA39CC0, 0xB2BA10),
        [("vm_table", 0x8)] = (0xA39E80, 0xB2BBD0),
        [("vm_table", 0x9)] = (0xA3DF00, 0xB2FE80),
        [("vm_table", 0xA)] = (0xA3A730, 0xB2C480),
        [("vm_table", 0xB)] = (0xA3A730, 0xB2C480),
        [("vm_table", 0xD)] = (0xA3A730, 0xB2C480),
        [("vm_table", 0x10)] = (0xA3A730, 0xB2C480),
        [("vm_table", 0x12)] = (0xA3A730, 0xB2C480),
        [("vm_table", 0x13)] = (0xA3A730, 0xB2C480),
        [("vm_table", 0x16)] = (0xA3A730, 0xB2C480),
        [("vm_table", 0x1A)] = (0xA3B320, 0xB2D070),
        [("vm_table", 0x20)] = (0xA3C5A0, 0xB2E2F0),
        [("vm_table", 0x21)] = (0xA39700, 0xB2B450),
        [("vm_table", 0x22)] = (0xA397E0, 0xB2B530),
        [("vm_table", 0x23)] = (0xA3C250, 0xB2DFA0),
        [("vm_table", 0x24)] = (0xA3BDA0, 0xB2DAF0),
        [("vm_table", 0x26)] = (0xA3B770, 0xB2D4C0),
        [("vm_table", 0x27)] = (0xA3C7F0, 0xB2E540),
        [("vm_table", 0x29)] = (0xA3C6F0, 0xB2E440),
        [("vm_table", 0x2B)] = (0xA34670, 0xB25BB0),
        [("vm_table", 0x2F)] = (0xA3AB80, 0xB2C8D0),
        [("vm_table", 0x32)] = (0xA3B8C0, 0xB2D610),
        [("vm_table", 0x33)] = (0xA392D0, 0xB2B020),
        [("vm_table", 0x39)] = (0xA39A00, 0xB2B750),
        [("vm_table", 0x3B)] = (0xA39C30, 0xB2B980),
        [("vm_table", 0x3C)] = (0xA3D690, 0xB2F3E0),
        [("vm_table", 0x3E)] = (0xA3D7E0, 0xB2F530),
        [("vm_table", 0x40)] = (0xA3BFE0, 0xB2DD30),
        [("vm_table", 0x52)] = (0xA3C870, 0xB2E5C0),
        [("vm_table", 0x58)] = (0xA38160, 0xB29D70),
        [("vm_table", 0x59)] = (0xA381D0, 0xB29DE0),
        [("vm_table", 0x5D)] = (0xA3BAA0, 0xB2D7F0),
        [("vm_table", 0x60)] = (0xA3B0B0, 0xB2CE00),
        [("vm_table", 0x61)] = (0xA3AF70, 0xB2CCC0),
        [("vm_table", 0x64)] = (0xA39390, 0xB2B0E0),
        [("vm_table", 0x6E)] = (0xA3B220, 0xB2CF70),
        [("vm_table", 0x73)] = (0xA3BCF0, 0xB2DA40),
        [("vm_table", 0x74)] = (0xA3A610, 0xB2C360),
        [("vm_table", 0x7D)] = (0xA3A140, 0xB2BE90),
        [("vm_table", 0x8B)] = (0xA3B7D0, 0xB2D520),
        [("vm_table", 0xA1)] = (0xA39330, 0xB2B080),
        [("vm_table", 0xA2)] = (0xA3E0A0, 0xB30020),
        [("vm_table", 0xB1)] = (0xA39400, 0xB2B150),
        [("vm_table", 0xCE)] = (0xA39300, 0xB2B050),
        [("vm_table", 0x11)] = (0xA3A730, 0xB2C480),
        [("vm_table", 0x15)] = (0xA3A730, 0xB2C480),
        [("vm_table", 0xB0)] = (0xA3E0E0, 0xB30060),
        [("vm_table", 0xD4)] = (0xA3C080, 0xB2DDD0),
        [("vm_table", 0xC)] = (0xA3A730, 0xB2C480),
        [("bone_table", 0x0)] = (0xA34DA0, 0xB262E0),
        [("bone_table", 0x1)] = (0xA34DA0, 0xB262E0),
        [("bone_table", 0x4)] = (0xA34F60, 0xB264A0),
        [("draw_table", 0x2)] = (0xA357E0, 0xB27050),
        [("draw_table", 0x5)] = (0xA37CC0, 0xB298D0),
        [("draw_table", 0x6)] = (0xA38310, 0xB29F20),
        [("spr_table", 0x2)] = (0xA37A40, 0xB29650),
        [("prim_table", 0x8)] = (0xA36990, 0xB28110),
        [("prim_table", 0x30)] = (0xA36FE0, 0xB11100),
        [("pop_table", 0x0)] = (0xA388B0, 0xB2A4C0),
        [("pop_table", 0x1)] = (0xA388E0, 0xB2A4F0),
        [("pop_table", 0x4)] = (0xA38960, 0xB2A570),
        [("pop_table", 0x5)] = (0xA38A20, 0xB2A630),
        [("pop_table", 0x7)] = (0xA38C10, 0xB2A820),
        [("pop_table", 0xB)] = (0xA38C80, 0xB2A890),
        [("ptype_table", 0x1)] = (0xA386D0, 0xB2A2E0),
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<uint, uint> Effect236Aliases = Effect236Slots.Values.Where(pair => pair.Handler != pair.Canonical).Distinct().ToFrozenDictionary(pair => pair.Handler, pair => pair.Canonical);

    private static uint RequireEffect236Slot(string table, int index, uint handler)
    {
        if (!Effect236Slots.TryGetValue((table, index), out var selected) || handler != selected.Handler)
        {
            throw new NotSupportedException($"Native cinematic effect 236 table {table} slot {index:X} requires its selected handler contract.");
        }
        return selected.Canonical;
    }
}
