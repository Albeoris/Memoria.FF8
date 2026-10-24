using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect323Profile
{
    public const ushort Effect = 323;
    public const uint Entry = 0x659A80;
    public const uint Initializer = 0x659A90;
    public const uint RootCallback = 0x659B30;
    public const uint MainCallback = 0x659DD0;
    public const uint BaseAddress = 0x2505230;
    public const uint InitializerGlobal = 0x2505230;
    public const uint ParametersGlobal = 0x2505234;
    public const uint CasterGlobal = 0x2505238;
    public const uint ParticleDescriptor = 0x2505240;
    public const uint ProgrammeDescriptor = 0x2505250;
    public const uint MeshDescriptor = 0x2505260;
    public const uint MainDescriptor = 0x2505270;
    public const uint RootDescriptor = 0x2505280;
    public const uint RootBank = 0x2505290;
    public const int RootBankLength = 20;
    public const uint CursorGlobal = 0x25052A4;
    public const uint ArenaGlobal = 0x25052A8;
    public const uint TextureNameAddress = 0xFE429C;
    public const uint CameraAddress = 0xFE0FCC;
    public const uint SoundAddress = 0xFE12B0;
    public const uint ProgrammeAddress = 0xFE2BE4;
    public const int WorkspaceOffset = 0x18;
    public const int WorkspaceLength = 0x270;
    public const int FirstPacketOffset = 0x1824;
    public const int SecondPacketOffset = 0x11824;
    public const int MorphOffset = 0x1754;
    public const int MorphLength = 208;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, RootBankLength, 1)];
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> CommonPools = [new(MainDescriptor, 0, 0xBC, 1), new(MeshDescriptor, 0xBC, 0x30, 32), new(ProgrammeDescriptor, 0x6BC, 0x288, 3), new(ParticleDescriptor, 0xE54, 0x24, 64)];
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges = [(BaseAddress, 12), (CursorGlobal, 8)];
}
