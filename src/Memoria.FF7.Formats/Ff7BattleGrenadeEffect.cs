namespace Memoria.FF7.Formats;

public sealed record Ff7BattleGrenadeState(int Frame, ushort Targets, short X = 0, short Y = 0, short Z = 0, int CollisionRadius = 0);
