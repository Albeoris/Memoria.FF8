using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect316Profile
{
    public const ushort Effect = 316;
    public const uint Entry = 0x6610B0;
    public const uint Initializer = 0x6610C0;
    public const uint RootCallback = 0x661190;
    public const uint MainCallback = 0x661350;
    public const uint BaseAddress = 0x2505520;
    public const uint InitializerGlobal = 0x2505520;
    public const uint ParametersGlobal = 0x2505524;
    public const uint CasterGlobal = 0x2505528;
    public const uint MainDescriptor = 0x2505530;
    public const uint RootDescriptor = 0x2505540;
    public const uint RootBank = 0x2505550;
    public const uint CursorGlobal = 0x2505564;
    public const uint RandomGlobal = 0x2505568;
    public const uint AmplitudeGlobal = 0x250556C;
    public const uint ColorGlobal = 0x2505570;
    public const uint WorkGlobal = 0x2505574;
    public const int RootStride = 0x14;
    public const int MainStride = 0x81C;
    public const int RecordOffset = 0x18;
    public const int RecordStride = 16;
    public const int RecordCapacity = 128;
    public const int RecordHintOffset = 0x818;
    public const int ProjectedRowsOffset = 0x81C;
    public const int FirstPacketOffset = 0x881C;
    public const int SecondPacketOffset = 0x1881C;
    public const uint TextureNameAddress = 0x1016084;
    public const uint EmbeddedTimAddress = 0x1005E64;
    public const uint CameraAddress = 0x1005C50;
    public const uint SoundAddress = 0x1005D50;
    public const uint VibrationAddress = 0x1005D54;
    public const uint ProjectedQuadsAddress = 0x1005AE4;
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges = [(BaseAddress, 12), (CursorGlobal, 20)];
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> FixedPools = [new(RootDescriptor, RootBank, RootStride, 1)];
}
