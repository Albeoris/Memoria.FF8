using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect251Profile
{
    public const ushort Effect = 251;
    public const uint Entry = 0x6F44D0;
    public const uint Initializer = 0x5755C0;
    public const uint RootCallback = 0x6F4580;
    public const uint MainCallback = 0x6F5000;
    public const uint BurstCallback = 0x6F4890;
    public const uint BaseAddress = 0x255638C;
    public const uint CursorGlobal = BaseAddress;
    public const uint ParametersGlobal = 0x2556390;
    public const uint CasterGlobal = 0x2556394;
    public const uint TargetGlobal = 0x2556398;
    public const uint SpriteDescriptor = 0x25563D0;
    public const uint TrailDescriptor = 0x25563B0;
    public const uint ActDescriptor = 0x25563A0;
    public const uint MainDescriptor = 0x25563E0;
    public const uint RootDescriptor = 0x25563F0;
    public const uint RootBank = 0x2556400;
    public const int RootBankLength = 16;
    public const uint StageAmountGlobal = 0x255643C;
    public const uint BurstDescriptor = 0x2556440;
    public const uint ArenaGlobal = 0x2556450;
    public const uint SavedViewAddress = 0x2556458;
    public const uint PauseAddress = 0x2556388;
    public const int StorageLength = 16 + 4 + 4 + 32;
    public const int CommonQueueLength = 8;
    public const int IoOffset = 0xA24C;
    public const int FirstPacketOffset = 0xB300;
    public const int SecondPacketOffset = 0x1B300;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, RootBankLength, 1)];
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> FixedPools = Pools;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> CommonPools = [new(MainDescriptor, 0xA294, 0x5EC, 1), new(BurstDescriptor, 0, 0x24, 150), new(TrailDescriptor, 0x1700, 0x34C, 1), new(ActDescriptor, 0, 0x158, 16), new(SpriteDescriptor, 0x1580, 0x18, 16)];
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges = [(BaseAddress, 16), (StageAmountGlobal, 4), (ArenaGlobal, 4), (SavedViewAddress, 32)];
}
