using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect63Profile
{
    public const ushort Effect = 63;
    public const uint Entry = 0x6E6BE0;
    public const uint Initializer = 0x6E6BF0;
    public const uint RootCallback = 0x6E6CB0;
    public const uint MainCallback = 0x6E6E10;
    public const uint NodeCallback = 0x6E7450;
    public const uint PointHelper = 0x6E7710;
    public const uint BeamHelper = 0x6E7790;
    public const uint DirectionHelper = 0x6E7920;
    public const uint SpriteCallback = 0x6E7970;
    public const uint FadeHelper = 0x6E7A30;
    public const uint BaseAddress = 0x25450A0;
    public const uint InitializerGlobal = BaseAddress;
    public const uint ParametersGlobal = 0x25450A4;
    public const uint CasterGlobal = 0x25450A8;
    public const uint TargetGlobal = 0x25450AC;
    public const uint MainDescriptor = 0x25450B0;
    public const uint SpriteDescriptor = 0x25450C0;
    public const uint RootDescriptor = 0x25450D0;
    public const uint RootBank = 0x25450E0;
    public const uint CursorGlobal = 0x25450F0;
    public const uint WorkGlobal = 0x25450FC;
    public const int RootStride = 16;
    public const int SpriteStride = 28;
    public const int SpriteCount = 30;
    public const int MainStride = 0xE08;
    public const int MainOffset = 0x348;
    public const int FirstPacketOffset = 0x1150;
    public const int SecondPacketOffset = 0x11150;
    public const int PacketBytes = 0x10000;
    public const int MorphOffset = 0x21150;
    public const int MaximumMorphBytes = 1512;
    public const uint TextureNameAddress = 0x1354A1C;
    public const uint CameraAddress = 0x134BFA0;
    public const uint SpriteAddress = 0x134C1B8;
    public const uint BeamAddress = 0x134C2FC;
    public const uint FirstProgrammeAddress = 0x134F87C;
    public const uint SecondProgrammeAddress = 0x134E154;
    public const uint ThirdProgrammeAddress = 0x1350048;
    public const uint SoundAddress = 0x13549F4;
    public const uint FirstPointAddress = 0x13549F8;
    public const uint SecondPointAddress = 0x1354A00;
    public const uint FirstSelectorsAddress = 0x1354A08;
    public const uint SecondSelectorsAddress = 0x1354A28;
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges = [(BaseAddress, 16), (CursorGlobal, 4), (WorkGlobal, 4)];
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> FixedPools = [new(RootDescriptor, RootBank, RootStride, 1)];
}
