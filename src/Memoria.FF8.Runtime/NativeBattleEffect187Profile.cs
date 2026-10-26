using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect187Profile
{
    public const ushort Effect = 187;
    public const uint ArenaAddress = 0x20DFAB8;
    public const uint Initializer = 0x575570;
    public const uint Entry = 0x6472E0;
    public const uint RootCallback = 0x64DD50;
    public const uint MainCallback = 0x6474C0;
    public const uint RootDescriptor = 0x24FD6C0;
    public const uint RootBank = 0x24FD6A0;
    public const uint ChildDescriptor = 0x24FD990;
    public const uint ChildBank = 0x24FD9A0;
    public const uint ArenaGlobal = 0x24FE8E0;
    public const uint BaseMatrix = 0x24FE880;
    public const uint ViewMatrix = 0x24FE8A0;
    public const uint CameraAddress = 0xE41F28;
    public const int CameraLength = 324;
    public const uint VibrationAddress = 0xE41CE8;
    public const int VibrationLength = 342;
    public const uint TextureAddress = 0xFAD4A8;
    public const int TextureLength = 168224;
    public const uint StreamAddress = 0xFA54A4;
    public const uint SoundAddress = 0xE41E40;
    public const uint BaseAddress = 0xE41E50;
    public static readonly ImmutableArray<uint> MeshAddresses = [0xE40848, 0xE40160, 0xE41140, 0xE41B28, 0xE3EFF0, 0xE41788, 0xE413E8, 0xE3D3A8];
    public static readonly ImmutableArray<int> MeshLengths = [2296, 1768, 680, 448, 3448, 928, 928, 928];
    public static readonly ImmutableArray<uint> SpriteAddresses = [0xE3CFD0, 0xE3CFF8, 0xE3D300, 0xE3CC38, 0xE3D154];
    public static readonly ImmutableArray<int> SpriteLengths = [40, 348, 168, 428, 428];
    public static readonly ImmutableArray<int> SpriteFrames = [1, 9, 6, 16, 16];
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, 16, 2), new(ChildDescriptor, ChildBank, 36, 100)];
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges = [(0xE41E50, 12), (0xE41E98, 4), (0xFA94A8, 0x4000), (0x24FD458, 0x248), (0x24FD6D0, 0x208), (0x24FD978, 24), (0x24FE7B0, 0x13C)];
}
