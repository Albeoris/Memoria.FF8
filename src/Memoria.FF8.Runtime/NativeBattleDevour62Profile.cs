using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDevour62Profile
{
    public const uint Entry = 0x7E4B00;
    public const uint Initializer = 0x7E4AB0;
    public const uint InitializerGlobal = 0x2601318;
    public const uint RootCallback = 0x7E4C10;
    public const uint TargetCallback = 0x7E4D90;
    public const uint AuxiliaryCallback = 0x7E4E30;
    public const uint QuadCallback = 0x7E4F60;
    public const uint ActorFadeCallback = 0x7E5290;
    public const uint MusicCallback = 0x7E55F0;
    public const int DrawStep = 0x6000;
    public static ImmutableArray<NativeBattleCureFamilyPool> Pools { get; } = [new(0x2601408, 0x2601340, 100, 2), new(0x2601780, 0x2601790, 88, 4), new(0x2601418, 0x2601428, 84, 10)];
}
