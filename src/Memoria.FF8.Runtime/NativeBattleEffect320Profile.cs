using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect320Profile
{
    public const ushort Effect = 320;
    public const uint Entry = 0x65BC10;
    public const uint Initializer = 0x65BC20;
    public const uint RootCallback = 0x65BCC0;
    public const uint MainCallback = 0x65BE90;
    public const uint ParticleCallback = 0x65DA90;
    public const uint BaseAddress = 0xFF12A8;
    public const uint MutableAnchorAddress = 0xFF12A8;
    public const uint InitializerGlobal = 0x2505348;
    public const uint CursorGlobal = 0x250534C;
    public const uint ParametersGlobal = 0x2505350;
    public const uint CasterGlobal = 0x2505354;
    public const uint ParticleDescriptor = 0x2505358;
    public const uint MainDescriptor = 0x2505368;
    public const uint RootDescriptor = 0x2505378;
    public const uint RootBank = 0x2505388;
    public const uint ArenaGlobal = 0x250539C;
    public const int StorageLength = 22;
    public const int CommonQueueLength = 8;
    public const int MainStride = 0x754;
    public const int ParticleStride = 0x20;
    public const int ParticleCapacity = 64;
    public const int ParticleBankOffset = 0x754;
    public const int MorphOffset = 0xF54;
    public const int PacketBank0Offset = 0x1314;
    public const int PacketBank1Offset = 0x11314;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, RootBank, 20, 1)];
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> FixedPools = Pools;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> CommonPools = [new(MainDescriptor, 0, MainStride, 1), new(ParticleDescriptor, ParticleBankOffset, ParticleStride, ParticleCapacity)];
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges = [(MutableAnchorAddress, 2), (InitializerGlobal, 16), (ArenaGlobal, 4)];
}
