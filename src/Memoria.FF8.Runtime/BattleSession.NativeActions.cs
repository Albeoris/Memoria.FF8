namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private BattleActionPresentation? resolvingNativeAction;
    private BattleNativeAction FindInstalledNativeAction(byte command, ushort id, byte? crisis = null)
    {
        BattleNativeAction[] matches = definition.NativeActions.Where(action => action.Command == command && action.ActionId == id && (action.CrisisLevel is null || action.CrisisLevel == crisis)).ToArray();
        return matches.Length == 1 ? matches[0] : throw new NotSupportedException($"Native command {command}/{id} does not identify one installed action for crisis {crisis}.");
    }

    private BattleNativeAction FindNativeCommandAction(BattleCombatant actor, byte command, ushort id, byte? crisis = null)
    {
        if (command == 11 && id == 0)
        {
            return NativeDuelControl(actor, crisis ?? throw new InvalidDataException("Native Duel control requires its retained crisis BYTE."));
        }
        if (command == 0 && id == 5)
        {
            return NativeReturnDamageAction();
        }
        if (command == 0 && id == 8)
        {
            return NativePhoenixRevivalAction();
        }
        if (command == 0 && id is 2 or 4 or 6)
        {
            return NativePostActionEffect(id);
        }
        if (command == 38 && id == byte.MaxValue && crisis is null)
        {
            BattleNativeAction dance = FindInstalledNativeAction(240, 6);
            return dance with { Command = command, DispatchCommand = command, ActionId = id, TargetFlags = null };
        }
        if (command is 1 or 12 or 28 && id == 0)
        {
            if (GetGuardianForm(actor.Id) is { EntryCompleted: true } form)
            {
                BattleNativeAction physical = GuardianFormResources(form.GuardianForceId).PhysicalAction;
                return physical with { Command = command, DispatchCommand = command, ActionId = id, Name = "Attack" };
            }
            BattleNativeAction attack = actor.NativeAttack ?? throw new NotSupportedException("Native physical command requires its character-owned equipped weapon and junction metadata.");
            BattleNativeCommandBinding? binding = actor.NativeCommands.FirstOrDefault(binding => binding.Command == command);
            string? name = binding?.Name ?? (command == 28 && HasRetainedReservedCommand(actor, command) ? definition.NativeCommandNames.ElementAtOrDefault(command) : null);
            return attack with { Command = command, DispatchCommand = command, Name = name ?? attack.Name, TargetFlags = binding?.TargetFlags ?? attack.TargetFlags };
        }
        return FindInstalledNativeAction(command, id, crisis);
    }

    private bool NativeAiRejectedEmptyTarget(byte? callback, BattleTargetRequest request)
    {
        return callback is not null && request.Selection is NativeBattleTargetRequestAdapter.DeferredOperands { IsEmpty: true } && nativeActionScratch.TargetUnavailableB0E == true;
    }

    private BattleCombatant? ReadOriginalAiAnchor(AiTargetSelection selection)
    {
        return selection.Request.OriginalProjection is { } projection ? ReadOriginalSelectedActor(projection.Operands, activeOnly: false) : null;
    }

    private static bool IsNativeRevive(BattleActionDefinition action)
    {
        return action.NativeAction?.AttackType is 5 or 6 || action.Effect == BattleActionEffect.Revive;
    }

    private bool ActionHasValidTarget(BattleTargetRequest request, BattleNativeGraphicCommand? graphic = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        return graphic is not null || (request.Selection switch
        {
            BattleTargetSelection.None => true,
            NativeBattleTargetRequestAdapter.DeferredOperands original => NativeBattleTargetRequestAdapter.IsOriginalSelectionAdmitted(
                original, ReadOriginalTargetOccupant, reference => IsBattleTargetEligible(FindTargetActor(reference)!, request),
                request.Replacement == BattleTargetReplacement.UseCurrentBattlePolicy && !nativeScriptState.DisableAutomaticTargetReplacement),
            _ => ResolveBattleTargets(request, sampleRandom: false).Length != 0
        });
    }

    private static void ValidateNativeActionContract(BattleNativeAction action)
    {
        if (action.PresentationOnly && (action.Command != 0 || action.ActionId != 9 || action.MagicId != 0 || action.AttackType != 0 || action.Power != 0 || action.HitCount != 1 || action.Element != 0 || action.Statuses0 != 0 || action.Statuses1 != 0 || action.AttackFlags != 3 || action.DamagePresentation != 0))
        {
            throw new InvalidDataException("A presentation-only action requires the empty native control command without damage, status or effect payloads.");
        }
        if (action.AttackType is not (0 or 1 or 2 or 3 or 4 or 5 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19 or 20 or 21 or 22 or 23 or 24 or 25 or 26 or 27 or 28 or 29 or 30 or 31 or 32 or 33 or 34 or 35 or 36))
        {
            throw new NotSupportedException($"Native action {action.Command}/{action.ActionId} attack type {action.AttackType} requires its original invocation or persistent effect owner.");
        }
        if (action.HitCount == 0 || action.RawRecord.IsDefault || action.SupplementalRecord.IsDefault)
        {
            throw new InvalidDataException("Native action lacks its effective hit count or source records.");
        }
    }

    private int? PrepareNativeMiniMog()
    {
        NativePocketStationState pocket = definition.NativePocketStation ?? throw new NotSupportedException("MiniMog requires the saved PocketStation block.");
        pocket.Validate();
        bool availableInPocketStation = (pocket.Flags & 0x0F) == 0x0D;
        bool availableThroughProvider = (pocket.Flags & 0x0E) == 0 && definition.ChocoboWorldMode is ChocoboWorldMode.AngeloSearch or ChocoboWorldMode.Memoria && definition.ChocoboRewards.AcquiredUniqueRewardIds.Contains(ChocoboRewardCatalog.MogsAmulet);
        if (!availableInPocketStation && !availableThroughProvider)
        {
            return 72;
        }
        // 48B2E0 averages the levels in the occupied physical party slots, including KO members.
        BattleCombatant[] party = allies.Where(member => member.IsActive).ToArray();
        uint cost = checked((uint)(100 * (party.Sum(member => member.Level) / party.Length)));
        return TrySpendNativeMoogleGil(cost) ? null : 53;
    }

    private void ResolveInstalledNativeHitCore(BattleCombatant actor, BattleCombatant recipient, BattleActionPresentation command, BattleActionDefinition action, BattleNativeAction native)
    {
        if (native.PresentationOnly)
        {
            MarkNativeContactFlags(1, 0);
            SetNativeContactSequence(0);
            SetNativeContactDamage(0);
            FinalizeNativeContactPresentation();
            NoteNativeContactCommit(recipient);
            RefreshNativePresentationStatus(recipient);
            return;
        }
        if (native.Command == 241 && native.ActionId is 65530 or 65532)
        {
            recipient.NativeCombatantFlags &= ~0x4000u;
            MarkNativeContactFlags(1, 0);
            FinalizeNativeContactPresentation();
            ApplyDamage(actor, recipient, 0, action.Name, publishDamage: false);
            if (native.ActionId == 65532)
            {
                BeginNativeContactSecondary(actor, 0, false);
                NativeContactWorking contact = nativeContactWorking!;
                contact.Secondary = contact.Secondary! with { Flags2 = 1 };
                ApplyDamage(recipient, actor, 0, action.Name, counterEligible: false, publishDamage: false);
                EndNativeContactSecondary();
            }
            return;
        }
        if (native.Command == 0 && native.ActionId == 9)
        {
            //48FE20's internal9 default has zero magnitude and DF5 suppresses action-memory/counter production.
            SetNativeContactDamage(0);
            FinalizeNativeContactPresentation();
            ApplyDamage(actor, recipient, 0, action.Name, counterEligible: false, publishDamage: false);
            return;
        }
        if (native.Command == 246 && native.ActionId == 43 && actor == recipient)
        {
            // 48FE20 case F6 sets KO and emits a zero-amount death contact through 494410.
            MarkNativeContactFlags(1, 0);
            recipient.NativeStatus0 |= 1;
            ApplyDamage(actor, recipient, 0, action.Name, counterEligible: false, publishDamage: false, cause: EncounterHpCause.DeathStatus);
            return;
        }
        if (ResolveNativeGraphicHit(actor, recipient, native) || ResolveNativePostActionHit(actor, recipient, native))
        {
            return;
        }
        if (native.AttackType is 2 or 3 or 5 or 6 or 8 or 11 or 15 or 20 or 21 or 22 or 26 or 33)
        {
            recipient.NativeCombatantFlags &= ~0x4000u;
        }
        if (TryResolveNativeReturnDamage(actor, recipient, command, native))
        {
            return;
        }
        //4922B0 removes Sleep/Confuse before its formula dispatch, including special attack types.
        if (nativeContactWorking is { } formulaContact)
        {
            formulaContact.FormulaStarted = true;
        }
        if (nativeContactWorking is { AttackClass: 0 })
        {
            RemoveNativeStatuses(recipient, 0, 0x4001);
        }
        if (TryResolveNativeSpecialHit(actor, recipient, command, native))
        {
            return;
        }
        if (native.AttackType == 24)
        {
            MarkNativeContactFlags(1, 0);
            ResolveMoogleDance(actor, recipient, command);
            return;
        }
        ushort saved = NativeSavedStatusBits(recipient);
        uint statuses = NativeBattleStatusBits(recipient);
        if (native.AttackType is 13 or 16)
        {
            ResolveNativeLevelChange(actor, recipient, command, native);
            return;
        }
        bool bypass = native.IgnoresImmunity;
        bool physical = native.UsesPhysicalDamageFormula;
        bool offensiveContactBlocked = NativeOffensiveContactBlocked(native, saved, statuses);
        bool curative = native.AttackType is 3 or 4 or 5 or 6 or 21 or 25 or 32;
        if (native.Command is 243 or 248 or 251 or 253 && NativeGunbladeAttack.TriggerSucceeded(ReadGunbladeTiming(actor, command)))
        {
            //48FE20 publishes ADEbit2 before the type10 formula, including its immunity return and separate damage critical byte.
            MarkNativeContactFlags(0, 2);
        }
        if (physical && offensiveContactBlocked)
        {
            Log("Miss", actor.Id, recipient.Id, 0, $"{action.Name}: {recipient.Name} is immune.");
            return;
        }
        if (curative && (native.AttackType is not (5 or 6) || (saved & 0x40) == 0))
        {
            MarkNativeContactFlags(0, 1);
        }
        int damage;
        bool applyStatuses = true;
        if (native.AttackType is 5 or 6)
        {
            if ((saved & 0x40) != 0)
            {
                curative = false;
                //491940/4935A0 enter the complete491AD0 contract, including immunity before RNG/statuses.
                if (NativeMagicContactBlocked(native, saved, statuses))
                {
                    MarkNativeContactFlags(0, 4);
                    Log("Miss", actor.Id, recipient.Id, 0, $"{action.Name}: {recipient.Name} is immune.");
                    return;
                }
                int spirit = EffectiveStat(recipient, BattleStatId.Spirit);
                damage = ModifyNativeMagicDamage(recipient, native, NativeBattleDamage.Magic(actor.Magic, spirit, native.Power, NextRandomByte(), !actor.IsPlayer, Difficulty));
            }
            else if ((saved & 1) != 0 || recipient.CurrentHp == 0)
            {
                MarkNativeContactFlags(4, 1);
                byte fieldRules = definition.NativeFieldBattleRules ?? throw new NotSupportedException("Native revival requires the current field battle prohibition byte.");
                if ((fieldRules & 0x40) == 0)
                {
                    ApplyNativeActionStatuses(actor, recipient, native with { Statuses0 = 1, Statuses1 = 0 }, true);
                }
                else
                {
                    MarkNativeContactFlags(0, 4);
                }
                int divisor = 8;
                if (native.AttackType == 5 && native.Command is 4 or 13 && (actor.IsPlayer))
                {
                    uint passiveFlags = actor.NativePlayerStatRules?.NativeStatusPassiveFlags ?? throw new NotSupportedException("Native item revival requires the equipped character-ability flags.");
                    divisor = (passiveFlags & 2) != 0 ? 4 : 8;
                }
                int revived = native.AttackType == 6 ? recipient.MaximumHp : Math.Max(1, recipient.MaximumHp / divisor);
                SetNativeContactDamage(-revived);
                FinalizeNativeContactPresentation();
                StoreNativeHealing(recipient, 1);
                int diagnosticBeforeHp = recipient.CurrentHp;
                recipient.CurrentHp = revived;
                ObserveDiagnosticResolution("healing", actor, recipient, revived, diagnosticBeforeHp, recipient.CurrentHp);
                RecordNativeActionMemory(actor, recipient, command);
                Log("Healing", actor.Id, recipient.Id, revived, $"{action.Name}: {recipient.Name} revived.");
                DispatchNativeActionCounter(recipient);
                return;
            }
            else
            {
                MarkNativeContactFlags(0, 5);
                Log("Miss", actor.Id, recipient.Id, 0, $"{action.Name}: {recipient.Name} is not KO.");
                return;
            }
        }
        else if (physical)
        {
            if (native.AttackType != 10 && !NativePhysicalActionHits(actor, recipient, native))
            {
                MarkNativeContactFlags(0, 4);
                Log("Miss", actor.Id, recipient.Id, 0, $"{action.Name}: {actor.Name} missed.");
                return;
            }
            bool critical = native.AttackType != 10 && NativePhysicalCritical(actor, native);
            if (critical)
            {
                MarkNativeContactFlags(0, 2);
            }
            int vitality = native.AttackType == 36 ? 0 : EffectiveStat(recipient, BattleStatId.Vitality);
            if (native.AttackType == 7 && BlocksPercentageDamage(recipient))
            {
                MarkNativeContactFlags(0, 4);
            }
            damage = native.AttackType switch
            {
                7 => BlocksPercentageDamage(recipient) ? 0 : (int)((long)native.Power * recipient.CurrentHp / 16),
                18 => 5 * actor.MaximumHp,
                34 => recipient.IsPlayer ? native.Power * recipient.NativeKillCount : 0,
                10 => NativeGunbladeAttack.Damage(EffectiveStat(actor, BattleStatId.Strength), vitality, native.Power, NextRandomByte(), ResolveGunbladeTiming(actor, recipient, command)),
                _ => NativeBattleDamage.Physical(EffectiveStat(actor, BattleStatId.Strength), vitality, native.Power, NextRandomByte())
            };
            if ((statuses & 0x20) != 0)
            {
                if (damage != 0)
                {
                    MarkNativeContactFlags(0x10, 0);
                }
                damage >>= 1;
            }
            if ((statuses & 0x800000) != 0)
            {
                damage *= 2;
            }
            if (critical)
            {
                damage *= 2;
            }
            if ((saved & 0x40) != 0)
            {
                damage >>= 1;
            }
            damage = ModifyNativeElement(recipient, native, damage, true);
        }
        else
        {
            int spirit = native.AttackType is 15 or 22 ? 0 : EffectiveStat(recipient, BattleStatId.Spirit);
            bool magic = native.UsesMagicDamageFormula;
            bool magicCure = native.AttackType is 3 or 21;
            bool earthImmune = native.HasEarthElement && HasNativeFloat(statuses);
            if (offensiveContactBlocked || magicCure && !bypass && ((saved & 1) != 0 || (statuses & 0x200000) != 0 || earthImmune))
            {
                if (magic || magicCure)
                {
                    MarkNativeContactFlags(0, 4);
                }
                Log("Miss", actor.Id, recipient.Id, 0, $"{action.Name}: {recipient.Name} is immune.");
                return;
            }
            switch (native.AttackType)
            {
                case 0:
                case 12:
                case 14:
                case 30:
                case 31:
                    MarkNativeContactFlags(1, 0);
                    damage = 0;
                    applyStatuses = false;
                    if (native.AttackType == 12)
                    {
                        ResolveNativeScan(actor, recipient);
                    }
                    break;
                case 3:
                    damage = -NativeBattleDamage.Cure(actor.Magic, native.Power, NextRandomByte());
                    break;
                case 4:
                case 25:
                case 32:
                    if ((native.Hit ?? 255) <= NextRandomByte() % 100 + 1)
                    {
                        MarkNativeContactFlags(0, 4);
                        Log("Miss", actor.Id, recipient.Id, 0, $"{action.Name}: {recipient.Name} was unaffected.");
                        return;
                    }
                    damage = native.AttackType == 32 ? (int)(-(long)native.Power * recipient.MaximumHp / 16) : native.AttackType == 25 ? -(actor.MaximumHp - ReadNativeRecoverSourceHp(actor)) : -50 * native.Power;
                    if ((native.AttackFlags & 3) == 2 && (actor.IsPlayer))
                    {
                        uint passiveFlags = actor.NativePlayerStatRules?.NativeStatusPassiveFlags ?? throw new NotSupportedException("Native item healing requires the equipped character-ability flags.");
                        if ((passiveFlags & 2) != 0)
                        {
                            damage *= 2;
                        }
                    }
                    applyStatuses = native.StatusAttack > NextRandomByte() % 100 + 1;
                    break;
                case 21:
                    damage = (int)(-(long)native.Power * recipient.MaximumHp / 16);
                    break;
                case 8:
                    damage = BlocksPercentageDamage(recipient) ? 0 : (int)((long)native.Power * recipient.CurrentHp / 16);
                    break;
                case 20:
                    damage = BlocksPercentageDamage(recipient) ? 0 : (int)((long)action.Level * recipient.MaximumHp / ((native.PowerModifier ?? 0) - (native.LevelModifier ?? 0) + 100));
                    break;
                case 11:
                case 15:
                    damage = NativeBattleDamage.GuardianForce(spirit, native.Power, action.Level, native.LevelModifier ?? 0, native.PowerModifier ?? 0, action.DamagePercent, NextRandomByte(), native.Command == 254 ? nativeActionScratch.GuardianBoostPercent ?? throw new NotSupportedException("Native GF formula lacks its4850A0 Boost byte.") : 100);
                    break;
                case 26:
                    if (recipient.Level % (native.Hit ?? 255) != 0)
                    {
                        MarkNativeContactFlags(0, 4);
                        Log("Miss", actor.Id, recipient.Id, 0, $"{action.Name}: level condition failed.");
                        return;
                    }
                    damage = NativeBattleDamage.Magic(actor.Magic, spirit, native.Power, NextRandomByte(), !actor.IsPlayer, Difficulty);
                    break;
                case 27:
                    applyStatuses = false;
                    damage = 100 * native.Power - (native.Hit ?? 255);
                    break;
                case 28:
                    applyStatuses = false;
                    damage = Math.Max(0, recipient.CurrentHp - 1);
                    break;
                case 29:
                    applyStatuses = false;
                    damage = 1000 * (native.Power * action.Level / 1000 + 1);
                    break;
                case 35:
                    applyStatuses = false;
                    damage = 1;
                    break;
                case 33:
                    uint counter = definition.NativeElapsedCounter ?? throw new NotSupportedException("Native attack type33 requires its field elapsed counter DWORD at battle entry.");
                    damage = unchecked((int)(native.Power * (counter / 1000)));
                    break;
                default:
                    damage = NativeBattleDamage.Magic(actor.Magic, spirit, native.Power, NextRandomByte(), !actor.IsPlayer, Difficulty);
                    break;
            }
            if (curative)
            {
                if (native.AttackType is 3 or 21 && (statuses & 0x40) != 0)
                {
                    if (damage != 0)
                    {
                        MarkNativeContactFlags(0x20, 0);
                    }
                    damage >>= 1;
                }
                if ((saved & 4) != 0 && native.AttackType is 3 or 21)
                {
                    damage = 0;
                }
                if ((saved & 0x40) != 0)
                {
                    damage = -damage;
                }
            }
            else if (magic)
            {
                damage = ModifyNativeMagicDamage(recipient, native, damage);
            }
        }
        bool drainFormula = physical || native.UsesMagicDamageFormula || native.AttackType is 5 or 6 && !curative;
        int? drainDamage = null;
        bool drainHealing = false;
        if (drainFormula && native.DrainsHitPoints)
        {
            //48F600/491AD0 resolve the secondary half before statuses and primary commitment modifiers.
            int percent = native.StatusAttack - GetNativeStatusResistance(recipient, 23);
            long signedDrain = percent <= 0 ? 0 : -(long)damage * percent / 100;
            if (((NativeSavedStatusBits(actor) ^ saved) & 0x40) != 0)
            {
                signedDrain = -signedDrain;
            }
            drainHealing = percent <= 0 || signedDrain < 0;
            drainDamage = checked((int)Math.Min(9_999, Math.Abs(signedDrain)));
        }
        if (applyStatuses)
        {
            ApplyNativeActionStatuses(actor, recipient, native with { Statuses1 = native.AppliedBattleStatuses }, curative);
        }
        if (command.DrawCast)
        {
            damage = damage * (NextRandomByte() + 10) / 150;
        }
        if (native.Command is 1 or 12 or 28 or 243 or 248 or 251 or 253 && (NativeSavedStatusBits(actor) & 0x20) != 0)
        {
            damage = damage * 3 >> 1;
        }
        if (native.Command is 28 or 243)
        {
            damage *= 3;
        }
        if (native.Command == 2 && (NativeBattleStatusBits(actor) & 0x2000000) != 0)
        {
            damage *= 5;
        }
        if (nativeContactWorking is { CoverSource: not null })
        {
            damage /= 2;
        }
        int maximum = (native.AttackFlags & 8) != 0 ? 60_000 : 9_999;
        int damageLimit = actor.IsPlayer && command.Ability == BattleAbility.GuardianForce && action.Ability == BattleAbility.GuardianForce &&
            native is { Command: 254, KernelSection: 2 } && native.KernelIndex == action.Id && native.ActionId == action.Id + 64 &&
            action.GuardianForceSummonDamageLimit is int summonLimit
            ? summonLimit
            : NativePrimaryDamageLimit(native.AttackFlags);
        damage = Math.Clamp(damage, -maximum, damageLimit);
        SetNativeContactDamage(damage);
        FinalizeNativeContactPresentation();
        bool deathStatus = (NativeSavedStatusBits(recipient) & 1) != 0 && recipient.CurrentHp > 0;
        if (deathStatus)
        {
            damage = recipient.CurrentHp;
        }
        RecordNativeActionMemory(actor, recipient, command);
        if (damage >= 0)
        {
            ApplyDamage(actor, recipient, damage, action.Name, bypassDamageLimit: true, cause: deathStatus ? EncounterHpCause.DeathStatus : EncounterHpCause.Damage);
        }
        else
        {
            ApplyHealing(recipient, -damage, action.Name);
            DispatchNativeActionCounter(recipient);
        }
        if (drainDamage is int drain)
        {
            BeginNativeContactSecondary(actor, drain, drainHealing);
            if (!drainHealing)
            {
                ApplyDamage(recipient, actor, drain, action.Name, counterEligible: false, publishDamage: drain > 0, bypassDamageLimit: true);
            }
            else
            {
                ApplyHealing(actor, drain, action.Name);
            }
            EndNativeContactSecondary();
        }
    }

    private int NativePrimaryDamageLimit(byte attackFlags)
    {
        int nativeMaximum = (attackFlags & 8) != 0 ? 60_000 : 9_999;
        return definition.Tuning.MaximumDamage == 9_999 ? nativeMaximum : definition.Tuning.MaximumDamage;
    }

    private bool NativePhysicalActionHits(BattleCombatant actor, BattleCombatant recipient, BattleNativeAction native)
    {
        int hit = native.Hit ?? 255;
        uint targetStatus = NativeBattleStatusBits(recipient);
        int draw = (targetStatus & 9) != 0 || hit == 255 ? 0 : NextRandomByte();
        return NativeBattleDamage.PhysicalHits(hit, actor.Luck, recipient.Evasion, recipient.Luck, NativeSavedStatusBits(actor), targetStatus, draw);
    }

    private bool NativePhysicalCritical(BattleCombatant actor, BattleNativeAction native)
    {
        return NativeBattleDamage.PhysicalCritical(native.Critical.GetValueOrDefault(), actor.Luck, NextRandomByte());
    }

    private int ModifyNativeMagicDamage(BattleCombatant recipient, BattleNativeAction native, int damage)
    {
        uint statuses = NativeBattleStatusBits(recipient);
        if ((native.AttackFlags & 3) == 1 && (statuses & 0x40) != 0 && damage != 0)
        {
            MarkNativeContactFlags(0x20, 0);
        }
        damage = NativeBattleDamage.MagicMitigation(damage, native.AttackFlags, statuses);
        return ModifyNativeElement(recipient, native, damage, false);
    }

    private int ModifyNativeElement(BattleCombatant recipient, BattleNativeAction native, int damage, bool physical)
    {
        if (native.Element == 0)
        {
            return damage;
        }
        int element = System.Numerics.BitOperations.TrailingZeroCount((uint)native.Element);
        int defense = EffectiveNativeElementDefense(recipient, element);
        return NativeBattleDamage.Element(damage, native.Element, defense, physical, native.ElementPercent.GetValueOrDefault());
    }

    private static int EffectiveNativeElementDefense(BattleCombatant target, int element)
    {
        return element == 7 && (NativeSavedStatusBits(target) & 0x40) != 0 ? 700 : target.ElementDefenseAt(element);
    }

    private static bool HasNativeFloat(uint battleStatuses) => (battleStatuses & 0x2000) != 0;

    private static bool NativeMagicContactBlocked(BattleNativeAction native, ushort saved, uint statuses)
    {
        return !native.IgnoresImmunity && ((saved & 5) != 0 || (statuses & 0x200800) != 0 || native.HasEarthElement && HasNativeFloat(statuses));
    }

    private static bool NativeOffensiveContactBlocked(BattleNativeAction native, ushort saved, uint statuses)
    {
        if (native.UsesPhysicalDamageFormula)
        {
            return !native.IgnoresImmunity && ((saved & 4) != 0 || (statuses & 0x180800) != 0);
        }
        if (native.UsesMagicDamageFormula)
        {
            return NativeMagicContactBlocked(native, saved, statuses);
        }
        return native.UsesFixedDamageFormula && ((saved & 4) != 0 || (statuses & 0x800) != 0);
    }

    private static bool BlocksPercentageDamage(BattleCombatant target) => (target.NativeCombatantFlags & 0x10000) != 0;

    private void DispatchNativeActionCounter(BattleCombatant recipient)
    {
        NoteNativeContactCommit(recipient);
        if (recipient.IsPlayer || recipient.NativeAi is not null)
        {
            QueueNativeReaction(recipient, 2);
            if (!recipient.IsPlayer && NativeImmediateCounterAllowed(recipient) && (recipient.NativeCombatantFlags & 0x10) != 0)
            {
                RunNativeAi(recipient, 4);
            }
        }
        RefreshNativePresentationStatus(recipient);
    }
}
