using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect319Profile
{
    public const ushort Effect = 319;
    public const uint Entry = 0x65DB30;
    public const uint Initializer = 0x65DB40;
    public const uint RootCallback = 0x65DBE0;
    public const uint MainCallback = 0x65DDA0;
    public const uint BaseAddress = 0x25053A0;
    public const uint InitializerGlobal = BaseAddress;
    public const uint CursorGlobal = 0x25053A4;
    public const uint ParametersGlobal = 0x25053A8;
    public const uint CasterGlobal = 0x25053AC;
    public const uint SpriteDescriptor = 0x25053B0;
    public const uint BoneDescriptor = 0x25053C0;
    public const uint MainDescriptor = 0x25053D0;
    public const uint RootDescriptor = 0x25053E0;
    public const uint RootBank = 0x25053F0;
    public const int RootBankLength = 20;
    public const uint ArenaGlobal = 0x2505410;
    public const uint TextureNameAddress = 0xFF7798;
    public const uint CameraAddress = 0xFF75D0;
    public const uint SoundAddress = 0xFF75CC;
    public const int IoOffset = 0xAA4;
    public const int MorphOffset = 0xAEC;
    public const int FirstPacketOffset = 0xD1C;
    public const int SecondPacketOffset = 0x10D1C;
    public const int ThirdPacketOffset = 0x20D1C;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, RootBankLength, 1)];
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> CommonPools = [new(MainDescriptor, 0, 0x898, 1), new(BoneDescriptor, 0x898, 0x1C, 5), new(SpriteDescriptor, 0x924, 0x18, 16)];
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges = [(BaseAddress, 16), (ArenaGlobal, 4)];
}
