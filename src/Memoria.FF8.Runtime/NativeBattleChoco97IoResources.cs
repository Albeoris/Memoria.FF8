using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleChoco97IoResources(NativeBattleEffectHeapAllocation Script, ImmutableArray<NativeBattleEffectFileAsset> Files);
