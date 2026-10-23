using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSigh46Range(uint Address, int Length);

public static class NativeBattleSigh46Profile
{
    public const ushort Effect = 46;
    public const uint Entry = 0x831B50;
    public const uint Initializer = 0x831B30;
    public const uint RootCallback = 0x831CB0;
    public const uint RootBank = 0x264EC88;
    public const uint RootDescriptor = 0x264ED50;
    public const uint BaseAddress = 0x264EC60;
    public const uint InitializerGlobal = 0x264EC60;
    public const uint PrimaryCursorGlobal = 0x264EC64;
    public const uint SecondaryCursorGlobal = 0x264EC80;
    public const uint EmitterPointerGlobal = 0x265278C;
    public const uint GeometryPointerGlobal = 0x26527A4;
    public const uint EmitterScanCursorGlobal = 0x264EFA0;
    public const uint GeometryScanCursorGlobal = 0x2652798;
    public const int EmitterStorageLength = 0x870;
    public const int GeometryStorageLength = 0x10680;
    public const int PacketBankBytes = 0x6000;
    public const uint SourceAddress = 0x15C3398;
    public const int SourceLength = 260;
    public const uint ChannelBase = 0x26527B4;
    public const uint MeshAAddress = 0x15C0D58;
    public const uint MeshBAddress = 0x15C1858;
    public const int MeshLength = 2816;
    public const int MutablePeLength = MeshLength * 2;
    public const uint StaticAddress = 0x15B82A4;
    public const int StaticLength = 45772;
    public const uint CameraAddress = 0x15C349C;
    public const int CameraLength = 160;
    public const uint SoundAddress = 0x15B82A0;
    public const uint ColorTableAAddress = 0x15BCD10;
    public const uint ColorTableBAddress = 0x15C0D14;
    public const uint DelayAddress = 0x15C353C;
    public const uint FadeAddress = 0x15C3550;
    public const uint TextureNameAddress = 0x15C3570;
    public const int TextureLength = 167456;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, 100, 2), new(0x2652610, 0x2652620, 88, 4), new(0x2652370, 0x2651B80, 676, 3), new(0x264ED60, 0x264ED70, 140, 4), new(0x264EFA8, 0x264EFB8, 140, 80), new(0x2652600, 0x2652380, 64, 10)];
    public static readonly ImmutableArray<NativeBattleSigh46Range> ChannelRegions = [new(0x26527b4, 272), new(0x26528c8, 44), new(0x26528f8, 44), new(0x2652928, 22), new(0x2652940, 22), new(0x2652958, 22), new(0x2652970, 44), new(0x2652c54, 22), new(0x2652c6c, 22), new(0x2652c84, 22), new(0x2652c9c, 44), new(0x2652ccc, 44), new(0x2652cfc, 44), new(0x2652d2c, 44), new(0x2652d5c, 284), new(0x2652e7c, 44), new(0x2652eac, 44), new(0x2652edc, 22), new(0x2652ef4, 22), new(0x2652f0c, 22), new(0x2652f24, 44), new(0x2653224, 22), new(0x265323c, 22), new(0x2653254, 44), new(0x2653284, 44), new(0x26532b4, 44), new(0x26532e4, 44), new(0x2653314, 28), new(0x2653334, 28), new(0x2653354, 28), new(0x2653374, 14), new(0x2653384, 14), new(0x2653394, 14), new(0x26533a4, 28), new(0x26535bc, 14), new(0x26535cc, 14), new(0x26535dc, 28), new(0x26535fc, 28), new(0x265361c, 28), new(0x265363c, 28), new(0x265365c, 28), new(0x265367c, 28), new(0x265369c, 28), new(0x26536bc, 14), new(0x26536cc, 14), new(0x26536dc, 14), new(0x26536ec, 28), new(0x26538e8, 14), new(0x26538f8, 14), new(0x2653908, 28), new(0x2653928, 28), new(0x2653948, 28), new(0x2653968, 28), new(0x2653988, 222), new(0x2653a68, 30), new(0x2653a88, 30), new(0x2653aa8, 15), new(0x2653ab8, 15), new(0x2653ac8, 15), new(0x2653ad8, 30), new(0x2653d38, 15), new(0x2653d48, 15), new(0x2653d58, 30), new(0x2653d78, 30), new(0x2653d98, 30), new(0x2653db8, 30), new(0x2653dd8, 30), new(0x2653df8, 30), new(0x2653e18, 30), new(0x2653e38, 15), new(0x2653e48, 15), new(0x2653e58, 15), new(0x2653e68, 30), new(0x2654080, 15), new(0x2654090, 15), new(0x26540a0, 30), new(0x26540c0, 30), new(0x26540e0, 30), new(0x2654100, 30), new(0x2654120, 18), new(0x2654134, 18), new(0x2654148, 18), new(0x265415c, 9), new(0x2654168, 9), new(0x2654174, 9), new(0x2654180, 18), new(0x2654344, 9), new(0x2654350, 9), new(0x265435c, 18), new(0x2654370, 18), new(0x2654384, 18), new(0x2654398, 18)];
    public static readonly ImmutableArray<NativeBattleSigh46Range> ImmutableRegions = [new(0x15b82a4, 35508), new(0x15c2358, 38), new(0x15c2380, 44), new(0x15c23b0, 44), new(0x15c23e0, 44), new(0x15c2410, 84), new(0x15c2468, 44), new(0x15c2498, 44), new(0x15c24c8, 22), new(0x15c24e0, 28), new(0x15c2508, 196), new(0x15c25d0, 28), new(0x15c25f0, 28), new(0x15c2610, 14), new(0x15c2620, 26), new(0x15c2648, 184), new(0x15c2704, 28), new(0x15c2724, 28), new(0x15c2744, 14), new(0x15c2754, 32), new(0x15c277c, 30), new(0x15c279c, 30), new(0x15c27bc, 30), new(0x15c27dc, 15), new(0x15c27ec, 28), new(0x15c2814, 198), new(0x15c28dc, 30), new(0x15c28fc, 30), new(0x15c291c, 15), new(0x15c292c, 24), new(0x15c2954, 162), new(0x15c29f8, 18), new(0x15c2a0c, 18), new(0x15c2a20, 9), new(0x15c2a30, 2408), new(0x15c353c, 18), new(0x15c3550, 32)];
}
