namespace Memoria.FF8.Runtime;

public static partial class NativeBattleRenzokukenSpecialProfiles
{
    public static NativeBattleRenzokukenSpecialProfile Renzokuken334 { get; } = new(334, 0x587790, 0x21F7FD8, 0x21F7FC8, 0x589940, 0x21F91A0, 0x21F91B0, 0x5878B0) { PacketStride = 0x8000 };
    public static NativeBattleRenzokukenSpecialProfile Renzokuken335 { get; } = new(335, 0x5856B0, 0x21F51A0, 0x21F5190, 0x587500, 0x21F6368, 0x21F6378, 0x5857D0) { PacketStride = 0xC000 };
    public static NativeBattleRenzokukenSpecialProfile Renzokuken336 { get; } = new(336, 0x583040, 0x21F2368, 0x21F2358, 0x585420, 0x21F3530, 0x21F3540, 0x583160) { PacketStride = 0xC000 };
    public static NativeBattleRenzokukenSpecialProfile Renzokuken337 { get; } = new(337, 0x580990, 0x21EF4D0, 0x21EF4C0, 0x582DB0, 0x21F0698, 0x21F06A8, 0x580AB0) { PacketStride = 0xC000 };
    public static NativeBattleRenzokukenSpecialProfile Renzokuken339 { get; } = new(339, 0x57E5D0, 0x21EC638, 0x21EC628, 0x580700, 0x21ED800, 0x21ED810, 0x57E6F0) { PacketStride = 0xC000 };
    public static NativeBattleRenzokukenSpecialProfile Renzokuken340 { get; } = new(340, 0x57BC00, 0x21E9800, 0x21E97F0, 0x57E340, 0x21EA9C8, 0x21EA9D8, 0x57BD20) { PacketStride = 0xC000 };
    public static NativeBattleRenzokukenSpecialProfile Renzokuken341 { get; } = new(341, 0x579A80, 0x21E6968, 0x21E6958, 0x57B970, 0x21E7B30, 0x21E7B40, 0x579BA0) { PacketStride = 0xC000 };
    public static NativeBattleRenzokukenSpecialProfile Renzokuken342 { get; } = new(342, 0x5776D0, 0x21E3B30, 0x21E3B20, 0x5797F0, 0x21E4CF8, 0x21E4D08, 0x5777F0) { PacketStride = 0xC000 };
    public static NativeBattleRenzokukenSpecialProfile Renzokuken343 { get; } = new(343, 0x575650, 0x21E0C78, 0x21E0C68, 0x577440, 0x21E1E60, 0x21E1E70, 0x575770) { PacketStride = 0xC000 };

    public static NativeBattleRenzokukenSpecialProfile ForEffect(ushort effect) => effect switch
    {
        161 => Renzokuken161,
        334 => Renzokuken334,
        335 => Renzokuken335,
        336 => Renzokuken336,
        337 => Renzokuken337,
        339 => Renzokuken339,
        340 => Renzokuken340,
        341 => Renzokuken341,
        342 => Renzokuken342,
        343 => Renzokuken343,
        _ => throw new NotSupportedException($"Native Renzokuken effect {effect} has no original special profile.")
    };
}
