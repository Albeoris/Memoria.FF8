using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public interface IGameTableOverrideSource
{
    GameTableOverrides? TableOverrides { get; }
}

public sealed record GameTableOverrides(NativeSalaryTable Salary, ImmutableArray<byte> DrawPoints, ImmutableArray<TripleTriadCard> Cards);
