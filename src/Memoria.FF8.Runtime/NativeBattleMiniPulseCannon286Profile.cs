using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleMiniPulseCannon286Profile
{
    public const ushort Effect = 286;
    public const uint Entry = 0x679E10;
    public const uint Initializer = 0x679E20;
    public const uint RootCallback = 0x679EC0;
    public const uint MainCallback = 0x67A070;
    public const uint RootDescriptor = 0x2507CD8;
    public const uint RootBank = 0x2507CE8;
    public const int RootBankLength = 20;
    public const uint BaseAddress = 0x2507CA8;
    public const uint InitializerGlobal = BaseAddress;
    public const uint ParametersGlobal = 0x2507CAC;
    public const uint CasterGlobal = 0x2507CB0;
    public const uint CursorGlobal = 0x2507CFC;
    public const uint ArenaGlobal = 0x2507D00;
    public const uint ArenaAddress = 0x20DFAB8;
    public const int ScratchOffset = 0xB54;
    public const int ScratchLength = 1152;
    public const int FirstPacketOffset = 0xFD4;
    public const int SecondPacketOffset = 0x10FD4;
    public const int PacketBankBytes = 0x10000;
    public const uint ProgrammeAAddress = 0x107DD94;
    public const int ProgrammeALength = 11352;
    public const int ProgrammeAWorkspaceLength = 556;
    public const uint ProgrammeBAddress = 0x10809EC;
    public const int ProgrammeBLength = 15808;
    public const int ProgrammeBWorkspaceLength = 344;
    public const uint ProgrammeCAddress = 0x1078BC8;
    public const int ProgrammeCLength = 17468;
    public const int ProgrammeCWorkspaceLength = 676;
    public const uint ProgrammeDAddress = 0x107D004;
    public const int ProgrammeDLength = 3472;
    public const int ProgrammeDWorkspaceLength = 144;
    public const uint SpriteAddress = 0x10789A8;
    public const int SpriteLength = 272;
    public const uint CameraAddress = 0x10847B0;
    public const int CameraLength = 812;
    public const uint SoundAddress = 0x10847AC;
    public const uint SourcePointAddress = 0x1084AE0;
    public const uint TextureNameAddress = 0x1084AE8;
    public const int TextureLength = 167968;

    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, 20, 1), new(0x2507CB8, ArenaAddress, 36, 32), new(0x2507CC8, ArenaAddress + 0x480, 1748, 1)];
}
