using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMagmaBreath48Range(uint Address, int Length);

public static class NativeBattleMagmaBreath48Profile
{
    public const ushort Effect = 48;
    public const uint Entry = 0x824CF0;
    public const uint Initializer = 0x824CD0;
    public const uint RootCallback = 0x824E40;
    public const uint RootBank = 0x26481E0;
    public const uint RootDescriptor = 0x26482A8;
    public const uint BaseAddress = 0x26481B4;
    public const uint InitializerGlobal = 0x26481B4;
    public const uint PrimaryCursorGlobal = 0x26481B8;
    public const uint SecondaryCursorGlobal = 0x26481D4;
    public const uint EmitterPointerGlobal = 0x26495C4;
    public const uint GeometryPointerGlobal = 0x26495DC;
    public const uint EmitterScanCursorGlobal = 0x26489A8;
    public const uint GeometryScanCursorGlobal = 0x26495D0;
    public const int EmitterStorageLength = 0x870;
    public const int GeometryStorageLength = 0x10680;
    public const int PacketBankBytes = 0x6000;
    public const uint SourceAddress = 0x15B7C88;
    public const int SourceLength = 260;
    public const uint ChannelBase = 0x26495EC;
    public const uint MeshAAddress = 0x15AE4FC;
    public const uint MeshBAddress = 0x15B0514;
    public const int MeshLength = 8216;
    public const int MutablePeLength = MeshLength * 2;
    public const uint StaticAddress = 0x15ADC6C;
    public const int StaticLength = 41248;
    public const uint CameraAddress = 0x15B7D8C;
    public const int CameraLength = 656;
    public const uint SoundAddress = 0x15ADC68;
    public const uint ColorScriptAddress = 0x15ADC28;
    public const uint TextureNameAddress = 0x15B801C;
    public const int TextureLength = 167968;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, 100, 2), new(0x2649448, 0x2649458, 88, 4), new(0x26480F0, 0x2648100, 60, 3), new(0x26491A8, 0x26489B8, 676, 3), new(0x26482B8, 0x26482C8, 88, 20), new(0x2649438, 0x26491B8, 64, 10)];
    public static readonly ImmutableArray<NativeBattleMagmaBreath48Range> ChannelRegions = [new(0x26495ec, 80), new(0x264965c, 80), new(0x26496cc, 80), new(0x264973c, 110), new(0x26497ac, 110), new(0x2649c0c, 110), new(0x2649c7c, 110), new(0x2649cec, 110), new(0x2649d5c, 30), new(0x2649d7c, 30), new(0x2649d9c, 30), new(0x2649dbc, 15), new(0x2649dcc, 15), new(0x2649ddc, 15), new(0x2649dec, 30), new(0x264a094, 15), new(0x264a0a4, 15), new(0x264a0b4, 30), new(0x264a0d4, 30), new(0x264a0f4, 30), new(0x264a114, 30), new(0x264a134, 30), new(0x264a154, 30), new(0x264a174, 30), new(0x264a194, 15), new(0x264a1a4, 15), new(0x264a1b4, 15), new(0x264a1c4, 30), new(0x264a498, 15), new(0x264a4a8, 15), new(0x264a4b8, 30), new(0x264a4d8, 30), new(0x264a4f8, 30), new(0x264a518, 30), new(0x264a538, 30), new(0x264a558, 30), new(0x264a578, 30), new(0x264a598, 15), new(0x264a5a8, 15), new(0x264a5b8, 15), new(0x264a5c8, 30), new(0x264a5e8, 49), new(0x264a61c, 49), new(0x264a650, 98), new(0x264a6b4, 98), new(0x264a718, 98), new(0x264a77c, 98), new(0x264a7e0, 15), new(0x264a7f0, 15), new(0x264a800, 30), new(0x264a820, 30), new(0x264a840, 30), new(0x264a860, 30), new(0x264a880, 30), new(0x264a8a0, 30), new(0x264a8c0, 30), new(0x264a8e0, 15), new(0x264a8f0, 15), new(0x264a900, 15), new(0x264a910, 30), new(0x264abb8, 15), new(0x264abc8, 15), new(0x264abd8, 30), new(0x264abf8, 30), new(0x264ac18, 30), new(0x264ac38, 30), new(0x264ac58, 80), new(0x264acac, 34), new(0x264acd0, 34), new(0x264acf4, 34), new(0x264ad18, 17), new(0x264ad2c, 17), new(0x264ad40, 17), new(0x264ad54, 34), new(0x264ad78, 42), new(0x264ada4, 42), new(0x264add0, 521), new(0x264afdc, 17), new(0x264aff0, 34), new(0x264b014, 34), new(0x264b038, 34), new(0x264b05c, 34), new(0x264b080, 30), new(0x264b0a0, 30), new(0x264b0c0, 30), new(0x264b0e0, 15), new(0x264b0f0, 15), new(0x264b100, 15), new(0x264b110, 30), new(0x264b328, 15), new(0x264b338, 15), new(0x264b348, 30), new(0x264b368, 30), new(0x264b388, 30), new(0x264b3a8, 30)];
    public static readonly ImmutableArray<NativeBattleMagmaBreath48Range> ImmutableRegions = [new(0x15adc6c, 2192), new(0x15b252c, 1464), new(0x15b309c, 13326), new(0x15b64ac, 110), new(0x15b651c, 110), new(0x15b658c, 110), new(0x15b65fc, 55), new(0x15b6634, 55), new(0x15b666c, 55), new(0x15b66a4, 110), new(0x15b6714, 55), new(0x15b674c, 55), new(0x15b6784, 55), new(0x15b67bc, 110), new(0x15b682c, 36), new(0x15b6854, 246), new(0x15b694c, 30), new(0x15b696c, 30), new(0x15b698c, 15), new(0x15b699c, 37), new(0x15b69c4, 74), new(0x15b6a10, 74), new(0x15b6a5c, 74), new(0x15b6aa8, 30), new(0x15b6ac8, 30), new(0x15b6ae8, 30), new(0x15b6b08, 15), new(0x15b6b18, 120), new(0x15b6ba4, 80), new(0x15b6c08, 80), new(0x15b6c6c, 30), new(0x15b6c8c, 30), new(0x15b6cac, 30), new(0x15b6ccc, 49), new(0x15b6d00, 49), new(0x15b6d34, 98), new(0x15b6d98, 98), new(0x15b6dfc, 98), new(0x15b6e60, 15), new(0x15b6e70, 36), new(0x15b6e98, 246), new(0x15b6f90, 30), new(0x15b6fb0, 30), new(0x15b6fd0, 15), new(0x15b6fe0, 120), new(0x15b705c, 80), new(0x15b70b0, 34), new(0x15b70d4, 34), new(0x15b70f8, 34), new(0x15b711c, 42), new(0x15b7148, 42), new(0x15b7174, 101), new(0x15b71dc, 28), new(0x15b7204, 198), new(0x15b72cc, 30), new(0x15b72ec, 30), new(0x15b730c, 15), new(0x15b7320, 2408)];
}
