using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEvilEye282Profile
{
    public const ushort Effect = 282;
    public const uint Entry = 0x67D400;
    public const uint Initializer = 0x67D410;
    public const uint RootCallback = 0x67D4C0;
    public const uint MainCallback = 0x67D680;
    public const uint RootDescriptor = 0x2507ED0;
    public const uint RootBank = 0x2507EE0;
    public const int RootBankLength = 20;
    public const uint BaseAddress = 0x2507E90;
    public const uint InitializerGlobal = BaseAddress;
    public const uint ParametersGlobal = 0x2507E94;
    public const uint CasterGlobal = 0x2507E98;
    public const uint CursorGlobal = 0x2507EF4;
    public const uint SeedGlobal = 0x2507EF8;
    public const uint CueGlobal = 0x2507EFC;
    public const uint NormalizedViewAddress = 0x2507F08;
    public const uint ArenaGlobal = 0x2507F00;
    public const uint ArenaAddress = 0x20DFAB8;
    public const int ScratchOffset = 0x142C;
    public const int ScratchLength = 512;
    public const int FirstPacketOffset = 0x162C;
    public const int SecondPacketOffset = 0x1162C;
    public const int PacketBankBytes = 0x10000;
    public const uint ProgrammeAAddress = 0x10A0558;
    public const int ProgrammeALength = 10744;
    public const int ProgrammeAWorkspaceLength = 1352;
    public const uint ProgrammeBAddress = 0x109EC64;
    public const int ProgrammeBLength = 6388;
    public const int ProgrammeBWorkspaceLength = 384;
    public const uint SpriteAddress = 0x109EAB8;
    public const int SpriteLength = 428;
    public const uint CameraAddress = 0x10A2F54;
    public const int CameraLength = 192;
    public const uint SoundAddress = 0x10A2F50;
    public const uint SourcePointAddress = 0x10A3018;
    public const uint TextureNameAddress = 0x10A3020;
    public const int TextureLength = 166432;

    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, 20, 1), new(0x2507EA0, ArenaAddress, 20, 128), new(0x2507EB0, ArenaAddress + 0xA00, 408, 3), new(0x2507EC0, ArenaAddress + 0xEC8, 1380, 1)];
}
