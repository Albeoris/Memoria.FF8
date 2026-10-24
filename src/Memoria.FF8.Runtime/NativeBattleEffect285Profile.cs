using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect285Profile
{
    public const ushort Effect = 285;
    public const uint Entry = 0x67A860;
    public const uint Initializer = 0x67A870;
    public const uint RootCallback = 0x67A900;
    public const uint MainCallback = 0x67AC00;
    public const uint ParticleCallback = 0x67B720;
    public const uint BaseAddress = 0x2507D08;
    public const uint InitializerGlobal = 0x2507D08;
    public const uint ParametersGlobal = 0x2507D0C;
    public const uint CasterGlobal = 0x2507D10;
    public const uint ParticleDescriptor = 0x2507D18;
    public const uint MainDescriptor = 0x2507D28;
    public const uint RootDescriptor = 0x2507D38;
    public const uint RootBank = 0x2507D48;
    public const int RootBankLength = 20;
    public const uint CentroidGlobal = 0x2507D5C;
    public const uint CursorGlobal = 0x2507D60;
    public const uint IntensityGlobal = 0x2507D64;
    public const uint ArenaGlobal = 0x2507D68;
    public const uint ViewAddress = 0x2507D70;
    public const uint SavedFlagsAddress = 0x2507D90;
    public const uint TextureNameAddress = 0x108FAC0;
    public const uint FirstProgrammeAddress = 0x1084BBC;
    public const uint SecondProgrammeAddress = 0x1089B00;
    public const int FirstWorkspaceOffset = 0x10;
    public const int FirstWorkspaceLength = 0x944;
    public const int SecondWorkspaceOffset = 0x954;
    public const int SecondWorkspaceLength = 0xC64;
    public const int MorphOffset = 0x1AB8;
    public const int MorphLength = 896;
    public const int IoOffset = 0x1E38;
    public const int FirstPacketOffset = 0x1E80;
    public const int SecondPacketOffset = 0x11E80;
    public const int ThirdPacketOffset = 0x21E80;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, RootBankLength, 1)];
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> CommonPools = [new(MainDescriptor, 0x500, 0x15B8, 1), new(ParticleDescriptor, 0, 20, 64)];
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges = [(BaseAddress, 12), (CentroidGlobal, 2), (CursorGlobal, 12), (ViewAddress, 32), (SavedFlagsAddress, 28)];
}
