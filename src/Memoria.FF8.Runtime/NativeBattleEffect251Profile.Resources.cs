using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect251Profile
{
    public const uint EmbeddedTimAddress = 0x143CC90;
    public const int EmbeddedTimLength = 132640;
    public const uint CameraAddress = 0x142D5AC;
    public const int CameraLength = 396;
    public const uint TrigonometryAddress = 0x1438C90;
    public const int TrigonometryLength = 16384;
    public const int StreamFileId = 713;
    public const string StreamFileName = "mag250_h.00";
    public const int StreamFileLength = 168480;
    public static readonly ImmutableArray<uint> ProgrammeAddresses = [0x142fb40, 0x1431f88, 0x1433318, 0x1432448, 0x142d8a4, 0x1431418, 0x1434430];
    public static readonly ImmutableArray<int> ProgrammeLengths = [6360, 1216, 4376, 3792, 8860, 2928, 17224];
    public static readonly ImmutableArray<int> ProgrammeWorkspaceLengths = [456, 192, 416, 392, 520, 324, 588];
    public static readonly ImmutableArray<int> ProgrammeDurations = [20, 18, 25, 14, 20, 10, 95];
    public static readonly ImmutableArray<uint> GeometryAddresses = [0x142fb50, 0x143054c, 0x1431f94, 0x1433328, 0x1433b14, 0x143245c, 0x1432938, 0x1432bac, 0x142d8bc, 0x142e6f8, 0x142e96c, 0x142ebe0, 0x1431428, 0x1431904, 0x1434448, 0x14370b4, 0x1437ae0, 0x1437f14];
    public static readonly ImmutableArray<int> GeometryLengths = [2556, 2604, 628, 2028, 628, 1244, 628, 628, 3644, 628, 628, 2156, 1244, 628, 11372, 2604, 1076, 628];
    public static readonly ImmutableArray<uint> SpriteAddresses = [0x143878c, 0x1438938, 0x1438ae4, 0x142d744, 0xc79220];
    public static readonly ImmutableArray<int> SpriteLengths = [428, 428, 428, 352, 292];
    public static readonly ImmutableArray<int> SpriteFrames = [16, 16, 16, 13, 10];
    public static readonly ImmutableArray<uint> SoundAddresses = [0x142D738, 0x142D73C, 0x142D740];
    public static readonly ImmutableArray<(uint Address, int Length)> ScriptRanges = [(0x1434430, 17224)];
    public static readonly ImmutableArray<(uint Address, int Length)> StaticRanges = [(0x1438778, 4), (0x145D2B0, 4), (0x145D2B4, 4), (TrigonometryAddress, TrigonometryLength), (0x142D738, 12)];
}
