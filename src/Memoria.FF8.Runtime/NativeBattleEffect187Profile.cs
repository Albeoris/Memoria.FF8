using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect187Profile
{
    public const ushort Effect = 187;
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
    public static readonly ImmutableArray<uint> MeshAddresses = [0xE40848, 0xE40160, 0xE41140, 0xE41B28, 0xE3EFF0];
    public static readonly ImmutableArray<int> MeshLengths = [2296, 1768, 680, 448, 3448];
    public static readonly ImmutableArray<uint> SpriteAddresses = [0xE3CFD0, 0xE3CFF8, 0xE3D300, 0xE3CC38, 0xE3D154];
    public static readonly ImmutableArray<int> SpriteLengths = [40, 348, 168, 428, 428];
    public static readonly ImmutableArray<int> SpriteFrames = [1, 9, 6, 16, 16];
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, 16, 2), new(ChildDescriptor, ChildBank, 36, 100)];
}
