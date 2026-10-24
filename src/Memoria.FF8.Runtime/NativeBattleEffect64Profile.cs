using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect64Profile
{
    public const ushort Effect = 64;
    public const uint Entry = 0x6E5180;
    public const uint Initializer = 0x6E5190;
    public const uint RootCallback = 0x6E5260;
    public const uint MainCallback = 0x6E53F0;
    public const uint BaseAddress = 0x2545018;
    public const uint InitializerGlobal = BaseAddress;
    public const uint ParametersGlobal = 0x254501C;
    public const uint CasterGlobal = 0x2545020;
    public const uint CursorGlobal = 0x254508C;
    public const uint ArenaGlobal = 0x2545098;
    public const uint RootDescriptor = 0x2545068;
    public const uint RootBank = 0x2545078;
    public const int RootBankLength = 20;
    public const uint MainDescriptor = 0x2545058;
    public const int MainOffset = 0x318;
    public const int MainStride = 0x488;
    public const int MainCount = 1;
    public const uint ScriptDescriptor = 0x2545048;
    public const int ScriptOffset = 0;
    public const int ScriptStride = 0x2C;
    public const int ScriptCount = 18;
    public const uint ProgrammeDescriptor = 0x2545038;
    public const int ProgrammeOffset = 0x7A0;
    public const int ProgrammeStride = 0x598;
    public const int ProgrammeCount = 16;
    public const uint ParticleDescriptor = 0x2545028;
    public const int ParticleOffset = 0x6120;
    public const int ParticleStride = 0x1C;
    public const int ParticleCount = 128;
    public const int FirstPacketOffset = 0x6F20;
    public const int SecondPacketOffset = 0x16F20;
    public const uint TextureNameAddress = 0x134BF90;
    public const int TextureLength = 133664;
    public const uint CameraAddress = 0x1342D40;
    public const int CameraLength = 436;
    public const uint VibrationAddress = 0x1342EF8;
    public const int VibrationLength = 316;
    public const uint SoundAddress = 0x1342EF4;
    public const uint SpriteDirectoryAddress = 0x13433C0;
    public const uint ProgrammeDirectoryAddress = 0x13473C8;
    public static readonly ImmutableArray<uint> ProgrammeAddresses = [0x13433C8, 0x1343C30];
    public static readonly ImmutableArray<int> ProgrammeLengths = [2152, 14228];
    public static readonly ImmutableArray<int> ProgrammeWorkspaceLengths = [152, 1400];
    public static readonly ImmutableArray<uint> SpriteAddresses = [0x1343034, 0x1343214];
    public static readonly ImmutableArray<int> SpriteLengths = [480, 428];
    public static readonly ImmutableArray<uint> ScriptAddresses = [0x134B560, 0x134B7FC, 0x134B880, 0x134B904, 0x134B988, 0x134BA0C, 0x134BA90, 0x134BB14, 0x134BB98, 0x134BC1C, 0x134BCA0, 0x134BD04, 0x134BD68, 0x134BD9C, 0x134BE00, 0x134BE64, 0x134BEC8, 0x134BF2C];
    public static readonly ImmutableDictionary<uint, int> ScriptLengths = ScriptAddresses.Select((address, index) => new KeyValuePair<uint, int>(address, checked((int)((index + 1 < ScriptAddresses.Length ? ScriptAddresses[index + 1] : TextureNameAddress) - address)))).ToImmutableDictionary();
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, RootBankLength, 1), new(MainDescriptor, MainOffset, MainStride, MainCount), new(ScriptDescriptor, ScriptOffset, ScriptStride, ScriptCount), new(ProgrammeDescriptor, ProgrammeOffset, ProgrammeStride, ProgrammeCount), new(ParticleDescriptor, ParticleOffset, ParticleStride, ParticleCount)];
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges = [(BaseAddress, 12), (CursorGlobal, 4), (ArenaGlobal, 4)];
}
