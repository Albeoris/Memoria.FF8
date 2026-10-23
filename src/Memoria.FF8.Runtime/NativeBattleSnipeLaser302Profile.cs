using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleSnipeLaser302Profile
{
    public const ushort Effect = 302;
    public const uint Entry = 0x66CA50;
    public const uint Initializer = 0x66CA60;
    public const uint RootCallback = 0x66CB00;
    public const uint RootDescriptor = 0x25076C8;
    public const uint RootBank = 0x2507670;
    public const int RootBankLength = 20;
    public const uint BaseAddress = 0x2507660;
    public const uint CasterGlobal = BaseAddress;
    public const uint InitializerGlobal = 0x2507664;
    public const uint ArenaGlobal = 0x2507668;
    public const uint WorkingGlobal = ArenaGlobal;
    public const uint ParametersGlobal = 0x250766C;
    public const uint CursorGlobal = 0x2507684;
    public const uint ArenaAddress = 0x20DFAB8;
    public const int ScratchOffset = 0x1588;
    public const int ScratchLength = 392;
    public const int FirstPacketOffset = 0x1710;
    public const int SecondPacketOffset = 0x11710;
    public const int PacketBankBytes = 0x10000;
    public const uint ProgrammeAAddress = 0x1036F60;
    public const int ProgrammeALength = 11196;
    public const int ProgrammeAWorkspaceLength = 944;
    public const uint ProgrammeBAddress = 0x1039B1C;
    public const int ProgrammeBLength = 4704;
    public const int ProgrammeBWorkspaceLength = 232;
    public const uint ProgrammeCAddress = 0x1035CF4;
    public const int ProgrammeCLength = 4716;
    public const int ProgrammeCWorkspaceLength = 432;
    public const uint ProgrammeDAddress = 0x103AD7C;
    public const int ProgrammeDLength = 2968;
    public const int ProgrammeDWorkspaceLength = 124;
    public const uint StaticMeshAddress = 0x103462C;
    public const int StaticMeshLength = 5832;
    public const uint MeshAddress = StaticMeshAddress;
    public const int MeshLength = StaticMeshLength;
    public const uint SpriteAAddress = 0x1034270;
    public const int SpriteALength = 528;
    public const uint SpriteBAddress = 0x1034480;
    public const int SpriteBLength = 428;
    public const uint CameraAddress = 0x103B914;
    public const int CameraLength = 668;
    public const uint SoundAddress = 0x103BBB0;
    public const uint SourcePointsAddress = 0x103BBB8;
    public const uint SourcePointAddress = SourcePointsAddress;
    public const uint TextureNameAddress = 0x103BBC8;
    public const int TextureLength = 166944;

    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, 20, 1), new(0x2507688, ArenaAddress, 168, 8), new(0x2507698, ArenaAddress + 0x540, 36, 16), new(0x25076A8, ArenaAddress + 0x780, 20, 16), new(0x25076B8, ArenaAddress + 0x8C0, 1636, 2)];
}
