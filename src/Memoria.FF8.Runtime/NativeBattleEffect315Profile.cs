using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect315Profile
{
    public const ushort Effect = 315;
    public const uint Entry = 0x598DB0;
    public const uint Initializer = 0x598D90;
    public const uint BaseAddress = 0x2239530;
    public const uint TargetGlobal = BaseAddress;
    public const uint ParametersGlobal = 0x223B648;
    public const uint CasterGlobal = 0x223B64C;
    public const uint InitializerGlobal = 0x223B650;
    public const uint ArenaGlobal = 0x223B654;
    public const uint CursorGlobal = 0x223B658;
    public const uint VibrationGlobal = 0x223B65C;
    public const uint RootBank = 0x2239538;
    public const int RootStride = 16;
    public const int RootCount = 2;
    public const int RootBankLength = 32;
    public const uint RootDescriptor = 0x2239558;
    public const uint ParticleAddress = 0x2239568;
    public const int ParticleStride = 24;
    public const int ParticleCount = 200;
    public const int ParticleLength = 4800;
    public const uint ChildDescriptor = 0x223A828;
    public const uint ChildBank = 0x223A838;
    public const int ChildStride = 36;
    public const int ChildCount = 100;
    public const int ChildBankLength = 3600;
    public const uint ArenaAddress = 0x20DFAB8;
    public const int FirstPacketOffset = 0;
    public const int SecondPacketOffset = 0xC000;
    public const uint CameraAddress = 0xCD2B08;
    public const int CameraLength = 316;
    public const uint VibrationAddress = 0xCD57A0;
    public const int VibrationLength = 282;
    public const uint SoundAAddress = 0xCD58BC;
    public const uint SoundBAddress = 0xCD58C0;
    public const uint PointRowsAddress = 0xCD58C8;
    public const uint VectorRowsAddress = 0xCD58E0;
    public const uint CasterColorsAddress = 0xCD5910;
    public const uint RecipientColorsAddress = 0xCD5940;
    public const uint TextureNameAddress = 0xCD59A0;
    public const int TextureLength = 199712;
    public static readonly ImmutableArray<uint> SpriteAddresses = [0xCD2C44, 0xCD2DF0, 0xCD2F68];
    public static readonly ImmutableArray<int> SpriteLengths = [428, 376, 560];
    public static readonly ImmutableArray<uint> MeshAddresses = [0xCD3198, 0xCD3A48, 0xCD3F08, 0xCD47B0];
    public static readonly ImmutableArray<int> MeshLengths = [2224, 1216, 2216, 4080];
    public static readonly ImmutableArray<int> MeshVertexCounts = [81, 48, 80, 121];
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, RootStride, RootCount), new(ChildDescriptor, ChildBank, ChildStride, ChildCount)];
}
