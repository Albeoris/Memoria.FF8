using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleBoomerangSword304Profile
{
    public const ushort Effect = 304;
    public const uint Entry = 0x669790;
    public const uint Initializer = 0x6697A0;
    public const uint RootCallback = 0x669840;
    public const uint RootDescriptor = 0x25075C0;
    public const uint RootBank = 0x2507568;
    public const int RootBankLength = 20;
    public const uint BaseAddress = 0x2507558;
    public const uint CasterGlobal = BaseAddress;
    public const uint InitializerGlobal = 0x250755C;
    public const uint ArenaGlobal = 0x2507560;
    public const uint WorkingGlobal = ArenaGlobal;
    public const uint ParametersGlobal = 0x2507564;
    public const uint CursorGlobal = 0x250757C;
    public const uint ArenaAddress = 0x20DFAB8;
    public const int ScratchOffset = 0x1558;
    public const int ScratchLength = 408;
    public const int FirstPacketOffset = 0x16F0;
    public const int SecondPacketOffset = 0x116F0;
    public const int PacketBankBytes = 0x10000;
    public const uint ProgrammeAddress = 0x102F48C;
    public const int ProgrammeLength = 7740;
    public const int ProgrammeWorkspaceLength = 460;
    public const uint MeshAddress = 0x102EA98;
    public const int MeshLength = 2076;
    public const uint TrailMeshAddress = 0x102F2B4;
    public const int TrailMeshLength = 472;
    public const uint SpriteAddress = 0x102E94C;
    public const int SpriteLength = 332;
    public const uint CameraAddress = 0x10312CC;
    public const int CameraLength = 356;
    public const uint SoundAddress = 0x10312C8;
    public const uint TextureNameAddress = 0x1031430;
    public const int TextureLength = 170016;

    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, 20, 1), new(0x25075B0, ArenaAddress, 172, 1), new(0x25075A0, ArenaAddress + 0xAC, 48, 32), new(0x2507590, ArenaAddress + 0x6AC, 484, 3), new(0x2507580, ArenaAddress + 0xC58, 36, 64)];
}
