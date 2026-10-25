using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect320Profile
{
    public const string TextureName = "mag319.tim";
    public const uint TextureNameAddress = 0xFF12B0;
    public const int TextureLength = 167456;
    public const uint CameraAddress = 0xFE729C;
    public const int CameraLength = 468;
    public const int CameraModelOffset = 32;
    public const uint SpriteAddress = 0xFE7470;
    public const int SpriteLength = 428;
    public const uint SoundAddress = 0xFE7298;
    public const int MorphLength = 960;
    public const int MaximumVertices = 120;
    public const uint Programme1Address = 0xFE761C;
    public const uint Programme2Address = 0xFE9294;
    public const uint Programme3Address = 0xFE9BCC;
    public static readonly ImmutableArray<uint> ProgrammeAddresses = [0xFE761C, 0xFE9294, 0xFE9BCC];
    public static readonly ImmutableArray<int> ProgrammeLengths = [7288, 2360, 30416];
    public static readonly ImmutableArray<int> ProgrammeWorkspaceLengths = [496, 272, 1044];
    public static readonly ImmutableArray<uint> GeometryAddresses = [0xFE7638, 0xFE7F34, 0xFE81A8, 0xFE879C, 0xFE8A10, 0xFE92A0, 0xFE9C00, 0xFE9E74, 0xFEA0E8, 0xFEABB4, 0xFEB1A8, 0xFEC3A4, 0xFEC9D0, 0xFEDA8C, 0xFEE140, 0xFEF1FC, 0xFEFFD8];
    public static readonly ImmutableArray<int> GeometryLengths = [2300, 628, 1524, 628, 628, 1580, 628, 628, 2764, 1524, 4604, 1580, 4284, 1716, 4284, 3548, 1524];
    public static readonly ImmutableArray<(uint Address, int Length)> StaticRanges = [(SoundAddress, 4), (0xFE0FAC, 5), (0xFE0FB4, 1), (0xFE0FC8, 1), (0xFF12A0, 16), (TextureNameAddress, 11), (0xFF12BC, 11), (0xFF12C8, 11), (0xFF12D4, 5)];
}
