using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleStorm11Resources(ImmutableArray<byte> Stream, NativeBattleGfAnimationSource Mouth, NativeBattleGfAnimationSource Cone, ImmutableArray<byte> Debris, ImmutableArray<byte> Ring, ImmutableArray<uint> MouthFlags, ImmutableArray<uint> ConeFlags, ImmutableArray<NativeBattleModuleResource> Sprites, NativeBattleEffectHeapAllocation Camera, uint Sound);
