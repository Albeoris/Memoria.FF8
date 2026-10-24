using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect227Profile
{
    public const ushort Effect = 227;
    public const uint Entry = 0x693790;
    public const uint Initializer = 0x6937A0;
    public const uint RootCallback = 0x693840;
    public const uint MainCallback = 0x693A10;
    public const uint ParticleCallback = 0x694360;
    public const uint BaseAddress = 0x2508700;
    public const uint InitializerGlobal = 0x2508700;
    public const uint RecordCursorGlobal = 0x2508704;
    public const uint CursorGlobal = 0x2508708;
    public const uint SecondCursorGlobal = 0x250870C;
    public const uint ParametersGlobal = 0x2508710;
    public const uint CasterGlobal = 0x2508714;
    public const uint ParticleDescriptor = 0x2508718;
    public const uint MainDescriptor = 0x2508728;
    public const uint RootDescriptor = 0x2508738;
    public const uint RootBank = 0x2508748;
    public const int RootBankLength = 16;
    public const uint AnchorAddress = 0x2508758;
    public const uint EndpointAddress = 0x2508760;
    public const uint ArenaGlobal = 0x2508768;
    public const uint TextureNameAddress = 0x1132020;
    public const uint CameraAddress = 0x112D4C0;
    public const uint VibrationAddress = 0x112D1C8;
    public const uint AnchorPointAddress = 0x1132018;
    public const int RecordOffset = 0x61C;
    public const int RecordStride = 16;
    public const int RecordCount = 128;
    public const int MorphOffset = 0xE1C;
    public const int FirstPacketOffset = 0xE24;
    public const int SecondPacketOffset = 0x10E24;
    public const int ThirdPacketOffset = 0x20E24;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, RootBankLength, 1)];
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> CommonPools = [new(MainDescriptor, 0x180, 0x49C, 1), new(ParticleDescriptor, 0, 0x18, 16)];
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges = [(BaseAddress, 24), (AnchorAddress, 20)];
}
