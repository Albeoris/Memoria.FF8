using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect326Profile
{
    public const ushort Effect = 326;
    public const uint ArenaAddress = 0x20DFAB8;
    public const uint Initializer = 0x575540;
    public const uint Entry = 0x62A7E0;
    public const uint RootCallback = 0x6326B0;
    public const uint MainCallback = 0x62A9C0;
    public const uint RootDescriptor = 0x24F0ED8;
    public const uint RootBank = 0x24F0EB8;
    public const uint ChildDescriptor = 0x24F1320;
    public const uint ChildBank = 0x24F1330;
    public const uint ArenaGlobal = 0x24F22DC;
    public const uint BaseMatrix = 0x24F2248;
    public const uint ViewMatrix = 0x24F2268;
    public const uint CameraAddress = 0xE197D0;
    public const int CameraLength = 438;
    public const uint VibrationAddress = 0xE194E4;
    public const int VibrationLength = 360;
    public const uint TextureAddress = 0xEF4C30;
    public const int TextureLength = 168224;
    public const uint StreamAddress = 0xEECC2C;
    public const uint SoundAddress = 0xE1964C;
    public const uint PixelSource = 0xEECC30;
    public const uint PixelOutput = 0xEF0C30;
    public const uint EmbeddedModelAddress = 0xE18CF8;
    public const int EmbeddedModelLength = 0x7EC;
    public static readonly ImmutableArray<uint> MeshAddresses = [0xE163F8, 0xE15D10, 0xE17450, 0xE16CF0, 0xE17BB0, 0xE18150, 0xE18B38, 0xE148E8, 0xE11798, 0xE13040, 0xE18798, 0xE183F8, 0xE113F8, 0xE11058];
    public static readonly ImmutableArray<int> MeshLengths = [2296, 1768, 1888, 1888, 1440, 680, 448, 3448, 6312, 6312, 928, 928, 928, 928];
    public static readonly ImmutableArray<uint> SpriteAddresses = [0xE10C80, 0xE10CA8, 0xE10FB0, 0xE108E8, 0xE10E04];
    public static readonly ImmutableArray<int> SpriteLengths = [40, 348, 168, 428, 428];
    public static readonly ImmutableArray<int> SpriteFrames = [1, 9, 6, 16, 16];
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges = [(0xE19668, 12), (0xE196B0, 4), (0x24F0BD0, 0x180), (0x24F0DE0, 0xD8), (0x24F0F78, 0x2F0), (0x24F12F8, 0x28), (0x24F2174, 0x134), (0x24F22DC, 12)];
    public static readonly ImmutableArray<(uint Model, uint Control)> ModelBindings = [(0x24F1268, 0x24F22E8), (0x24F0EE8, 0x24F2140), (0x24F0D50, 0x24F22A8)];
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, 16, 2), new(ChildDescriptor, ChildBank, 36, 100)];
    public static readonly ImmutableArray<(uint Address, int Length)> StaticRanges = [(0xE19674, 24), (0xE196A8, 8), (0xE196B8, 8), (0xE196C0, 16), (0xE19710, 32), (0xE19730, 16), (0xE19740, 12), (0xE19790, 32), (0xE197B0, 32), (0xE19988, 224), (0xE15668, 1008)];
}
