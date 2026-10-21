using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleWindBlast29Profile
{
    public const uint Entry = 0x8794A0;
    public const uint Initializer = 0x879480;
    public const uint InitializerGlobal = 0x26D6A40;
    public const uint RootCallback = 0x8795C0;
    public const uint TargetCallback = 0x8797C0;
    public const uint GenericCallback = 0x8798D0;
    public const int DrawStep = 0x8000;
    public static ImmutableArray<NativeBattleCureFamilyPool> Pools { get; } = [new(0x26D6B30, 0x26D6A68, 100, 2), new(0x26D7350, 0x26D7360, 88, 4), new(0x26D7340, 0x26D6B50, 676, 3)];
}
