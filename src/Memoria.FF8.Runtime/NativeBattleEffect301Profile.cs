using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect301Profile
{
    public const ushort Effect = 301;
    public const uint Entry = 0x66D8E0;
    public const uint Initializer = 0x66D8F0;
    public const uint RootCallback = 0x66D9F0;
    public const uint MainCallback = 0x66DBD0;
    public const uint BaseAddress = 0x25076D8;
    public const uint InitializerGlobal = BaseAddress;
    public const uint ParametersGlobal = 0x25076DC;
    public const uint CasterGlobal = 0x25076E0;
    public const uint SpriteDescriptor = 0x25076E8;
    public const uint MeshDescriptor = 0x25076F8;
    public const uint MainDescriptor = 0x2507708;
    public const uint RootDescriptor = 0x2507718;
    public const uint RootBank = 0x2507728;
    public const int RootBankLength = 20;
    public const uint CursorGlobal = 0x250773C;
    public const uint SavedCasterFlagsGlobal = 0x2507740;
    public const uint SavedPartyFlagsAddress = 0x2507744;
    public const uint ArenaGlobal = 0x2507754;
    public const uint TextureNameAddress = 0x103DCA0;
    public const uint CameraAddress = 0x103DB5C;
    public const uint VibrationAddress = 0x103BBD8;
    public const uint SoundAddress = 0x103DC98;
    public const uint ScriptPointerAddress = 0x103DC9C;
    public const uint ProgrammeAddress = 0x103D46C;
    public const int MorphOffset = 0x1494;
    public const int MorphLength = 0x4D70;
    public const int FirstPacketOffset = 0x6204;
    public const int SecondPacketOffset = 0x16204;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, RootBankLength, 1)];
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> CommonPools = [new(MainDescriptor, 0, 0xE14, 1), new(MeshDescriptor, 0xE14, 0x1C, 32), new(SpriteDescriptor, 0x1194, 0x18, 32)];
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges = [(BaseAddress, 12), (CursorGlobal, 20), (ArenaGlobal, 4)];
}
