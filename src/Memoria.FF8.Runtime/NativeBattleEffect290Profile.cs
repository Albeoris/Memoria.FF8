using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect290Profile
{
    public const ushort Effect = 290;
    public const uint Entry = 0x675280;
    public const uint Initializer = 0x675290;
    public const uint RootCallback = 0x675340;
    public const uint MainCallback = 0x6767B0;
    public const uint BaseAddress = 0x2507B38;
    public const uint InitializerGlobal = BaseAddress;
    public const uint ParametersGlobal = 0x2507B3C;
    public const uint CasterGlobal = 0x2507B40;
    public const uint ScriptDescriptor = 0x2507B48;
    public const uint SpriteDescriptor = 0x2507B58;
    public const uint ParticleDescriptor = 0x2507B68;
    public const uint TargetDescriptor = 0x2507B78;
    public const uint MainDescriptor = 0x2507B88;
    public const uint RootDescriptor = 0x2507B98;
    public const uint RootBank = 0x2507BA8;
    public const int RootBankLength = 20;
    public const uint CursorGlobal = 0x2507BBC;
    public const uint InternalRandomGlobal = 0x2507BC0;
    public const uint BlendColorAddress = 0x2507BC4;
    public const uint ArenaGlobal = 0x2507BC8;
    public const uint TextureNameAddress = 0x106E894;
    public const uint CameraAddress = 0x106A2DC;
    public const uint SoundAddress = 0x106A39C;
    public const uint VibrationAddress = 0x106A3A0;
    public const uint FirstProgrammeAddress = 0x1067CB8;
    public const uint SecondProgrammeAddress = 0x106971C;
    public const uint ThirdProgrammeAddress = 0x10688C4;
    public const uint FirstSpriteAddress = 0x10676E0;
    public const uint SecondSpriteAddress = 0x1067B58;
    public const uint MainSpriteAddress = 0x106784C;
    public const uint SmokeSourceAddress = 0x10679F8;
    public const uint TrigonometryAddress = 0x106A4C8;
    public const int TrigonometryLength = 16384;
    public const uint NormalizationAddress = 0x106E4C8;
    public const int NormalizationLength = 384;
    public const int IoOffset = 0x85C0;
    public const int FirstPacketOffset = 0x8608;
    public const int SecondPacketOffset = 0x14608;
    public const int File360Offset = 0x20608;
    public const int MorphOffset = 0x2060C;
    public const int MorphLength = 640;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, RootBankLength, 1)];
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> CommonPools = [new(MainDescriptor, 0x1CDC, 0x68E4, 1), new(TargetDescriptor, 0x1970, 0x124, 3), new(ParticleDescriptor, 0x6F0, 0x128, 16), new(SpriteDescriptor, 0x3F0, 0x18, 32), new(ScriptDescriptor, 0, 0x24, 28)];
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges = [(BaseAddress, 12), (CursorGlobal, 16)];
}
