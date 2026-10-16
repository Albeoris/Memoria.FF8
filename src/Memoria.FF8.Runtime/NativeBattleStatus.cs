using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleStatusRules(byte BattleSpeed, ImmutableArray<byte> Durations);
public sealed record NativeBattleStatusSnapshot(ushort Saved, uint Battle, short[] Timers);
public sealed record NativeBattleStatusAllocation(ushort Saved, uint Battle, ImmutableArray<byte> Resistances);
