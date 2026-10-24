using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect140Profile
{
    public const ushort Effect = 140;
    public const uint Entry = 0x6A6300;
    public const uint Initializer = 0x6A6310;
    public const uint LoaderCallback = 0x6A6360;
    public const uint RootCallback = 0x6A6530;
    public const uint MainCallback = 0x6A6750;
    public const uint BaseAddress = 0x2517AA0;
    public const uint FirstEndBankGlobal = 0x2517AA0;
    public const uint SecondEndBankGlobal = 0x2517AA4;
    public const uint InitializerGlobal = 0x2517AA8;
    public const uint PauseGlobal = 0x2517AAC;
    public const uint ParametersGlobal = 0x2517AB0;
    public const uint CasterGlobal = 0x2517AB4;
    public const uint SpriteDescriptor = 0x2517AB8;
    public const uint ParticleDescriptor = 0x2517AC8;
    public const uint MainDescriptor = 0x2517AD8;
    public const uint RootDescriptor = 0x2517AE8;
    public const uint RootBank = 0x2517AF8;
    public const int RootBankLength = 20;
    public const uint CursorGlobal = 0x2517B0C;
    public const uint ControlGlobal = 0x2517B10;
    public const uint InitialCommonGlobal = 0x2517B18;
    public const uint RandomGlobal = 0x2517B1C;
    public const uint CommonPassGlobal = 0x2517B20;
    public const uint CompletionGlobal = 0x2517B24;
    public const uint ArenaGlobal = 0x2517B28;
    public const uint ViewAddress = 0x2517B30;
    public const uint TextureNameAddress = 0x11D4E84;
    public const int TextureLength = 136224;
    public const uint ModelAddress = 0x11B537C;
    public const int ModelLength = 71896;
    public const uint ModelSkeletonAddress = ModelAddress + 24;
    public const int ModelSkeletonLength = 2848;
    public const uint VibrationAddress = 0x11D0048;
    public const int VibrationLength = 280;
    public const uint ColorBlendAddress = 0x11D0160;
    public const uint SpriteAddress = 0x11D495C;
    public const int SpriteLength = 448;
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges = [(BaseAddress, 24), (CursorGlobal, 8), (InitialCommonGlobal, 13), (ArenaGlobal, 4), (ViewAddress, 32)];
    public static readonly ImmutableArray<uint> ProgrammeAddresses = [0x11C6C54, 0x11C8FDC, 0x11CCBA0, 0x11CEF60];
    public static readonly ImmutableArray<int> ProgrammeLengths = [9096, 15300, 9152, 4316];
    public static readonly ImmutableArray<int> ProgrammeWorkspaceLengths = [0x178, 0x288, 0x4E0, 0xF8];
    public static readonly ImmutableArray<uint> MeshAddresses = [0x11D04B4, 0x11D2594, 0x11D3778, 0x11D4B1C];
    public static readonly ImmutableArray<int> MeshLengths = [8416, 4580, 4580, 872];
    public static readonly ImmutableArray<uint> MutableMeshAddresses = [0x11D04B4, 0x11D2594, 0x11D3778];
    public static readonly ImmutableArray<(uint Address, int Length)> BoundSources = [(ModelAddress, ModelLength), (0x11D04B4, 8416), (0x11D2594, 4580), (0x11D3778, 4580)];
    public static readonly ImmutableArray<uint> SoundAddresses = [0x11D003C, 0x11D0040, 0x11D0044];
}
