using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect60Profile
{
    public const uint Entry = 0x7EB090;
    public const uint Initializer = 0x7EB070;
    public const uint InitializerGlobal = 0x2604078;
    public const uint RootCallback = 0x7EB200;
    public const uint TargetCallback = 0x7EB440;
    public const uint DirectorCallback = 0x7EB580;
    public const uint GenericPrimaryCallback = 0x7EE0F0;
    public const uint GenericSecondaryCallback = 0x7F1A30;
    public const uint ActPrimaryCallback = 0x7EB8C0;
    public const uint ActSecondaryCallback = 0x7EDE50;
    public const uint DebrisEmitterCallback = 0x7F1C70;
    public const uint DebrisCallback = 0x7F1E50;
    public const uint StageCallback = 0x7F1B20;
    public const uint SharedAddress = 0x2606C00;
    public const uint SavedRotationAddress = 0x2606C58;
    public const uint MorphScratchAddress = 0x2606C78;
    public const int MorphScratchLength = 968;
    public const int DrawStep = 0x8000;
    public static ImmutableArray<NativeBattleCureFamilyPool> Pools { get; } = [new(0x2604258, 0x2604190, 100, 2), new(0x2606A80, 0x2606A90, 88, 4), new(0x2604160, 0x2604088, 72, 3), new(0x26067E0, 0x2606298, 676, 2), new(0x2606280, 0x2605DD8, 396, 3), new(0x2604268, 0x2604278, 140, 50), new(0x2606A70, 0x26067F0, 64, 10)];
}
