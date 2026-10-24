using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect209Profile
{
    public const ushort Effect = 209;
    public const uint Entry = 0x637500;
    public const uint Initializer = 0x575520;
    public const uint RootCallback = 0x639100;
    public const uint MainCallback = 0x6375D0;
    public const uint AlternateCallback = 0x638BD0;
    public const uint CleanupCallback = 0x639170;
    public const uint BaseAddress = 0x24F3618;
    public const uint StageAfterColor40Global = 0x24F3618;
    public const uint TargetGlobal = 0x24F361C;
    public const uint RootBank = 0x24F3620;
    public const uint RootDescriptor = 0x24F3640;
    public const uint SavedColor44Global = 0x24F3650;
    public const uint ChildDescriptor = 0x24F3658;
    public const uint ChildBank = 0x24F3668;
    public const int ChildStride = 36;
    public const int ChildCount = 100;
    public const uint ParametersGlobal = 0x24F4478;
    public const uint StageAfterColor44Global = 0x24F447C;
    public const uint CasterGlobal = 0x24F4480;
    public const uint GuardianGlobal = 0x24F4484;
    public const uint GridAddress = 0x24F4488;
    public const int GridRows = 28;
    public const int GridColumns = 41;
    public const int GridStride = 12;
    public const int GridLength = GridRows * GridColumns * GridStride;
    public const uint SavedColor40Global = 0x24F7A58;
    public const uint ArenaGlobal = 0x24F7A5C;
    public const uint CursorGlobal = 0x24F7A60;
    public const uint VibrationGlobal = 0x24F7A64;
    public const uint TimAddress = 0xF47340;
    public const int TimLength = 68128;
    public const uint CameraAddress = 0xE25AF0;
    public const int CameraLength = 2128;
    public const int CameraModelOffset = 32;
    public const uint VibrationAddress = 0xE2B2C8;
    public const int VibrationLength = 300;
    public const uint SoundAddress = 0xE2B3F4;
    public const uint AlternateSoundAddress = 0xE2B3F8;
    public const uint MeshDirectoryAddress = 0xE2B400;
    public const uint InitialColorsAddress = 0xE2B430;
    public const uint CycleColorsAddress = 0xE2B450;
    public const uint ScrollColorsAddress = 0xE2B4B0;
    public const uint StageDepthRowsAddress = 0xE2B4F8;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, 16, 2), new(ChildDescriptor, ChildBank, ChildStride, ChildCount)];
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges = [(BaseAddress, 8), (SavedColor44Global, 4), (ParametersGlobal, 16), (GridAddress, GridLength), (SavedColor40Global, 16)];
    public static readonly ImmutableArray<uint> MeshAddresses = [0xE277B8, 0xE290E0, 0xE288F0, 0xE26340, 0xE29588];
    public static readonly ImmutableArray<int> MeshLengths = [4408, 1192, 2032, 5240, 624];
    public static readonly ImmutableArray<(uint Address, int Length)> StaticRanges = [(SoundAddress, 8), (MeshDirectoryAddress, 4), (InitialColorsAddress, 32), (CycleColorsAddress, 96), (ScrollColorsAddress, 72), (StageDepthRowsAddress, 32)];
}
