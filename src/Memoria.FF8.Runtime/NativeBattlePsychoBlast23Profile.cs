using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattlePsychoBlast23Profile
{
    public const uint Entry = 0x893020;
    public const uint Initializer = 0x893000;
    public const uint InitializerGlobal = 0x27011B8;
    public const uint RootCallback = 0x893190;
    public const uint TargetCallback = 0x8933D0;
    public const uint DirectorCallback = 0x893500;
    public const uint GenericCallback = 0x896250;
    public const uint ActDelayedCallback = 0x893950;
    public const uint ActPrimaryCallback = 0x896150;
    public const uint ActFollowupCallback = 0x895FF0;
    public const uint DebrisEmitterCallback = 0x899B80;
    public const uint DebrisCallback = 0x899CC0;
    public const uint SharedAddress = 0x27041B8;
    public const uint BasisAddress = 0x27012B0;
    public const uint MorphScratchAddress = 0x2704220;
    public const int MorphScratchLength = 672;
    public const int DrawStep = 0x8000;
    public static ImmutableArray<NativeBattleCureFamilyPool> Pools { get; } = [new(0x27013D0, 0x2701308, 100, 2), new(0x2704038, 0x2704048, 88, 4), new(0x27012A0, 0x27011C8, 72, 3), new(0x2703D98, 0x2703AF0, 676, 1), new(0x2703AD8, 0x2703658, 384, 3), new(0x27013E0, 0x27013F0, 176, 50), new(0x2704028, 0x2703DA8, 64, 10)];
}
