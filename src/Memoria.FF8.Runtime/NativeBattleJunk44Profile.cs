using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleJunk44Profile
{
    public const ushort Effect = 44;
    public const uint Entry = 0x6E7AE0;
    public const uint Initializer = 0x6E7AF0;
    public const uint RootCallback = 0x6E7B90;
    public const uint RootDescriptor = 0x2545150;
    public const uint RootBank = 0x2545160;
    public const int RootBankLength = 20;
    public const uint BaseAddress = 0x2545100;
    public const uint InitializerGlobal = 0x2545100;
    public const uint ParametersGlobal = 0x2545104;
    public const uint CasterGlobal = 0x2545108;
    public const uint CursorGlobal = 0x2545174;
    public const uint ArenaGlobal = 0x2545180;
    public const uint ArenaAddress = 0x20DFAB8;
    public const int PacketBankBytes = 0x10000;
    public const int PacketOffset = 0x12D4;
    public const int ScratchOffset = 0x212D4;
    public const uint ScratchAddress = ArenaAddress + ScratchOffset;
    public const int ScratchLength = 72;
    public const uint ProgrammeAddress = 0x1354CE0;
    public const int ProgrammeLength = 472;
    public const int ProgrammeWorkspaceLength = 64;
    public const uint SpriteAddress = 0x1354B80;
    public const int SpriteLength = 352;
    public const uint DebrisTableAddress = 0x1362730;
    public const uint CameraAddress = 0x1354A30;
    public const int CameraLength = 332;
    public const uint SoundAddress = 0x1354B7C;
    public const uint TextureNameAddress = 0x13627D8;
    public const int TextureLength = 213536;

    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, 20, 1), new(0x2545110, ArenaAddress, 88, 32), new(0x2545120, ArenaAddress + 0xB00, 52, 16), new(0x2545130, ArenaAddress + 0xE40, 32, 32), new(0x2545140, ArenaAddress + 0x1240, 76, 1)];
}
