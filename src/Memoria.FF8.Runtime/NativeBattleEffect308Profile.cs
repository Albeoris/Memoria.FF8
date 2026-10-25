using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect308Profile
{
    public const ushort Effect = 308;
    public const uint Entry = 0x667670;
    public const uint Initializer = 0x667680;
    public const uint RootCallback = 0x667720;
    public const uint MainCallback = 0x668DE0;
    public const uint ScriptCallback = 0x667990;
    public const uint BaseAddress = 0x25074D0;
    public const uint InitializerGlobal = BaseAddress;
    public const uint ParametersGlobal = 0x25074D4;
    public const uint CasterGlobal = 0x25074D8;
    public const uint ScriptDescriptor = 0x25074E0;
    public const uint ParticleDescriptor = 0x25074F0;
    public const uint ActDescriptor = 0x2507500;
    public const uint MainDescriptor = 0x2507510;
    public const uint RootDescriptor = 0x2507520;
    public const uint RootBank = 0x2507530;
    public const int RootBankLength = 20;
    public const uint CursorGlobal = 0x2507544;
    public const uint InternalRandomGlobal = 0x2507548;
    public const uint BlendColorAddress = 0x250754C;
    public const uint ArenaGlobal = 0x2507550;
    public const int StorageLength = 28;
    public const int CommonQueueLength = 8;
    public const uint TextureNameAddress = 0x102E940;
    public const uint CameraAddress = 0x10257E8;
    public const uint VibrationAddress = 0x102590C;
    public const uint SoundAddress = 0x1025908;
    public const int IoOffset = 0x29D0;
    public const int FirstPacketOffset = 0x2A18;
    public const int SecondPacketOffset = 0x12A18;
    public const int MorphOffset = 0x22A18;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, RootBankLength, 1)];
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> FixedPools = Pools;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> CommonPools = [new(MainDescriptor, 0x1B3C, 0xE94, 1), new(ActDescriptor, 0xEBC, 0x190, 8), new(ParticleDescriptor, 0x7BC, 0x1C, 64), new(ScriptDescriptor, 0, 0x2C, 45)];
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges = [(BaseAddress, 12), (CursorGlobal, 16)];
    public static readonly ImmutableArray<(uint Address, int Length)> ScriptRanges = [(0x102E5A0, 404), (0x102E734, 256), (0x102E834, 266)];
}
