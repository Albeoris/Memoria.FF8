using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect314Profile
{
    public const ushort Effect = 314;
    public const uint Entry = 0x662890;
    public const uint Initializer = 0x6628A0;
    public const uint RootCallback = 0x662970;
    public const uint MainCallback = 0x662F40;
    public const uint ParticleCallback = 0x664D20;
    public const uint BaseAddress = 0x2505578;
    public const uint InitializerGlobal = 0x2505578;
    public const uint ParametersGlobal = 0x250557C;
    public const uint CasterGlobal = 0x2505580;
    public const uint ParticleDescriptor = 0x2505588;
    public const uint MainDescriptor = 0x2505598;
    public const uint RootDescriptor = 0x25055A8;
    public const uint RootBank = 0x25055B8;
    public const uint CursorGlobal = 0x25055CC;
    public const uint RandomGlobal = 0x25055D0;
    public const uint AmplitudeGlobal = 0x25055D4;
    public const uint ColorGlobal = 0x25055D8;
    public const uint WorkGlobal = 0x25055DC;
    public const int RootStride = 0x14;
    public const int MainStride = 0x4C28;
    public const int MainOffset = 0xA80;
    public const int ParticleStride = 0x30;
    public const int ParticleCapacity = 0x38;
    public const int SparkOffset = 0x720;
    public const int SparkCapacity = 128;
    public const int SparkStride = 16;
    public const int FragmentOffset = 0xF24;
    public const int FragmentCapacity = 256;
    public const int FragmentStride = 0x38;
    public const int FirstPacketOffset = 0x56F0;
    public const int SecondPacketOffset = 0x156F0;
    public const uint CameraAddress = 0x1016090;
    public const uint VibrationAddress = 0x10161A0;
    public const uint SoundAddress = 0x101619C;
    public static readonly ImmutableArray<uint> QuadAddresses = [0x10162C4, 0x1016424, 0x1016568, 0x1016714];
    public static readonly ImmutableArray<int> QuadLengths = [328, 300, 404, 404];
    public static readonly ImmutableArray<int> QuadMaximumLife = [12, 11, 15, 15];
    public static readonly ImmutableArray<uint> ProgramAddresses = [0x101AA50u, 0x101AAB0u, 0x101AA7Cu, 0x101AADCu, 0x101AB08u, 0x101AB34u, 0x101AB64u, 0x101AB94u, 0x101ABC0u, 0x101ABECu, 0x101AC18u, 0x101AC38u, 0x101AC64u, 0x101AC90u, 0x101ACB0u, 0x101ACDCu, 0x101AD08u, 0x101AD28u, 0x101AD58u, 0x101AD88u, 0x101ADB4u, 0x101ADE0u, 0x101AE0Cu, 0x101AE2Cu, 0x101AE58u, 0x101AE84u, 0x101AEA4u, 0x101AED0u, 0x101AEFCu];
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges = [(BaseAddress, 12), (CursorGlobal, 20)];
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> FixedPools = [new(RootDescriptor, RootBank, RootStride, 1)];
}
