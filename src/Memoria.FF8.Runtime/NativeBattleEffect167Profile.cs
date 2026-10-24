using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect167Profile
{
    public const ushort Effect = 167;
    public const uint Entry = 0x5E2A10;
    public const uint Initializer = 0x5E29F0;
    public const uint RootCallback = 0x5E4E20;
    public const uint MainCallback = 0x5E2AD0;
    public const uint BaseAddress = 0x23BD350;
    public const uint FirstControlAddress = 0x23BD350;
    public const uint SecondControlAddress = 0x23BD3A8;
    public const uint TargetGlobal = 0x23BD400;
    public const uint FirstHistoryAddress = 0x23BD408;
    public const uint RootBank = 0x23BD598;
    public const uint RootDescriptor = 0x23BD5A8;
    public const uint FirstRecordsAddress = 0x23BD5B8;
    public const int FirstRecordStride = 24;
    public const int FirstRecordCount = 100;
    public const uint FirstSamplesAddress = 0x23BDF18;
    public const uint SecondSamplesAddress = 0x23BE018;
    public const uint SavedPositionAddress = 0x23BE118;
    public const uint EndpointsAddress = 0x23BE120;
    public const uint ChildDescriptor = 0x23BE130;
    public const uint ChildBank = 0x23BE140;
    public const int ChildStride = 36;
    public const int ChildCount = 100;
    public const uint ParametersGlobal = 0x23BEF50;
    public const uint CasterGlobal = 0x23BEF54;
    public const uint InitializerGlobal = 0x23BEF58;
    public const uint SecondHistoryAddress = 0x23BEF60;
    public const uint MarkerGlobal = 0x23BF028;
    public const uint ArenaGlobal = 0x23BF02C;
    public const uint CursorGlobal = 0x23BF030;
    public const uint TextureNameAddress = 0xD716CC;
    public const int TextureLength = 132640;
    public const uint CameraAddress = 0xD6F7A4;
    public const int CameraLength = 300;
    public const uint SpriteAddress = 0xD6F8D0;
    public const int SpriteLength = 428;
    public const uint SoundAddress = 0xD71564;
    public const uint MappingAddress = 0xD715EC;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, 16, 1), new(ChildDescriptor, ChildBank, ChildStride, ChildCount)];
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges = [(FirstControlAddress, 80), (SecondControlAddress, 80), (TargetGlobal, 4), (FirstHistoryAddress, 60), (FirstRecordsAddress, 2400), (FirstSamplesAddress, 256), (SecondSamplesAddress, 256), (SavedPositionAddress, 8), (EndpointsAddress, 16), (ParametersGlobal, 12), (SecondHistoryAddress, 200), (MarkerGlobal, 12)];
    public static readonly ImmutableArray<uint> MeshAddresses = [0xD6FA7C, 0xD7042C];
    public static readonly ImmutableArray<int> MeshLengths = [2480, 2224];
    public static readonly ImmutableArray<(uint Address, int Length)> StaticRanges = [(SoundAddress, 4), (MappingAddress, 224)];
}
