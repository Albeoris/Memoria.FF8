using System.Collections.Immutable;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class CrossoverGuestHostActions
{
    private GameplaySaveLimitProgress? originLimitProgress;
    private readonly Dictionary<(ushort Ability, int Target, ushort Targets, bool Geometry), string?> abilityCapabilities = [];

    public void BindLimitProgress(GameplaySaveLimitProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        progress.Validate();
        originLimitProgress = progress;
    }

    private IReadOnlyList<CrossoverGuestAbility> ReadOriginAbilities() => ReadIntegratedAbilities(CrossoverGuestAbilityCatalog.Read(assets, encounter.Working, kit, originLimitProgress, limitSelection?.Crisis ?? 0));

    private BattleNativeAction NativeAbility(ushort ability) => ReadOriginAbilities().SingleOrDefault(choice => choice.Key == ability)?.NativeAction ?? throw new InvalidOperationException("The selected ability is not available in the original guest's current command state.");

    internal string? AbilityTargetUnsupportedReason(ushort ability, int target)
    {
        CrossoverGuestAbility? choice = ReadOriginAbilities().SingleOrDefault(choice => choice.Key == ability);
        if (choice is null)
        {
            return "The original ability is not available.";
        }
        return choice.NativeAction?.TargetFlags is byte flags && TargetSelectionUnsupportedReason(flags, target) is { } reason ? reason : AbilityUnsupportedReason(choice, target);
    }

    public string? PrepareMagicUnsupportedReason(Ff7BattleGuestPhysicalHit prepared) => PreparedAbilityUnsupportedReason(prepared, false);
    public string? PrepareItemUnsupportedReason(Ff7BattleGuestPhysicalHit prepared) => PreparedAbilityUnsupportedReason(prepared, true);

    private string? PreparedAbilityUnsupportedReason(Ff7BattleGuestPhysicalHit prepared, bool item)
    {
        Ff7BattleGuestPreparedContact receipt = prepared.PreparedContact ?? throw new InvalidDataException("The native action requires its prepared host contact.");
        BattleNativeAction original = item ? OriginalItem(receipt.Invocation.GuestAbility) : NativeAbility(receipt.Invocation.GuestAbility);
        byte flags = receipt.Receipt.Damage.Missed ? (byte)1 : original.AttackType is 5 or 6 ? (byte)4 : (byte)0;
        byte healing = receipt.Receipt.Damage.Damage < 0 ? (byte)1 : (byte)0;
        NativeBattleContactPresentation presentation = NativeBattleContactPresentations.Complete(original.DamagePresentation, flags, healing, original.AttackType, 255);
        NativeBattleContactHalf contact = NativeBattleContactHalf.FromScratch(receipt.Invocation.TargetSlot == ActorSlot ? (byte)0 : (byte)3, presentation.Sequence, presentation.Flags2, presentation.HitFlags3, (ushort)(receipt.Receipt.Target.Statuses & 1), Math.Abs(receipt.Receipt.Damage.Damage), 0);
        string? reason = ProbeAbility(original, receipt.Invocation.TargetSlot, contact);
        if (reason is not null)
        {
            ushort key = item ? CrossoverGuestAbilityCatalog.Key(4, original.ActionId) : receipt.Invocation.GuestAbility;
            abilityCapabilities[(key, receipt.Invocation.TargetSlot, AbilityTargetMask(original, receipt.Invocation.TargetSlot), readForeignGeometry is not null)] = reason;
        }
        return reason;
    }

    public IReadOnlyList<Ff7BattleGuestAbility> Abilities
    {
        get
        {
            List<Ff7BattleGuestAbility> result = [];
            foreach (CrossoverGuestAbility choice in ReadOriginAbilities())
            {
                if (HandlesOperation(choice.Key))
                {
                    bool available = host.Actors.Any(actor => actor.Actor.Slot >= 4 && actor.Actor.HitPoints > 0 && OperationAvailable(choice.Key, actor.Actor.Slot));
                    result.Add(new(Ff7BattleCommand.Magic, choice.Key, choice.Name, 0x12, choice.Stock, available ? null : "No compatible enemy is available.") { Category = choice.Category, RequiresMagic = true });
                    continue;
                }
                if (choice.Command == 5 && choice.NativeAction is null && LimitReady)
                {
                    result.Add(new(Ff7BattleCommand.Limit, 0, choice.Name, 0x12) { Category = choice.Category, RequiresMagic = false });
                    continue;
                }
                byte flags = choice.NativeAction?.TargetFlags ?? 0;
                string? reason = choice.RuleUnsupportedReason ?? (choice.NativeAction is { } original ? CrossoverBattleAbilityRules.UnsupportedReason(original) ?? TargetingUnsupportedReason(original) : "This ability requires its native command selection process.");
                if (reason is null)
                {
                    string? lastReason = null;
                    bool supported = false;
                    foreach (var target in host.Actors.Where(actor => AbilityTargetIsLegal(flags, actor.Actor)))
                    {
                        int slot = target.Actor.Slot;
                        if (!CanSelectTarget(flags, slot))
                        {
                            continue;
                        }
                        string? capability = AbilityUnsupportedReason(choice, slot);
                        if (capability is null)
                        {
                            supported = true;
                            break;
                        }
                        lastReason = capability;
                    }
                    reason = supported ? null : lastReason ?? "No valid target is available for this ability.";
                }
                byte hostFlags = CrossoverBattleAbilityRules.HostTargetFlags(flags);
                result.Add(new(Ff7BattleCommand.Magic, choice.Key, choice.Name, hostFlags, choice.Stock, reason) { Category = choice.Category, RequiresMagic = choice.Command is 2 or 15 or 254 || IsDrawCast(choice.Key), CanTargetDead = (flags & 1) != 0, AllTargets = (flags & 0x30) == 0x20 });
            }
            return result;
        }
    }

    public string? TargetUnsupportedReason(Ff7BattleCommand command, ushort ability, Ff7BattleGuestAttackTarget target)
    {
        if (command == Ff7BattleCommand.Magic && HandlesOperation(ability))
        {
            return OperationAvailable(ability, target.Slot) ? null : "The selected operation is unavailable for this target.";
        }
        if (command == Ff7BattleCommand.Limit)
        {
            return SupportsAbility(command, ability, target) ? null : "The selected native Limit process cannot target this actor.";
        }
        CrossoverGuestAbility? choice = ReadOriginAbilities().SingleOrDefault(choice => choice.Key == ability);
        if (choice?.NativeAction?.TargetFlags is not byte flags)
        {
            return "The original ability has no target selection contract.";
        }
        if (TargetSelectionUnsupportedReason(flags, target.Slot) is { } reason)
        {
            return reason;
        }
        return AbilityUnsupportedReason(choice, target.Slot);
    }

    public bool SupportsAbility(Ff7BattleCommand command, ushort ability, Ff7BattleGuestAttackTarget target)
    {
        if (command == Ff7BattleCommand.Magic && HandlesOperation(ability))
        {
            return OperationAvailable(ability, target.Slot);
        }
        if (command == Ff7BattleCommand.Limit)
        {
            return ability == 0 && LimitReady && SupportsLimit(target);
        }
        CrossoverGuestAbility? choice = ReadOriginAbilities().SingleOrDefault(choice => choice.Key == ability);
        return command == Ff7BattleCommand.Magic && choice?.NativeAction is { TargetFlags: byte flags } && CanSelectTarget(flags, target.Slot) && AbilityUnsupportedReason(choice, target.Slot) is null;
    }

    private bool CanSelectTarget(byte flags, int target) => TargetSelectionUnsupportedReason(flags, target) is null;

    private string? TargetSelectionUnsupportedReason(byte flags, int target)
    {
        if (target is < 0 or 3 or > 9 || host.Actors.SingleOrDefault(actor => actor.Actor.Slot == target) is not { } selected)
        {
            return "The selected actor is not present.";
        }
        bool revive = (flags & 1) != 0;
        bool knockedOut = selected.Actor.HitPoints == 0 || (selected.Actor.Statuses & 1) != 0;
        if (revive && !knockedOut)
        {
            return "The original ability requires a knocked-out target.";
        }
        if (revive && target >= 4)
        {
            return "The host cannot restore a knocked-out enemy after its removal lifecycle.";
        }
        if (!revive && (knockedOut || (selected.Actor.Statuses & 0x4000) != 0))
        {
            return "The original ability requires a living, non-petrified target.";
        }
        if ((selected.Actor.Conditions & 8) == 0 && !(revive && target < 3))
        {
            return "The selected actor is not currently targetable.";
        }
        if ((flags & 0x30) == 0x20 || NativeBattleLimitRules.CanSelectEitherSide(flags))
        {
            return null;
        }
        bool selectedSide = (flags & 0x40) != 0 ? target >= 4 : target < 3;
        return selectedSide ? null : "The original ability cannot select this side.";
    }

    private static bool AbilityTargetIsLegal(byte flags, Ff7BattleAiActor actor)
    {
        bool revive = (flags & 1) != 0;
        bool alive = actor.HitPoints != 0 && (actor.Statuses & 0x4001) == 0;
        bool knockedOut = actor.HitPoints == 0 || (actor.Statuses & 1) != 0;
        return actor.Slot is >= 0 and < 10 and not 3 && (!revive || actor.Slot < 3) && (revive ? knockedOut : alive) && ((actor.Conditions & 8) != 0 || revive && actor.Slot < 3);
    }

    private static string? TargetingUnsupportedReason(BattleNativeAction action)
    {
        if (action.TargetFlags is not byte flags)
        {
            return "The native command has no target selection contract.";
        }
        if ((flags & 2) != 0)
        {
            return "The bridge cannot yet own the native random target selection process.";
        }
        return (flags & 0x30) == 0x30 ? "The native command uses an unsupported target selection mode." : null;
    }

    private string? AbilityUnsupportedReason(CrossoverGuestAbility choice, int target)
    {
        if (IsDrawCast(choice.Key) && !OperationAvailable((ushort)(DrawStockKey | DrawMagic(choice.Key)), DrawSource(choice.Key)))
        {
            return "The Draw source is no longer available.";
        }
        if (choice.RuleUnsupportedReason is { } rule)
        {
            return rule;
        }
        if (choice.NativeAction is not { } original)
        {
            return "This ability requires its native command selection process.";
        }
        if ((CrossoverBattleAbilityRules.UnsupportedReason(original) ?? TargetingUnsupportedReason(original)) is { } unsupported)
        {
            return unsupported;
        }
        return PresentationUnsupportedReason(original, choice.Key, target);
    }

    private string? PresentationUnsupportedReason(BattleNativeAction original, ushort ability, int target)
    {
        ushort targets = AbilityTargetMask(original, target);
        var key = (ability, target, targets, readForeignGeometry is not null);
        if (attack.Visual is null || host.Actors.Any(actor => (targets & (1 << actor.Actor.Slot)) != 0 && !host.IsActorPresentationReady(actor.Actor.Slot)))
        {
            return "The original actor presentation is not ready.";
        }
        if (!abilityCapabilities.TryGetValue(key, out string? reason))
        {
            reason = ProbeAbility(original, target);
            abilityCapabilities.Add(key, reason);
        }
        return reason;
    }

    private string? ProbeAbility(BattleNativeAction original, int target, NativeBattleContactHalf? preparedContact = null)
    {
        if (attack.Visual is not { } visual)
        {
            return "The original caster model is not ready.";
        }
        CrossoverGunbladeHostPlacement placement = new(Ff7BattlePosition.FromVector(host.VisualPlacement(ActorSlot).Position), host.VisualPlacement(ActorSlot).Yaw);
        NativeBattleContactPresentation presentation = NativeBattleContactPresentations.Complete(original.DamagePresentation, original.AttackType is 5 or 6 ? (byte)4 : (byte)0, original.AttackType is 3 or 4 or 5 or 6 or 21 or 25 or 32 ? (byte)1 : (byte)0, original.AttackType, 255);
        NativeBattleContactHalf contact = preparedContact ?? NativeBattleContactHalf.FromScratch(target == ActorSlot ? (byte)0 : (byte)3, presentation.Sequence, presentation.Flags2, presentation.HitFlags3, 0, 0, 0);
        try
        {
            ushort mask = AbilityTargetMask(original, target);
            int[] slots = host.Actors.Where(actor => (mask & (1 << actor.Actor.Slot)) != 0).Select(actor => actor.Actor.Slot).Order().ToArray();
            ImmutableArray<CrossoverNativeMagicTarget> targets = CrossoverNativeMagicTarget.Bind(ActorSlot, slots, (_, index) => contact with { ActorIndex = index });
            CrossoverNativeMagicPresentationState state = fireProgram.Begin(visual.Model with { Index = 0 }, target, encounter.CrtRandom, targets[0].Contact, original, encounter.GlobalRandom, CreateArena(placement)) with { Targets = targets };
            return fireProgram.TryPreflight(state,
                () => ReadAbilityAnchor(placement, target),
                () => ReadAbilityTransform(placement, target),
                CrossoverNativeSpriteRenderer.Camera(host.RenderCameraOutput, placement.Matrix),
                readForeignGeometry is null ? null : () => CrossoverNativeForeignTarget.Read(host, target, readForeignGeometry(target), placement.Matrix), out string? reason,
                readGroupForeign: readForeignGeometry is null ? null : slot => CrossoverNativeForeignTarget.Read(host, slot, readForeignGeometry(slot), placement.Matrix)) ? null : reason;
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidDataException or InvalidOperationException or ArgumentException or OverflowException)
        {
            return exception.Message;
        }
    }

    private ushort AbilityTargetMask(BattleNativeAction action, int target)
    {
        byte flags = action.TargetFlags ?? throw new InvalidDataException("The original action has no target contract.");
        if ((flags & 0x30) == 0x10)
        {
            return checked((ushort)(1 << target));
        }
        bool all = (flags & 0x30) == 0x20;
        ushort mask = 0;
        foreach (var actor in host.Actors)
        {
            int slot = actor.Actor.Slot;
            if (AbilityTargetIsLegal(flags, actor.Actor) && (all || (slot < 3) == (target < 3)))
            {
                mask |= (ushort)(1 << slot);
            }
        }
        return mask;
    }

    private NativeBattleModuleAnchor ReadAbilityAnchor(CrossoverGunbladeHostPlacement placement, int target)
    {
        NativeBattleVector anchor = placement.ResolvePosition(host.GuestEffectTargetAnchor(target));
        return new(anchor.X, anchor.Y, anchor.Z, Word(host.GuestAbilityTarget(target).CollisionRadius / CrossoverGunbladeHostPlacement.OriginToHostScale));
    }

    private NativeBattleModelTransform ReadAbilityTransform(CrossoverGunbladeHostPlacement placement, int target)
    {
        NativeBattleVector point = placement.ResolvePosition(host.RenderActorPlacement(target).Position);
        short scale = checked((short)NativePrecision53.RoundInteger(host.GuestAbilityTarget(target).CollisionRadius / CrossoverGunbladeHostPlacement.OriginToHostScale * 4096 / 2000));
        return new(point, point, 0, 0, scale);
    }
}
