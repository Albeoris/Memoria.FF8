namespace Memoria.FF8.Runtime;

/// <summary>The physical directories and entry points of the cinematic effect families.</summary>
public sealed record NativeBattleCinematicProfile(int Effect, uint Entry, uint Initializer, uint InitializationAddress, uint OwnerDirectoryAddress, uint QueueAddress, uint BankAddress, uint Callback, uint LoadCallback, uint File0Publication, uint File1Publication, uint InstructionTable, int TextureCount, int SourceBase, int LastSource, string InitialFile, bool Lit)
{
    public static bool IsSupported(int effect) => effect is 6 or 18 or 75 or 77 or 248 or 201 or 202 or 203 or 204 or 205 or 206 or 208 or 211 or 219 or 220 or 221 or 223 or 228 or 229 or 230 or 234 or 235 or 238 or 256 or 261 or 262 or 263;

    public static NativeBattleCinematicProfile ForInitializationAddress(uint address)
    {
        return ForEffect(address switch
        {
            0x1876870 => 18,
            0x1875EAC => 75,
            0x1875448 => 77,
            0x186FBEC => 208,
            0x186F1B0 => 211,
            0x186E6D0 => 219,
            0x186DCEC => 220,
            0x185EEFC => 248,
            0x1874894 => 201,
            0x187726C => 6,
            0x1873A60 => 202,
            0x1872EF8 => 203,
            0x1872384 => 204,
            0x1871878 => 205,
            0x1870640 => 206,
            0x1867954 => 234,
            0x1866F60 => 235,
            0x186B438 => 228,
            0x186AA40 => 229,
            0x186A02C => 230,
            0x186BE34 => 223,
            0x186D234 => 221,
            0x1865154 => 238,
            0x185AA9C => 256,
            0x1858BD8 => 261,
            0x18581A8 => 262,
            0x185777C => 263,
            _ => throw new NotSupportedException("The selected initialization directory has no cinematic profile.")
        });
    }

    public static NativeBattleCinematicProfile ForEffect(int effect)
    {
        return effect switch
        {
            18 => new(18, 0xB4A530, 0xB4A500, 0x1876870, 0x187697C, 0x2796EC0, 0x2796ED8, 0xB4ABA0, 0xB53850, 0x2796ED4, 0x2796ED0, 0x1876D34, 6, 0x16D, 0x17E, "mag017_b.00", true),
            75 => new(75, 0xB3E8E0, 0xB3E8B0, 0x1875EAC, 0x1875F80, 0x2796E88, 0x2796E98, 0xB3EF50, 0xB45FB0, 0x2796EBC, 0x2796EB8, 0x1876338, 4, 0x233, 0x232, "mag074_b.00", false),
            77 => new(77, 0xB302F0, 0xB302C0, 0x1875448, 0x18755BC, 0x2796E50, 0x2796E60, 0xB30990, 0xB3A150, 0x2796E84, 0x2796E80, 0x1875974, 9, 0x189, 0x18B, "mag076_b.00", false),
            208 => new(208, 0xAD7900, 0xAD78D0, 0x186FBEC, 0x186FD50, 0x2796CC0, 0x2796CD0, 0xAD7F70, 0xADE7D0, 0x2796CF0, 0x2796CF4, 0x1870108, 10, 0x233, 0x232, "mag207_b.00", false),
            211 => new(211, 0xACA120, 0xACA0F0, 0x186F1B0, 0x186F2FC, 0x2796C88, 0x2796C98, 0xACA790, 0xAD3300, 0x2796CBC, 0x2796CB8, 0x186F6B4, 9, 0x233, 0x232, "mag210_b.00", false),
            219 => new(219, 0xABD420, 0xABD3F0, 0x186E6D0, 0x186E8C0, 0x2796C50, 0x2796C68, 0xABDA90, 0xAC5950, 0x2796C64, 0x2796C60, 0x186EC78, 15, 0x2C4, 0x2C8, "mag218_b.00", false),
            220 => new(220, 0xAB3320, 0xAB32F0, 0x186DCEC, 0x186DDE0, 0x2796C18, 0x2796C30, 0xAB3990, 0xAB8EA0, 0x2796C2C, 0x2796C28, 0x186E198, 5, 0x233, 0x232, "mag219_b.00", false),
            248 => new(248, 0x9C08D0, 0x9C08A0, 0x185EEFC, 0x185EFAC, 0x27966D8, 0x27966F0, 0x9C0F40, 0x9C5240, 0x27966E8, 0x27966EC, 0x185F364, 3, 0x233, 0x232, "mag247_b.00", false),
            201 => new(201, 0xB25780, 0xB25750, 0x1874894, 0x1874B58, 0x2796E18, 0x2796E28, 0xB25DF0, 0xB2BB40, 0x2796E4C, 0x2796E48, 0x1874F10, 23, 0x233, 0x23F, "mag200_b.00", true),
            6 => new(6, 0xB58080, 0xB58050, 0x187726C, 0x18774AC, 0x2796EF8, 0x2796F10, 0xB586F0, 0xB60530, 0x2796F0C, 0x2796F08, 0x1877864, 13, 0x16D, 0x17E, "mag005_b.00", true),
            202 => new(202, 0xB189A0, 0xB18970, 0x1873A60, 0x1873FA4, 0x2796DE0, 0x2796DF0, 0xB19010, 0xB21090, 0x2796E14, 0x2796E10, 0x187435C, 40, 0x23E, 0x265, "mag201_b.00", false),
            203 => new(203, 0xB0C1A0, 0xB0C170, 0x1872EF8, 0x1873170, 0x2796DA8, 0x2796DB8, 0xB0C820, 0xB143F0, 0x2796DDC, 0x2796DD8, 0x1873528, 19, 0x264, 0x270, "mag202_b.00", false),
            204 => new(204, 0xAFFCA0, 0xAFFC70, 0x1872384, 0x1872608, 0x2796D70, 0x2796D80, 0xB00310, 0xB07C20, 0x2796DA4, 0x2796DA0, 0x18729C0, 17, 0x26F, 0x27E, "mag203_b.00", false),
            205 => new(205, 0xAF4520, 0xAF44F0, 0x1871878, 0x1871A94, 0x2796D30, 0x2796D48, 0xAF4B90, 0xAFAD70, 0x2796D6C, 0x2796D68, 0x1871E4C, 12, 0x27D, 0x288, "mag204_b.00", false),
            206 => new(206, 0xAE2DD0, 0xAE2DA0, 0x1870640, 0x1870F50, 0x2796CF8, 0x2796D08, 0xAE3470, 0xAEFAE0, 0x2796D2C, 0x2796D28, 0x1871340, 76, 0x287, 0x2BF, "mag205_b.00", false),
            234 => new(234, 0xA488F0, 0xA488C0, 0x1867954, 0x1867A08, 0x27969E8, 0x2796A00, 0xA48F60, 0xA4D9C0, 0x27969FC, 0x27969F8, 0x1867DC0, 3, 0x233, 0x232, "mag233_b.00", false),
            235 => new(235, 0xA3E370, 0xA3E340, 0x1866F60, 0x1867064, 0x27969B0, 0x27969C8, 0xA3E9E0, 0xA44370, 0x27969C4, 0x27969C0, 0x186741C, 6, 0x233, 0x232, "mag234_b.00", false),
            228 => new(228, 0xA855C0, 0xA85590, 0x186B438, 0x186B544, 0x2796B38, 0x2796B50, 0xA85C30, 0xA8B230, 0x2796B4C, 0x2796B48, 0x186B8FC, 6, 0x233, 0x232, "mag227_b.00", false),
            229 => new(229, 0xA7AA20, 0xA7A9F0, 0x186AA40, 0x186AB48, 0x2796B00, 0x2796B18, 0xA7B090, 0xA80F60, 0x2796B14, 0x2796B10, 0x186AF00, 6, 0x233, 0x232, "mag228_b.00", false),
            230 => new(230, 0xA70B70, 0xA70B40, 0x186A02C, 0x186A150, 0x2796AC8, 0x2796AE0, 0xA711E0, 0xA76420, 0x2796ADC, 0x2796AD8, 0x186A508, 7, 0x233, 0x232, "mag229_b.00", false),
            223 => new(223, 0xA8F890, 0xA8F860, 0x186BE34, 0x186BF5C, 0x2796B70, 0x2796B80, 0xA8FF00, 0xA96AF0, 0x2796BA4, 0x2796BA0, 0x186C314, 7, 0x233, 0x232, "mag222_b.00", false),
            221 => new(221, 0xAA5560, 0xAA5530, 0x186D234, 0x186D37C, 0x2796BE0, 0x2796BF0, 0xAA5BD0, 0xAAEA40, 0x2796C10, 0x2796C14, 0x186D7B4, 8, 0x233, 0x232, "mag220_b.00", false),
            238 => new(238, 0xA1FDC0, 0xA1FD90, 0x1865154, 0x1865278, 0x2796910, 0x2796920, 0xA20430, 0xA25630, 0x279690C, 0x2796908, 0x1865630, 7, 0x233, 0x232, "mag237_b.00", false),
            256 => new(256, 0x979400, 0x9793D0, 0x185AA9C, 0x185AB20, 0x2796548, 0x2796560, 0x979A70, 0x97E0A0, 0x2796558, 0x2796540, 0x185AED8, 1, 0x233, 0x232, "mag255_b.00", false),
            261 => new(261, 0x9598D0, 0x9598A0, 0x1858BD8, 0x1858DE4, 0x2796158, 0x27963F8, 0x959F40, 0x960270, 0x27961E8, 0x27961EC, 0x185919C, 16, 0x2C8, 0x2CA, "mag260_b.00", false),
            262 => new(262, 0x94F9F0, 0x94F9C0, 0x18581A8, 0x18582E8, 0x2795DE0, 0x2796080, 0x950060, 0x955350, 0x2795DF0, 0x2795DD8, 0x18586A0, 8, 0x233, 0x232, "mag261_b.00", false),
            263 => new(263, 0x945810, 0x9457E0, 0x185777C, 0x18578B8, 0x2795DA8, 0x2795DB8, 0x945E80, 0x94B470, 0x2795DA4, 0x2795DA0, 0x1857C70, 8, 0x233, 0x232, "mag262_b.00", false),
            _ => throw new NotSupportedException("The selected effect has no cinematic directory profile.")
        };
    }

    public bool HasVibrationDirectory => Effect is not (75 or 219 or 220 or 248 or 228 or 229 or 230 or 234 or 235 or 238 or 256 or 261 or 262 or 263);

    public int StaticLength => checked((int)(InstructionTable + 0x147 * 4 - InitializationAddress));

    public uint BoneTable => Effect switch
    {
        18 => 0x18769A4,
        75 or 77 or 208 or 211 or 219 or 220 => checked(OwnerDirectoryAddress + 0x28),
        248 => 0x185EFD4,
        201 => 0x1874B80,
        6 => 0x18774D4,
        202 => 0x1873FCC,
        203 => 0x1873198,
        204 => 0x1872630,
        205 => 0x1871ABC,
        206 => 0x1870F78,
        234 => 0x1867A30,
        235 => 0x186708C,
        228 => 0x186B56C,
        229 => 0x186AB70,
        230 => 0x186A178,
        223 => 0x186BF84,
        221 => 0x186D3A4,
        238 => 0x18652A0,
        256 => 0x185AB48,
        261 => 0x1858E0C,
        262 => 0x1858310,
        263 => 0x18578E0,
        _ => throw new NotSupportedException("The selected effect has no cinematic bone table.")
    };

    public int BoneCount => Effect == 206 ? 32 : 18;
    public uint PrimitiveTable => checked(BoneTable + (uint)(BoneCount * 4));
    public uint PrimitiveSizes => checked(PrimitiveTable + 60 * 4);
    public uint SpriteTable => Effect == 221 ? 0x186D570 : checked(PrimitiveSizes + 20);
    public uint PlacementTable => checked(SpriteTable + 11 * 4);
    public uint ParticleInstructionTable => checked(PlacementTable + 5 * 4);
    public uint DrawTable => checked(ParticleInstructionTable + 24 * 4);
}
