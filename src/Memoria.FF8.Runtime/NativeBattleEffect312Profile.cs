using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect312Profile
{
    public const ushort Effect = 312;
    public const uint Entry = 0x59CB90;
    public const uint Initializer = 0x59CB70;
    public const uint RootCallback = 0x5A10A0;
    public const uint MainCallback = 0x59CC60;
    public const uint CleanupCallback = 0x5A1110;
    public const uint BaseAddress = 0x223CCA0;
    public const int StorageLength = 0x4240;
    public const uint TargetGlobal = BaseAddress;
    public const uint EnvironmentAreaAddress = 0x223CCA8;
    public const uint RootBank = 0x223CCB8;
    public const uint RootDescriptor = 0x223CCD8;
    public const uint FirstRecordsAddress = 0x223CCE8;
    public const int FirstRecordStride = 24;
    public const int FirstRecordCount = 100;
    public const uint SecondRecordsAddress = 0x223D648;
    public const int SecondRecordStride = 28;
    public const int SecondRecordCount = 100;
    public const uint EnvironmentOffsetAddress = 0x223E138;
    public const uint MatrixAddress = 0x223E148;
    public const uint EnvironmentRestoreAddress = 0x223E168;
    public const int EnvironmentRestoreLength = 64;
    public const uint ChildDescriptor = 0x223E1A8;
    public const uint ChildBank = 0x223E1B8;
    public const int ChildStride = 36;
    public const int ChildCount = 100;
    public const uint SavedActorsAddress = 0x223EFC8;
    public const int SavedActorStride = 28;
    public const int SavedActorCount = 3;
    public const int SavedActorLength = 20;
    public const uint PathAddress = 0x223F090;
    public const int PathStride = 820;
    public const int PathCount = 6;
    public const uint RibbonAddress = 0x2240A30;
    public const int RibbonStride = 24;
    public const int RibbonCount = 48;
    public const uint TargetPointAddress = 0x2240EB0;
    public const uint ParametersGlobal = 0x2240EB8;
    public const uint CasterGlobal = 0x2240EBC;
    public const uint AnchorAddress = 0x2240EC8;
    public const uint CueGlobal = 0x2240ED0;
    public const uint CapturedOffsetAddress = 0x2240ED4;
    public const uint ArenaGlobal = 0x2240ED8;
    public const uint CursorGlobal = 0x2240EDC;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, 16, 2), new(ChildDescriptor, ChildBank, ChildStride, ChildCount)];
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> FixedPools = Pools;
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges = [(TargetGlobal, 4), (EnvironmentAreaAddress, 12), (FirstRecordsAddress, FirstRecordStride * FirstRecordCount), (SecondRecordsAddress, SecondRecordStride * SecondRecordCount), (EnvironmentOffsetAddress, 8), (MatrixAddress, 32), (EnvironmentRestoreAddress, EnvironmentRestoreLength), (SavedActorsAddress, SavedActorLength), (SavedActorsAddress + SavedActorStride, SavedActorLength), (SavedActorsAddress + 2 * SavedActorStride, SavedActorLength), (PathAddress, PathStride * PathCount), (RibbonAddress, RibbonStride * RibbonCount), (TargetPointAddress, 8), (ParametersGlobal, 12), (AnchorAddress, 8), (CueGlobal, 4), (CapturedOffsetAddress, 4), (ArenaGlobal, 8)];
}
