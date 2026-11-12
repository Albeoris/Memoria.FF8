using System.Buffers.Binary;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public enum Ff7BattleCommand : byte
{
    Attack = 1,
    Magic = 2,
    Item = 4,
    Limit = 0x14,
    Change = 0x12,
    Defend = 0x13
}

public sealed record Ff7BattleCommandOption(Ff7BattleCommand Command, ushort Argument, ushort ValidTargets, bool SelectSingle, bool DefaultsToEnemies, int Cost = 0, int GroupUses = 0)
{
    public bool AllTargets { get; init; }
    public ushort? CandidateTargets { get; init; }
    public string? DisplayName { get; init; }
    public string? Category { get; init; }
    public string? UnsupportedReason { get; init; }
}

public sealed partial class Ff7BattleSession
{
    public IReadOnlyList<Ff7BattleCommandOption> Commands(int slot)
    {
        if (slot != ReadyPlayer)
        {
            return [];
        }
        Ff7BattleActorState actor = Actor(slot);
        Ff7BattlePlayerStats stats = party[slot]!.Value;
        List<Ff7BattleCommandOption> commands = [];
        if (Ff7NativeBattleLimits.IsReady(actor.LimitGauge) && limits.Count != 0)
        {
            foreach (byte limit in Ff7NativeBattleLimits.ReadLearnedIds(characters[slot]!, kernel))
            {
                if (limit is 0x80 or 0x87 || limit == 0x95 && beatRushSymbols.Count != 0)
                {
                    commands.Add(Option(Ff7BattleCommand.Limit, limit, limits[limit - 0x80].TargetFlags));
                }
            }
        }
        if (commands.Count == 0)
        {
            commands.Add(Option(Ff7BattleCommand.Attack, 0, stats.AttackTargetFlags));
        }
        foreach (Ff7BattleSpell spell in Ff7NativeBattleSpells.Read(characters[slot]!, kernel))
        {
            if (spell.Attack.CastingCost <= actor.Actor.MagicPoints && (actor.Actor.Statuses & 0x80) == 0)
            {
                commands.Add(Option(Ff7BattleCommand.Magic, spell.AttackId, spell.Attack.TargetFlags, spell.Attack.CastingCost) with { GroupUses = actor.AllUses?.GetValueOrDefault(spell.AttackId) ?? 0 });
            }
        }
        foreach (Ff7InventorySlot item in state.Inventory)
        {
            if (item.Quantity > 0 && ReadBattleItem((ushort)item.ItemId) is { } attack)
            {
                commands.Add(Option(Ff7BattleCommand.Item, (ushort)item.ItemId, attack.TargetFlags, item.Quantity, item.ItemId == 7));
            }
        }
        commands.Add(new(Ff7BattleCommand.Change, 0, (ushort)(1 << slot), true, false));
        commands.Add(new(Ff7BattleCommand.Defend, 0, (ushort)(1 << slot), true, false));
        if (IsGuestVitalsSlot(slot))
        {
            IFf7BattleGuestAttack? guestAttack = GetGuestAttack(slot);
            IFf7BattleGuestItems? items = guestAttack as IFf7BattleGuestItems;
            List<Ff7BattleCommandOption> guest = items?.HasOriginItems == true ? [] : commands.Where(option => option.Command == Ff7BattleCommand.Item).ToList();
            if (guestAttack is not null)
            {
                ushort targets = Mask(state.Actors.Where(target => target.Actor.Slot >= 4 && IsAlive(target) && (target.Actor.Conditions & 8) != 0 && guestAttack.Supports(DescribeGuestTarget(target.Actor.Slot))));
                guest.Insert(0, new(Ff7BattleCommand.Attack, 0, targets, true, true));
                if (guestAttack is not IFf7BattleGuestAbilities && guestAttack is IFf7BattleGuestLimit { LimitReady: true } limit)
                {
                    ushort limitTargets = Mask(state.Actors.Where(target => GuestLimitTarget(limit, target)));
                    if (limitTargets != 0)
                    {
                        guest.Insert(0, new(Ff7BattleCommand.Limit, 0, limitTargets, true, true));
                    }
                }
                if (guestAttack is IFf7BattleGuestAbilities abilities)
                {
                    int abilityIndex = 1;
                    foreach (Ff7BattleGuestAbility ability in abilities.Abilities)
                    {
                        bool enemies = (ability.TargetFlags & 2) != 0;
                        bool locked = (ability.TargetFlags & 0x10) != 0;
                        bool single = (ability.TargetFlags & 4) == 0 || (ability.TargetFlags & 8) != 0;
                        ushort candidates = Mask(state.Actors.Where(target => target.Actor.Slot != 3 && (!ability.CanTargetDead || target.Actor.Slot < 3) && ((target.Actor.Conditions & 8) != 0 || ability.CanTargetDead && target.Actor.Slot < 3) && IsAlive(target) != ability.CanTargetDead && (!locked || (target.Actor.Slot >= 4) == enemies)));
                        ushort abilityTargets = Mask(state.Actors.Where(target => (candidates & (1 << target.Actor.Slot)) != 0 && abilities.SupportsAbility(ability.Command, ability.Ability, DescribeGuestTarget(target.Actor.Slot))));
                        string? reason = ability.UnsupportedReason;
                        if (reason is null && ability.RequiresMagic && !Ff7GuestBattleStatuses.CanCastMagic(actor.Actor.Statuses))
                        {
                            reason = "The character's current status prevents using this ability.";
                        }
                        if (reason is null && candidates == 0)
                        {
                            reason = "No valid target is available for this ability.";
                        }
                        guest.Insert(abilityIndex++, new(ability.Command, ability.Ability, abilityTargets, single, enemies, ability.Stock) { AllTargets = ability.AllTargets, CandidateTargets = candidates, DisplayName = ability.Name, Category = ability.Category, UnsupportedReason = reason });
                    }
                }
                else if (guestAttack is IFf7BattleGuestMagic magic && Ff7GuestBattleStatuses.CanCastMagic(actor.Actor.Statuses))
                {
                    int magicIndex = 1;
                    foreach (ushort ability in magic.MagicIds)
                    {
                        ushort magicTargets = Mask(state.Actors.Where(target => IsAlive(target) && (target.Actor.Conditions & 8) != 0 && SupportsGuestAbility(guestAttack, ability, DescribeGuestTarget(target.Actor.Slot))));
                        if (magicTargets != 0)
                        {
                            guest.Insert(magicIndex++, new(Ff7BattleCommand.Magic, ability, magicTargets, true, (magicTargets & 0x3F0) != 0, magic.MagicStock(ability)));
                        }
                    }
                }
            }
            if (items?.HasOriginItems == true)
            {
                guest.AddRange(GuestItemOptions(items));
            }
            return guest;
        }
        return commands;

        Ff7BattleCommandOption Option(Ff7BattleCommand command, ushort argument, byte flags, int cost = 0, bool revive = false)
        {
            bool enemies = (flags & 2) != 0;
            bool locked = (flags & 0x10) != 0 || revive;
            ushort validTargets = Mask(state.Actors.Where(target => (!IsGuestVitalsSlot(target.Actor.Slot) || GuestRecoveryCommand(command, argument)) && (!locked || (target.Actor.Slot >= 4) == enemies) && (IsAlive(target) != revive) && ((target.Actor.Conditions & 8) != 0 || revive && target.Actor.Slot < 3)));
            return new(command, argument, validTargets, (flags & 4) == 0 || (flags & 8) != 0, enemies, cost);
        }
    }

    public bool Submit(int slot, Ff7BattleCommand command, ushort argument, ushort targets, bool group = false)
    {
        if (IsGuestVitalsSlot(slot) && command == Ff7BattleCommand.Attack && argument == 0 && !group && System.Numerics.BitOperations.IsPow2((uint)targets))
        {
            return SubmitGuestAttack(slot, System.Numerics.BitOperations.TrailingZeroCount((uint)targets));
        }
        if (IsGuestVitalsSlot(slot) && command == Ff7BattleCommand.Limit)
        {
            return argument == 0 && !group && System.Numerics.BitOperations.IsPow2(targets) && SubmitGuestLimit(slot, System.Numerics.BitOperations.TrailingZeroCount(targets));
        }
        if (IsGuestVitalsSlot(slot) && command == Ff7BattleCommand.Magic)
        {
            return SubmitGuestMagicTargets(slot, targets, argument, group);
        }
        if (IsGuestVitalsSlot(slot) && command == Ff7BattleCommand.Item && GetGuestAttack(slot) is IFf7BattleGuestItems { HasOriginItems: true })
        {
            return !group && System.Numerics.BitOperations.IsPow2((uint)targets) && SubmitGuestItem(slot, System.Numerics.BitOperations.TrailingZeroCount((uint)targets), argument);
        }
        Ff7BattleCommandOption? option = Commands(slot).SingleOrDefault(candidate => candidate.Command == command && candidate.Argument == argument);
        if (option is null || targets == 0 || (targets & ~option.ValidTargets) != 0)
        {
            return false;
        }
        if (group ? option.GroupUses <= 0 || targets != (option.ValidTargets & ((targets & 15) == 0 ? 0x3F0 : 15)) : option.SelectSingle && (targets & (targets - 1)) != 0)
        {
            return false;
        }
        Ff7BattleActorState actor = Actor(slot);
        if (command == Ff7BattleCommand.Limit && argument == 0x95)
        {
            state = state with { BeatRush = new(slot, targets, Ff7BeatRushReel.Start(state.BeatRushPosition)) };
        }
        else
        {
            state = state with { Queue = Ff7BattleCommandQueue.StageReady(state.Queue, slot, (byte)command, argument, targets, group) };
        }
        Replace(actor with { AwaitingTurn = false });
        return true;
    }

    public void CancelBeatRush()
    {
        if (state.BeatRush is { Reel.Stopped: false } selection)
        {
            Ff7BattleActorState actor = Actor(selection.Slot);
            Replace(actor with { AwaitingTurn = true });
            state = state with { BeatRush = null, BeatRushPosition = selection.Reel.Position };
        }
    }

    private void AdvanceBeatRush(bool confirm)
    {
        Ff7BattleLimitSelection selection = state.BeatRush!;
        if (!IsAlive(Actor(selection.Slot)))
        {
            state = state with { BeatRush = null, BeatRushPosition = selection.Reel.Position };
            return;
        }
        Ff7BeatRushReel reel = selection.Reel.Advance(confirm);
        state = state with { BeatRush = selection with { Reel = reel }, BeatRushPosition = reel.Position };
        if (reel.Stopped)
        {
            state = state with { Queue = Ff7BattleCommandQueue.StageReady(state.Queue, selection.Slot, (byte)Ff7BattleCommand.Limit, 0x95, selection.Targets) };
        }
    }

    private static Ff7BattleQueuedCommand ResolveEnemyCommand(Ff7BattleQueuedCommand command)
    {
        if (command.ActorSlot >= 4 && command.CommandId == 0x20 && command.AttackId is < 0x100)
        {
            ushort attack = command.AttackId.Value;
            if (attack < 0x38)
            {
                return command with { CommandId = (byte)Ff7BattleCommand.Magic, Argument = attack, AttackId = null };
            }
            throw new NotSupportedException($"FFVII enemy attack {attack:X2} requires its native command-bank consumer.");
        }
        return command;
    }

    private int Animation(Ff7BattleQueuedCommand command)
    {
        if (command.ActorSlot >= 4 && command.CommandId is 0x20 or (byte)Ff7BattleCommand.Magic)
        {
            ushort attackId = command.AttackId ?? command.Argument;
            return Enemy(command.ActorSlot).Attacks.First(attack => attack.AttackId == attackId).ActionAnimation;
        }
        return command.CommandId switch
        {
            (byte)Ff7BattleCommand.Attack => Actor(command.ActorSlot).Identity == 1 ? 0x14 + (WeaponModelSelector(command.ActorSlot) >> 4) : 0x14,
            (byte)Ff7BattleCommand.Magic => 0x1D,
            (byte)Ff7BattleCommand.Item => (command.TargetMask & 15) != 0 ? 0x20 : 0x21,
            (byte)Ff7BattleCommand.Change => (Actor(command.ActorSlot).Actor.Conditions & 0x40) != 0 ? 3 : 4,
            (byte)Ff7BattleCommand.Limit => command.Argument == 0x95 && BeatRushVisible == Ff7BeatRushSlot.Miss ? -1 : 0x3C,
            _ => -1
        };
    }

    private ushort ResolveQueuedTargets(Ff7BattleQueuedCommand command)
    {
        int slot = command.ActorSlot;
        Ff7BattleAttack? record = command.CommandId switch
        {
            0x20 => Data.Attacks.Single(attack => attack.Slot == command.Argument && attack.Id == command.AttackId).Data,
            (byte)Ff7BattleCommand.Magic => ReadKernelAttack(command.Argument),
            (byte)Ff7BattleCommand.Item => ReadBattleItem(command.Argument),
            (byte)Ff7BattleCommand.Limit => limits[command.Argument - 0x80],
            _ => null
        };
        if (record is null && command.CommandId != (byte)Ff7BattleCommand.Attack)
        {
            return command.TargetMask;
        }
        byte flags = record?.TargetFlags ?? party[slot]!.Value.AttackTargetFlags;
        ushort special = record?.SpecialFlags ?? ushort.MaxValue;
        Ff7BattleAiMasks masks = Ff7BattleAiCallbacks.ProjectMasks(state.Actors.Select(actor => actor.Actor).ToArray(), 0);
        ushort group0 = Mask(state.Actors.Where(actor => FacingGroup(actor.Actor) == 0));
        ushort group2 = Mask(state.Actors.Where(actor => FacingGroup(actor.Actor) == 2));
        Ff7BattleTargetMasks selection = new(masks.TurnEligible, masks.Targetable, masks.KnockedOut, masks.Targetable, group0, group2);
        ushort targets = Ff7NativeBattleTargets.Resolve(slot, command.TargetMask, flags, special, command.GroupTarget, selection, random);
        return Ff7NativeBattleTargets.ResolveNumericalPass(targets, flags, command.CommandId, random);
    }

    private void AdvanceAction(Ff7BattleActionState active)
    {
        Ff7BattleQueuedCommand command = active.Command;
        if (command.CommandId == 0x24 && state.Messages is { BlocksQueue: true })
        {
            return;
        }
        if (!active.Applied)
        {
            BeginSourceModel(active);
            BeginSoundAction();
            cameraTargetRows = [];
            ApplyAction(command);
            command = command with { TargetMask = BeginCameraAction(command) };
            active = active with { Command = command };
            Ff7BattleHit? hit = state.LastHit is { } current && current.Tick == Tick ? current : null;
            active = active with { Applied = true, PendingDisplayHit = hit };
        }
        Ff7BattleAnimationResources? presentation = active.Animation >= 0 ? animations : null;
        if (presentation is not null)
        {
            active = active with { Motion = active.Motion?.Advance() };
            Ff7BattleActorState actor = Actor(command.ActorSlot);
            string model = Ff7BattleAnimationResources.ModelPrefix(actor.Identity, command.ActorSlot >= 4);
            ushort? limit = command.CommandId == (byte)Ff7BattleCommand.Limit ? command.Argument : null;
            BattleActionSequence sequence = presentation.Get(model, active.Animation, limit);
            // TODO(TD-024) Bind the remaining original effect callbacks after native asset readiness.
            AdvanceActorReactionModels(actorModelCursor, command.ActorSlot);
            actorModelCursor = command.ActorSlot + 1;
            BattleActionPoseUpdate update = sequence.Advance(active.Pose ?? new(), effectReady: true, primaryCallbacksReady: !HasPrimaryPresentationCallbacks, linkedActorReady: LinkedModelReady(command.ActorSlot));
            active = active with { Pose = update.State };
            PublishSourceModel(active, update.State);
            foreach (BattleActionInstruction instruction in update.Events)
            {
                if (instruction.Opcode is 0xF7 or 0xBE && command.TargetMask != 0)
                {
                    bool suppressed = instruction.Opcode == 0xBE && command.ActorSlot < 3 && Actor(command.ActorSlot).Identity == 2 && instruction.Operands.Span[0] == 8 && (Actor(command.ActorSlot).Actor.Statuses & 0x800) != 0;
                    if (!suppressed)
                    {
                        active = ScheduleTargetReactions(active, command.TargetMask, instruction.Operands.Span[0]);
                    }
                }
                else if (instruction.Opcode == 0x92)
                {
                    SetCameraActiveAction();
                }
                else if (instruction.Opcode == 0xB9)
                {
                    SetCameraEntry(instruction.Operands.Span[0]);
                }
                else if (instruction.Opcode == 0xD1)
                {
                    ReadOnlySpan<byte> operands = instruction.Operands.Span;
                    active = active with { Motion = BeginApproach(active, BinaryPrimitives.ReadUInt16LittleEndian(operands), operands[4])?.Advance() };
                }
                else if (instruction.Opcode == 0xD0)
                {
                    ReadOnlySpan<byte> operands = instruction.Operands.Span;
                    short[] vertical = presentation.JumpDeltas(operands[2]);
                    active = active with { Motion = BeginApproach(active, BinaryPrimitives.ReadInt16LittleEndian(operands), vertical.Length, vertical)?.Advance() };
                }
                else if (instruction.Opcode == 0xFA)
                {
                    active = active with { Motion = null };
                    ResetActorPosition(command.ActorSlot);
                }
                else if (instruction.Opcode == 0xFC)
                {
                    active = active with { FacingYaw = TurnTowardTarget(active) };
                }
                else if (instruction.Opcode == 0xE5)
                {
                    active = active with { FacingYaw = null };
                    ResetActorFacing(command.ActorSlot);
                }
                else if (instruction.Opcode == 0xE3 && command.CommandId == (byte)Ff7BattleCommand.Change)
                {
                    active = active with { Motion = null };
                }
                else if (instruction.Opcode == 0xEC && command.TargetMask != 0)
                {
                    state = state with { Action = active };
                    Ff7BattleActionState beforeEffect = active;
                    active = StartEffect(active);
                    EffectEntrySound(beforeEffect, active);
                }
                else if (instruction.Opcode == 0xEA)
                {
                    state = state with { PrimaryEffects = Ff7BattlePrimaryEffects.ScheduleActionTitle(EnsurePrimaryEffects(), command.CommandId, command.Argument, Ff7NativeBattleMessage.DefaultSpeed) };
                }
                else if (instruction.Opcode == 0xEB && command is { CommandId: (byte)Ff7BattleCommand.Item, TargetMask: not 0 })
                {
                    Ff7BattleActionState beforeEffect = active;
                    active = StartItemEffect(active);
                    EffectEntrySound(beforeEffect, active);
                }
                else if (instruction.Opcode == 0xD8)
                {
                    ReadOnlySpan<byte> operands = instruction.Operands.Span;
                    Ff7BattleSecondaryState secondary = EnsureSecondaryEffects();
                    state = state with { SecondaryEffects = Ff7BattleSecondaryEffects.ScheduleSound(secondary, BinaryPrimitives.ReadUInt16LittleEndian(operands[1..]), operands[0]) };
                }
                else if (instruction.Opcode == 0xAD)
                {
                    ReadOnlySpan<byte> operands = instruction.Operands.Span;
                    int bone = operands[0] == byte.MaxValue ? -1 : presentation.JointSelector(model, operands[0]);
                    Ff7BattleSecondaryState secondary = EnsureSecondaryEffects();
                    state = state with { SecondaryEffects = Ff7BattleSecondaryEffects.ScheduleGun(secondary, command.ActorSlot, bone, BinaryPrimitives.ReadInt16LittleEndian(operands[1..]), operands[3], operands[4]) };
                }
                else if (instruction.Opcode == 0xE0)
                {
                    Ff7BattleSecondaryState secondary = EnsureSecondaryEffects();
                    state = state with { SecondaryEffects = Ff7BattleSecondaryEffects.ScheduleE0(secondary, command.ActorSlot, presentation.JointSelector(model, 0)) };
                }
                else if (instruction.Opcode == 0x9D && command is { CommandId: (byte)Ff7BattleCommand.Limit, Argument: 0x95, TargetMask: not 0 } && instruction.Operands.Span[0] == 0 && active.BeatRushEffect is null)
                {
                    active = StartPrimaryBeatRush(active);
                }
            }
            state = state with { Action = active };
            PublishSourceMotion(active);
            AdvanceCameraPresentation();
        }
        state = state with { Action = active };
    }

    private void FinishActionPresentation(Ff7BattleActionState active)
    {
        Ff7BattleQueuedCommand command = active.Command;
        if (animations is not null && active.Animation >= 0 && active.Pose?.Completed != true || !NativeActorModelsReady || HasTargetReactions(active) || state.PrimaryEffects?.HasOwner(0) == true)
        {
            state = state with { Action = active };
            return;
        }
        if (command.Priority != 0 && command.CommandId != 0x23)
        {
            FinishTurn(command.ActorSlot);
        }
        state = state with { Action = null, DisplayedHit = active.PendingDisplayHit is { } undisplayed ? undisplayed with { Tick = Tick } : state.DisplayedHit };
    }

    private Ff7BattleActionState PublishTargetReactions(Ff7BattleActionState active, IEnumerable<int> targets)
    {
        foreach (int slot in targets)
        {
            QueueImpactPresentation(active.Command, slot);
            Ff7BattleHit? hit = active.PendingDisplayHit;
            if (hit is not null && hit.Target == slot)
            {
                state = state with { DisplayedHit = hit with { Tick = Tick } };
                active = active with { PendingDisplayHit = null };
            }
            if (hit is null || hit.Target != slot || !hit.Damage.Missed && hit.Damage.Damage > 0)
            {
                state = state with { Reaction = new(slot, Tick) };
            }
        }
        return active;
    }

    private Ff7BattleActionState ScheduleTargetReactions(Ff7BattleActionState action, ushort targets, byte delay)
    {
        state = state with { PrimaryEffects = Ff7BattlePrimaryEffects.ScheduleTargetReactions(EnsurePrimaryEffects(), targets, delay) };
        return action with { TargetReactions = null, Reactions = null, PrimaryCallbacksRunning = true };
    }

    private static bool HasTargetReactions(Ff7BattleActionState action)
    {
        return action.Reactions is { RemainingTargets: not 0 } || action.TargetReactions is { Pending.Length: > 0 };
    }

    private static Ff7BattleReactionState TargetReactionState(Ff7BattleActionState action)
    {
        return action.TargetReactions ?? new(action.Reactions is { RemainingTargets: not 0 } previous ? [previous] : []);
    }

    private void ApplyAction(Ff7BattleQueuedCommand command)
    {
        Ff7BattleActorState source = Actor(command.ActorSlot);
        switch (command.CommandId)
        {
            case 0x24:
                break;
            case 0x23:
                Ff7BattleDamageAction poison = Ff7NativeBattleStatusClock.PoisonAttack;
                if (command.ActorSlot >= 4)
                {
                    poison = poison with { Affinity = Ff7NativeBattleDamage.ResolveEnemyAffinity(Enemy(command.ActorSlot), Ff7NativeBattleStatusClock.PoisonElement) };
                }
                ApplyHit(command, command.ActorSlot, poison, null, Ff7NativeBattleStatusClock.PoisonElement);
                break;
            case (byte)Ff7BattleCommand.Defend:
                Replace(source with { Defending = true });
                break;
            case (byte)Ff7BattleCommand.Change:
                Replace(source with { Defending = false, Actor = source.Actor with { Conditions = source.Actor.Conditions ^ 0x40u } });
                break;
            default:
                ResolveAction(command);
                break;
        }
    }

    private void ResolveAction(Ff7BattleQueuedCommand command)
    {
        int slot = command.ActorSlot;
        Ff7BattleActorState source = Actor(slot);
        bool beatRush = command.CommandId == (byte)Ff7BattleCommand.Limit && command.Argument == 0x95;
        Ff7BattleAttack? record = command.CommandId switch
        {
            0x20 => Data.Attacks.Single(attack => attack.Slot == command.Argument && attack.Id == command.AttackId).Data,
            (byte)Ff7BattleCommand.Magic => ReadKernelAttack(command.Argument),
            (byte)Ff7BattleCommand.Limit when beatRush => Ff7NativeBattleLimits.ReadBeatRush(kernel, state.BeatRush!.Reel.ReadResult(beatRushSymbols)),
            (byte)Ff7BattleCommand.Limit => limits[command.Argument - 0x80],
            (byte)Ff7BattleCommand.Item => ReadBattleItem(command.Argument) ?? throw new InvalidDataException("FFVII action references a non-battle item."),
            (byte)Ff7BattleCommand.Attack => null,
            _ => throw new NotSupportedException($"FFVII battle command {command.CommandId:X2} has no action consumer.")
        };
        Ff7BattleActionVitals sourceVitals = new(source.Actor.HitPoints, source.Actor.MaxHitPoints, source.Actor.MagicPoints, source.Actor.MaxMagicPoints, source.Actor.Statuses);
        foreach (Ff7BattleGuestSlotState guest in state.GetGuestSlotStates())
        {
            int guestSlot = guest.Binding.HostSlot;
            if ((command.TargetMask & (1 << guestSlot)) != 0)
            {
                ValidateGuestIncoming(command, guestSlot, record);
            }
        }
        if (record is not null && !Ff7NativeBattleActionResolver.TrySpendCastingCost(sourceVitals, record, out sourceVitals))
        {
            return;
        }
        if (command.CommandId == (byte)Ff7BattleCommand.Item)
        {
            if (!Ff7NativeBattleActionResolver.TrySpendItem(state.Inventory, command.Argument, out Ff7InventorySlot[] remaining))
            {
                return;
            }
            state = state with { Inventory = remaining };
        }
        byte limitGauge = command.CommandId == (byte)Ff7BattleCommand.Limit ? Ff7NativeBattleLimits.Consume(source.LimitGauge) : source.LimitGauge;
        Dictionary<byte, int>? allUses = source.AllUses;
        if (command.GroupTarget)
        {
            byte spell = checked((byte)command.Argument);
            int available = allUses?.GetValueOrDefault(spell) ?? 0;
            if (available <= 0)
            {
                return;
            }
            allUses = new(allUses!) { [spell] = available - 1 };
        }
        Replace(source with { Defending = false, LimitGauge = limitGauge, AllUses = allUses, Actor = source.Actor with { MagicPoints = sourceVitals.Mp } });
        if (beatRush)
        {
            state = state with { BeatRush = null };
            if (record is null)
            {
                int target = System.Numerics.BitOperations.TrailingZeroCount((uint)command.TargetMask);
                state = state with { LastHit = new(slot, target, 0x95, new(true, false, 0), Tick) };
                return;
            }
        }
        Ff7BattleDamageAction action = record is null ? party[slot]!.Value.Attack : Ff7BattleDamageAction.FromAttack(record);
        if (command.GroupTarget && (command.TargetMask & (command.TargetMask - 1)) != 0)
        {
            action = action with { Split = true };
        }
        ushort elements = record is null ? party[slot]!.Value.AttackElements : record.Elements;
        foreach (Ff7BattleActorState target in state.Actors.Where(target => (command.TargetMask & 1 << target.Actor.Slot) != 0).ToArray())
        {
            if (!Ff7NativeBattleActionResolver.CanTarget(new(target.Actor.HitPoints, target.Actor.MaxHitPoints, target.Actor.MagicPoints, target.Actor.MaxMagicPoints, target.Actor.Statuses), record))
            {
                continue;
            }
            Ff7BattleDamageAction hit = target.Actor.Slot >= 4 ? action with { Affinity = Ff7NativeBattleDamage.ResolveEnemyAffinity(Enemy(target.Actor.Slot), elements) } : action;
            ApplyHit(command, target.Actor.Slot, hit, record, elements);
        }
    }

    private void ApplyHit(Ff7BattleQueuedCommand command, int targetSlot, Ff7BattleDamageAction action, Ff7BattleAttack? record, ushort elements)
    {
        ValidateGuestIncoming(command, targetSlot, record);
        Ff7BattleActorState source = Actor(command.ActorSlot);
        Ff7BattleActorState target = Actor(targetSlot);
        if (action.Formula >> 4 is 1 or 0xB)
        {
            (source, action) = ProjectPhysicalFacing(source, target, action);
            Replace(source);
        }
        Ff7BattleAiActor native = target.Actor;
        uint immunity = targetSlot >= 4 ? ~Enemy(targetSlot).NativeStatusMask : 0;
        Ff7BattleActionVitals sourceVitals = new(source.Actor.HitPoints, source.Actor.MaxHitPoints, source.Actor.MagicPoints, source.Actor.MaxMagicPoints, source.Actor.Statuses);
        Ff7BattleActionVitals targetVitals = new(native.HitPoints, native.MaxHitPoints, native.MagicPoints, native.MaxMagicPoints, native.Statuses, immunity);
        Ff7BattleActionReceipt result;
        if (GetGuestProjection(targetSlot) is { } projection)
        {
            if (projection.OriginElementDefenses is { } defenses)
            {
                action = action with { GuestElementAdjustment = Ff7GuestBattleElements.DefenseAdjustment(elements, defenses) };
            }
            IReadOnlyList<byte>? statusResistances = projection.OriginStatusResistances is { } statuses ? statuses : null;
            result = Ff7NativeBattleActionResolver.ResolveGuestContact(Stats(source), sourceVitals, Stats(target), targetVitals, action, Ff7GuestBattleStatuses.ProjectIncoming(record), random, source.Actor.Slot == targetSlot, statusResistances);
        }
        else
        {
            result = Ff7NativeBattleActionResolver.Resolve(Stats(source), sourceVitals, Stats(target), targetVitals, action, record, random, source.Actor.Slot == targetSlot);
        }
        if (source.Actor.Slot != targetSlot)
        {
            Replace(source with { Actor = source.Actor with { HitPoints = result.Source.Hp, MagicPoints = result.Source.Mp, Statuses = result.Source.Statuses } });
        }
        cameraTargetRows?.Add(targetSlot);
        CaptureImpactPresentation(command, targetSlot, source, result, action, record);
        PublishHit(command, target, action, result);
    }

    private void PublishHit(Ff7BattleQueuedCommand command, Ff7BattleActorState target, Ff7BattleDamageAction action, Ff7BattleActionReceipt result, bool runCounters = true)
    {
        int targetSlot = target.Actor.Slot;
        Ff7BattleAiActor native = target.Actor;
        byte gauge = target.LimitGauge;
        if (targetSlot < 3 && (result.Target.Statuses & 1) != 0)
        {
            gauge = 0;
        }
        else if (targetSlot < 3 && !IsGuestVitalsSlot(targetSlot) && command.ActorSlot >= 4 && !action.TargetsMagicPoints && result.Damage.Damage > 0)
        {
            gauge = Ff7NativeBattleLimits.GainFromEnemyDamage(gauge, result.Damage.Damage, native.MaxHitPoints, native.Statuses, limitDivisors[targetSlot]);
        }
        Ff7BattleStatusTimers timers = Ff7NativeBattleStatusClock.Apply(target.Timers, native.Statuses, result.Target.Statuses);
        uint conditions = action.BackAttack && !result.Damage.Missed ? native.Conditions ^ 0x80u : native.Conditions;
        native = native with { HitPoints = result.Target.Hp, MagicPoints = result.Target.Mp, Statuses = result.Target.Statuses, Conditions = conditions };
        Replace(target with { Actor = native, Timers = timers, LimitGauge = gauge });
        state = state with { LastHit = new(command.ActorSlot, targetSlot, command.AttackId ?? command.Argument, result.Damage, Tick) };
        if (runCounters && targetSlot >= 4 && !result.Damage.Missed)
        {
            PublishEnemyCounters(command, native, (result.AddedStatuses & 1) != 0, action.UsePhysicalStats || action.Formula >> 4 == 1);
        }
    }

    private void PublishEnemyCounters(Ff7BattleQueuedCommand command, Ff7BattleAiActor target, bool knockedOut, bool physical)
    {
        int targetSlot = target.Slot;
        ushort bit = (ushort)(1 << targetSlot);
        ushort processed = command.Priority >= 2 ? (ushort)(1 << command.ActorSlot) : (ushort)0;
        Ff7BattleAiCounterPlan plan = Ff7BattleAiCallbacks.SelectCounter(target, command.ActorSlot, bit, bit, knockedOut ? bit : (ushort)0, 0, processed, physical ? (ushort)0 : (ushort)4);
        Replace(Actor(targetSlot) with { Actor = plan.Target });
        foreach (byte callback in plan.Callbacks)
        {
            InvokeAi(targetSlot, callback);
        }
    }

    private (Ff7BattleActorState Source, Ff7BattleDamageAction Action) ProjectPhysicalFacing(Ff7BattleActorState source, Ff7BattleActorState target, Ff7BattleDamageAction action)
    {
        int sourceGroup = FacingGroup(source.Actor);
        int targetGroup = FacingGroup(target.Actor);
        bool sourceFacing = (source.Actor.Conditions & 0x80) != 0;
        bool targetFacing = (target.Actor.Conditions & 0x80) != 0;
        if (sourceGroup == 1 && targetGroup != 1)
        {
            sourceFacing = targetGroup == 0;
        }
        if (sourceFacing != ((source.Actor.Conditions & 0x80) != 0))
        {
            source = source with { Actor = source.Actor with { Conditions = source.Actor.Conditions ^ 0x80u } };
        }
        byte multiplier = target.Actor.Slot < 3 ? (byte)16 : Enemy(target.Actor.Slot).Rewards.Raw.Span[0xA2];
        return (source, action with { BackAttack = sourceGroup != targetGroup && sourceFacing == targetFacing, BackAttackMultiplier = multiplier });
    }

    private int FacingGroup(Ff7BattleAiActor actor)
    {
        bool enemy = actor.Slot >= 4;
        return Ff7NativeAtb.BattleTypeForLayout(Layout) switch
        {
            0 or 1 => enemy ? 1 : 0,
            2 => enemy ? 0 : 1,
            4 => enemy ? (actor.Conditions & 2) != 0 ? 2 : 0 : 1,
            _ => enemy ? 1 : (actor.Slot == 1) == (Layout == 7) ? 0 : 2
        };
    }

    private Ff7BattleAttack ReadKernelAttack(int attackId)
    {
        return Ff7BattleAttack.Read(kernel[1].Data.Span.Slice(attackId * Ff7BattleAttack.RecordSize, Ff7BattleAttack.RecordSize));
    }

    private Ff7BattleAttack? ReadBattleItem(ushort itemId)
    {
        return Ff7NativeBattleActionResolver.TryReadItem(itemId, kernel);
    }

    internal Ff7Progress PublishVitals(Ff7Progress progress)
    {
        Ff7CharacterState[] updated = (Ff7CharacterState[])progress.Characters.Clone();
        foreach (Ff7BattleActorState actor in state.Actors.Where(actor => actor.Actor.Slot < 3 && !IsGuestVitalsSlot(actor.Actor.Slot)))
        {
            Ff7BattleAiActor native = actor.Actor;
            int index = Array.FindIndex(updated, character => character.Id == actor.Identity);
            Ff7CharacterState original = updated[index];
            byte[] record = (byte[])original.NativeRecord.Clone();
            record[15] = actor.LimitGauge;
            BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(44), checked((ushort)native.HitPoints));
            BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(48), native.MagicPoints);
            record[31] = (byte)((record[31] & 0xCFu) | (native.Statuses & 0x30));
            record[32] = (byte)((record[32] & ~1) | ((native.Conditions & 0x40) != 0 ? 0 : 1));
            updated[index] = original with { NativeRecord = record, HitPoints = (int)native.HitPoints, MagicPoints = native.MagicPoints };
        }
        return progress with { Characters = updated, Inventory = (Ff7InventorySlot[])state.Inventory.Clone(), BattleRng = random.Capture() };
    }

    internal Ff7Progress PublishVictory(Ff7Progress progress, long fieldGeneration)
    {
        int[] guestStoryCharacters = state.GetGuestSlotStates().Select(guest => guest.Binding.StoryCharacter).ToArray();
        return Ff7DebugVictory.ApplyResolved(PublishVitals(progress), Formation, kernel, fieldGeneration, RequestId, random, IsDefeated, guestStoryCharacters: guestStoryCharacters);
    }

    public Ff7BattleReward PreviewVictory()
    {
        if (Outcome != Ff7BattleOutcome.Victory)
        {
            throw new InvalidOperationException("Only a completed FFVII victory has rewards.");
        }
        Ff7BattleRng preview = new(kernel[2].Data.Span, random.Capture());
        return Ff7DebugVictory.ResolveRewards(Formation, preview, IsDefeated);
    }

    private bool IsBossBattle => animations is not null && state.Actors.Any(actor => actor.Actor.Slot >= 4 && Ff7BattleActorDeath.IsBossKind(animations.DeathType(ActorModel(actor.Actor.Slot))));

    private bool IsDefeated(SceneEnemyInstance instance)
    {
        SceneBattleEnemy enemy = Data.Enemies.Single(candidate => ReferenceEquals(candidate.Placement, instance));
        return !IsAlive(Actor(enemy.FormationSlot + 4));
    }
}
