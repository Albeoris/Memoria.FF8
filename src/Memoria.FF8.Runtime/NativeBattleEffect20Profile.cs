using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect20Profile
{
    public const uint Entry = 0x89FBC0;
    public const uint Initializer = 0x89FBA0;
    public const uint InitializerGlobal = 0x2721590;
    public const uint RootCallback = 0x89FD00;
    public const uint TargetCallback = 0x89FFB0;
    public const uint MainCallback = 0x8A0070;
    public const uint SpriteCallback = 0x8A01B0;
    public const uint ParticleCallback = 0x8A0990;
    public const uint FadeInCallback = 0x89FE90;
    public const uint FadeOutCallback = 0x8A3150;
    public const int DrawStep = 0xA000;
    public static ImmutableArray<NativeBattleCureFamilyPool> Pools { get; } = [new(0x2721680, 0x27215B8, 100, 2), new(0x2726770, 0x2726780, 88, 4), new(0x2721690, 0x27216A0, 120, 40), new(0x2722968, 0x2722978, 152, 100), new(0x2726760, 0x27264E0, 64, 10)];
}
