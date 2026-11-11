using System.Collections.Immutable;
using System.Numerics;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record CrossoverGuestFireHostState(Ff7BattleGuestInvocation Invocation, long HostTick, long Subticks, bool HalfSubtick, CrossoverGunbladeHostPlacement Placement, ushort TargetRadius, Ff7BattleGuestPhysicalHit Prepared, CrossoverBattleAction StockAction, CrossoverNativeFirePresentationState Presentation, bool ContactPublished);
internal sealed record CrossoverGuestCommittedSpell(long Ordinal, ushort Spell, int OriginRow);
internal sealed record CrossoverGuestHostActionsSnapshot(CrossoverBattleStockSnapshot Stock, NativeCrtRandomState? OriginCrtRandom, NativeCrtRandomState CrtRandom, long NextOrdinal, CrossoverGuestFireHostState? Fire)
{
    public ImmutableArray<CrossoverGuestCommittedSpell>? CommittedSpells { get; init; }
    public bool LegacyBattleMaskContinuation { get; init; }
    public CrossoverGuestAudioState? Audio { get; init; }
}

/// <summary>One host provider for native Gunblade and the explicit original Fire profile.</summary>
internal sealed class CrossoverGuestHostActions : IFf7BattleGuestAttack, IFf7BattleGuestMagic
{
    private readonly CrossoverBattleRoster roster;
    private CrossoverFf8BattleKit kit;
    private readonly BattleAssets assets;
    private readonly Ff7BattleSession host;
    private readonly CrossoverGunbladeHostAttack attack;
    private readonly CrossoverNativeFirePresentation fireProgram;
    private readonly CrossoverNativeSpriteRenderer compositor;
    private NativeCharacterVitals working;
    private NativeCrtRandomState random;
    private readonly NativeCrtRandomState? originRandom;
    private long nextOrdinal;
    private CrossoverGuestFireHostState? fire;
    private Ff7BattleGuestPhysicalHit? prepared;
    private ImmutableArray<CrossoverGuestCommittedSpell> committedSpells;
    private readonly bool legacyBattleMaskContinuation;
    private CrossoverGuestAudioState audio;
    private readonly NativeBattleGraphicsRules graphicsRules;

    public CrossoverGuestHostActions(CrossoverBattleRoster roster, CrossoverFf8BattleKit kit, NativeCharacterVitals origin, NativeCrtRandomState? crt, CrossoverGunbladeHostAttack attack, Ff7BattleSession host, GameAssets game, BattleAssets assets, CrossoverGuestHostActionsSnapshot? saved = null)
    {
        this.roster = roster;
        this.kit = kit;
        this.assets = assets;
        graphicsRules = game.ReadNativeBattleGraphicsRules();
        audio = saved?.Audio ?? CrossoverGuestAudioState.Create();
        audio.Validate(roster.EncounterId, host.Tick);
        this.attack = attack;
        this.host = host;
        working = saved?.Stock.Restore(roster, origin) ?? origin.DeepCopy();
        string encounter = $"mixed:{roster.EncounterId:N}";
        legacyBattleMaskContinuation = saved?.LegacyBattleMaskContinuation == true || saved is { NextOrdinal: > 1 } && saved.Stock.Magic.EncounterId != encounter;
        working = saved is null || saved.NextOrdinal == 1 && saved.Fire is null
            ? MagicCastingService.BeginEncounter(working, encounter)
            : working with { MagicSystem = working.MagicSystem with { EncounterId = encounter } };
        originRandom = saved?.OriginCrtRandom ?? crt;
        random = saved?.CrtRandom ?? crt ?? NativeCrtRandomState.FromColdProcess();
        nextOrdinal = saved?.NextOrdinal ?? 1;
        if (nextOrdinal < 1 || nextOrdinal - 1 > working.MagicSystem.CommittedActionIds.Length)
        {
            throw new InvalidDataException("The guest action ordinal exceeds its recorded spell costs.");
        }
        committedSpells = saved?.CommittedSpells ?? Enumerable.Range(1, checked((int)(nextOrdinal - 1))).Select(ordinal => new CrossoverGuestCommittedSpell(ordinal, 1, kit.RecordRow)).ToImmutableArray();
        fire = saved?.Fire;
        fireProgram = new(game, assets.ReadNativeGraphicsResource(new() { IsPlayer = true, ModelId = 0, WeaponId = kit.Weapon }));
        compositor = new(assets.ReadNativePrimitiveTables(), game.ReadNativeEffectMeshes(), graphicsRules.SummonColors!);
        if (nextOrdinal < 1 || saved is not null && saved.OriginCrtRandom != crt || fire is { } active && (active.Invocation.GuestAbility != 1 || active.Invocation.RequestId != host.RequestId || active.Invocation.FormationId != host.Formation.FormationId || active.Invocation.ActorSlot != ActorSlot || active.Prepared.PreparedContact?.Invocation != active.Invocation || active.StockAction.Ordinal >= nextOrdinal || !CrossoverBattleBridge.HasCommittedStock(roster, working, active.StockAction)))
        {
            throw new InvalidDataException("The saved guest spell owner contradicts its native encounter and committed stock.");
        }
        if (fire is not null)
        {
            fire = fire with { Presentation = CrossoverNativeFirePresentation.Restore(fire.Presentation) };
        }
        this.kit = CrossoverFf8BattleKit.ReadSquall(working, assets);
        attack.ReprojectKit(this.kit);
        if (host.GuestProjection != Projection(this.kit))
        {
            throw new InvalidDataException("The current host guest stats disagree with its native working magic stock.");
        }
        if (committedSpells.IsDefault || committedSpells.Length != nextOrdinal - 1 || committedSpells.Where((cost, index) => cost.Ordinal != index + 1 || cost.Spell != 1 || cost.OriginRow != kit.RecordRow).Any())
        {
            throw new InvalidDataException("The guest spell cost ledger contradicts its action ordinals.");
        }
    }

    public int ActorSlot => attack.ActorSlot;
    public Guid EncounterId => attack.EncounterId;
    public Ff7BattleGuestInvocation? Invocation => fire?.Invocation ?? attack.Invocation;
    public long HostTick => fire?.HostTick ?? attack.HostTick;
    public bool ContactPublished => fire?.ContactPublished ?? attack.ContactPublished;
    public CrossoverFf8BattleActorVisual? Visual => fire is { } current && attack.Visual is { } visual ? visual with { Model = current.Presentation.Cast.Model, OriginToHost = current.Placement.Matrix, BodyTextures = current.Presentation.BodyTextures, Textures = current.Presentation.Textures } : attack.Visual;
    public ImmutableArray<NativeBattleModuleDraw> Draws => fire?.Presentation.Draws ?? [];
    public NativeBattleEffectTextureState? Textures => fire?.Presentation.Textures;
    public CrossoverNativeEffectVisual? EffectVisual => fire is { Presentation.Draws.IsEmpty: false } current ? new(compositor, current.Presentation.Draws, current.Presentation.Textures, current.Placement.Matrix, CrossoverGunbladeHostPlacement.OriginToHostScale) : null;
    public string MagicName(ushort ability) => ability == 1 ? kit.Spells.Single(IsFire).Name ?? throw new InvalidDataException("Original Fire has no localized source name.") : throw new NotSupportedException("The guest ability has no admitted native spell name.");
    public ImmutableArray<NativeBattleModuleDraw> LastSounds { get; private set; } = [];
    public ImmutableArray<NativeBattleFireSound> LastFireSounds { get; private set; } = [];
    public NativeCharacterVitals WorkingStock => working;
    public NativeCrtRandomState CurrentCrtRandom => random;
    public NativeCrtRandomState? OriginCrtRandom => originRandom;
    public bool UsedMagic => nextOrdinal > 1;
    public bool LegacyBattleMaskContinuation => legacyBattleMaskContinuation;

    public CrossoverGuestHostActionsSnapshot Capture() => new(CrossoverBattleStockSnapshot.Capture(roster, working), originRandom, random, nextOrdinal, fire is { } current ? current with { Presentation = CrossoverNativeFirePresentation.Capture(current.Presentation) } : null) { CommittedSpells = committedSpells, LegacyBattleMaskContinuation = legacyBattleMaskContinuation, Audio = audio };
    public ImmutableArray<CrossoverGuestAudioReceipt> PeekSoundRequests() => audio.Pending;
    public void AcknowledgeSoundRequests(long throughOrdinal) => audio = audio.Acknowledge(throughOrdinal);
    public bool Supports(Ff7BattleGuestAttackTarget target) => attack.Supports(target);
    public int MagicStock(ushort ability) => ability == 1 && kit.Commands.Contains((byte)2) && kit.Spells.Any(IsFire) ? MagicCastingService.Quote(working, kit.RecordRow, 1, MagicCastContext.Battle).Available : 0;
    public bool SupportsMagic(ushort ability, Ff7BattleGuestAttackTarget target) => ability == 1 && (target.Actor.Statuses & ~Ff7GuestBattleStatuses.SupportedHost) == 0 && attack.Supports(target) && kit.Spells.Any(IsFire);

    public Ff7BattleGuestPhysicalHit DescribeMagic(ushort ability)
    {
        if (ability != 1 || MagicStock(ability) <= 0)
        {
            throw new InvalidOperationException("Squall has no available original Fire stock.");
        }
        Ff7BattleDamageActor source = new(Project(kit.Level, 7, 6, 100, 99), 0, CrossoverBattleFireRules.ProjectMagic(kit.Stats.Magic), 0, Project(kit.Stats.Luck, 16, 14, 255, 255), 0, 0, 0, 0);
        return new(source, new(0x22, 8, 100, LongRange: true), 1);
    }

    public void PrepareMagic(Ff7BattleGuestPhysicalHit value)
    {
        if (prepared is not null || Invocation is not null || value.PreparedContact is not { Invocation.GuestAbility: 1 })
        {
            throw new InvalidOperationException("The guest spell already has an active prepared receipt.");
        }
        prepared = value;
    }

    public void Begin(Ff7BattleGuestInvocation invocation, Ff7BattlePlacement source, Ff7BattleGuestAttackTarget target)
    {
        if (invocation.GuestAbility == 0)
        {
            attack.Begin(invocation, source, target);
            return;
        }
        if (invocation.GuestAbility != 1 || Invocation is not null || prepared is not { PreparedContact: { } receipt } hit || receipt.Invocation != invocation || !SupportsMagic(1, target))
        {
            throw new InvalidDataException("Mixed Fire requires one prepared native receipt and admitted enemy occurrence.");
        }
        CrossoverBattleAbilityId ability = new(SessionGame.Ff8, CrossoverBattleAbilityKind.Spell, 1);
        CrossoverBattleActorId targetId = roster.Participants.Single(participant => participant.HostSlot == target.Slot).Combat;
        CrossoverBattleAdmission admission = CrossoverBattleBridge.Prepare(roster, kit, working, nextOrdinal, ability, targetId, [new(roster.Version, ability, targetId.Origin, targetId.Kind, targetId.NativeId)]);
        CrossoverBattleAction stockAction = admission.Action ?? throw new InvalidOperationException(admission.Reason);
        NativeCharacterVitals reserved = admission.Vitals;
        CrossoverGunbladeHostPlacement placement = new(Ff7BattlePosition.FromVector(source.Position), source.Yaw);
        NativeBattleGraphicsModel model = attack.Visual?.Model ?? throw new InvalidDataException("Mixed Fire has no loaded native Squall source.");
        BattleNativeAction original = kit.Spells.Single(IsFire);
        byte flags = receipt.Receipt.Damage.Missed ? (byte)1 : (byte)0;
        byte hitFlags = receipt.Receipt.Damage.Damage < 0 ? (byte)1 : (byte)0;
        NativeBattleContactPresentation presentation = NativeBattleContactPresentations.Complete(original.DamagePresentation, flags, hitFlags, original.AttackType, 255);
        NativeBattleContactHalf contact = NativeBattleContactHalf.FromScratch(3, presentation.Sequence, presentation.Flags2, presentation.HitFlags3, (ushort)(receipt.Receipt.Target.Statuses & 1), Math.Abs(receipt.Receipt.Damage.Damage), 0);
        CrossoverNativeFirePresentationState program;
        try
        {
            program = fireProgram.Begin(model with { Index = 0 }, target.Slot, random, contact);
        }
        catch
        {
            working = CrossoverBattleBridge.ReleaseStock(roster, reserved, stockAction);
            throw;
        }
        working = CrossoverBattleBridge.CommitStock(roster, reserved, stockAction);
        committedSpells = committedSpells.Add(new(nextOrdinal, 1, kit.RecordRow));
        kit = CrossoverFf8BattleKit.ReadSquall(working, assets);
        attack.ReprojectKit(kit);
        host.ReprojectGuestVitals(Projection(kit));
        nextOrdinal++;
        NativeBattleGraphicsPresentationStep clock = NativeBattleGraphicsPresentationClock.Advance(0, false, TimeSpan.FromTicks(HostTime(invocation.StartedAt)));
        fire = new(invocation, invocation.StartedAt, clock.Subticks, clock.HalfSubtick, placement, target.CollisionRadius, hit, stockAction, program, false);
        prepared = null;
    }

    public Ff7BattleGuestAttackUpdate Advance(long hostTick)
    {
        LastSounds = [];
        LastFireSounds = [];
        if (fire is not { } current)
        {
            Ff7BattleGuestAttackUpdate update = attack.Advance(hostTick);
            if (attack.Visual is { } visual)
            {
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
                    NativeBattleGraphicsResource source = assets.ReadNativeGraphicsResource(new() { IsPlayer = true, ModelId = 0, WeaponId = kit.Weapon });
                    NativeBattleVector point = NativeBattlePoseMatrices.ReadBone(visual.Model, source.PoseSource!, 241, 0);
                    byte pan = CrossoverNativeSpriteRenderer.Camera(host.CameraOutput, visual.OriginToHost).ReadPan(point);
                    NativeBattleModuleSound sound = NativeBattleTriggerAudio.Resolve501640(0, point, pan, graphicsRules.ActorSounds1120);
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
            CrossoverNativeFirePresentationStep next = fireProgram.Advance(current.Presentation, () =>
            {
                Vector3 anchor = Vector3.Transform(host.GuestEffectTargetAnchor(current.Invocation.TargetSlot), inverse);
                return new(Word(anchor.X), Word(anchor.Y), Word(anchor.Z), Word(current.TargetRadius / CrossoverGunbladeHostPlacement.OriginToHostScale));
            }, () =>
            {
                Vector3 position = Vector3.Transform(host.VisualPlacement(current.Invocation.TargetSlot).Position, inverse);
                NativeBattleVector point = new(Word(position.X), Word(position.Y), Word(position.Z));
                short scale = checked((short)NativePrecision53.RoundInteger(current.TargetRadius / CrossoverGunbladeHostPlacement.OriginToHostScale * 4096 / 2000));
                return new(point, point, 0, 0, scale);
            });
            current = current with { Presentation = next.State, ContactPublished = current.ContactPublished || next.Contact };
            random = next.State.Random;
            LastSounds = next.Sounds;
            LastFireSounds = next.FireSounds;
            foreach (NativeBattleModuleDraw sound in next.Sounds)
            {
                QueueSound(hostTick, CrossoverGuestAudioKind.Module, new(sound.Sound, sound.SoundFlags, sound.Anchor), current.Placement.Matrix);
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
        if (fire is { } current)
        {
            attack.ResumeStandingFromCast(current.Presentation.Cast.Model with { Index = ActorSlot }, current.HostTick, current.Subticks, current.HalfSubtick, completed: false);
            fire = null;
        }
        attack.CancelAtTerminal();
    }

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
        audio = audio.Append(EncounterId, hostTick, kind, sound);
    }
    internal static Ff7BattleGuestProjection Projection(CrossoverFf8BattleKit kit) => new(checked((ushort)kit.MaximumHitPoints), Project(kit.Level, 7, 6, 100, 99), Project(kit.Stats.Luck, 16, 14, 255, 255), Project(kit.Stats.Vitality, 6, 24, 255, 255), Project(kit.Stats.Spirit, 5, 17, 255, 255));
    private static byte Project(int value, int from, int to, int sourceCap, int targetCap) => checked((byte)(value <= from ? value * to / from : to + (value - from) * (targetCap - to) / (sourceCap - from)));
}
