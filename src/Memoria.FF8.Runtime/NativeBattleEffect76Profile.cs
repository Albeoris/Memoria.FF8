using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect76Profile
{
    public const ushort Effect = 76;
    public const uint Entry = 0x61F9A0;
    public const uint Initializer = 0x61F980;
    public const uint RootCallback = 0x6225A0;
    public const uint MainCallback = 0x61FA80;
    public const uint DriverCallback = 0x61FCD0;
    public const uint BaseAddress = 0x24C3270;
    public const uint PointAddress = BaseAddress;
    public const uint TargetGlobal = 0x24C3278;
    public const uint RootBank = 0x24C3280;
    public const uint RootDescriptor = 0x24C3290;
    public const uint FirstRecordsAddress = 0x24C32A0;
    public const int RecordStride = 32;
    public const int FirstRecordCount = 100;
    public const uint SecondRecordsAddress = 0x24C3F20;
    public const int SecondRecordCount = 200;
    public const uint MusicCompletionGlobal = 0x24C5820;
    public const uint SavedPointAddress = 0x24C5828;
    public const uint ChildDescriptor = 0x24C5830;
    public const uint ChildBank = 0x24C5840;
    public const int ChildStride = 36;
    public const int ChildCount = 100;
    public const uint ParametersGlobal = 0x24C6650;
    public const uint InitializerGlobal = 0x24C6654;
    public const uint CasterGlobal = 0x24C6658;
    public const uint ViewAddress = 0x24C6660;
    public const uint CapturedWorldAddress = 0x24C6680;
    public const uint ArenaGlobal = 0x24C66A0;
    public const uint CursorGlobal = 0x24C66A4;
    public const uint VibrationGlobal = 0x24C66A8;
    public const uint TextureNameAddress = 0xDE4224;
    public const int TextureLength = 67104;
    public const uint CameraAddress = 0xDE4160;
    public const int CameraLength = 196;
    public const uint MusicAddress = 0xEAA7EC;
    public const int MusicLength = 138272;
    public const uint VibrationAddress = 0xDE3FF0;
    public const int VibrationLength = 344;
    public const uint SoundAddress = 0xDE4148;
    public const uint AlignmentAxisAddress = 0xDE4150;
    public const uint AudioDirectoryLiteralAddress = 0xDE4230;
    public const int StageFileId = 38;
    public const int StageFileLength = 77136;
    public const int StageDirectoryOffset = 0x137C;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, 16, 1), new(ChildDescriptor, ChildBank, ChildStride, ChildCount)];
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges = [(BaseAddress, 12), (FirstRecordsAddress, 0x2580), (MusicCompletionGlobal, 1), (SavedPointAddress, 8), (ParametersGlobal, 12), (ViewAddress, 0x4C)];
    public static readonly ImmutableArray<uint> MeshAddresses = [0xDE2DE0, 0xDE2EB8, 0xDE25E8, 0xDE27F0, 0xDE37A8, 0xDE1748, 0xDE2470];
    public static readonly ImmutableArray<int> MeshLengths = [216, 2288, 520, 1520, 2120, 3368, 376];
    public static readonly ImmutableArray<(uint Address, int Length)> StaticRanges = [(SoundAddress, 4), (AlignmentAxisAddress, 12), (AudioDirectoryLiteralAddress, 10)];
}
