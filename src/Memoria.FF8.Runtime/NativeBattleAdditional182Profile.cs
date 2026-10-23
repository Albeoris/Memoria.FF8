using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleAdditional182Profile
{
    public const ushort Effect = 182;
    public const uint Entry = 0x5C9FE0;
    public const uint Initializer = 0x5C9FC0;
    public const uint RootCallback = 0x5CB4E0;
    public const uint MainSetCallback = 0x5CA0E0;
    public const uint MainClearCallback = 0x5CA970;
    public const uint Callback5CA490 = 0x5CA490;
    public const uint Callback5CA6B0 = 0x5CA6B0;
    public const uint Callback5CA810 = 0x5CA810;
    public const uint Callback5CAED0 = 0x5CAED0;
    public const uint Callback5CB130 = 0x5CB130;
    public const uint Callback5CB390 = 0x5CB390;
    public const uint BaseAddress = 0x2304220;
    public const uint FirstTargetGlobal = 0x2304220;
    public const uint RootBank = 0x2304228;
    public const uint RootDescriptor = 0x2304238;
    public const ushort RootStride = 16;
    public const ushort RootCount = 1;
    public const int RootBankLength = 16;
    public const uint ChildDescriptor = 0x23056E8;
    public const uint ChildBank = 0x23056F8;
    public const ushort ChildStride = 36;
    public const ushort ChildCount = 100;
    public const int ChildBankLength = 3600;
    public const uint ParticleABase = 0x2304248;
    public const uint ParticleBBase = 0x2304D88;
    public const int ParticleStride = 24;
    public const int ParticleCountA = 120;
    public const int ParticleCountB = 100;
    public const int ParticleBankABytes = 0xB40;
    public const int ParticleBankBBytes = 0x960;
    public const uint ParametersGlobal = 0x2306508;
    public const uint CasterGlobal = 0x230650C;
    public const uint InitializerGlobal = 0x2306510;
    public const uint HistoryAddress = 0x2306518;
    public const int HistoryCount = 5;
    public const int HistoryStride = 36;
    public const int HistoryBytes = 180;
    public const uint WorkingGlobal = 0x23065CC;
    public const uint CursorGlobal = 0x23065D0;
    public const uint ArenaAddress = 0x20DFAB8;
    public const int PacketStep = 0xC000;
    public const int PacketBytes = 0x18000;
    public const uint HistoryMeshAddress = 0xD3AE78;
    public const uint PrimaryMeshAddress = 0xD3B3D8;
    public const uint MeshPointersAddress = 0xD3D394;
    public const uint SpriteAAddress = 0xD3ABC0;
    public const uint SpriteBAddress = 0xD3ACCC;
    public const uint CameraClearAddress = 0xD39370;
    public const uint CameraSetAddress = 0xD39FD8;
    public const int CameraClearLength = 0xC68;
    public const int CameraSetLength = 0xBE8;
    public const uint SoundClearAddress = 0xD3D388;
    public const uint SoundSetAddress = 0xD3D38C;
    public const uint ColorsAddress = 0xD3D3C4;
    public const uint TextureNameAddress = 0xD3D3E4;

    public static ImmutableArray<uint> MeshVariantAddresses { get; } = [0xD3B648, 0xD3D118, 0xD3BB28, 0xD3C758, 0xD3C008, 0xD3B8B8, 0xD3BD98, 0xD3CEA8, 0xD3C9C8, 0xD3CC38, 0xD3C4E8, 0xD3C278];
}
