namespace Memoria.FF8.Runtime;

public sealed record NativeBattleHeroProfile(ushort Effect, uint Entry, uint Initializer, uint Setup, uint Root, uint Child, uint Draw, uint InitializerGlobal, uint MainDescriptor, uint MainBank, uint ChildDescriptor, uint MainSource, uint OrbitSource, uint FanSource, ushort ChildStride, ushort ChildCapacity, int AnchorOffset, int MainOffset, int OrbitOffset, int SpokeOffset, int ScratchOffset, int FirstDrawOffset)
{
    public const string TextureName = "mag308.tim";
    public const uint Sound = 300188;
    public const uint FilenameAddress = 0x10244D8;

    public uint SoundAddress => Effect == 309 ? 0x10257E0u : 0x10244D4u;

    public static NativeBattleHeroProfile Hero { get; } = new(309, 0x666DD0, 0x666DE0, 0x666E00, 0x666E70, 0x667030, 0x667330, 0x2507480, 0x25074A0, 0x25074B0, 0x2507490, 0x10244E4, 0x1024D6C, 0x1025528, 0x3CC, 1, 0x110, 0x118, 0x254, 0x10, 0x3CC, 0x3D4);
    public static NativeBattleHeroProfile HolyWar { get; } = new(310, 0x6664A0, 0x6664B0, 0x6664D0, 0x666540, 0x666730, 0x666A90, 0x2507430, 0x2507450, 0x2507460, 0x2507440, 0x10231D8, 0x1023A60, 0x102421C, 0x3D0, 6, 0x114, 0x11C, 0x258, 0x14, 0x16E0, 0x16E8);

    public static NativeBattleHeroProfile ForEffect(ushort effect)
    {
        return effect switch
        {
            309 => Hero,
            310 => HolyWar,
            _ => throw new NotSupportedException("The Hero module profile requires effect309 or310.")
        };
    }
}
