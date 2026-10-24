using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect345Profile
{
    public const ushort Effect = 345;
    public const uint Entry = 0x705A80;
    public const uint Initializer = 0x705A60;
    public const uint RootCallback = 0x705C00;
    public const uint BaseAddress = 0x25570A0;
    public const uint InitializerGlobal = 0x25570A0;
    public const uint PrimaryCursorGlobal = 0x25570A4;
    public const uint SecondaryCursorGlobal = 0x2557204;
    public const uint RootDescriptor = 0x25572D8;
    public const uint RootBank = 0x2557210;
    public const uint AppearanceContext = 0x25570B0;
    public const uint AppearanceProjectedRows = 0x2557770;
    public const uint NormalProjectedRows = 0x255DC10;
    public const uint ScratchCursorGlobal = 0x255EA84;
    public const uint AllocationCursorGlobal = 0x255EA90;
    public const uint ParticleContextGlobal = 0x255EA94;
    public const uint AppearanceDescriptor = 0x255EA98;
    public static readonly ImmutableArray<NativeBattleCureFamilyPool> Pools =
    [
        new(0x25572D8, 0x2557210, 100, 2),
        new(0x255DA98, 0x255DAA8, 88, 4),
        new(0x25571E0, 0x2557108, 72, 3),
        new(0x255D7F8, 0x255CD68, 676, 4),
        new(0x25572E8, 0x25572F8, 112, 10),
        new(0x2557760, 0x25596B0, 280, 50),
        new(0x255DA88, 0x255D808, 64, 10),
        new(0x255EA30, 0x255E670, 320, 3)
    ];
    public static readonly ImmutableArray<(uint Address, int Length)> OwnedRanges =
    [
        (0x25570A0, 10),
        (0x25570B0, 84),
        (0x2557204, 8),
        (0x2557758, 8),
        (0x2557770, 0x970),
        (0x255CD60, 8),
        (0x255DC08, 8),
        (0x255DC10, 0x970),
        (0x255E610, 2),
        (0x255E618, 84),
        (0x255EA40, 2),
        (0x255EA44, 4),
        (0x255EA50, 2),
        (0x255EA78, 2),
        (0x255EA7C, 2),
        (0x255EA84, 20),
        (0x255EA9C, 4),
        (0x255EAAC, 11),
        (0x255EAB8, 6)
    ];
}
