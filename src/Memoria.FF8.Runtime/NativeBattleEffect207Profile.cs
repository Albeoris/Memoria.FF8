using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect207Profile
{
    public const ushort Effect = 207;
    public const uint Entry = 0x6A3AB0;
    public const uint Initializer = 0x6A3AC0;
    public const uint RootCallback = 0x6A3B70;
    public const uint MainCallback = 0x6A3D90;
    public const uint BaseAddress = 0x25102C0;
    public const uint CasterGlobal = 0x25102C0;
    public const uint InitializerGlobal = 0x25102C4;
    public const uint ArenaGlobal = 0x25102C8;
    public const uint MutableMeshAddress = 0x25102CC;
    public const int MutableMeshLength = 0x7740;
    public const uint ParametersGlobal = 0x2517A0C;
    public const uint RootBank = 0x2517A10;
    public const uint CursorGlobal = 0x2517A24;
    public const uint ParticleDescriptor = 0x2517A28;
    public const uint MainDescriptor = 0x2517A38;
    public const uint RootDescriptor = 0x2517A48;
    public const uint MatrixAddress = 0x2517A58;
    public const uint ViewAddress = 0x2517A80;
    public const uint TextureNameAddress = 0x11B5364;
    public const int TextureLength = 101408;
    public const uint CameraAddress = 0x11AC278;
    public const int CameraLength = 412;
    public const int CameraModelOffset = 40;
    public const uint SoundAddress = 0x11AC274;
    public const uint SpriteAddress = 0x11AC414;
    public const int SpriteLength = 528;
    public const uint OriginalMeshAddress = 0x11AC624;
    public const int OriginalMeshLength = 7632;
    public const uint ProgrammeAddress = 0x11AE3F4;
    public const int ProgrammeLength = 19952;
    public const int ProgrammeWorkspaceLength = 0x4B8;
    public const int ProgrammeWorkspaceOffset = 0x30;
    public const uint SelectorsAddress = 0x11B5370;
    public const uint SqrtTableAddress = 0x11B3164;
    public const uint ShapingTableAddress = 0x11B3364;
    public const int ShapingTableLength = 8192;
    public const int CommonQueueLength = 8;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, 20, 1)];
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> FixedPools = Pools;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> CommonPools = [new(ParticleDescriptor, 0, 32, 64), new(MainDescriptor, 0x800, 0x4E8, 1)];
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges = [(CasterGlobal, 12), (MutableMeshAddress, MutableMeshLength), (ParametersGlobal, 4), (CursorGlobal, 4), (MatrixAddress, 32), (ViewAddress, 32)];
    public static readonly ImmutableArray<uint> GeometryAddresses = [0x11AE41C, 0x11AE690, 0x11AE904, 0x11AEB78, 0x11AF8A4, 0x11AFD88, 0x11B0744, 0x11B1010];
    public static readonly ImmutableArray<int> GeometryLengths = [628, 628, 628, 3372, 1252, 2492, 2252, 3764];
    public static readonly ImmutableArray<(uint Address, int Length)> StaticRanges = [(SoundAddress, 4), (OriginalMeshAddress, MutableMeshLength), (SqrtTableAddress, 512), (ShapingTableAddress, ShapingTableLength), (SelectorsAddress, 8)];
}
