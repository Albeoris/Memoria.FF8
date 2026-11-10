using System.Collections.Immutable;
using System.Numerics;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record CrossoverGuestFireHostState(Ff7BattleGuestInvocation Invocation, long HostTick, long Subticks, bool HalfSubtick, CrossoverGunbladeHostPlacement Placement, ushort TargetRadius, Ff7BattleGuestPhysicalHit Prepared, CrossoverBattleAction StockAction, CrossoverNativeFirePresentationState Presentation, bool ContactPublished);
internal sealed record CrossoverGuestHostActionsSnapshot(CrossoverBattleStockSnapshot Stock, NativeCrtRandomState? OriginCrtRandom, NativeCrtRandomState CrtRandom, long NextOrdinal, CrossoverGuestFireHostState? Fire);

/// <summary>One host provider for native Gunblade and the explicit original Fire profile.</summary>
internal sealed class CrossoverGuestHostActions : IFf7BattleGuestAttack, IFf7BattleGuestMagic
{
    private readonly CrossoverBattleRoster roster;
    private readonly CrossoverFf8BattleKit kit;
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

    public CrossoverGuestHostActions(CrossoverBattleRoster roster, CrossoverFf8BattleKit kit, NativeCharacterVitals origin, NativeCrtRandomState? crt, CrossoverGunbladeHostAttack attack, Ff7BattleSession host, GameAssets game, BattleAssets assets, CrossoverGuestHostActionsSnapshot? saved = null)
    {
        this.roster = roster;
        this.kit = kit;
        this.attack = attack;
        this.host = host;
        working = saved?.Stock.Restore(roster, origin) ?? origin.DeepCopy();
        originRandom = saved?.OriginCrtRandom ?? crt;
        random = saved?.CrtRandom ?? crt ?? NativeCrtRandomState.FromColdProcess();
        nextOrdinal = saved?.NextOrdinal ?? 1;
        fire = saved?.Fire;
        fireProgram = new(game, assets.ReadNativeGraphicsResource(new() { IsPlayer = true, ModelId = 0, WeaponId = kit.Weapon }));
        compositor = new(assets.ReadNativePrimitiveTables(), game.ReadNativeEffectMeshes(), game.ReadNativeBattleGraphicsRules().SummonColors!);
        if (nextOrdinal < 1 || saved is not null && saved.OriginCrtRandom != crt || fire is { } active && (active.Invocation.GuestAbility != 1 || active.Invocation.RequestId != host.RequestId || active.Invocation.FormationId != host.Formation.FormationId || active.Invocation.ActorSlot != ActorSlot || active.Prepared.PreparedContact?.Invocation != active.Invocation || active.StockAction.Ordinal >= nextOrdinal || !CrossoverBattleBridge.HasCommittedStock(roster, working, active.StockAction)))
        {
            throw new InvalidDataException("The saved guest spell owner contradicts its native encounter and committed stock.");
        }
        if (fire is not null)
        {
            fire = fire with { Presentation = CrossoverNativeFirePresentation.Restore(fire.Presentation) };
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

    public CrossoverGuestHostActionsSnapshot Capture() => new(CrossoverBattleStockSnapshot.Capture(roster, working), originRandom, random, nextOrdinal, fire is { } current ? current with { Presentation = CrossoverNativeFirePresentation.Capture(current.Presentation) } : null);
    public bool Supports(Ff7BattleGuestAttackTarget target) => attack.Supports(target);
    public int MagicStock(ushort ability) => ability == 1 && kit.Commands.Contains((byte)2) && kit.Spells.Any(IsFire) && !working.Records[kit.RecordRow].Junction.MagicJunctions.Contains((byte)1) ? MagicCastingService.Quote(working, kit.RecordRow, 1, MagicCastContext.Battle).Available : 0;
    public bool SupportsMagic(ushort ability, Ff7BattleGuestAttackTarget target) => ability == 1 && target.Actor.Statuses == 0 && attack.Supports(target) && kit.Statuses is { Saved: 0, Battle: 0 } && kit.Spells.Any(IsFire);

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
            return attack.Advance(hostTick);
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
    private static byte Project(int value, int from, int to, int sourceCap, int targetCap) => checked((byte)(value <= from ? value * to / from : to + (value - from) * (targetCap - to) / (sourceCap - from)));
}
