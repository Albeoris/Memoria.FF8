using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record CrossoverEnemyScan(long Tick, int Slot, string Name, string Description, int Level, uint HitPoints, uint MaximumHitPoints, ushort MagicPoints, ushort MaximumMagicPoints, string CombatStatistics);
internal sealed record CrossoverDrawCastReceipt(ushort Ability, int SourceSlot, byte Magic, int Quantity);

internal sealed partial class CrossoverGuestHostActions
{
    private const ushort DrawStockKey = 0xE000;
    private CrossoverDrawCastReceipt? preparedDrawCast;
    public CrossoverEnemyScan? LastScan { get; private set; }

    private IReadOnlyList<CrossoverGuestAbility> ReadIntegratedAbilities(IReadOnlyList<CrossoverGuestAbility> original)
    {
        var enemies = host.Actors.Where(actor => actor.Actor.Slot >= 4).Select(actor => host.EnemyExtension(actor.Actor.Slot)).OfType<Ff7ResolvedEnemyExtension>().ToArray();
        if (enemies.Length == 0)
        {
            return original;
        }
        List<CrossoverGuestAbility> choices = original.Where(choice => choice.Command != 6).ToList();
        if (kit.Commands.Contains((byte)6))
        {
            foreach (byte magic in enemies.SelectMany(enemy => enemy.DrawSlots).Select(slot => slot.MagicId).Distinct().Order())
            {
                BattleActionDefinition spell = assets.ActionDefinitions.Single(action => action.Ability == BattleAbility.Magic && action.Id == magic);
                choices.Add(new((ushort)(DrawStockKey | magic), 6, "Draw", spell.DisplayName ?? spell.Name, null, 1, null));
            }
            foreach (var actor in host.Actors.Where(actor => actor.Actor.Slot >= 4))
            {
                if (host.EnemyExtension(actor.Actor.Slot) is not { } profile)
                {
                    continue;
                }
                foreach (var slot in profile.DrawSlots)
                {
                    BattleNativeAction action = assets.NativeActions.Single(action => action.Command == 2 && action.ActionId == slot.MagicId);
                    ushort key = (ushort)(0xD000 | ((actor.Actor.Slot - 4) << 6) | slot.MagicId);
                    choices.Add(new(key, 6, "Draw Cast", $"{action.Name} ({host.Name(actor.Actor.Slot)})", action, 1, null));
                }
            }
        }
        return choices;
    }

    public bool HandlesOperation(ushort ability) => (ability & 0xFF00) == DrawStockKey || ability == 50 && host.Actors.Any(actor => actor.Actor.Slot >= 4 && host.EnemyExtension(actor.Actor.Slot) is not null);

    private static bool IsDrawCast(ushort ability) => (ability & 0xFC00) == 0xD000;
    private static int DrawSource(ushort ability) => 4 + ((ability & 0x3FF) >> 6);
    private static byte DrawMagic(ushort ability) => (byte)(ability & 63);

    public bool PrepareSource(ushort ability, Func<byte> nextRandomByte, out string? failure)
    {
        preparedDrawCast = null;
        failure = null;
        if (!IsDrawCast(ability))
        {
            return true;
        }
        int sourceSlot = DrawSource(ability);
        byte magic = DrawMagic(ability);
        if (!OperationAvailable((ushort)(DrawStockKey | magic), sourceSlot) || !host.Actors.Any(actor => actor.Actor.Slot == sourceSlot && actor.Actor.HitPoints > 0 && (actor.Actor.Conditions & 8) != 0))
        {
            failure = "The Draw source is no longer available.";
            return false;
        }
        var source = host.EnemyExtension(sourceSlot)!;
        var slot = source.DrawSlots.Single(entry => entry.MagicId == magic);
        BattleActionDefinition spell = assets.ActionDefinitions.Single(action => action.Ability == BattleAbility.Magic && action.Id == magic);
        int quantity = NativeBattleDrawRules.Quantity(kit.Level, source.Level, kit.Stats.Magic, spell.DrawResist, slot.Resistance, nextRandomByte());
        if (quantity == 0)
        {
            failure = "Draw failed.";
            return false;
        }
        preparedDrawCast = new(ability, sourceSlot, magic, quantity);
        encounter.Working = NativeBattleDrawRules.Resolve(encounter.Working, kit.RecordRow, magic, quantity, true, out _);
        return true;
    }

    private bool OperationAvailable(ushort ability, int target)
    {
        if (target < 4 || host.EnemyExtension(target) is not { } profile || !host.Actors.Any(actor => actor.Actor.Slot == target && actor.Actor.HitPoints > 0 && (actor.Actor.Conditions & 8) != 0))
        {
            return false;
        }
        if (ability == 50)
        {
            return kit.Commands.Contains((byte)2) && encounter.Working.GetMagicCount(kit.RecordRow, 50) > 0 && !string.IsNullOrWhiteSpace(profile.ScanDescription);
        }
        byte magic = (byte)(ability & 255);
        return kit.Commands.Contains((byte)6) && profile.DrawSlots.Any(slot => slot.MagicId == magic);
    }

    public string ExecuteOperation(ushort ability, int targetSlot, Func<byte> nextRandomByte)
    {
        if (!OperationAvailable(ability, targetSlot))
        {
            return "The enemy is no longer available.";
        }
        var source = host.EnemyExtension(targetSlot)!;
        if (ability == 50)
        {
            CrossoverBattleActorId targetId = roster.Participants.Single(participant => participant.HostSlot == targetSlot).Combat;
            CrossoverBattleAdmission admission = CrossoverBattleBridge.Prepare(roster, kit, encounter.Working, encounter.NextOrdinal, new(SessionGame.Ff8, CrossoverBattleAbilityKind.Spell, 50), targetId);
            if (admission.Action is not { } action)
            {
                return admission.Reason ?? "Scan is unavailable.";
            }
            encounter.Working = CrossoverBattleBridge.CommitStock(roster, admission.Vitals, action);
            encounter.CommittedSpells = encounter.CommittedSpells.Add(new(encounter.NextOrdinal++, 50, kit.RecordRow));
            encounter.ReprojectKits(ActorSlot);
            var enemy = host.Actors.Single(actor => actor.Actor.Slot == targetSlot).Actor;
            var native = host.GuestAbilityTarget(targetSlot).Native ?? throw new InvalidDataException("Scan requires the FFVII target definition.");
            string[] elements = ["Fire", "Ice", "Lightning", "Earth", "Poison", "Gravity", "Water", "Wind", "Holy"];
            string affinities = string.Join("; ", native.Affinities.Where(affinity => affinity.Type < elements.Length).Select(affinity => $"{elements[affinity.Type]}: {affinity.Rate switch { 2 => "Weak", 4 => "Resist", 5 => "Immune", 6 => "Absorb", _ => "Special" }}"));
            string statistics = $"ATK {source.Attack}   MAG {source.Magic}   DEF {enemy.Defense}   MDEF {enemy.MagicDefense}   SPD {native.Speed}   EVA {native.Evasion}   LUCK {native.Luck}";
            if (affinities.Length > 0)
            {
                statistics += "\n" + affinities;
            }
            LastScan = new(host.Tick, targetSlot, host.Name(targetSlot), source.ScanDescription ?? throw new InvalidDataException("Scan requires the resolved description."), source.Level, enemy.HitPoints, enemy.MaxHitPoints, enemy.MagicPoints, enemy.MaxMagicPoints, statistics);
            return "Scan complete.";
        }
        byte magic = (byte)(ability & 255);
        var slot = source.DrawSlots.Single(entry => entry.MagicId == magic);
        BattleActionDefinition spell = assets.ActionDefinitions.Single(action => action.Ability == BattleAbility.Magic && action.Id == magic);
        int quantity = NativeBattleDrawRules.Quantity(kit.Level, source.Level, kit.Stats.Magic, spell.DrawResist, slot.Resistance, nextRandomByte());
        encounter.Working = NativeBattleDrawRules.Resolve(encounter.Working, kit.RecordRow, magic, quantity, false, out MagicStockMutation? mutation);
        MagicStockMutation result = mutation ?? throw new InvalidDataException("Stock Draw requires its inventory receipt.");
        if (quantity > 0)
        {
            encounter.CommittedSpells = encounter.CommittedSpells.Add(new(encounter.NextOrdinal++, magic, kit.RecordRow, quantity));
        }
        encounter.ReprojectKits(ActorSlot);
        return quantity == 0 ? "Draw failed." : result.Accepted == 0 ? "No room to stock this magic." : $"Drew {result.Accepted} {spell.DisplayName ?? spell.Name}.";
    }
}
