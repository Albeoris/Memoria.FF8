using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Immutable571240/C81678 mesh with its original vertex and eight primitive-group directories.</summary>
public sealed record NativeBattleStaticMeshResource(byte Id, uint Address, ImmutableArray<byte> Bytes);
