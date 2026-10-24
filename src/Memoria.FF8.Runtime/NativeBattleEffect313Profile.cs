using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect313Profile
{
    public const ushort Effect = 313;
    public const uint Entry = 0x59B0D0;
    public const uint Initializer = 0x59B0B0;
    public const uint RootCallback = 0x59CA20;
    public const uint MainCallback = 0x59B180;
    public const uint CleanupCallback = 0x59C9E0;
    public const uint BaseAddress = 0x223B668;
    public const uint RootBank = BaseAddress;
    public const uint RootDescriptor = 0x223B688;
    public const uint RecordsAddress = 0x223B698;
    public const int RecordStride = 24;
    public const int RecordCount = 80;
    public const uint ChildDescriptor = 0x223BE18;
    public const uint ChildBank = 0x223BE28;
    public const uint SavedActorsAddress = 0x223CC38;
    public const int SavedActorStride = 20;
    public const int SavedActorCount = 3;
    public const uint TargetPointAddress = 0x223CC78;
    public const uint ParametersGlobal = 0x223CC80;
    public const uint CasterGlobal = 0x223CC84;
    public const uint InitializerGlobal = 0x223CC88;
    public const uint CenterAddress = 0x223CC90;
    public const uint ArenaGlobal = 0x223CC98;
    public const uint CursorGlobal = 0x223CC9C;
    public const uint TextureNameAddress = 0xCD8D80;
    public const uint CameraAddress = 0xCD59B0;
    public const uint SoundAddress = 0xCD8CE8;
    public const uint RecordSpriteAddress = 0xCD5E8C;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, 16, 2), new(ChildDescriptor, ChildBank, 36, 100)];
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges = [(RecordsAddress, RecordStride * RecordCount), (SavedActorsAddress, 2), (SavedActorsAddress + 4, 16), (SavedActorsAddress + SavedActorStride, 2), (SavedActorsAddress + SavedActorStride + 4, 16), (SavedActorsAddress + SavedActorStride * 2, 2), (SavedActorsAddress + SavedActorStride * 2 + 4, 16), (TargetPointAddress, 20), (CenterAddress, 16)];
}
