using Memoria.FF7.Formats;
using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record CrossoverBattleFireContact(Guid EncounterId, long ActionOrdinal, CrossoverBattleActorId Target, Ff7BattleActionReceipt Result);

/// <summary>First Fire-only mixed rules profile, calibrated to the two original starting characters.</summary>
internal static class CrossoverBattleFireRules
{
    public const int Version = 1;

    public static CrossoverBattleFireContact ReceiveContact(CrossoverBattleRoster roster, CrossoverBattleAction action, NativeCharacterVitals working, CrossoverFf8BattleKit source, BattleNativeAction fire, SceneBattleEnemy enemy, Ff7BattleAiActor target, IReadOnlyList<KernelSection> hostKernel, Ff7BattleRng random, CrossoverBattleFireContact? completed = null)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(fire);
        ArgumentNullException.ThrowIfNull(enemy);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(hostKernel);
        ArgumentNullException.ThrowIfNull(random);
        if (roster.Version != Version)
        {
            throw new NotSupportedException($"Mixed Fire rules version {roster.Version} is unsupported.");
        }
        CrossoverBattleActorId targetIdentity = new(SessionGame.Ff7, CrossoverBattleActorKind.Enemy, enemy.Placement.EnemyId, enemy.FormationSlot + 4);
        if (action.Ability != new CrossoverBattleAbilityId(SessionGame.Ff8, CrossoverBattleAbilityKind.Spell, 1) || action.Target != targetIdentity || target.Slot != targetIdentity.Occurrence || action.OriginRow != source.RecordRow)
        {
            throw new InvalidDataException("Mixed Fire contact requires the admitted Squall action and its physical enemy target.");
        }
        if (!CrossoverBattleBridge.HasCommittedStock(roster, working, action))
        {
            throw new InvalidOperationException("Mixed Fire contact requires its native cast cost receipt.");
        }
        if (completed is not null)
        {
            if (completed.EncounterId != roster.EncounterId || completed.ActionOrdinal != action.Ordinal || completed.Target != action.Target)
            {
                throw new InvalidDataException("A completed mixed Fire contact belongs to a different action occurrence.");
            }
            return completed;
        }
        if (fire is not { Command: 2, ActionId: 1, KernelSection: 1, KernelIndex: 1, AttackType: 2, Power: 18, Element: 1, Statuses0: 0, Statuses1: 0 } || !source.Spells.Contains(fire) || !source.Commands.Contains((byte)2))
        {
            throw new NotSupportedException("Mixed Fire requires Squall's original available Fire ability record.");
        }
        if (target.Statuses != 0 || source.Statuses.Saved != 0 || source.Statuses.Battle != 0)
        {
            throw new NotSupportedException("Mixed Fire requires a native FFVII enemy and the status-free initial rules profile.");
        }

        ReadOnlyMemory<byte> hostAttacks = hostKernel.Single(section => section.Type == 1).Data;
        const int fireIndex = 27;
        if (hostAttacks.Length < (fireIndex + 1) * Ff7BattleAttack.RecordSize)
        {
            throw new InvalidDataException("Mixed Fire requires the host's original kernel spell calibration record.");
        }
        Ff7BattleAttack reference = Ff7BattleAttack.Read(hostAttacks.Span.Slice(fireIndex * Ff7BattleAttack.RecordSize, Ff7BattleAttack.RecordSize));
        if (reference is not { DamageCalculation: 0x22, Power: 8, Accuracy: 100, Elements: 1 })
        {
            throw new NotSupportedException("Mixed Fire requires the original FFVII host calibration profile.");
        }
        byte level = Map(source.Level, 7, 6, 100, 99);
        byte magic = ProjectMagic(source.Stats.Magic);
        byte luck = Map(source.Stats.Luck, 16, 14, 255, 255);
        Ff7BattleDamageActor attacker = new(level, 0, magic, 0, luck, 0, 0, 0, 0);
        Ff7BattleDamageActor defender = Ff7NativeBattleDamage.FromEnemy(enemy) with { Defense = target.Defense, MagicDefense = target.MagicDefense };
        Ff7BattleActionVitals sourceVitals = new(source.HitPoints, (uint)source.MaximumHitPoints, 0, 0, 0);
        Ff7BattleActionVitals targetVitals = new(target.HitPoints, target.MaxHitPoints, target.MagicPoints, target.MaxMagicPoints, target.Statuses, ~enemy.NativeStatusMask);
        Ff7BattleDamageAffinity affinity = Ff7NativeBattleDamage.ResolveEnemyAffinity(enemy, reference.Elements);
        Ff7BattleDamageAction mechanics = Ff7BattleDamageAction.FromAttack(reference) with { Affinity = affinity };
        Ff7BattleActionReceipt receipt = Ff7NativeBattleActionResolver.Resolve(attacker, sourceVitals, defender, targetVitals, mechanics, reference, random);
        return new(roster.EncounterId, action.Ordinal, action.Target, receipt);
    }

    public static byte ProjectMagic(int magic) => Map(magic, 6, 23, 255, 255);

    private static byte Map(int value, int sourceReference, int hostReference, int sourceLimit, int hostLimit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, sourceLimit);
        if (value <= sourceReference)
        {
            return (byte)(value * hostReference / sourceReference);
        }
        return (byte)(hostReference + (value - sourceReference) * (hostLimit - hostReference) / (sourceLimit - sourceReference));
    }
}
