namespace Memoria.FF8.Runtime;

public sealed record NativeBattleAiAbility(byte Command, byte Sequence, ushort Id, int AttackType = 0, int Power = 0, int Element = 0, ushort Statuses0 = 0, uint Statuses1 = 0, int StatusAttack = 0);
public sealed record NativeBattleAiDefinition(byte[][] Scripts, NativeBattleAiAbility[] Abilities, int LevelBand, string[] Texts);
public sealed record NativeBattleEnemyLevel(int MaximumHp, int Strength, int Vitality, int Magic, int Spirit, int Speed, int[] ElementDefenses, BattleDrawSpell[] DrawSpells, int[] DrawGuardianForces, int AiLevelBand);
public sealed record NativeBattleEnemyTemplate(byte SceneLevel, NativeBattleEnemyLevel[] Levels);
public sealed record NativeBattleParticipantSnapshot(bool Active, bool Hidden, bool Untargetable, bool Suspended, int ActorIndex, int? ResolvedLevel, uint[] Variables, byte TurnCount, string? LastAttackerId, BattleAbility? LastAttackerAbility, bool DeathHandled);
