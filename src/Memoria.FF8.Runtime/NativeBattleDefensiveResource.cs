using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Immutable native571240 mesh binding from off_C81678[8+variant].</summary>
public sealed record NativeBattleDefensiveResource(byte Variant, uint Address, ImmutableArray<byte> Bytes);
