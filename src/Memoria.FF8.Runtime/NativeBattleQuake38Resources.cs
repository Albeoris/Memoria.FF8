using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleQuake38Resources(NativeBattleEffectInitializerAsset Texture, NativeBattleEffectHeapAllocation Camera, NativeBattleModuleResource Chip, NativeBattleModuleResource Dust, NativeBattleModuleResource Rock, ImmutableArray<byte> ZoneMap, ImmutableArray<byte> DelayMap, ImmutableArray<short> WallDepth, ImmutableArray<byte> StepShade, ImmutableArray<short> ShakeA, ImmutableArray<short> ShakeB, uint RumbleSound, uint QuakeSound);
