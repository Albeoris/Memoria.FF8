using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect210Profile
{
    public const ushort Effect = 210;
    public const uint Entry = 0x6F6800;
    public const uint Initializer = 0x5755D0;
    public const uint RootCallback = 0x6F6890;
    public const uint MainCallback = 0x6F7310;
    public const uint BaseAddress = 0x2556478;
    public const uint FrozenGlobal = 0x2556478;
    public const uint CursorGlobal = 0x255647C;
    public const uint SecondCursorGlobal = 0x2556480;
    public const uint ParametersGlobal = 0x2556484;
    public const uint CasterGlobal = 0x2556488;
    public const uint ActDescriptor = 0x2556490;
    public const uint EchoDescriptor = 0x25564A0;
    public const uint MeshDescriptor = 0x25564B0;
    public const uint SpriteDescriptor = 0x25564C0;
    public const uint MainDescriptor = 0x25564D0;
    public const uint RootDescriptor = 0x25564E0;
    public const uint RootBank = 0x25564F0;
    public const int RootBankLength = 16;
    public const uint RetainedPairAddress = 0x2556520;
    public const uint SavedPositionAddress = 0x2556530;
    public const uint CenterAddress = 0x2556538;
    public const uint BrightnessGlobal = 0x2556540;
    public const uint EarlyDescriptor = 0x2556548;
    public const uint ArenaGlobal = 0x2556558;
    public const uint ViewAddress = 0x2556560;
    public const uint TimAddress = 0x1470DC4;
    public const int TimLength = 132640;
    public const uint CameraAddress = 0x145D2BC;
    public const int CameraLength = 544;
    public const int CameraModelOffset = 24;
    public const uint SoundAddress = 0x145D4DC;
    public const uint SelectorsAddress = 0x14708AC;
    public const uint MainSelectorsAddress = 0x14913E4;
    public const int CommonQueueLength = 8;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, 16, 1)];
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> FixedPools = Pools;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> CommonPools = [new(ActDescriptor, 0, 0x714, 3), new(EchoDescriptor, 0x1BBC, 0x34C, 1), new(MeshDescriptor, 0x183C, 0x1C, 32), new(SpriteDescriptor, 0x153C, 0x18, 32), new(MainDescriptor, 0xA750, 0x3C0, 1), new(EarlyDescriptor, 0, 0x24, 150)];
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges = [(BaseAddress, 20), (RetainedPairAddress, 8), (SavedPositionAddress, 14), (BrightnessGlobal, 4), (ArenaGlobal, 4), (ViewAddress, 32)];
    public static readonly ImmutableArray<uint> ActAddresses = [0x146D8A4, 0x1462010, 0x146A668, 0x146ED2C];
    public static readonly ImmutableArray<int> ActLengths = [5256, 34392, 12860, 7040];
    public static readonly ImmutableArray<int> ActWorkspaceLengths = [0xE0, 0x244, 0x258, 0x1C0];
    public static readonly ImmutableArray<int> ActGeometryCounts = [2, 7, 3, 3];
    public static readonly ImmutableArray<uint> MeshAddresses = [0x145D6AC, 0x1461D98, 0x145E1C4, 0x14603F4];
    public static readonly ImmutableArray<int> MeshLengths = [2840, 632, 8752, 6564];
    public static readonly ImmutableArray<uint> SpriteAddresses = [0x14708C0, 0x1470A6C, 0x1470C18, 0x145D4EC];
    public static readonly ImmutableArray<int> SpriteLengths = [428, 428, 428, 448];
    public static readonly ImmutableArray<uint> GeometryAddresses = [0x146D8B4, 0x146E1F0, 0x1462034, 0x1462F60, 0x146410C, 0x14657B8, 0x1465FAC, 0x1469008, 0x1469ACC, 0x146A67C, 0x146C5E8, 0x146D0AC, 0x146ED40, 0x146F5DC, 0x146FD40];
    public static readonly ImmutableArray<int> GeometryLengths = [2364, 2124, 3884, 4524, 5804, 2036, 12380, 2756, 788, 8044, 2756, 844, 2204, 1892, 1484];
    public static readonly ImmutableArray<(uint Address, int Length)> StaticRanges = [(SoundAddress, 16), (SelectorsAddress, 3), (MainSelectorsAddress, 7)];
}
