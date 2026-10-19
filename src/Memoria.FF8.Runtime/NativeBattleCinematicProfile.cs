namespace Memoria.FF8.Runtime;

/// <summary>The physical directories and entry points of the seven cinematic summon families.</summary>
public sealed record NativeBattleCinematicProfile(int Effect, uint Entry, uint Initializer, uint InitializationAddress, uint OwnerDirectoryAddress, uint QueueAddress, uint BankAddress, uint Callback, uint LoadCallback, uint File0Publication, uint File1Publication, uint InstructionTable, int TextureCount, int SourceBase, int LastSource, string InitialFile, bool Lit)
{
    public static NativeBattleCinematicProfile ForEffect(int effect)
    {
        return effect switch
        {
            201 => new(201, 0xB25780, 0xB25750, 0x1874894, 0x1874B58, 0x2796E18, 0x2796E28, 0xB25DF0, 0xB2BB40, 0x2796E4C, 0x2796E48, 0x1874F10, 23, 0x233, 0x23F, "mag200_b.00", true),
            6 => new(6, 0xB58080, 0xB58050, 0x187726C, 0x18774AC, 0x2796EF8, 0x2796F10, 0xB586F0, 0xB60530, 0x2796F0C, 0x2796F08, 0x1877864, 13, 0x16D, 0x17E, "mag005_b.00", true),
            202 => new(202, 0xB189A0, 0xB18970, 0x1873A60, 0x1873FA4, 0x2796DE0, 0x2796DF0, 0xB19010, 0xB21090, 0x2796E14, 0x2796E10, 0x187435C, 40, 0x23E, 0x265, "mag201_b.00", false),
            203 => new(203, 0xB0C1A0, 0xB0C170, 0x1872EF8, 0x1873170, 0x2796DA8, 0x2796DB8, 0xB0C820, 0xB143F0, 0x2796DDC, 0x2796DD8, 0x1873528, 19, 0x264, 0x270, "mag202_b.00", false),
            204 => new(204, 0xAFFCA0, 0xAFFC70, 0x1872384, 0x1872608, 0x2796D70, 0x2796D80, 0xB00310, 0xB07C20, 0x2796DA4, 0x2796DA0, 0x18729C0, 17, 0x26F, 0x27E, "mag203_b.00", false),
            205 => new(205, 0xAF4520, 0xAF44F0, 0x1871878, 0x1871A94, 0x2796D30, 0x2796D48, 0xAF4B90, 0xAFAD70, 0x2796D6C, 0x2796D68, 0x1871E4C, 12, 0x27D, 0x288, "mag204_b.00", false),
            206 => new(206, 0xAE2DD0, 0xAE2DA0, 0x1870640, 0x1870F50, 0x2796CF8, 0x2796D08, 0xAE3470, 0xAEFAE0, 0x2796D2C, 0x2796D28, 0x1871340, 76, 0x287, 0x2BF, "mag205_b.00", false),
            _ => throw new NotSupportedException("The selected effect has no cinematic directory profile.")
        };
    }

    public int StaticLength => checked((int)(InstructionTable + 0x147 * 4 - InitializationAddress));

    public uint BoneTable => Effect switch
    {
        201 => 0x1874B80,
        6 => 0x18774D4,
        202 => 0x1873FCC,
        203 => 0x1873198,
        204 => 0x1872630,
        205 => 0x1871ABC,
        206 => 0x1870F78,
        _ => throw new NotSupportedException("The selected effect has no cinematic bone table.")
    };

    public int BoneCount => Effect == 206 ? 32 : 18;
    public uint PrimitiveTable => checked(BoneTable + (uint)(BoneCount * 4));
    public uint PrimitiveSizes => checked(PrimitiveTable + 60 * 4);
    public uint DrawTable => checked(PrimitiveSizes + 20 + 11 * 4 + 5 * 4 + 24 * 4);
}
