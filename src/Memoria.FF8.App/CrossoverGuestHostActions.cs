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
    public ImmutableArray<Ff7BattleGuestPhysicalHit> GroupPrepared { get; init; } = [];
    public CrossoverGuestItemDebit? ItemCost { get; init; }
    public CrossoverDrawCastReceipt? DrawCast { get; init; }
}
internal sealed record CrossoverGuestCommittedSpell(long Ordinal, ushort Spell, int OriginRow, int DrawQuantity = 0);
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

/// <summary>One host provider for original attacks and source-owned abilities through the common encounter bridge.</summary>
internal sealed partial class CrossoverGuestHostActions : IFf7BattleGuestAttack, IFf7BattleGuestMagic, IFf7BattleGuestGroupMagic, IFf7BattleGuestAbilities, IFf7BattleGuestCamera, IFf7BattleGuestItems, IFf7BattleGuestLimit, IFf7BattleGuestOperation
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
        abilityCatalog = new((working, currentKit, limits, crisis) => CrossoverGuestAbilityCatalog.Read(assets, working, currentKit, limits, crisis));
        this.encounter = encounter;
        graphicsRules = game.ReadNativeBattleGraphicsRules();
        this.attack = attack;
        this.host = host;
        ValidateItemQueue();
        fire = saved?.Fire;
        NativeBattleGraphicsResource sourceResource = assets.ReadNativeGraphicsResource(new() { IsPlayer = true, ModelId = kit.CharacterId, WeaponId = kit.Weapon });
        fireProgram = new(game, sourceResource, assets);
        limitResource = sourceResource;
        limitProgram = new(game, sourceResource, graphicsRules);
        roughDivideProgram = new(game, sourceResource, graphicsRules.SummonColors!);
        limitOptions = originLimitOptions;
        limitUiRandom = limitOptions is null ? null : saved?.LimitUiRandom ?? originLimitUiRandom;
        limitSelection = saved?.LimitSelection ?? CrossoverNativeRenzokukenSelection.Begin(host.RequestId, host.Formation.FormationId);
        limitSelection?.Validate(host.Tick);
        limit = RestoreLimit(saved?.Limit);
        compositor = new(assets.ReadNativePrimitiveTables(), game.ReadNativeEffectMeshes(), graphicsRules.SummonColors!);
        if (fire is { Presentation.Action: null } legacy)
        {
            byte command = legacy.Invocation.GuestCommand == 4 ? (byte)4 : (byte)2;
            BattleNativeAction original = assets.NativeActions.SingleOrDefault(action => action.Command == command && action.ActionId == legacy.Invocation.GuestAbility) ?? throw new InvalidDataException("The recorded guest action has no original source ability.");
            fire = legacy with { Presentation = legacy.Presentation with { Action = original, GlobalRandom = encounter.GlobalRandom } };
        }
        if (fire is { } active && ((active.Invocation.GuestCommand == 4 && !CrossoverGuestItemInventory.Supports(active.Invocation.GuestAbility)) || active.Presentation.Action is null || (active.DrawCast is not null ? active.DrawCast.Ability : active.Invocation.GuestCommand == 4 ? active.Presentation.Ability : CrossoverGuestAbilityCatalog.Key(active.Presentation.Action.Command, active.Presentation.Ability)) != active.Invocation.GuestAbility || active.Invocation.RequestId != host.RequestId || active.Invocation.FormationId != host.Formation.FormationId || active.Invocation.ActorSlot != ActorSlot || active.Prepared.PreparedContact?.Invocation != active.Invocation || !OwnsActiveCost(active)))
        {
            throw new InvalidDataException("The saved guest spell owner contradicts its native encounter and committed stock.");
        }
        if (fire is not null)
        {
            fire = fire with { Presentation = CrossoverNativeMagicPresentation.Restore(fire.Presentation) };
        }
        this.kit = CrossoverFf8BattleKit.Read(encounter.Working, assets, kit.CharacterId, kit.RecordRow);
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
        kit = CrossoverFf8BattleKit.Read(encounter.Working, assets, kit.CharacterId, kit.RecordRow);
        attack.ReprojectKit(kit);
        host.ReprojectGuestVitals(ActorSlot, Projection(kit));
    }

    private bool OwnsActiveCost(CrossoverGuestFireHostState active)
    {
        if (active.DrawCast is { } draw)
        {
            return active.StockAction is null && active.ItemCost is null && IsDrawCast(draw.Ability) && draw.SourceSlot == DrawSource(draw.Ability) && draw.SourceSlot is >= 4 and <= 9 && draw.Magic == DrawMagic(draw.Ability) && draw.Magic is >= 1 and <= 56 && draw.Quantity is >= 1 and <= 9 && active.Presentation.Action is { Command: 2 } action && action.ActionId == draw.Magic;
        }
        if (active.Invocation.GuestCommand == 4)
        {
            return active.StockAction is null && active.Presentation.Command == 4 && active.ItemCost is { Refunded: false } cost &&
                cost.Selection.ActorSlot == ActorSlot && cost.Selection.TargetSlot == active.Invocation.TargetSlot && cost.Selection.ItemId == active.Invocation.GuestAbility &&
                cost.Selection.SubmittedAt <= active.Invocation.StartedAt &&
                encounter.Items?.Ledger.Any(debit => !debit.Refunded && debit.Ordinal == cost.Ordinal && debit.Selection == cost.Selection && debit.OriginSlot == cost.OriginSlot) == true;
        }
        if (active.Presentation.Action?.Command != 2)
        {
            return active.StockAction is null && active.ItemCost is null;
        }
        return active.ItemCost is null && active.StockAction is { } stock && stock.Actor.Occurrence == ActorSlot &&
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

    public float ViewTranslationY => (fire?.Presentation.ViewTranslationY ?? 0) * CrossoverGunbladeHostPlacement.OriginToHostScale;
    public void BindForeignGeometry(Func<int, Ff7BattleActorGeometry> geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        if (readForeignGeometry is not null)
        {
            throw new InvalidOperationException("The guest already has a host geometry owner.");
        }
        readForeignGeometry = geometry;
        capabilityVersion++;
    }

    public bool SuppressForeignActor(int slot) => fire?.Presentation.SuppressedForeignSlots.Contains(slot) == true;

    public bool SuppressForeignShadow(int slot)
    {
        if (fire is not { Presentation.Effect: { } effect } active)
        {
            return false;
        }
        CrossoverNativeMagicTarget? target = active.Presentation.BoundTargets.SingleOrDefault(target => target.HostSlot == slot && target.Contact.ActorIndex != 0);
        return target is not null && (effect.Graphics.Models[target.Contact.ActorIndex].Flags0 & 0x20) != 0;
    }

    public Matrix4x4? ReadForeignPlacement(int hostSlot)
    {
        if (fire is not { Presentation.Effect: { } effect } active)
        {
            return null;
        }
        CrossoverNativeMagicTarget? target = active.Presentation.BoundTargets.SingleOrDefault(target => target.HostSlot == hostSlot && target.Contact.ActorIndex != 0);
        return target is null ? null : active.Placement.ResolvePresentationTransform(effect.ForeignPlacement(target.Contact.ActorIndex));
    }
    public int ActorSlot => attack.ActorSlot;
    public Guid EncounterId => attack.EncounterId;
    public Ff7BattleGuestInvocation? Invocation => limit?.Invocation ?? fire?.Invocation ?? attack.Invocation;
    public long HostTick => limit?.HostTick ?? fire?.HostTick ?? attack.HostTick;
    public bool ContactPublished => limit is { } current ? current.Program.PublishedContacts > 0 : fire?.ContactPublished ?? attack.ContactPublished;
    public CrossoverFf8BattleActorVisual? Visual => LimitVisual ?? (fire is { } current && attack.Visual is { } visual ? visual with { Model = current.Presentation.Cast.Model, OriginToHost = current.Placement.Matrix, BodyTextures = current.Presentation.BodyTextures, Textures = current.Presentation.Textures } : attack.Visual);
    public ImmutableArray<NativeBattleModuleDraw> Draws => fire?.Presentation.Draws ?? [];
    public NativeBattleEffectTextureState? Textures => fire?.Presentation.Textures;
    public CrossoverNativeEffectVisual? EffectVisual => LimitEffectVisual ?? (fire is { } current ? new(compositor, current.Presentation.Draws, current.Presentation.Textures, current.Placement.Matrix, CrossoverGunbladeHostPlacement.OriginToHostScale) { Ordering = current.Presentation.Ordering, Memory = current.Presentation.Memory, StageColor = current.Presentation.StageDepth is not 0 ? new(graphicsRules.SummonColors!, current.Presentation.StageColor.Depth, current.Presentation.StageColor.TargetRgb) : null } : attack.AttackEffectVisual(compositor));
    public string MagicName(ushort ability) => NativeAbility(ability).Name ?? throw new InvalidDataException("The original ability has no localized source name.");
    public ImmutableArray<NativeBattleModuleDraw> LastSounds { get; private set; } = [];
    public CrossoverGunbladeHostAttack Attack => attack;
    internal CrossoverGuestFireHostState? ActiveAbility => fire;
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
    public int MagicStock(ushort ability) => ReadOriginAbilities().SingleOrDefault(choice => choice.Key == ability)?.Stock ?? 0;
    public bool SupportsMagic(ushort ability, Ff7BattleGuestAttackTarget target) => SupportsAbility(Ff7BattleCommand.Magic, ability, target);

    public Ff7BattleGuestPhysicalHit DescribeMagic(ushort ability)
    {
        if (!ReadOriginAbilities().Any(choice => choice.Key == ability && choice.NativeAction is not null))
        {
            throw new InvalidOperationException("The FFVIII guest has no available stock for this original spell.");
        }
        CrossoverGuestAbility choice = ReadOriginAbilities().Single(choice => choice.Key == ability);
        return CrossoverBattleAbilityRules.Describe(kit, NativeAbility(ability), choice.AbilityLevel, choice.DamagePercent);
    }

    public void PrepareMagic(Ff7BattleGuestPhysicalHit value)
    {
        if (prepared is not null || Invocation is not null || value.PreparedContact is not { Invocation.GuestAbility: > 0 })
        {
            throw new InvalidOperationException("The guest spell already has an active prepared receipt.");
        }
        prepared = value;
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
        if (Invocation is not null || prepared is not { PreparedContact: { } receipt } hit || receipt.Invocation != invocation || !SupportsMagic(invocation.GuestAbility, target))
        {
            throw new InvalidDataException("The mixed ability requires one prepared native receipt and an admitted target occurrence.");
        }
        BattleNativeAction original = NativeAbility(invocation.GuestAbility);
        CrossoverBattleAction? stockAction = null;
        NativeCharacterVitals reserved = encounter.Working;
        if (original.Command == 2 && !IsDrawCast(invocation.GuestAbility))
        {
            CrossoverBattleAbilityId ability = new(SessionGame.Ff8, CrossoverBattleAbilityKind.Spell, original.ActionId);
            CrossoverBattleActorId targetId = roster.Participants.Single(participant => participant.HostSlot == target.Slot).Combat;
            CrossoverBattleAdmission admission = CrossoverBattleBridge.Prepare(roster, kit, encounter.Working, encounter.NextOrdinal, ability, targetId);
            stockAction = admission.Action ?? throw new InvalidOperationException(admission.Reason);
            reserved = admission.Vitals;
        }
        CrossoverGunbladeHostPlacement placement = new(Ff7BattlePosition.FromVector(source.Position), source.Yaw);
        NativeBattleGraphicsModel model = attack.Visual?.Model ?? throw new InvalidDataException("The mixed spell has no loaded native caster.");
        CrossoverNativeMagicPresentationState program;
        try
        {
            ImmutableArray<CrossoverNativeMagicTarget> targets = PresentationTargets(preparedGroup.IsEmpty ? [hit] : preparedGroup, original);
            program = fireProgram.Begin(model with { Index = 0 }, target.Slot, encounter.CrtRandom, targets[0].Contact, original, encounter.GlobalRandom, CreateArena(placement)) with { Targets = targets };
        }
        catch
        {
            if (stockAction is not null)
            {
                encounter.Working = CrossoverBattleBridge.ReleaseStock(roster, reserved, stockAction);
            }
            throw;
        }
        if (stockAction is not null)
        {
            encounter.Working = CrossoverBattleBridge.CommitStock(roster, reserved, stockAction);
            encounter.CommittedSpells = encounter.CommittedSpells.Add(new(encounter.NextOrdinal, original.ActionId, kit.RecordRow));
            encounter.NextOrdinal++;
            encounter.ReprojectKits(ActorSlot);
        }
        NativeBattleGraphicsPresentationStep clock = NativeBattleGraphicsPresentationClock.Advance(0, false, TimeSpan.FromTicks(HostTime(invocation.StartedAt)));
        if (IsDrawCast(invocation.GuestAbility) && preparedDrawCast?.Ability != invocation.GuestAbility)
        {
            throw new InvalidDataException("Draw Cast requires its committed source result before presentation.");
        }
        fire = new(invocation, invocation.StartedAt, clock.Subticks, clock.HalfSubtick, placement, target.CollisionRadius, hit, stockAction, program, false) { DrawCast = preparedDrawCast, GroupPrepared = preparedGroup };
        preparedDrawCast = null;
        prepared = null;
        preparedGroup = [];
    }

    public Ff7BattleGuestAttackUpdate Advance(long hostTick)
    {
        LastSounds = [];
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
            throw new InvalidDataException("The mixed ability advances once per native FFVII host tick.");
        }
        NativeBattleGraphicsPresentationStep clock = NativeBattleGraphicsPresentationClock.Advance(current.Subticks, current.HalfSubtick, TimeSpan.FromTicks(HostTime(hostTick) - HostTime(current.HostTick)));
        Ff7BattleGuestPhysicalHit? hit = null;
        ImmutableArray<Ff7BattleGuestPhysicalHit> groupHits = [];
        if (clock.Callbacks != 0)
        {
            if (clock.Callbacks != 1)
            {
                throw new InvalidDataException("The mixed ability received multiple native callbacks in one host update.");
            }
            CrossoverNativeMagicPresentationStep next = fireProgram.Advance(current.Presentation, () =>
            {
                NativeBattleVector anchor = current.Placement.ResolvePosition(host.GuestEffectTargetAnchor(current.Invocation.TargetSlot));
                return new(anchor.X, anchor.Y, anchor.Z, Word(current.TargetRadius / CrossoverGunbladeHostPlacement.OriginToHostScale));
            }, () =>
            {
                NativeBattleVector point = current.Placement.ResolvePosition(host.RenderActorPlacement(current.Invocation.TargetSlot).Position);
                short scale = checked((short)NativePrecision53.RoundInteger(current.TargetRadius / CrossoverGunbladeHostPlacement.OriginToHostScale * 4096 / 2000));
                return new(point, point, 0, 0, scale);
            }, CrossoverNativeSpriteRenderer.Camera(host.RenderCameraOutput, current.Placement.Matrix), readForeignGeometry is null ? null : () => CrossoverNativeForeignTarget.Read(host, current.Invocation.TargetSlot, readForeignGeometry(current.Invocation.TargetSlot), current.Placement.Matrix),
                readGroupForeign: readForeignGeometry is null ? null : slot => CrossoverNativeForeignTarget.Read(host, slot, readForeignGeometry(slot), current.Placement.Matrix));
            current = current with { Presentation = next.State, ContactPublished = current.ContactPublished || next.Contact };
            encounter.CrtRandom = next.State.Random;
            encounter.GlobalRandom = next.State.GlobalRandom ?? encounter.GlobalRandom;
            LastSounds = next.Sounds;
            foreach (NativeBattleModuleDraw sound in next.Sounds)
            {
                QueueSound(hostTick, CrossoverGuestAudioKind.Module, new(sound.Sound, sound.SoundFlags, sound.Anchor), current.Placement.Matrix);
            }
            foreach (NativeBattleModuleSound sound in next.EffectSounds)
            {
                QueueSound(hostTick, CrossoverGuestAudioKind.Module, sound, current.Placement.Matrix);
            }
            if (next.Contact)
            {
                if (current.GroupPrepared.IsEmpty)
                {
                    hit = current.Prepared;
                }
                else
                {
                    groupHits = next.ContactTargets.Select(slot => current.GroupPrepared.Single(value => value.PreparedContact!.Invocation.TargetSlot == slot)).ToImmutableArray();
                }
            }
        }
        current = current with { HostTick = hostTick, Subticks = clock.Subticks, HalfSubtick = clock.HalfSubtick };
        fire = current;
        if (current.Presentation.Complete)
        {
            attack.ResumeStandingFromCast(current.Presentation.Cast.Model with { Index = ActorSlot }, hostTick, clock.Subticks, clock.HalfSubtick);
            fire = null;
        }
        return new(hit, fire is null) { GroupHits = groupHits };
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
