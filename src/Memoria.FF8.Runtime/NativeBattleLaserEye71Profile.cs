namespace Memoria.FF8.Runtime;

public static class NativeBattleLaserEye71Profile
{
    public const uint Entry = 0x7D6150;
    public const uint Initializer = 0x7D6130;
    public const uint InitializerGlobal = 0x25FA408;
    public const uint CursorGlobal = 0x25FA40C;
    public const uint PacketEndGlobal = 0x25FA428;
    public const uint SavedBindingAddress = 0x25FCDA8;
    public const uint SourceAddress = 0x1789344;
    public const int SourceLength = 0x60B0;
    public const uint ArenaAddress = 0x20DFAB8;
    public const int DrawBankLength = 0x6000;
    public const int EmittersOffset = 0xC000;
    public const int EmitterCount = 20;
    public const int EmitterBytes = EmitterCount * 108;
    public const int GeometryOffset = EmittersOffset + EmitterBytes;
    public const int GeometryCount = 50;
    public const int GeometryBytes = GeometryCount * 672;
    public const int WorkingBytes = GeometryOffset + GeometryBytes;
    public static NativeBattleCureFamilyPool[] Pools =>
    [
        new(0x25FA4F8, 0x25FA430, 100, 2),
        new(0x25FCC10, 0x25FCC20, 88, 4),
        new(0x25FC970, 0x25FC180, 676, 3),
        new(0x25FA508, 0x25FA518, 148, 10),
        new(0x25FAAE8, 0x25FAAF8, 96, 60),
        new(0x25FCC00, 0x25FC980, 64, 10)
    ];
}
