using System.Collections.Immutable;
using System.Numerics;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record CrossoverGuestFireHostState(Ff7BattleGuestInvocation Invocation, long HostTick, long Subticks, bool HalfSubtick, CrossoverGunbladeHostPlacement Placement, ushort TargetRadius, Ff7BattleGuestPhysicalHit Prepared, CrossoverBattleAction? StockAction, CrossoverNativeMagicPresentationState Presentation, bool ContactPublished)
{
    public CrossoverGuestItemDebit? ItemCost { get; init; }
}
internal sealed record CrossoverGuestCommittedSpell(long Ordinal, ushort Spell, int OriginRow);
internal sealed record CrossoverGuestHostActionsSnapshot(CrossoverBattleStockSnapshot Stock, NativeCrtRandomState? OriginCrtRandom, NativeCrtRandomState CrtRandom, long NextOrdinal, CrossoverGuestFireHostState? Fire)
{
    public ImmutableArray<CrossoverGuestCommittedSpell>? CommittedSpells { get; init; }
    public bool LegacyBattleMaskContinuation { get; init; }
    public CrossoverGuestAudioState? Audio { get; init; }
    public CrossoverGuestItemInventory? Items { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public CrossoverNativeRenzokukenSelection? LimitSelection { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public CrossoverGuestRenzokukenHostState? Limit { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleUiRandomState? LimitUiRandom { get; init; }

    public CrossoverGuestEncounterSnapshot EncounterState() => new(Stock, OriginCrtRandom, CrtRandom, NextOrdinal, CommittedSpells, LegacyBattleMaskContinuation, Audio, Items);
    public CrossoverGuestActorSnapshot ActorState() => new(Fire, LimitSelection, Limit, LimitUiRandom);
}

internal sealed record CrossoverGuestActorSnapshot(CrossoverGuestFireHostState? Fire, CrossoverNativeRenzokukenSelection? LimitSelection, CrossoverGuestRenzokukenHostState? Limit, NativeBattleUiRandomState? LimitUiRandom);

/// <summary>One host provider for native Gunblade and the admitted original spell profiles.</summary>
internal sealed partial class CrossoverGuestHostActions : IFf7BattleGuestAttack, IFf7BattleGuestMagic, IFf7BattleGuestCamera, IFf7BattleGuestItems, IFf7BattleGuestLimit
{
    private readonly CrossoverBattleRoster roster;
    private CrossoverFf8BattleKit kit;
    private readonly BattleAssets assets;
    private readonly Ff7BattleSession host;
    private readonly CrossoverGunbladeHostAttack attack;
    private readonly CrossoverNativeMagicPresentation fireProgram;
    private readonly CrossoverNativeSpriteRenderer compositor;
    private readonly CrossoverGuestEncounter encounter;
    private CrossoverGuestFireHostState? fire;
    private Ff7BattleGuestPhysicalHit? prepared;
    private readonly NativeBattleGraphicsRules graphicsRules;
    private Func<int, Ff7BattleActorGeometry>? readForeignGeometry;

    public CrossoverGuestHostActions(CrossoverBattleRoster roster, CrossoverFf8BattleKit kit, NativeCharacterVitals origin, NativeCrtRandomState? crt, CrossoverGunbladeHostAttack attack, Ff7BattleSession host, GameAssets game, BattleAssets assets, CrossoverGuestHostActionsSnapshot? saved = null, GameplaySaveEconomy? originEconomy = null, ushort? originLimitOptions = null, NativeBattleUiRandomState? originLimitUiRandom = null)
        : this(roster, kit, attack, host, game, assets, new(roster, origin, host, attack.GlobalRandom, crt, originEconomy, saved?.EncounterState()), saved?.ActorState(), originLimitOptions, originLimitUiRandom)
    {
    }

    public CrossoverGuestHostActions(CrossoverBattleRoster roster, CrossoverFf8BattleKit kit, CrossoverGunbladeHostAttack attack, Ff7BattleSession host, GameAssets game, BattleAssets assets, CrossoverGuestEncounter encounter, CrossoverGuestActorSnapshot? saved, ushort? originLimitOptions, NativeBattleUiRandomState? originLimitUiRandom)
    {
        this.roster = roster;
        this.kit = kit;
        this.assets = assets;
        this.encounter = encounter;
        graphicsRules = game.ReadNativeBattleGraphicsRules();
        this.attack = attack;
        this.host = host;
        ValidateItemQueue();
        fire = saved?.Fire;
        NativeBattleGraphicsResource sourceResource = assets.ReadNativeGraphicsResource(new() { IsPlayer = true, ModelId = kit.CharacterId, WeaponId = kit.Weapon });
        fireProgram = new(game, sourceResource);
        limitResource = sourceResource;
        limitProgram = new(game, sourceResource, graphicsRules);
        roughDivideProgram = new(game, sourceResource, graphicsRules.SummonColors!);
        limitOptions = originLimitOptions;
        limitUiRandom = limitOptions is null ? null : saved?.LimitUiRandom ?? originLimitUiRandom;
        limitSelection = saved?.LimitSelection ?? (limitOptions is not null ? CrossoverNativeRenzokukenSelection.Begin(host.RequestId, host.Formation.FormationId) : null);
        limitSelection?.Validate(host.Tick);
        limit = RestoreLimit(saved?.Limit);
        compositor = new(assets.ReadNativePrimitiveTables(), game.ReadNativeEffectMeshes(), graphicsRules.SummonColors!);
        if (fire is { } active && ((active.Invocation.GuestCommand == 4 ? !CrossoverGuestItemInventory.Supports(active.Invocation.GuestAbility) : active.Invocation.GuestAbility is not (1 or 4 or 7 or 21)) || active.Presentation.Ability != active.Invocation.GuestAbility || active.Invocation.RequestId != host.RequestId || active.Invocation.FormationId != host.Formation.FormationId || active.Invocation.ActorSlot != ActorSlot || active.Prepared.PreparedContact?.Invocation != active.Invocation || !OwnsActiveCost(active)))
        {
            throw new InvalidDataException("The saved guest spell owner contradicts its native encounter and committed stock.");
        }
        if (fire is not null)
        {
            fire = fire with { Presentation = CrossoverNativeMagicPresentation.Restore(fire.Presentation) };
        }
        this.kit = CrossoverFf8BattleKit.Read(encounter.Working, assets, kit.CharacterId);
        attack.ReprojectKit(this.kit);
        Ff7BattleGuestProjection currentProjection = Projection(this.kit);
        Ff7BattleGuestProjection savedProjection = host.GetGuestProjection(ActorSlot) ?? throw new InvalidDataException("The host guest has no original stat binding.");
        Ff7BattleGuestProjection restoredProjection = savedProjection with
        {
            Dexterity = savedProjection.Dexterity ?? currentProjection.Dexterity,
            Evasion = savedProjection.Evasion ?? currentProjection.Evasion,
            OriginElementDefenses = savedProjection.OriginElementDefenses ?? currentProjection.OriginElementDefenses,
            OriginStatusResistances = savedProjection.OriginStatusResistances ?? currentProjection.OriginStatusResistances
        };
        if (restoredProjection != currentProjection)
        {
            throw new InvalidDataException("The current host guest stats disagree with its native working magic stock.");
        }
        if (savedProjection != restoredProjection)
        {
            host.ReprojectGuestVitals(ActorSlot, currentProjection);
        }
        host.BindGuestSpeed(ActorSlot, ProjectSpeed(kit.Stats.Speed));
        encounter.Register(this);
    }

    internal void ReprojectWorkingKit()
    {
        kit = CrossoverFf8BattleKit.Read(encounter.Working, assets, kit.CharacterId);
        attack.ReprojectKit(kit);
        host.ReprojectGuestVitals(ActorSlot, Projection(kit));
    }

    private bool OwnsActiveCost(CrossoverGuestFireHostState active)
    {
        if (active.Invocation.GuestCommand == 4)
        {
            return active.StockAction is null && active.Presentation.Command == 4 && active.ItemCost is { Refunded: false } cost &&
                cost.Selection.ActorSlot == ActorSlot && cost.Selection.TargetSlot == active.Invocation.TargetSlot && cost.Selection.ItemId == active.Invocation.GuestAbility &&
                cost.Selection.SubmittedAt <= active.Invocation.StartedAt &&
                encounter.Items?.Ledger.Any(debit => !debit.Refunded && debit.Ordinal == cost.Ordinal && debit.Selection == cost.Selection && debit.OriginSlot == cost.OriginSlot) == true;
        }
        return active.Presentation.Command == 2 && active.ItemCost is null && active.StockAction is { } stock && stock.Actor.Occurrence == ActorSlot &&
            stock.Target.Occurrence == active.Invocation.TargetSlot && stock.Ability.NativeId == active.Invocation.GuestAbility &&
            encounter.CommittedSpells.Any(cost => cost.Ordinal == stock.Ordinal && cost.OriginRow == stock.OriginRow && cost.Spell == stock.Ability.NativeId) &&
            CrossoverBattleBridge.HasCommittedStock(roster, encounter.Working, stock);
    }

    private void ValidateItemQueue()
    {
        if (host.Outcome != Ff7BattleOutcome.Fighting)
        {
            return;
        }
        Ff7BattleCommandQueueState queue = host.Capture().Queue;
        Ff7BattleQueuedCommand[] selected = queue.Ready.Concat(queue.Slots.Select(slot => slot?.Command)).OfType<Ff7BattleQueuedCommand>().Where(command => command is { GuestAttack: true, CommandId: 4 } && command.ActorSlot == ActorSlot).ToArray();
        if (encounter.Items?.PendingFor(ActorSlot) is { Selection: { } pending })
        {
            if (selected is not [var command] || command.ActorSlot != pending.ActorSlot || command.Argument != pending.ItemId || command.TargetMask != 1 << pending.TargetSlot)
            {
                throw new InvalidDataException("The saved guest item reservation does not match its one queued action.");
            }
        }
        else if (selected.Length != 0)
        {
            throw new InvalidDataException("A queued guest item has no original stock reservation.");
        }
    }

    public float ViewTranslationY => (fire?.Presentation.Blizzard?.ViewTranslationY ?? 0) * CrossoverGunbladeHostPlacement.OriginToHostScale;
    public void BindForeignGeometry(Func<int, Ff7BattleActorGeometry> geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        if (readForeignGeometry is not null)
        {
            throw new InvalidOperationException("The guest already has a host geometry owner.");
        }
        readForeignGeometry = geometry;
    }

    public bool SuppressForeignActor(int slot) => fire?.Presentation.Cure is { ForeignHidden: true, ForeignBodyDrawn: false } cure && cure.ForeignSlot == slot;

    public int ActorSlot => attack.ActorSlot;
    public Guid EncounterId => attack.EncounterId;
    public Ff7BattleGuestInvocation? Invocation => limit?.Invocation ?? fire?.Invocation ?? attack.Invocation;
    public long HostTick => limit?.HostTick ?? fire?.HostTick ?? attack.HostTick;
    public bool ContactPublished => limit is { } current ? current.Program.PublishedContacts > 0 : fire?.ContactPublished ?? attack.ContactPublished;
    public CrossoverFf8BattleActorVisual? Visual => LimitVisual ?? (fire is { } current && attack.Visual is { } visual ? visual with { Model = current.Presentation.Cast.Model, OriginToHost = current.Placement.Matrix, BodyTextures = current.Presentation.BodyTextures, Textures = current.Presentation.Textures } : attack.Visual);
    public ImmutableArray<NativeBattleModuleDraw> Draws => fire?.Presentation.Draws ?? [];
    public NativeBattleEffectTextureState? Textures => fire?.Presentation.Textures;
    public CrossoverNativeEffectVisual? EffectVisual => LimitEffectVisual ?? (fire is { } current ? new(compositor, current.Presentation.Draws, current.Presentation.Textures, current.Placement.Matrix, CrossoverGunbladeHostPlacement.OriginToHostScale) { Ordering = current.Presentation.Cure?.Ordering ?? current.Presentation.Blizzard?.Ordering ?? current.Presentation.Phoenix?.Ordering ?? current.Presentation.Remedy?.Ordering, Memory = current.Presentation.Cure?.Frame.Memory ?? current.Presentation.Blizzard?.Frame.Memory ?? current.Presentation.Phoenix?.Frame.Memory ?? current.Presentation.Remedy?.Frame.Memory, StageColor = current.Presentation.Remedy is { StageDepth: not 0 } remedy ? new(graphicsRules.SummonColors!, remedy.StageDepth) : null } : attack.AttackEffectVisual(compositor));
    public string MagicName(ushort ability) => ability is 1 or 4 or 7 or 21 ? kit.Spells.Single(spell => IsSpell(spell, ability)).Name ?? throw new InvalidDataException("The original spell has no localized source name.") : throw new NotSupportedException("The guest ability has no admitted native spell name.");
    public ImmutableArray<NativeBattleModuleDraw> LastSounds { get; private set; } = [];
    public ImmutableArray<NativeBattleFireSound> LastFireSounds { get; private set; } = [];
    public CrossoverGunbladeHostAttack Attack => attack;
    public NativeCharacterVitals WorkingStock => encounter.Working;
    public NativeCrtRandomState CurrentCrtRandom => encounter.CrtRandom;

    public CrossoverGuestActorSnapshot CaptureActor() => new(fire is { } current ? current with { Presentation = CrossoverNativeMagicPresentation.Capture(current.Presentation) } : null, limitSelection, CaptureLimit(), limitUiRandom);

    public CrossoverGuestHostActionsSnapshot Capture()
    {
        CrossoverGuestEncounterSnapshot shared = encounter.Capture();
        CrossoverGuestActorSnapshot actor = CaptureActor();
        return new(shared.Stock, shared.OriginCrtRandom, shared.CrtRandom, shared.NextOrdinal, actor.Fire)
        {
            CommittedSpells = shared.CommittedSpells,
            LegacyBattleMaskContinuation = shared.LegacyBattleMaskContinuation,
            Audio = shared.Audio,
            Items = shared.Items,
            LimitSelection = actor.LimitSelection,
            Limit = actor.Limit,
            LimitUiRandom = actor.LimitUiRandom
        };
    }

    public ImmutableArray<CrossoverGuestAudioReceipt> PeekSoundRequests() => encounter.Audio.Pending;
    public void AcknowledgeSoundRequests(long throughOrdinal) => encounter.Audio = encounter.Audio.Acknowledge(throughOrdinal);
    public bool Supports(Ff7BattleGuestAttackTarget target) => attack.Supports(target);
    public int MagicStock(ushort ability) => ability is 1 or 4 or 7 or 21 && kit.Commands.Contains((byte)2) && kit.Spells.Any(spell => IsSpell(spell, ability)) ? MagicCastingService.Quote(encounter.Working, kit.RecordRow, ability, MagicCastContext.Battle).Available : 0;
    public bool SupportsMagic(ushort ability, Ff7BattleGuestAttackTarget target) => ability is 1 or 4 or 7 or 21 && (ability != 21 ? attack.Supports(target) : target.Slot == ActorSlot || target.Slot is >= 0 and < 3 && readForeignGeometry is not null && host.IsActorPresentationReady(target.Slot)) && kit.Spells.Any(spell => IsSpell(spell, ability));

    public Ff7BattleGuestPhysicalHit DescribeMagic(ushort ability)
    {
        if (ability is not (1 or 4 or 7 or 21) || MagicStock(ability) <= 0)
        {
            throw new InvalidOperationException("The FFVIII guest has no available stock for this original spell.");
        }
        Ff7BattleDamageActor source = new(Project(kit.Level, 7, 6, 100, 99), 0, CrossoverBattleFireRules.ProjectMagic(kit.Stats.Magic), 0, Project(kit.Stats.Luck, 16, 14, 255, 255), 0, 0, 0, 0);
        if (ability == 21)
        {
            return new(source, new(0x25, 5, 255, LongRange: true, Healing: true), 0x200);
        }
        BattleNativeAction original = kit.Spells.Single(spell => IsSpell(spell, ability));
        return new(source, new(0x22, 8, 100, LongRange: true), Ff7GuestBattleElements.ToHost(original.Element));
    }

    public void PrepareMagic(Ff7BattleGuestPhysicalHit value)
    {
        if (prepared is not null || Invocation is not null || value.PreparedContact is not { Invocation.GuestAbility: 1 or 4 or 7 or 21 })
        {
            throw new InvalidOperationException("The guest spell already has an active prepared receipt.");
        }
        prepared = value;
    }

    public bool HasOriginItems => encounter.Items is not null;
    public IReadOnlyList<ushort> ItemIds => kit.Commands.Contains((byte)4) && HasOriginItems ? [1, 7, 16] : [];
    public int ItemStock(ushort item) => ItemIds.Contains(item) ? encounter.Items!.Slots.SingleOrDefault(stock => stock.ItemId == item)?.Count ?? 0 : 0;
    public string ItemName(ushort item) => OriginalItem(item).Name ?? throw new InvalidDataException("The original item has no localized source name.");

    public bool SupportsItem(ushort item, Ff7BattleGuestAttackTarget target)
        => ItemIds.Contains(item) && (target.Slot == ActorSlot || target.Slot is >= 0 and < 3 && readForeignGeometry is not null && host.IsActorPresentationReady(target.Slot));

    public Ff7BattleGuestItemEffect DescribeItem(ushort item)
    {
        BattleNativeAction original = OriginalItem(item);
        if (item == 7)
        {
            return new(0, ReviveDivisor: (kit.StatusPassives & 2) != 0 ? (byte)4 : (byte)8);
        }
        if (item == 16)
        {
            uint removed = Ff7GuestBattleStatuses.FromOrigin((ushort)(original.Statuses0 & Ff7GuestBattleStatuses.SupportedOrigin));
            if ((original.Statuses1 & 1) != 0)
            {
                removed |= Ff7NativeBattleActionResolver.Sleep;
            }
            return new(0, removed);
        }
        bool medData = (original.AttackFlags & 3) == 2 && (kit.StatusPassives & 2) != 0;
        return new(checked((ushort)(50 * original.Power * (medData ? 2 : 1))));
    }

    public void ReserveItem(Ff7BattleGuestItemSelection selection)
    {
        if (encounter.Items is not { } inventory || selection.RequestId != host.RequestId || selection.ActorSlot != ActorSlot || selection.SubmittedAt != host.Tick || ItemStock(selection.ItemId) == 0)
        {
            throw new InvalidDataException("The guest item does not belong to the current original inventory and host turn.");
        }
        encounter.Items = inventory.Reserve(selection);
    }

    public void CancelItem(int target, ushort item)
    {
        encounter.Items = (encounter.Items ?? throw new InvalidDataException("The guest has no origin inventory owner.")).Complete(ActorSlot, target, item, refund: true);
    }

    public void PrepareItem(Ff7BattleGuestPhysicalHit value)
    {
        if (prepared is not null || Invocation is not null || value.PreparedContact is not { Invocation.GuestCommand: 4 })
        {
            throw new InvalidDataException("The guest item requires one prepared host contact.");
        }
        prepared = value;
    }

    private BattleNativeAction OriginalItem(ushort item)
    {
        BattleNativeAction? original = assets.NativeActions.SingleOrDefault(action => action.Command == 4 && action.ActionId == item);
        bool supported = item switch
        {
            1 => original is { MagicId: 50, AttackType: 4, Power: 4, TargetFlags: 20, Hit: 255, StatusAttack: 0, Statuses0: 0, Statuses1: 0 },
            7 => original is { MagicId: 5, AttackType: 5, Power: 0, TargetFlags: 21, Hit: 255, StatusAttack: 200, Statuses0: 1, Statuses1: 0 },
            16 => original is { MagicId: 67, AttackType: 4, Power: 0, TargetFlags: 20, Hit: 255, StatusAttack: 200, Statuses0: 0x7E, Statuses1: 0x5201 },
            _ => false
        };
        return supported ? original! : throw new NotSupportedException("The guest item requires an admitted original battle item record.");
    }

    private void BeginItem(Ff7BattleGuestInvocation invocation, Ff7BattlePlacement source, Ff7BattleGuestAttackTarget target)
    {
        if (Invocation is not null || prepared is not { PreparedContact: { } receipt } hit || receipt.Invocation != invocation || encounter.Items is not { } inventory || inventory.PendingFor(ActorSlot) is not { } cost || !SupportsItem(invocation.GuestAbility, target) || cost.Selection.ItemId != invocation.GuestAbility || cost.Selection.TargetSlot != target.Slot)
        {
            throw new InvalidDataException("The guest item requires one reserved origin unit and one prepared host receipt.");
        }
        CrossoverGunbladeHostPlacement placement = new(Ff7BattlePosition.FromVector(source.Position), source.Yaw);
        NativeBattleGraphicsModel model = attack.Visual?.Model ?? throw new InvalidDataException("The guest item has no loaded original caster.");
        BattleNativeAction original = OriginalItem(invocation.GuestAbility);
        byte flags = receipt.Receipt.Damage.Missed ? (byte)1 : original.AttackType == 5 ? (byte)4 : (byte)0;
        byte healing = receipt.Receipt.Damage.Damage < 0 ? (byte)1 : (byte)0;
        NativeBattleContactPresentation contactPresentation = NativeBattleContactPresentations.Complete(original.DamagePresentation, flags, healing, original.AttackType, 255);
        NativeBattleContactHalf contact = NativeBattleContactHalf.FromScratch(target.Slot == ActorSlot ? (byte)0 : (byte)3, contactPresentation.Sequence, contactPresentation.Flags2, contactPresentation.HitFlags3, (ushort)(receipt.Receipt.Target.Statuses & 1), Math.Abs(receipt.Receipt.Damage.Damage), 0);
        CrossoverNativeMagicPresentationState program = fireProgram.Begin(model with { Index = 0 }, target.Slot, encounter.CrtRandom, contact, invocation.GuestAbility, 4);
        encounter.Items = inventory.Complete(ActorSlot, target.Slot, invocation.GuestAbility, refund: false);
        NativeBattleGraphicsPresentationStep clock = NativeBattleGraphicsPresentationClock.Advance(0, false, TimeSpan.FromTicks(HostTime(invocation.StartedAt)));
        fire = new(invocation, invocation.StartedAt, clock.Subticks, clock.HalfSubtick, placement, target.CollisionRadius, hit, null, program, false) { ItemCost = cost };
        prepared = null;
    }

    public void Begin(Ff7BattleGuestInvocation invocation, Ff7BattlePlacement source, Ff7BattleGuestAttackTarget target)
    {
        if (invocation.GuestCommand == 5)
        {
            BeginLimit(invocation, source, target);
            return;
        }
        if (invocation.GuestCommand == 4)
        {
            BeginItem(invocation, source, target);
            return;
        }
        if (invocation.GuestAbility == 0)
        {
            attack.GlobalRandom = encounter.GlobalRandom;
            attack.Begin(invocation, source, target);
            encounter.GlobalRandom = attack.GlobalRandom;
            return;
        }
        if (invocation.GuestAbility is not (1 or 4 or 7 or 21) || Invocation is not null || prepared is not { PreparedContact: { } receipt } hit || receipt.Invocation != invocation || !SupportsMagic(invocation.GuestAbility, target))
        {
            throw new InvalidDataException("Mixed Fire requires one prepared native receipt and admitted enemy occurrence.");
        }
        CrossoverBattleAbilityId ability = new(SessionGame.Ff8, CrossoverBattleAbilityKind.Spell, invocation.GuestAbility);
        CrossoverBattleActorId targetId = roster.Participants.Single(participant => participant.HostSlot == target.Slot).Combat;
        CrossoverBattleAdmission admission = CrossoverBattleBridge.Prepare(roster, kit, encounter.Working, encounter.NextOrdinal, ability, targetId, [new(roster.Version, ability, targetId.Origin, targetId.Kind, targetId.NativeId)]);
        CrossoverBattleAction stockAction = admission.Action ?? throw new InvalidOperationException(admission.Reason);
        NativeCharacterVitals reserved = admission.Vitals;
        CrossoverGunbladeHostPlacement placement = new(Ff7BattlePosition.FromVector(source.Position), source.Yaw);
        NativeBattleGraphicsModel model = attack.Visual?.Model ?? throw new InvalidDataException("The mixed spell has no loaded native caster.");
        BattleNativeAction original = kit.Spells.Single(spell => IsSpell(spell, invocation.GuestAbility));
        byte flags = receipt.Receipt.Damage.Missed ? (byte)1 : (byte)0;
        byte hitFlags = receipt.Receipt.Damage.Damage < 0 ? (byte)1 : (byte)0;
        NativeBattleContactPresentation presentation = NativeBattleContactPresentations.Complete(original.DamagePresentation, flags, hitFlags, original.AttackType, 255);
        NativeBattleContactHalf contact = NativeBattleContactHalf.FromScratch(invocation.GuestAbility == 21 && invocation.TargetSlot == invocation.ActorSlot ? (byte)0 : (byte)3, presentation.Sequence, presentation.Flags2, presentation.HitFlags3, (ushort)(receipt.Receipt.Target.Statuses & 1), Math.Abs(receipt.Receipt.Damage.Damage), 0);
        CrossoverNativeMagicPresentationState program;
        try
        {
            program = fireProgram.Begin(model with { Index = 0 }, target.Slot, encounter.CrtRandom, contact, invocation.GuestAbility);
        }
        catch
        {
            encounter.Working = CrossoverBattleBridge.ReleaseStock(roster, reserved, stockAction);
            throw;
        }
        encounter.Working = CrossoverBattleBridge.CommitStock(roster, reserved, stockAction);
        encounter.CommittedSpells = encounter.CommittedSpells.Add(new(encounter.NextOrdinal, invocation.GuestAbility, kit.RecordRow));
        encounter.NextOrdinal++;
        encounter.ReprojectKits(ActorSlot);
        NativeBattleGraphicsPresentationStep clock = NativeBattleGraphicsPresentationClock.Advance(0, false, TimeSpan.FromTicks(HostTime(invocation.StartedAt)));
        fire = new(invocation, invocation.StartedAt, clock.Subticks, clock.HalfSubtick, placement, target.CollisionRadius, hit, stockAction, program, false);
        prepared = null;
    }

    public Ff7BattleGuestAttackUpdate Advance(long hostTick)
    {
        LastSounds = [];
        LastFireSounds = [];
        if (limit is { } activeLimit)
        {
            return AdvanceLimit(activeLimit, hostTick);
        }
        if (fire is not { } current)
        {
            attack.AttackCrtRandom = encounter.CrtRandom;
            attack.GlobalRandom = encounter.GlobalRandom;
            Ff7BattleGuestAttackUpdate update = attack.Advance(hostTick);
            encounter.GlobalRandom = attack.GlobalRandom;
            encounter.CrtRandom = attack.AttackCrtRandom ?? encounter.CrtRandom;
            if (attack.Visual is { } visual)
            {
                foreach (NativeBattleModuleSound sound in attack.LastModuleSounds)
                {
                    QueueSound(hostTick, CrossoverGuestAudioKind.Module, sound, visual.OriginToHost);
                }
                foreach (NativeBattleAuxiliaryEmission emission in attack.LastAuxiliaryEmissions)
                {
                    if (emission.Sound is ushort sound)
                    {
                        NativeBattleVector point = emission.SoundAnchor ?? throw new InvalidDataException("The original guest auxiliary sound lost its bone position.");
                        QueueSound(hostTick, CrossoverGuestAudioKind.Auxiliary, new(sound, 0, new(point.X, point.Y, point.Z, 0)), visual.OriginToHost);
                    }
                }
                if (attack.LastTriggerSound)
                {
                    NativeBattleGraphicsResource source = assets.ReadNativeGraphicsResource(new() { IsPlayer = true, ModelId = kit.CharacterId, WeaponId = kit.Weapon });
                    NativeBattleVector point = NativeBattlePoseMatrices.ReadBone(visual.Model, source.PoseSource!, 241, 0);
                    byte pan = CrossoverNativeSpriteRenderer.Camera(host.CameraOutput, visual.OriginToHost).ReadPan(point);
                    NativeBattleModuleSound sound = NativeBattleTriggerAudio.Resolve501640(kit.CharacterId, point, pan, graphicsRules.ActorSounds1120);
                    QueueSound(hostTick, CrossoverGuestAudioKind.Trigger, sound, visual.OriginToHost);
                }
            }
            return update;
        }
        if (hostTick != current.HostTick + 1)
        {
            throw new InvalidDataException("Mixed Fire advances once per native FFVII host tick.");
        }
        NativeBattleGraphicsPresentationStep clock = NativeBattleGraphicsPresentationClock.Advance(current.Subticks, current.HalfSubtick, TimeSpan.FromTicks(HostTime(hostTick) - HostTime(current.HostTick)));
        Ff7BattleGuestPhysicalHit? hit = null;
        if (clock.Callbacks != 0)
        {
            if (clock.Callbacks != 1)
            {
                throw new InvalidDataException("Mixed Fire received multiple native callbacks in one host update.");
            }
            Matrix4x4.Invert(current.Placement.Matrix, out Matrix4x4 inverse);
            CrossoverNativeMagicPresentationStep next = fireProgram.Advance(current.Presentation, () =>
            {
                Vector3 anchor = Vector3.Transform(host.GuestEffectTargetAnchor(current.Invocation.TargetSlot), inverse);
                return new(Word(anchor.X), Word(anchor.Y), Word(anchor.Z), Word(current.TargetRadius / CrossoverGunbladeHostPlacement.OriginToHostScale));
            }, () =>
            {
                Vector3 position = Vector3.Transform(host.VisualPlacement(current.Invocation.TargetSlot).Position, inverse);
                NativeBattleVector point = new(Word(position.X), Word(position.Y), Word(position.Z));
                short scale = checked((short)NativePrecision53.RoundInteger(current.TargetRadius / CrossoverGunbladeHostPlacement.OriginToHostScale * 4096 / 2000));
                return new(point, point, 0, 0, scale);
            }, CrossoverNativeSpriteRenderer.Camera(host.RenderCameraOutput, current.Placement.Matrix), () => CrossoverNativeCureForeignTarget.Read(host, current.Invocation.TargetSlot, (readForeignGeometry ?? throw new InvalidDataException("The host ally geometry owner is unavailable."))(current.Invocation.TargetSlot), current.Placement.Matrix));
            current = current with { Presentation = next.State, ContactPublished = current.ContactPublished || next.Contact };
            encounter.CrtRandom = next.State.Random;
            LastSounds = next.Sounds;
            LastFireSounds = next.FireSounds;
            foreach (NativeBattleModuleDraw sound in next.Sounds)
            {
                QueueSound(hostTick, CrossoverGuestAudioKind.Module, new(sound.Sound, sound.SoundFlags, sound.Anchor), current.Placement.Matrix);
            }
            foreach (NativeBattleModuleSound sound in next.EffectSounds)
            {
                QueueSound(hostTick, CrossoverGuestAudioKind.Module, sound, current.Placement.Matrix);
            }
            foreach (NativeBattleFireSound sound in next.FireSounds)
            {
                if (sound.DescriptorAddress != 0xDEE130 || sound.Volume != 256)
                {
                    throw new InvalidDataException("The guest Fire sound has a foreign original descriptor.");
                }
                uint resource = next.State.Fire?.Effect.Binding.ProgramResources?.Sound ?? throw new InvalidDataException("The guest Fire sound lost its original program resource.");
                QueueSound(hostTick, CrossoverGuestAudioKind.Fire, new(resource, 0x100, sound.Position) { Pan = sound.Pan is int pan ? checked((byte)pan) : null }, current.Placement.Matrix);
            }
            if (next.Contact)
            {
                hit = current.Prepared;
            }
        }
        current = current with { HostTick = hostTick, Subticks = clock.Subticks, HalfSubtick = clock.HalfSubtick };
        fire = current;
        if (current.Presentation.Complete)
        {
            attack.ResumeStandingFromCast(current.Presentation.Cast.Model with { Index = ActorSlot }, hostTick, clock.Subticks, clock.HalfSubtick);
            fire = null;
        }
        return new(hit, fire is null);
    }

    public void AdvanceIdle(long hostTick, uint statuses, long? reactionTick) => attack.AdvanceIdle(hostTick, statuses, reactionTick);

    public void CancelAtTerminal()
    {
        limit = null;
        if (fire is { } current)
        {
            attack.ResumeStandingFromCast(current.Presentation.Cast.Model with { Index = ActorSlot }, current.HostTick, current.Subticks, current.HalfSubtick, completed: false);
            fire = null;
        }
        if (encounter.Items?.PendingFor(ActorSlot) is { Selection: { } pending })
        {
            CancelItem(pending.TargetSlot, pending.ItemId);
        }
        attack.CancelAtTerminal();
    }

    private static bool IsSpell(BattleNativeAction spell, ushort ability) => ability == 1 ? IsFire(spell) : ability == 4 ? spell is { Command: 2, ActionId: 4, MagicId: 144, AttackType: 2, Power: 18, Element: 2, Statuses0: 0, Statuses1: 0 } : ability == 7 ? spell is { Command: 2, ActionId: 7, MagicId: 3, AttackType: 2, Power: 18, Element: 4, Statuses0: 0, Statuses1: 0 } : ability == 21 && spell is { Command: 2, ActionId: 21, MagicId: 1, AttackType: 3, Power: 18, Element: 0, Statuses0: 0, Statuses1: 0 };
    private static bool IsFire(BattleNativeAction spell) => spell is { Command: 2, ActionId: 1, MagicId: 2, AttackType: 2, Power: 18, Element: 1, Statuses0: 0, Statuses1: 0 };
    private static long HostTime(long tick) => checked(tick * TimeSpan.TicksPerSecond) / Ff7BattleSession.LogicUpdatesPerSecond;
    private static short Word(float value) => checked((short)NativePrecision53.RoundInteger(value));
    private void QueueSound(long hostTick, CrossoverGuestAudioKind kind, NativeBattleModuleSound sound, Matrix4x4 originToHost)
    {
        if (sound.Pan is null)
        {
            NativeBattleModuleAnchor anchor = sound.Anchor ?? throw new InvalidDataException("The guest sound has neither an explicit pan nor a native position.");
            sound = sound with { Pan = CrossoverNativeSpriteRenderer.Camera(host.CameraOutput, originToHost).ReadPan(new(anchor.X, anchor.Y, anchor.Z)) };
        }
        encounter.Audio = encounter.Audio.Append(EncounterId, hostTick, kind, sound);
    }
    internal static Ff7BattleGuestProjection Projection(CrossoverFf8BattleKit kit) => new(checked((ushort)kit.MaximumHitPoints), Project(kit.Level, 7, 6, 100, 99), Project(kit.Stats.Luck, 16, 14, 255, 255), Project(kit.Stats.Vitality, 6, 24, 255, 255), Project(kit.Stats.Spirit, 5, 17, 255, 255))
    {
        Dexterity = ProjectSpeed(kit.Stats.Speed),
        Evasion = checked((byte)kit.Stats.Evasion),
        OriginElementDefenses = kit.Stats.ElementDefenses.ToImmutableArray(),
        OriginStatusResistances = kit.Statuses.Resistances
    };

    internal static byte ProjectSpeed(int speed) => Project(speed, 21, 6, 255, 255);
    private static byte Project(int value, int from, int to, int sourceCap, int targetCap) => checked((byte)(value <= from ? value * to / from : to + (value - from) * (targetCap - to) / (sourceCap - from)));
}
