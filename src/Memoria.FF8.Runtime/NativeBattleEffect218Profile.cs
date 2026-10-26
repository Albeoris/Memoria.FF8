using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect218Profile
{
    public const uint Entry = 0x6326D0;
    public const uint Initializer = 0x575550;
    public const uint ArenaAddress = 0x20DFAB8;
    public const uint RootDescriptor = 0x24F2418;
    public const uint RootCallback = 0x6374D0;
    public const uint MainCallback = 0x6328A0;
    public const uint MainDescriptor = 0x24F2518;
    public const uint SecondaryDescriptor = 0x24F23B8;
    public const uint ArenaGlobal = 0x24F3550;
    public const uint ParametersGlobal = 0x24F336C;
    public const uint CasterGlobal = 0x24F3370;
    public const uint TargetGlobal = 0x24F2368;
    public const uint CameraBasis = 0x24F2348;
    public const uint CurrentView = 0x24F23F8;
    public const uint StreamedModelAddress = 0x24F3558;
    public const uint StreamedDescriptorAddress = 0x24F3500;
    public const uint EmbeddedModelAddress = 0x24F2428;
    public const uint EmbeddedDescriptorAddress = 0x24F3338;
    public const int StreamedIoOffset = 0x11000;
    public const int ParticleAOffset = 0x2D000;
    public const int ParticleBOffset = 0x2E800;
    public const int ProjectionOffset = 0x2F800;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools = [new(RootDescriptor, 0x24F23D8, 0x10, 2), new(MainDescriptor, 0x24F2528, 0x24, 100), new(SecondaryDescriptor, 0x24F2370, 0x24, 2)];
}
