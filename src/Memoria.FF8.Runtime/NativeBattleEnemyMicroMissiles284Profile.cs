using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEnemyMicroMissiles284Profile
{
    public const ushort Effect = 284;
    public const uint Entry = 0x67B7A0;
    public const uint Initializer = 0x67B7B0;
    public const uint RootCallback = 0x67B850;
    public const uint RootDescriptor = 0x2507DF0;
    public const uint RootBank = 0x2507E00;
    public const int RootBankLength = 20;
    public const uint BaseAddress = 0x2507DB0;
    public const uint InitializerGlobal = BaseAddress;
    public const uint ParametersGlobal = 0x2507DB4;
    public const uint CasterGlobal = 0x2507DB8;
    public const uint CursorGlobal = 0x2507E14;
    public const uint ArenaGlobal = 0x2507E18;
    public const uint WorkingGlobal = ArenaGlobal;
    public const uint ArenaAddress = 0x20DFAB8;
    public const int ScratchOffset = 0x45A8;
    public const int ScratchLength = 512;
    public const int FirstPacketOffset = 0x47A8;
    public const int SecondPacketOffset = 0x147A8;
    public const int PacketBankBytes = 0x10000;
    public const uint MissileAddress = 0x108FEE4;
    public const int MissileLength = 9620;
    public const int MissileWorkspaceLength = 2020;
    public const uint ImpactAddress = 0x1092478;
    public const int ImpactLength = 4820;
    public const int ImpactWorkspaceLength = 160;
    public const uint WideImpactAddress = 0x109374C;
    public const int WideImpactLength = 5720;
    public const int WideImpactWorkspaceLength = 276;
    public const uint ParticleSpriteAddress = 0x108FAD0;
    public const int ParticleSpriteLength = 324;
    public const uint TrailSpriteAddress = 0x108FC14;
    public const int TrailSpriteLength = 324;
    public const uint ImpactSpriteAddress = 0x108FD58;
    public const int ImpactSpriteLength = 396;
    public const uint CameraAddress = 0x1094DA4;
    public const int CameraLength = 500;
    public const uint SoundAddress = 0x1094F98;
    public const uint TextureNameAddress = 0x104E320;
    public const int TextureLength = 167456;

    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, 20, 1), new(0x2507DC0, ArenaAddress, 36, 96), new(0x2507DD0, ArenaAddress + 0xD80, 324, 6), new(0x2507DE0, ArenaAddress + 0x1518, 2072, 6)];
}
