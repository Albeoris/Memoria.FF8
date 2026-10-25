using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect308Profile
{
    public const string TextureName = "mag307.tim";
    public const int TextureLength = 232992;
    public const int CameraLength = 288;
    public const int CameraModelOffset = 24;
    public const int VibrationLength = 300;
    public const uint ProgrammeDirectoryAddress = 0x102A400;
    public const uint SpriteTableAddress = 0x10262F8;
    public const uint NormalizationAddress = 0x102E3A0;
    public const uint NormalizationNonzeroAddress = 0x102E420;
    public const int NormalizationNonzeroLength = 384;
    public const int MorphLength = 544;
    public const int MaximumVertices = 68;
    public const int PrivatePacketUpperBound = 51992;
    public static readonly ImmutableArray<uint> ProgrammeAddresses = [0x1026308, 0x1027944, 0x1028be4, 0x10297f0];
    public static readonly ImmutableArray<int> ProgrammeLengths = [5692, 4768, 3084, 3084];
    public static readonly ImmutableArray<int> ProgrammeWorkspaceLengths = [376, 304, 272, 272];
    public static readonly ImmutableArray<uint> GeometryAddresses = [0x1026318, 0x1026e44, 0x1027954, 0x1027f48, 0x1028bf4, 0x10291e8, 0x1029800, 0x1029df4];
    public static readonly ImmutableArray<int> GeometryLengths = [2860, 1524, 1524, 2308, 1524, 628, 1524, 628];
    public static readonly ImmutableArray<uint> SpriteAddresses = [0x1025d20, 0x1025e8c, 0x1026198];
    public static readonly ImmutableArray<int> SpriteLengths = [364, 428, 352];
    public static readonly ImmutableArray<int> SpriteFrames = [12, 16, 13];
    public static readonly ImmutableArray<uint> ScriptCounterAddresses = [0x102e5a6, 0x102e5be, 0x102e5d6, 0x102e6ae, 0x102e72c, 0x102e7b0, 0x102e82c, 0x102e83a, 0x102e8ba, 0x102e936];
    public static readonly ImmutableArray<byte> SpriteSelectors = [0, 1, 3];
    public static readonly ImmutableArray<(uint Address, int Length)> StaticRanges = [(SoundAddress, 4), (ProgrammeDirectoryAddress, 32), (SpriteTableAddress, 8), (SpriteTableAddress + 12, 4), (NormalizationAddress, 2), (NormalizationNonzeroAddress, NormalizationNonzeroLength), (TextureNameAddress, 11)];
}
