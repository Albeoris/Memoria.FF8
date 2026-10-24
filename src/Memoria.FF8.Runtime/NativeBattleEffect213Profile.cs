using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect213Profile
{
    public const ushort Effect = 213;
    public const uint Entry = 0x69EAC0;
    public const uint Initializer = 0x69EAD0;
    public const uint RootCallback = 0x69EB70;
    public const uint MainCallback = 0x69EF60;
    public const uint BaseAddress = 0x2510170;
    public const uint CasterGlobal = 0x2510170;
    public const uint InitializerGlobal = 0x2510174;
    public const uint ArenaGlobal = 0x2510178;
    public const uint ParametersGlobal = 0x251017C;
    public const uint RootBank = 0x2510180;
    public const int RootBankLength = 20;
    public const uint CursorGlobal = 0x2510194;
    public const uint DebrisDescriptor = 0x2510198;
    public const uint RibbonDescriptor = 0x25101A8;
    public const uint ParticleDescriptor = 0x25101B8;
    public const uint MainDescriptor = 0x25101C8;
    public const uint RootDescriptor = 0x25101D8;
    public const uint TranslationAddress = 0x25101E8;
    public const uint TextureNameAddress = 0x1181060;
    public const uint CameraAddress = 0x117AC50;
    public const uint SoundAddress = 0x1181028;
    public const uint MutableStreamAddress = 0x117ADE0;
    public const int MutableStreamLength = 2464;
    public const int StreamVertexCount = 308;
    public const int StreamCopyOffset = 0x1B18;
    public const int MorphOffset = 0x24B8;
    public const int FirstPacketOffset = 0x26B8;
    public const int SecondPacketOffset = 0x126B8;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, RootBankLength, 1)];
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> CommonPools = [new(MainDescriptor, 0x1614, 0x504, 1), new(ParticleDescriptor, 0x614, 0x20, 128), new(RibbonDescriptor, 0x4D4, 0x14, 16), new(DebrisDescriptor, 0x1D4, 0x30, 16)];
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges = [(CasterGlobal, 16), (CursorGlobal, 4), (TranslationAddress, 12)];
}
