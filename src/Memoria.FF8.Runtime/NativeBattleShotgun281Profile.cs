using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleShotgun281Profile
{
    public const ushort Effect = 281;
    public const uint Entry = 0x67DCE0;
    public const uint Initializer = 0x67DCF0;
    public const uint RootCallback = 0x67DD90;
    public const uint MainCallback = 0x67DF80;
    public const uint RootDescriptor = 0x2507F68;
    public const uint RootBank = 0x2507F78;
    public const int RootBankLength = 20;
    public const uint BaseAddress = 0x2507F28;
    public const uint InitializerGlobal = BaseAddress;
    public const uint ParametersGlobal = 0x2507F2C;
    public const uint CasterGlobal = 0x2507F30;
    public const uint CursorGlobal = 0x2507F8C;
    public const uint BasisAAddress = 0x2507F90;
    public const uint BasisBAddress = 0x2507FB0;
    public const uint ArenaGlobal = 0x2507FD0;
    public const uint ArenaAddress = 0x20DFAB8;
    public const int ScratchOffset = 0x1DE8;
    public const int ScratchLength = 560;
    public const int FirstPacketOffset = 0x2018;
    public const int SecondPacketOffset = 0x12018;
    public const int PacketBankBytes = 0x10000;
    public const uint ProgrammeAAddress = 0x10A4D40;
    public const int ProgrammeALength = 10980;
    public const int ProgrammeAWorkspaceLength = 524;
    public const uint ProgrammeBAddress = 0x10A3230;
    public const int ProgrammeBLength = 6928;
    public const int ProgrammeBWorkspaceLength = 312;
    public const uint SpriteAAddress = 0x10A3140;
    public const int SpriteALength = 240;
    public const uint SpriteBAddress = 0x10A3030;
    public const int SpriteBLength = 272;
    public const uint CameraAddress = 0x10A7824;
    public const int CameraLength = 308;
    public const uint SoundAddress = 0x10A7958;
    public const uint MuzzlePointAddress = 0x10A7960;
    public const uint TextureNameAddress = 0x10A7968;
    public const int TextureLength = 100896;

    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, 20, 1), new(0x2507F38, ArenaAddress, 40, 128), new(0x2507F48, ArenaAddress + 0x1400, 332, 6), new(0x2507F58, ArenaAddress + 0x1BC8, 544, 1)];
}
