namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private BattleLimitFamily? ReadActivePresentationLimitFamily(BattleModelOwner owner)
    {
        BattleCombatant? actor = allies.FirstOrDefault(candidate => CreatePresentationModelOwner(candidate) == owner);
        if (actor is null)
        {
            return null;
        }
        if (nativeDuelProcesses.ContainsKey(actor.Id))
        {
            return BattleLimitFamily.Duel;
        }
        if (nativeShotProcesses.ContainsKey(actor.Id))
        {
            return BattleLimitFamily.Shot;
        }
        if (nativeRenzokukenProcesses.ContainsKey(actor.Id))
        {
            return BattleLimitFamily.Renzokuken;
        }
        if (ActiveAction is { } active && active.ActorId == actor.Id)
        {
            if (active.NativePlayerReaction is BattleNativePlayerReaction.AngeloRush or BattleNativePlayerReaction.AngeloRecover or BattleNativePlayerReaction.AngeloReverse or BattleNativePlayerReaction.AngeloPeriodic)
            {
                return BattleLimitFamily.Combine;
            }
            if (active.FreeNativeMagic && HasNativeAngelWing(actor))
            {
                return BattleLimitFamily.AngelWing;
            }
            if (active.Ability == BattleAbility.LimitBreak)
            {
                return PresentationLimitFamily(active.NativeAction, active.NativeQueuedCommand);
            }
        }
        if (expandedHostedEvent is { Command: { } command } && command.ActorId == actor.Id && command.Ability == BattleAbility.LimitBreak)
        {
            return command.NativeDuel is not null ? BattleLimitFamily.Duel : command.NativeRenzokuken is not null ? BattleLimitFamily.Renzokuken : PresentationLimitFamily(command.NativeAction, command.NativeQueuedCommand);
        }
        return null;
    }

    private static BattleLimitFamily? PresentationLimitFamily(BattleNativeAction? action, byte? command)
    {
        return command switch
        {
            5 or 249 or 250 => BattleLimitFamily.Renzokuken,
            11 or 239 or 241 => BattleLimitFamily.Duel,
            14 or 237 or 238 => BattleLimitFamily.Shot,
            15 => BattleLimitFamily.BlueMagic,
            16 or 247 => BattleLimitFamily.Slot,
            19 => action?.ActionId == 4 ? BattleLimitFamily.AngelWing : BattleLimitFamily.Combine,
            _ => action?.CharacterId is >= 6 and <= 10 ? BattleLimitFamilyRules.NativeFamilies(action.CharacterId.Value).First() : null
        };
    }

    private static BattleLimitFamily NativeLimitFamily(BattleNativeLimitDefinition definition)
    {
        return BattleLimitFamilyRules.NativeFamilies(definition.CharacterId).First();
    }

    private static IReadOnlyList<BattleNativeLimitFamilyDefinition> AssignedNativeLimitFamilies(BattleCombatant actor)
    {
        if (actor.NativeLimitFamilies is { } families)
        {
            return families;
        }
        if (actor.NativeLimitReadiness is not { } native)
        {
            return [];
        }
        return native.Command == 19 ? [new(BattleLimitFamily.Combine, native), new(BattleLimitFamily.AngelWing, native)] : [new(NativeLimitFamily(native), native)];
    }

    public bool HasNativeLimitFamily(string actorId, BattleLimitFamily family)
    {
        BattleCombatant actor = GetActor(actorId);
        if (actor.NativeLimitFamilies is null && actor.NativeLimitReadiness is null)
        {
            return actor.IsPlayer && actor.ModelId is >= 0 and <= 10 && BattleLimitFamilyRules.NativeFamilies(actor.ModelId).Contains(family);
        }
        return AssignedNativeLimitFamilies(actor).Any(entry => entry.Family == family);
    }

    private string? NativeLimitFamilyUnavailable(BattleCombatant actor, BattleNativeLimitFamilyDefinition family)
    {
        BattleWeaponCapabilities required = BattleLimitFamilyRules.RequiredWeapon(family.Family);
        if (required != BattleWeaponCapabilities.None && (actor.WeaponProfile is { } weapon ? (weapon.Capabilities & required) != required : actor.NativeLimitFamilies is not null))
        {
            return $"Requires {required}.";
        }
        if (family.Family == BattleLimitFamily.Combine && ((definition.NativeSavedBattleFlagsKnownMask & 0x10) == 0 || (CurrentNativeSavedBattleFlags() & 0x10) != 0 || definition.LimitProgress is not { AngeloFlags: >= 0x10 }))
        {
            return "Combine is unavailable in the current story or learned state.";
        }
        if (family.Family == BattleLimitFamily.AngelWing && ((definition.NativeSavedBattleFlagsKnownMask & 0x20) == 0 || (CurrentNativeSavedBattleFlags() & 0x20) == 0))
        {
            return "Angel Wing is not unlocked.";
        }
        if (family.Family == BattleLimitFamily.Shot && !nativeItems.Any(stock => stock.ItemId is >= 101 and <= 108 && stock.Count > 0))
        {
            return "No compatible ammunition.";
        }
        return null;
    }

    public IReadOnlyList<BattleNativeLimitFamilyChoice> GetNativeLimitFamilies(string actorId)
    {
        BattleCombatant actor = GetActor(actorId);
        return AssignedNativeLimitFamilies(actor).Select(entry =>
        {
            string name = entry.Family is BattleLimitFamily.Combine or BattleLimitFamily.AngelWing ? entry.Definition.InvocationChoices.ElementAtOrDefault(entry.Family == BattleLimitFamily.Combine ? 0 : 1)?.Name ?? entry.Family.ToString() : definition.NativeCommandNames.ElementAtOrDefault(entry.Definition.Command) ?? entry.Family.ToString();
            string? reason = NativeLimitFamilyUnavailable(actor, entry);
            return new BattleNativeLimitFamilyChoice(entry.Family, name, reason is null, reason);
        }).ToArray();
    }

    public void SelectNativeLimitFamily(string actorId, BattleLimitFamily family)
    {
        RequireNoPendingSlotsDeal();
        BattleCombatant actor = GetActor(actorId);
        if (!CanChoosePlayerCommand(actorId) || GetNativeLimitLevel(actorId) == 0 || nativeShotProcesses.ContainsKey(actorId) || nativeDuelProcesses.ContainsKey(actorId) || nativeRenzokukenProcesses.ContainsKey(actorId))
        {
            throw new InvalidOperationException("Limit family selection requires a ready actor and retained crisis.");
        }
        BattleNativeLimitFamilyDefinition selected = AssignedNativeLimitFamilies(actor).SingleOrDefault(entry => entry.Family == family) ?? throw new InvalidOperationException("Limit family is not assigned to this actor.");
        if (NativeLimitFamilyUnavailable(actor, selected) is { } reason)
        {
            throw new InvalidOperationException(reason);
        }
        actor.SelectedNativeLimitFamily = family;
    }

    private void RequireSelectedNativeLimitFamily(BattleCombatant actor)
    {
        BattleNativeLimitFamilyDefinition? selected = AssignedNativeLimitFamilies(actor).FirstOrDefault(entry => entry.Family == actor.SelectedNativeLimitFamily) ?? AssignedNativeLimitFamilies(actor).FirstOrDefault();
        if (selected is null)
        {
            throw new InvalidOperationException("No Limit family is assigned.");
        }
        if (NativeLimitFamilyUnavailable(actor, selected) is { } reason)
        {
            throw new InvalidOperationException(reason);
        }
    }
}
