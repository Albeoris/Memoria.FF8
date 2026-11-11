using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json.Serialization;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed record CrossoverGunbladeHostPlacement(Ff7BattlePosition Anchor, int Yaw)
{
    // Standing body bounds in the first original Cloud and Squall clips.
    public const float OriginToHostScale = 0.81593376f;
    [JsonIgnore]
    public Matrix4x4 Matrix => Matrix4x4.CreateTranslation(0, 1, -2200) * Matrix4x4.CreateScale(OriginToHostScale)
        * Matrix4x4.CreateRotationY(Yaw * MathF.Tau / 4096) * Matrix4x4.CreateTranslation(Anchor.ToVector() + new Vector3(0, 2.0688171f, 0));
}

internal sealed record CrossoverGunbladeHostSnapshot(Guid EncounterId, Ff7BattleGuestInvocation? Invocation, long HostTick, long Subticks, bool HalfSubtick, bool PendingPress, CrossoverGunbladeHostPlacement? Placement, CrossoverGunbladePresentationState? Presentation, NativeGlobalRandomState GlobalRandom, NativeBattleAuxiliaryState Auxiliary, short? Sine, short? Cosine, int AdjustedDistance)
{
    public NativeGlobalRandomState? OriginGlobalRandom { get; init; }
    public bool Cancelled { get; init; }
    public long? ReactionTick { get; init; }
}

/// <summary>First guest Attack profile. It delegates the encounter and all HP changes to FFVII.</summary>
internal sealed class CrossoverGunbladeHostAttack : IFf7BattleGuestAttack
{
    private static readonly NativeBattleVector Origin = new(0, 0, 2200);
    private readonly CrossoverBattleRoster roster;
    private CrossoverFf8BattleKit kit;
    private readonly BattleModel body;
    private readonly BattleModel weapon;
    private readonly NativeBattleGraphicsResource resource;
    private readonly ImmutableArray<byte> sequences;
    private readonly uint publicationDuration;
    private readonly NativeBattleGraphicsRules graphicsRules;
    private readonly Func<Vector3, byte> readHostPan;
    private readonly Func<SceneCameraPlacement> readHostCamera;
    private CrossoverGunbladeHostSnapshot state;

    public CrossoverGunbladeHostAttack(CrossoverBattleRoster roster, CrossoverFf8BattleKit kit, BattleAssets assets, GameAssets game, NativeGlobalRandomState globalRandom, Func<SceneCameraPlacement> readHostCamera, Func<Vector3, byte> readHostPan, CrossoverGunbladeHostSnapshot? snapshot = null)
    {
        roster.Validate();
        if (kit.Statuses.Battle != 0 || (kit.Statuses.Saved & ~Ff7GuestBattleStatuses.SupportedOrigin) != 0 || kit.Attack is not { AttackType: 10, Power: 20, Element: 0, Statuses0: 0, Statuses1: 0 } || kit.Weapon != 0 || kit.Stats.Hit != 255)
        {
            throw new NotSupportedException("The first guest Attack profile requires a status-free Squall with the original Revolver and an opening MP encounter.");
        }
        this.roster = roster;
        this.kit = kit;
        this.readHostCamera = readHostCamera;
        this.readHostPan = readHostPan;
        body = assets.LoadPlayer(0);
        weapon = assets.ResolveWeaponModel(0, kit.Weapon, body) ?? throw new InvalidDataException("The guest Squall resource has no original weapon model.");
        resource = assets.ReadNativeGraphicsResource(new() { IsPlayer = true, ModelId = 0, WeaponId = kit.Weapon });
        sequences = game.ReadNativeBattleCommandSequences();
        graphicsRules = game.ReadNativeBattleGraphicsRules();
        publicationDuration = graphicsRules.UiPublicationDuration ?? throw new NotSupportedException("The original guest trigger requires its executable UI publication period.");
        state = snapshot ?? new(roster.EncounterId, null, 0, 0, false, false, null, null, globalRandom, NativeBattleAuxiliaryState.Clear(), null, null, 0);
        state = state with { OriginGlobalRandom = state.OriginGlobalRandom ?? globalRandom };
        if (state.EncounterId != roster.EncounterId || state.Invocation is { } active && (active.RequestId != roster.HostRequestId || active.FormationId != roster.HostFormationId || active.ActorSlot != ActorSlot || state.Presentation is null || state.Placement is null || state.HostTick < active.StartedAt))
        {
            throw new InvalidDataException("Saved guest Attack state contradicts its host encounter or pending native presentation.");
        }
    }

    public int ActorSlot => roster.Guest.HostSlot;
    public Guid EncounterId => roster.EncounterId;
    public Ff7BattleGuestInvocation? Invocation => state.Invocation;
    public long HostTick => state.HostTick;
    public bool ContactPublished => Invocation is not null && state.Presentation?.ContactPublished == true;
    public bool TriggerWindowOpen => Invocation is not null && state.Presentation?.TriggerWindowOpen == true;
    public CrossoverFf8BattleActorVisual? Visual => state.Placement is { } placement && state.Presentation is { } presentation ? new(body, weapon, presentation.Model, placement.Matrix) : null;
    public CrossoverGunbladeHostSnapshot Capture() => state;
    public void ReprojectKit(CrossoverFf8BattleKit current)
    {
        if (current.RecordRow != kit.RecordRow || current.Weapon != kit.Weapon || current.Attack is not { AttackType: 10, Power: 20, Element: 0, Statuses0: 0, Statuses1: 0 } || current.Stats.Hit != 255)
        {
            throw new NotSupportedException("The updated guest kit requires an unsupported native physical action profile.");
        }
        kit = current;
    }
    public void ResumeStandingFromCast(NativeBattleGraphicsModel model, long hostTick, long subticks, bool halfSubtick, bool completed = true)
    {
        if (state.Invocation is not null || state.Presentation is null || model.Index != ActorSlot || hostTick < state.HostTick)
        {
            throw new InvalidDataException("The native casting source cannot replace an active Gunblade owner.");
        }
        state = state with { HostTick = hostTick, Subticks = subticks, HalfSubtick = halfSubtick, Cancelled = !completed, Presentation = state.Presentation with { Model = model, ReturnedToStanding = completed } };
    }
    public bool LastTriggerSound { get; private set; }
    public ImmutableArray<NativeBattleAuxiliaryEmission> LastAuxiliaryEmissions { get; private set; } = [];
    public NativeBattleSequenceParticle? LastContactEffect { get; private set; }

    public void PrepareStanding(Ff7BattlePlacement source)
    {
        if (state.Presentation is not null)
        {
            return;
        }
        NativeGlobalRandomState random = state.GlobalRandom;
        NativeBattleGraphicsModel model = new(ActorSlot, 0) { Transform = new(Origin, Origin, 0, 0, 4096), SequenceState = new() { Locals = NativeBattleSequenceState.ColdProcessLocals() }, LinkedTransform = new(default, default) };
        model = NativeBattleGraphicsModels.CompleteLoad(model, 0, 0, ref random);
        model = NativeBattleGraphicsModels.SelectSequence(model, 1);
        model = NativeBattleSequenceMachine.Advance(model, resource, new(0, 0)).Model;
        model = model with { Rotation14 = checked((short)source.Yaw), Flags2 = (ushort)(model.Flags2!.Value & ~4) };
        model = model with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, resource.PoseSource!) };
        state = state with { Placement = new(Ff7BattlePosition.FromVector(source.Position), source.Yaw), Presentation = new(model, NativeBattleUiServices.Initialize(), null) { ReturnedToStanding = true }, GlobalRandom = random };
    }

    public void AdvanceIdle(long hostTick, uint statuses, long? reactionTick)
    {
        if (state.Presentation is not { } presentation || state.Invocation is not null)
        {
            return;
        }
        NativeBattleGraphicsPresentationStep clock = NativeBattleGraphicsPresentationClock.Advance(state.Subticks, state.HalfSubtick, TimeSpan.FromTicks(HostTime(hostTick) - HostTime(state.HostTick)));
        NativeBattleGraphicsModel model = presentation.Model;
        ushort saved = Ff7GuestBattleStatuses.ToOrigin(statuses);
        uint transient = ((statuses & 4) != 0 ? 1u : 0u) | ((statuses & 0x100) != 0 ? 2u : 0u) | ((statuses & 0x200) != 0 ? 4u : 0u);
        model = model with { Flags8 = graphicsRules.Project(saved, transient) };
        if ((statuses & 1) != 0 && model.Sequence != 3)
        {
            model = NativeBattleGraphicsModels.SelectSequence(model, 3, overrideStatus: true);
        }
        else if (reactionTick is not null && reactionTick != state.ReactionTick)
        {
            model = NativeBattleGraphicsModels.SelectSequence(model, 4, overrideStatus: true);
        }
        for (long callback = 0; callback < clock.Callbacks; callback++)
        {
            model = NativeBattleSequenceMachine.Advance(model, resource, new(0, 0)).Model;
            model = model with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, resource.PoseSource!) };
        }
        state = state with { HostTick = hostTick, Subticks = clock.Subticks, HalfSubtick = clock.HalfSubtick, ReactionTick = reactionTick, Presentation = presentation with { Model = model } };
    }

    public void CancelAtTerminal()
    {
        if (state.Invocation is not null)
        {
            state = state with { Invocation = null, PendingPress = false, Cancelled = true };
        }
    }

    public bool Supports(Ff7BattleGuestAttackTarget target) => target.Native is not null && target.CollisionRadius > 0
        && roster.Participants.Any(actor => actor.HostSlot == target.Slot && actor.Combat.Origin == SessionGame.Ff7 && actor.Combat.Kind == CrossoverBattleActorKind.Enemy && actor.Combat.NativeId == target.Native.Placement.EnemyId);

    public void PressTrigger()
    {
        if (Invocation is not null && state.Presentation?.Widget is { ClosingPhase: 0, Input.TriggerTiming: 0 })
        {
            state = state with { PendingPress = true };
        }
    }

    public void Begin(Ff7BattleGuestInvocation invocation, Ff7BattlePlacement source, Ff7BattleGuestAttackTarget target)
    {
        if (state.Invocation is not null || invocation.RequestId != roster.HostRequestId || invocation.FormationId != roster.HostFormationId || invocation.ActorSlot != ActorSlot || invocation.TargetSlot != target.Slot || !Supports(target))
        {
            throw new InvalidOperationException("Guest Attack requires one free action slot and its admitted original MP occurrence.");
        }
        CrossoverGunbladeHostPlacement placement = new(Ff7BattlePosition.FromVector(source.Position), source.Yaw);
        if (!Matrix4x4.Invert(placement.Matrix, out Matrix4x4 hostToOrigin))
        {
            throw new InvalidDataException("Guest Attack has a singular origin-to-host placement.");
        }
        Vector3 mappedTarget = Vector3.Transform(target.Placement.Position, hostToOrigin);
        NativeBattleVector targetOrigin = new(checked((short)MathF.Round(mappedTarget.X)), 0, checked((short)MathF.Round(mappedTarget.Z)));
        short yaw = checked((short)NativeWorldScriptedPathMath.NativeAngle(Origin.X - targetOrigin.X, Origin.Z - targetOrigin.Z));
        //50D300 uses 2000 * modelScale38 / 4096 as the native contact clearance.
        short targetScale = checked((short)MathF.Round(target.CollisionRadius / CrossoverGunbladeHostPlacement.OriginToHostScale * 4096 / 2000));
        int distance = NativeBattleActionGeometryRules.ContactDistance(NativeBattleActionGeometryRules.DistanceScale(Origin, targetOrigin), 4096, targetScale);
        NativeGlobalRandomState global = state.GlobalRandom;
        NativeBattleGraphicsModel model;
        if (state.Presentation is { Complete: true } previous)
        {
            model = previous.Model;
        }
        else
        {
            model = new(ActorSlot, 0)
            {
                Transform = new(Origin, Origin, 0, 0, 4096),
                SequenceState = new() { Locals = NativeBattleSequenceState.ColdProcessLocals() },
                LinkedTransform = new(default, default)
            };
            model = NativeBattleGraphicsModels.CompleteLoad(model, 0, 0, ref global);
            model = NativeBattleGraphicsModels.SelectSequence(model, 1);
            model = NativeBattleSequenceMachine.Advance(model, resource, new(0, 0)).Model;
        }
        model = model with { Rotation14 = yaw, Flags2 = (ushort)(model.Flags2!.Value & ~4) };
        model = model with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, resource.PoseSource!) };
        CrossoverGunbladePresentationState presentation = CrossoverGunbladePresentation.Begin(model, resource, sequences, null);
        NativeBattleGraphicsPresentationStep clock = NativeBattleGraphicsPresentationClock.Advance(0, false, TimeSpan.FromTicks(HostTime(invocation.StartedAt)));
        state = new(roster.EncounterId, invocation, invocation.StartedAt, clock.Subticks, clock.HalfSubtick, false, placement, presentation, global, state.Auxiliary, state.Sine, state.Cosine, distance) { OriginGlobalRandom = state.OriginGlobalRandom };
    }

    public Ff7BattleGuestAttackUpdate Advance(long hostTick)
    {
        if (state.Invocation is null || state.Presentation is not { } presentation || state.Placement is not { } placement || hostTick != state.HostTick + 1)
        {
            throw new InvalidOperationException("Guest Attack advances one admitted host tick at a time.");
        }
        NativeBattleGraphicsPresentationStep clock = NativeBattleGraphicsPresentationClock.Advance(state.Subticks, state.HalfSubtick, TimeSpan.FromTicks(HostTime(hostTick) - HostTime(state.HostTick)));
        CrossoverGunbladeHostSnapshot next = state with { HostTick = hostTick, Subticks = clock.Subticks, HalfSubtick = clock.HalfSubtick };
        if (clock.Callbacks == 0)
        {
            state = next;
            LastTriggerSound = false;
            LastAuxiliaryEmissions = [];
            LastContactEffect = null;
            return new(null, false);
        }
        NativeGlobalRandomState random = state.GlobalRandom;
        short? sine = state.Sine;
        short? cosine = state.Cosine;
        Matrix4x4 originToHost = placement.Matrix;
        NativeBattleAuxiliaryStep tasks = state.Auxiliary.Advance(new()
        {
            ReadBone = (_, selector, fraction) => NativeBattlePoseMatrices.ReadBone(presentation.Model, resource.PoseSource!, selector, fraction),
            ReadPan = position => readHostPan(Vector3.Transform(new(position.X, position.Y, position.Z), originToHost))
        });
        NativeBattleAuxiliaryState auxiliary = tasks.State;
        NativeBattleSequenceParticle? contactEffect = null;
        NativeBattleTriggerReceipt? prepared = presentation.ContactTiming;
        Ff7BattleGuestPhysicalHit? hit = null;
        NativeBattleSequenceInputs inputs = new(0, 0)
        {
            ReadModelExternal = (_, variable) => variable switch
            {
                17 => state.AdjustedDistance,
                25 or 46 => 0,
                18 => sine ?? throw new InvalidDataException("The guest Attack read its sine before the native sequence initialized it."),
                19 => cosine ?? throw new InvalidDataException("The guest Attack read its cosine before the native sequence initialized it."),
                _ => throw new NotSupportedException($"The guest Attack profile has no native sequence variable {variable} owner.")
            },
            WriteExternal = (variable, value) =>
            {
                switch (variable)
                {
                    case 18:
                        sine = unchecked((short)NativeBattleCameraMath.Sine(value));
                        break;
                    case 19:
                        cosine = unchecked((short)NativeBattleCameraMath.Cosine(value));
                        break;
                    default:
                        throw new NotSupportedException($"The guest Attack profile has no writable native sequence variable {variable} owner.");
                }
            },
            ScheduleSoundParticle = (program, two, bone) => (auxiliary, random, _) = auxiliary.AppendSoundParticle(ActorSlot, two, bone, program, random),
            ApplyActionContacts = current =>
            {
                hit = ProjectContact(prepared ?? throw new InvalidDataException("The native Gunblade timing receipt is missing at contact."));
                return current;
            },
            ScheduleContactEffect = (current, program) =>
            {
                SceneCameraPlacement camera = readHostCamera();
                Vector3 direction = Vector3.TransformNormal(camera.Position - camera.Focus, Matrix4x4.CreateRotationY(-placement.Yaw * MathF.Tau / 4096));
                int angle = NativeWorldScriptedPathMath.NativeAngle((int)direction.X, (int)direction.Z);
                contactEffect = NativeBattleSequenceParticles.Read505900(program, angle);
                return (current, contactEffect.NextOffset);
            }
        };
        CrossoverGunbladePresentationStep step = CrossoverGunbladePresentation.Advance(presentation, resource, inputs, 0, publicationDuration, state.PendingPress, receipt => prepared = receipt);
        state = next with { Invocation = step.State.Complete ? null : next.Invocation, PendingPress = false, Presentation = step.State, GlobalRandom = random, Auxiliary = auxiliary, Sine = sine, Cosine = cosine };
        LastTriggerSound = step.TriggerSound;
        LastAuxiliaryEmissions = tasks.Emissions;
        LastContactEffect = contactEffect;
        return new(hit, step.State.Complete);
    }

    private Ff7BattleGuestPhysicalHit ProjectContact(NativeBattleTriggerReceipt contact)
    {
        byte level = Project(kit.Level, 7, 6, 100, 99);
        byte attack = Project(kit.Stats.Strength, 17, 36, 255, 255);
        byte luck = Project(kit.Stats.Luck, 16, 14, 255, 255);
        Ff7BattleDamageActor source = new(level, attack, 0, 0, luck, 0, 0, 0, 0);
        byte power = checked((byte)(8 * (contact.Timing / 20 + 2)));
        Ff7BattleDamageAction action = new(0x11, power, 96, UsePhysicalStats: true) { SuppressCritical = true };
        return new(source, action, 0);
    }

    private static long HostTime(long tick) => checked(tick * TimeSpan.TicksPerSecond) / Ff7BattleSession.LogicUpdatesPerSecond;

    private static byte Project(int value, int from, int to, int sourceCap, int targetCap)
        => checked((byte)(value <= from ? value * to / from : to + (value - from) * (targetCap - to) / (sourceCap - from)));
}
