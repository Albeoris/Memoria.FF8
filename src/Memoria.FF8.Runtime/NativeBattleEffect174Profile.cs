using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect174Profile
{
    public const ushort Effect = 174;
    public const uint Entry = 0x5D45A0;
    public const uint Initializer = 0x5D4580;
    public const uint RootCallback = 0x5D7440;
    public const uint MainCallback = 0x5D4660;
    public const uint CleanupCallback = 0x5D7400;
    public const uint BaseAddress = 0x2394208;
    public const uint TargetGlobal = 0x2394208;
    public const uint RootBank = 0x2394210;
    public const uint RootDescriptor = 0x2394230;
    public const uint FirstRecordsAddress = 0x2394240;
    public const int FirstRecordStride = 24;
    public const int FirstRecordCount = 100;
    public const uint SavedBonesAddress = 0x2394BA0;
    public const int BoneCount = 26;
    public const int SavedBonesLength = BoneCount * 32;
    public const uint ChildDescriptor = 0x2394FA0;
    public const uint ChildBank = 0x2394FB0;
    public const int ChildStride = 36;
    public const int ChildCount = 100;
    public const uint SavedActorsAddress = 0x2395DC0;
    public const int SavedActorStride = 20;
    public const int SavedActorCount = 7;
    public const uint ParametersGlobal = 0x2395E4C;
    public const uint CasterGlobal = 0x2395E50;
    public const uint InitializerGlobal = 0x2395E54;
    public const uint HistoryAddress = 0x2395E58;
    public const int HistoryStride = 1028;
    public const int HistoryCount = 5;
    public const int HistoryLength = 4 + SavedBonesLength;
    public const uint ArenaGlobal = 0x239726C;
    public const uint CursorGlobal = 0x2397270;
    public const uint TextureNameAddress = 0xD5E670;
    public const int TextureLength = 232480;
    public const uint CameraAddress = 0xD592FC;
    public const int CameraLength = 356;
    public const int CameraModelOffset = 40;
    public const uint SoundAddress = 0xD5E624;
    public const uint HistoryColorsAddress = 0xD5E62C;
    public const uint MaterialCycleAddress = 0xD5E640;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, 16, 2), new(ChildDescriptor, ChildBank, ChildStride, ChildCount)];
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges = BuildRanges();
    public static readonly ImmutableArray<uint> MeshAddresses = [0xD5DF34, 0xD5E2CC, 0xD597DC, 0xD5D684, 0xD5ADCC];
    public static readonly ImmutableArray<int> MeshLengths = [920, 856, 5616, 2224, 10424];
    public static readonly ImmutableArray<uint> SpriteAddresses = [0xD59648, 0xD59460];
    public static readonly ImmutableArray<int> SpriteLengths = [404, 60];
    public static readonly ImmutableArray<int> SpriteFrameCounts = [15, 1];
    public static readonly ImmutableArray<(uint Address, int Length)> StaticRanges = [(SoundAddress, 4), (HistoryColorsAddress, 20), (MaterialCycleAddress, 48)];

    private static ImmutableArray<(uint Address, int Length)> BuildRanges()
    {
        var ranges = ImmutableArray.CreateBuilder<(uint Address, int Length)>();
        ranges.Add((TargetGlobal, 4));
        ranges.Add((FirstRecordsAddress, FirstRecordStride * FirstRecordCount));
        ranges.Add((SavedBonesAddress, SavedBonesLength));
        for (int actor = 0; actor < SavedActorCount; actor++)
        {
            uint row = SavedActorsAddress + (uint)(actor * SavedActorStride);
            ranges.Add((row, 2));
            ranges.Add((row + 4, 16));
        }
        ranges.Add((ParametersGlobal, 4));
        ranges.Add((CasterGlobal, 8));
        for (int history = 0; history < HistoryCount; history++)
        {
            ranges.Add((HistoryAddress + (uint)(history * HistoryStride), HistoryLength));
        }
        ranges.Add((ArenaGlobal, 8));
        return ranges.ToImmutable();
    }
}
