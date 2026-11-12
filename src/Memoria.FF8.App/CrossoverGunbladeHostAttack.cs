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

    public (NativeBattleVector Target, short Yaw, short TargetScale, int Distance) ResolveContact(Ff7BattlePlacement target, ushort collisionRadius)
    {
        NativeBattleVector source = new(0, 0, 2200);
        NativeBattleVector nativeTarget = ResolvePosition(target.Position) with { Y = 0 };
        short yaw = checked((short)NativeWorldScriptedPathMath.NativeAngle(source.X - nativeTarget.X, source.Z - nativeTarget.Z));
        //50D300 uses 2000 * modelScale38 / 4096 as the native contact clearance.
        short scale = checked((short)MathF.Round(collisionRadius / OriginToHostScale * 4096 / 2000));
        int distance = NativeBattleActionGeometryRules.ContactDistance(NativeBattleActionGeometryRules.DistanceScale(source, nativeTarget), 4096, scale);
        return (nativeTarget, yaw, scale, distance);
    }

    public Matrix4x4 ResolvePresentationTransform(NativeBattleActionMatrix delta)
    {
        if (delta == NativeBattleForeignModelGeometry.Identity)
        {
            return Matrix4x4.Identity;
        }
        if (!Matrix4x4.Invert(Matrix, out Matrix4x4 hostToOrigin))
        {
            throw new InvalidDataException("The guest model has a singular origin-to-host placement.");
        }
        delta.Validate();
        var r = delta.Rotation;
        Matrix4x4 native = new(r[0] / 4096f, r[3] / 4096f, r[6] / 4096f, 0, r[1] / 4096f, r[4] / 4096f, r[7] / 4096f, 0, r[2] / 4096f, r[5] / 4096f, r[8] / 4096f, 0, delta.X, delta.Y, delta.Z, 1);
        return hostToOrigin * native * Matrix;
    }

    public NativeBattleVector ResolvePosition(Vector3 position)
    {
        if (!Matrix4x4.Invert(Matrix, out Matrix4x4 hostToOrigin))
        {
            throw new InvalidDataException("The guest model has a singular origin-to-host placement.");
        }
        Vector3 native = Vector3.Transform(position, hostToOrigin);
        return new(checked((short)MathF.Round(native.X)), checked((short)MathF.Round(native.Y)), checked((short)MathF.Round(native.Z)));
    }
}

internal sealed record CrossoverGunbladeHostSnapshot(Guid EncounterId, Ff7BattleGuestInvocation? Invocation, long HostTick, long Subticks, bool HalfSubtick, bool PendingPress, CrossoverGunbladeHostPlacement? Placement, CrossoverGunbladePresentationState? Presentation, NativeGlobalRandomState GlobalRandom, NativeBattleAuxiliaryState Auxiliary, short? Sine, short? Cosine, int AdjustedDistance)
{
    public NativeGlobalRandomState? OriginGlobalRandom { get; init; }
    public bool Cancelled { get; init; }
    public long? ReactionTick { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleEffectTextureState? IdleTextures { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleVector? EffectTarget { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CrossoverPhysicalHostEffectsState? PhysicalEffects { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int TargetJointCount { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public byte PreviousParticleBone { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ushort? SourceSavedStatuses { get; init; }
}

/// <summary>Runs the selected FFVIII actor's native Attack presentation in the FFVII host.</summary>
internal sealed partial class CrossoverGunbladeHostAttack : IFf7BattleGuestAttack
{
    private static readonly NativeBattleVector Origin = new(0, 0, 2200);
    private readonly CrossoverBattleRoster roster;
    private CrossoverFf8BattleKit kit;
    private readonly BattleModel body;
    private readonly BattleModel? weapon;
    private readonly GameAssets game;
    private readonly NativeBattleGraphicsResource resource;
    private readonly ImmutableArray<byte> sequences;
    private readonly uint publicationDuration;
    private readonly NativeBattleGraphicsRules graphicsRules;
    private readonly Func<Vector3, byte> readHostPan;
    private readonly Func<SceneCameraPlacement> readHostCamera;
    private readonly Func<int, Vector3>? readHostImpactAnchor;
    private readonly Func<int, int, Vector3>? readHostTargetBone;
    private CrossoverGunbladeHostSnapshot state;

    public CrossoverGunbladeHostAttack(CrossoverBattleRoster roster, CrossoverFf8BattleKit kit, BattleAssets assets, GameAssets game, NativeGlobalRandomState globalRandom, Func<SceneCameraPlacement> readHostCamera, Func<Vector3, byte> readHostPan, CrossoverGunbladeHostSnapshot? snapshot = null, Func<int, Vector3>? readHostImpactAnchor = null, Func<int, int, Vector3>? readHostTargetBone = null)
    {
        roster.Validate();
        if (kit.OpeningCombatUnavailableReason() is { } reason)
        {
            throw new NotSupportedException(reason);
        }
        this.roster = roster;
        this.kit = kit;
        this.game = game;
        this.readHostCamera = readHostCamera;
        this.readHostPan = readHostPan;
        this.readHostImpactAnchor = readHostImpactAnchor;
        this.readHostTargetBone = readHostTargetBone;
        ActorSlot = (roster.Guests.SingleOrDefault(guest => guest.Combat.NativeId == kit.CharacterId) ?? throw new InvalidDataException("The guest Attack kit does not belong to a bound combat actor.")).HostSlot;
        body = assets.LoadPlayer(kit.CharacterId);
        weapon = assets.ResolvePlayerRenderWeapon(kit.CharacterId, kit.Weapon, body);
        resource = assets.ReadNativeGraphicsResource(new() { IsPlayer = true, ModelId = kit.CharacterId, WeaponId = kit.Weapon });
        sequences = game.ReadNativeBattleCommandSequences();
        graphicsRules = game.ReadNativeBattleGraphicsRules();
        publicationDuration = NativeGunbladeAttack.HasTrigger(kit.CharacterId) ? graphicsRules.UiPublicationDuration ?? throw new NotSupportedException("The original guest trigger requires its executable UI publication period.") : 0;
        state = snapshot ?? new(roster.EncounterId, null, 0, 0, false, false, null, null, globalRandom, NativeBattleAuxiliaryState.Clear(), null, null, 0);
        state = state with { OriginGlobalRandom = state.OriginGlobalRandom ?? globalRandom, IdleTextures = state.IdleTextures is { } savedTextures ? NativeBattleEffectComponentStorage.Restore(savedTextures) : null };
        if (state.EncounterId != roster.EncounterId || state.Invocation is { } active && (active.RequestId != roster.HostRequestId || active.FormationId != roster.HostFormationId || active.ActorSlot != ActorSlot || state.Presentation is null || state.Placement is null || state.HostTick < active.StartedAt))
        {
            throw new InvalidDataException("Saved guest Attack state contradicts its host encounter or pending native presentation.");
        }
        if (state.Invocation is null && (state.EffectTarget is not null || state.PhysicalEffects is not null) || state.PhysicalEffects is not null && state.EffectTarget is null)
        {
            throw new InvalidDataException("Saved physical effects contradict the selected guest action occurrence.");
        }
        if (state.Invocation is { } pending && state.EffectTarget is null && state.Placement is { } savedPlacement && readHostImpactAnchor is not null)
        {
            state = state with { EffectTarget = savedPlacement.ResolvePosition(readHostImpactAnchor(pending.TargetSlot)) };
        }
        if (state.Presentation is { Model: { } savedModel } savedPresentation)
        {
            if (savedModel.Index != ActorSlot || savedModel.ModelId4 != kit.CharacterId || savedModel.FileSelector128 is byte savedWeapon && savedWeapon != resource.LinkedFileSelector128 || savedModel.FileSelector129 is byte savedBody && savedBody != resource.BodyFileSelector129)
            {
                throw new InvalidDataException("Saved guest Attack model contradicts its selected character or equipment.");
            }
            state = state with { Presentation = savedPresentation with { Model = savedModel with { FileSelector128 = resource.LinkedFileSelector128, FileSelector129 = resource.BodyFileSelector129 } } };
        }
    }

    public int ActorSlot { get; }
    public Guid EncounterId => roster.EncounterId;
    public Ff7BattleGuestInvocation? Invocation => state.Invocation;
    public long HostTick => state.HostTick;
    public bool ContactPublished => Invocation is not null && state.Presentation?.ContactPublished == true;
    public bool TriggerWindowOpen => Invocation is not null && state.Presentation?.TriggerWindowOpen == true;
    public CrossoverFf8BattleActorVisual? Visual => state.Placement is { } placement && state.Presentation is { } presentation ? new(body, weapon, PresentModel(presentation.Model), placement.Matrix) { BodyTextures = state.IdleTextures is null ? null : presentation.Model.BodyTextureSlots, Textures = state.IdleTextures, DetachedWeapon = state.PhysicalEffects?.WeaponMatrix, ShadowSource = graphicsRules.ShadowSource } : null;

    private NativeBattleGraphicsModel PresentModel(NativeBattleGraphicsModel model)
    {
        NativeBattleSummonColors colors = graphicsRules.SummonColors ?? throw new NotSupportedException("Guest presentation requires its selected graphics colors.");
        return model with { Color40 = model.Color40 ?? colors.Transition, Color44 = model.Color44 ?? (colors.Normal & 0xFFFFFFu) | 0x32000000u };
    }
    public CrossoverGunbladeHostSnapshot Capture() => state with
    {
        IdleTextures = state.IdleTextures is { } textures ? NativeBattleEffectComponentStorage.Capture(textures) : null,
        PhysicalEffects = state.PhysicalEffects is { } effects ? CapturePhysicalEffects(effects) : null
    };
    public void ReprojectKit(CrossoverFf8BattleKit current)
    {
        if (current.CharacterId != kit.CharacterId || current.RecordRow != kit.RecordRow || current.Weapon != kit.Weapon || current.Attack.AttackType != kit.Attack.AttackType)
        {
            throw new InvalidDataException("The updated guest kit contradicts the loaded character or weapon.");
        }
        if (current.OpeningCombatUnavailableReason() is { } reason)
        {
            throw new NotSupportedException(reason);
        }
        kit = current;
    }
    public void ResumeStandingFromCast(NativeBattleGraphicsModel model, long hostTick, long subticks, bool halfSubtick, bool completed = true)
    {
        if (state.Invocation is not null || state.Presentation is null || model.Index != ActorSlot || model.ModelId4 != kit.CharacterId || hostTick < state.HostTick)
        {
            throw new InvalidDataException("The native casting source cannot replace an active Gunblade owner.");
        }
        state = state with { HostTick = hostTick, Subticks = subticks, HalfSubtick = halfSubtick, Cancelled = !completed, Presentation = state.Presentation with { Model = model, ReturnedToStanding = completed } };
    }
    public bool LastTriggerSound { get; private set; }
    public ImmutableArray<NativeBattleAuxiliaryEmission> LastAuxiliaryEmissions { get; private set; } = [];
    public ImmutableArray<NativeBattleModuleSound> LastModuleSounds { get; private set; } = [];
    public NativeBattleSequenceParticle? LastContactEffect { get; private set; }
    public NativeCrtRandomState? AttackCrtRandom { get; set; }
    public NativeGlobalRandomState GlobalRandom
    {
        get => state.GlobalRandom;
        set => state = state with { GlobalRandom = value };
    }

    public void PrepareStanding(Ff7BattlePlacement source)
    {
        if (state.Presentation is not null)
        {
            return;
        }
        NativeGlobalRandomState random = state.GlobalRandom;
        NativeBattleGraphicsModel model = LoadStanding(ref random);
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
        uint transient = Ff7GuestBattleStatuses.ToOriginBattle(statuses);
        uint previousFlags = model.Flags8!.Value;
        uint flags = graphicsRules.Project(saved, transient);
        model = model with { Flags8 = flags };
        if ((flags & 2) != 0)
        {
            if (model.Sequence != 3)
            {
                model = NativeBattleGraphicsModels.SelectSequence(model, 3, overrideStatus: true);
            }
        }
        else if ((previousFlags & 2) != 0)
        {
            (model, byte? immediate) = NativeBattleGraphicsModels.PlanPlayerStanding509C80(model);
            if (immediate is byte standing && (flags & 0x18) == 0)
            {
                model = NativeBattleSequenceSelections.SelectResource505BC0(model, resource, standing);
            }
        }
        else if (reactionTick is not null && reactionTick != state.ReactionTick)
        {
            model = NativeBattleGraphicsModels.SelectSequence(model, 4, overrideStatus: true);
        }
        NativeBattleEffectTextureState? textures = state.IdleTextures;
        if (textures is null && model.Sequence is 3 or 4)
        {
            (model, textures) = CrossoverNativeActorTextures.Allocate(model, resource);
        }
        for (long callback = 0; callback < clock.Callbacks; callback++)
        {
            model = NativeBattleSequenceMachine.Advance(model, resource, new(0, 0) { ApplyTexture = CopyTexture }).Model;
            model = model with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, resource.PoseSource!) };
        }
        if (textures is { Count: > 0 })
        {
            textures = textures.Drain505D20(NativeBattleEffectMemory.FromColdProcess().Arena).State;
        }
        state = state with { HostTick = hostTick, Subticks = clock.Subticks, HalfSubtick = clock.HalfSubtick, ReactionTick = reactionTick, Presentation = presentation with { Model = model }, IdleTextures = textures, SourceSavedStatuses = saved };

        void CopyTexture(byte selector, int variant)
        {
            NativeBattleEffectTextureState atlas = textures ?? throw new InvalidOperationException("The guest reaction has no allocated source atlas.");
            if (NativeBattleTextureMotion.Plan50C780(resource.TextureAnimations ?? throw new InvalidDataException("The source model has no original texture animation table."), model.TextureAllocationMask, selector, variant) is { } copy)
            {
                textures = atlas.EnqueueCopy505EB0(copy.SourceX, copy.SourceY, copy.Width, copy.Height, copy.DestinationX, copy.DestinationY);
            }
        }
    }

    public void CancelAtTerminal()
    {
        if (state.Invocation is not null)
        {
            state = state with { Invocation = null, PendingPress = false, Cancelled = true, PhysicalEffects = null, EffectTarget = null };
        }
    }

    public bool Supports(Ff7BattleGuestAttackTarget target) => target.Native is not null && target.CollisionRadius > 0 && (kit.CharacterId != 8 || target.JointCount is > 1 and <= 256)
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
            throw new InvalidOperationException("Guest Attack requires one free action slot and an admitted original FFVII target occurrence.");
        }
        CrossoverGunbladeHostPlacement placement = new(Ff7BattlePosition.FromVector(source.Position), source.Yaw);
        (_, short yaw, _, int distance) = placement.ResolveContact(target.Placement, target.CollisionRadius);
        NativeGlobalRandomState global = state.GlobalRandom;
        NativeBattleGraphicsModel model;
        if (state.Presentation is { Complete: true } previous)
        {
            model = previous.Model;
        }
        else
        {
            model = LoadStanding(ref global);
        }
        model = model with { Rotation14 = yaw, Flags2 = (ushort)(model.Flags2!.Value & ~4) };
        model = model with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, resource.PoseSource!) };
        CrossoverGunbladePresentationState presentation = CrossoverGunbladePresentation.Begin(model, resource, sequences, null);
        NativeBattleGraphicsPresentationStep clock = NativeBattleGraphicsPresentationClock.Advance(0, false, TimeSpan.FromTicks(HostTime(invocation.StartedAt)));
        state = new(roster.EncounterId, invocation, invocation.StartedAt, clock.Subticks, clock.HalfSubtick, false, placement, presentation, global, state.Auxiliary, state.Sine, state.Cosine, distance)
        {
            OriginGlobalRandom = state.OriginGlobalRandom,
            EffectTarget = placement.ResolvePosition(readHostImpactAnchor?.Invoke(target.Slot) ?? target.Placement.Position),
            TargetJointCount = target.JointCount,
            PreviousParticleBone = state.PreviousParticleBone,
            SourceSavedStatuses = state.SourceSavedStatuses
        };
    }

    public Ff7BattleGuestAttackUpdate Advance(long hostTick)
    {
        if (state.Invocation is not { } invocation || state.Presentation is not { } presentation || state.Placement is not { } placement || hostTick != state.HostTick + 1)
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
            LastModuleSounds = [];
            LastContactEffect = null;
            return new(null, false);
        }
        NativeGlobalRandomState random = state.GlobalRandom;
        short? sine = state.Sine;
        short? cosine = state.Cosine;
        byte previousParticleBone = state.PreviousParticleBone;
        Matrix4x4 originToHost = placement.Matrix;
        NativeBattleAuxiliaryStep tasks = state.Auxiliary.Advance(new()
        {
            ReadBone = (_, selector, fraction) => NativeBattlePoseMatrices.ReadBone(presentation.Model, resource.PoseSource!, selector, fraction),
            ReadPan = position => readHostPan(Vector3.Transform(new(position.X, position.Y, position.Z), originToHost))
        });
        NativeBattleAuxiliaryState auxiliary = tasks.State;
        NativeBattleSequenceParticle? contactEffect = null;
        NativeBattleTriggerReceipt? prepared = presentation.ContactTiming;
        bool blinded = ((state.SourceSavedStatuses ?? kit.Statuses.Saved) & 8) != 0;
        Ff7BattleGuestPhysicalHit? hit = null;
        CrossoverPhysicalHostEffectsState? effects = state.PhysicalEffects;
        ImmutableArray<NativeBattleModuleSound>.Builder sounds = ImmutableArray.CreateBuilder<NativeBattleModuleSound>();
        if (effects is { Complete: false } activeEffects)
        {
            CrossoverPhysicalHostEffectsStep effectStep = AdvancePhysicalHostEffects(activeEffects, presentation.Model, placement);
            effects = effectStep.State;
            AttackCrtRandom = effects.Random;
            sounds.AddRange(effectStep.Sounds);
            if (effectStep.Contact)
            {
                if (presentation.ContactPublished)
                {
                    throw new InvalidDataException("The native projectile already published its contact.");
                }
                hit = ProjectContact(kit, 0);
                presentation = presentation with { ContactPublished = true };
            }
        }
        byte? targetCue = null;
        NativeBattleSequenceInputs inputs = new(0, 0)
        {
            ReadModelExternal = (_, variable) => variable switch
            {
                17 => state.AdjustedDistance,
                25 => !blinded && prepared is { Timing: >= 20 } ? 2 : 0,
                46 => 0,
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
            PlaySound = (current, cue) =>
            {
                NativeBattleSequenceSound source = new(checked((byte)cue.First), cue.Kind == NativeBattleSequenceCueKind.ActorSound, checked((byte)cue.Second), checked((ushort)cue.Third)) { OverrideOnContact4 = cue.OverrideSoundOnContact4 };
                // TODO(TD-025): Supply the mixed contact's flags for the sequence's alternate miss sound.
                NativeBattleModuleSound? sound = NativeBattleSequenceAudio.Resolve505AB0(source, ActorSlot, checked((byte)invocation.TargetSlot), _ => kit.CharacterId,
                    index => index == ActorSlot ? NativeBattlePoseMatrices.ReadBone(current, resource.PoseSource!, 241, 0) : state.EffectTarget ?? throw new InvalidDataException("The guest Attack sound lost its selected host target."),
                    position => readHostPan(Vector3.Transform(new(position.X, position.Y, position.Z), originToHost)), graphicsRules.ActorSounds1120, 0);
                if (sound is not null)
                {
                    sounds.Add(sound);
                }
            },
            SelectModelSequence = (current, opcode, _, selected) =>
            {
                if (opcode != 0xBF || selected != 4)
                {
                    throw new NotSupportedException($"The guest Attack has no native target presentation for opcode {opcode:X2} and sequence {selected}.");
                }
                targetCue = selected;
                return current;
            },
            ApplyActionContacts = current =>
            {
                if (NativeGunbladeAttack.HasTrigger(kit.CharacterId) != (prepared is not null))
                {
                    throw new InvalidDataException("The native physical contact timing contradicts the selected weapon's attack type.");
                }
                hit = ProjectContact(kit, blinded ? (short)1 : prepared?.Timing ?? 0);
                return current;
            },
            ScheduleContactEffect = (current, program) =>
            {
                contactEffect = ReadParticle(program, SelectParticleBone);
                QueueParticle(contactEffect, current, true);
                return (current, contactEffect.NextOffset);
            },
            ScheduleModelParticle = (current, program) =>
            {
                NativeBattleSequenceParticle particle = ReadParticle(program);
                QueueParticle(particle, current, false);
                return (current, particle.NextOffset);
            }
        };
        CrossoverGunbladePresentationStep? step = presentation.Complete ? null : CrossoverGunbladePresentation.Advance(presentation, resource, inputs, 0, publicationDuration, state.PendingPress, receipt => prepared = receipt);
        presentation = step?.State ?? presentation;
        bool complete = presentation.Complete && (effects is null or { Complete: true });
        state = next with { Invocation = complete ? null : next.Invocation, PendingPress = false, Presentation = presentation, GlobalRandom = random, Auxiliary = auxiliary, Sine = sine, Cosine = cosine, PhysicalEffects = complete ? null : effects, EffectTarget = complete ? null : next.EffectTarget, PreviousParticleBone = previousParticleBone };
        LastTriggerSound = !blinded && step?.TriggerSound == true;
        LastAuxiliaryEmissions = tasks.Emissions;
        LastModuleSounds = sounds.ToImmutable();
        LastContactEffect = contactEffect;
        return new(hit, complete) { AttackTargetCue = targetCue };

        void QueueParticle(NativeBattleSequenceParticle particle, NativeBattleGraphicsModel model, bool contact)
        {
            if (state.EffectTarget is not { } target)
            {
                if ((byte)particle.PackedParameters == 34)
                {
                    throw new InvalidDataException("The native projectile has no selected host target anchor.");
                }
                return;
            }
            NativeCrtRandomState crt = effects?.Random ?? AttackCrtRandom ?? NativeCrtRandomState.FromColdProcess();
            effects = QueuePhysicalHostParticle(effects, target, invocation.TargetSlot, particle, model, contact, crt);
        }

        NativeBattleSequenceParticle ReadParticle(NativeBattleAuxiliaryProgram program, Func<byte>? selectBone = null)
        {
            SceneCameraPlacement camera = readHostCamera();
            Vector3 direction = Vector3.TransformNormal(camera.Position - camera.Focus, Matrix4x4.CreateRotationY(-placement.Yaw * MathF.Tau / 4096));
            int angle = NativeWorldScriptedPathMath.NativeAngle((int)direction.X, (int)direction.Z);
            return NativeBattleSequenceParticles.Read505900(program, angle, selectBone);
        }

        byte SelectParticleBone()
        {
            if (state.TargetJointCount is < 2 or > 256)
            {
                throw new InvalidDataException("The guest Attack particle needs the selected host model's joint count.");
            }
            random = random.Draw(out ushort draw);
            previousParticleBone = checked((byte)((draw % (state.TargetJointCount - 1) + previousParticleBone + 1) % state.TargetJointCount));
            return previousParticleBone;
        }
    }

    private NativeBattleGraphicsModel LoadStanding(ref NativeGlobalRandomState random)
    {
        NativeBattleGraphicsModel model = new(ActorSlot, 0)
        {
            Transform = new(Origin, Origin, 0, 0, 4096),
            SequenceState = new() { Locals = NativeBattleSequenceState.ColdProcessLocals() },
            LinkedTransform = new(default, default),
            FileSelector128 = resource.LinkedFileSelector128,
            FileSelector129 = resource.BodyFileSelector129
        };
        model = NativeBattleGraphicsModels.CompleteLoad(model, kit.CharacterId, 0, ref random);
        model = NativeBattleGraphicsModels.SelectSequence(model, 1);
        return NativeBattleSequenceMachine.Advance(model, resource, new(0, 0)).Model;
    }

    internal static Ff7BattleGuestPhysicalHit ProjectContact(CrossoverFf8BattleKit kit, short timing)
    {
        BattleNativeAction native = kit.Attack;
        bool gunblade = NativeGunbladeAttack.HasTrigger(kit.CharacterId);
        byte accuracy = native.Hit ?? 255;
        byte power = ProjectPhysicalPower(native.Power, gunblade ? timing / 20 + 2 : 2);
        Ff7BattleAttack? status = Ff7GuestBattleStatuses.ToHostStatusRecord(native.Statuses0, native.Statuses1, native.StatusAttack);
        Ff7BattleDamageAction action = new(0x11, power, accuracy, CanBeBlinded: !gunblade, UsePhysicalStats: true, HasStatusOperation: status is not null, AddsStatuses: status is not null)
        {
            SuppressCritical = gunblade,
            AlwaysHits = gunblade || accuracy == 255
        };
        return new(ProjectPhysicalSource(kit) with { CriticalBonus = (byte)(native.Critical.GetValueOrDefault() * 100 / 256) }, action, Ff7GuestBattleStatuses.ToHostElements(native.Element))
        {
            ElementPercent = native.ElementPercent,
            StatusRecord = status
        };
    }

    // Original physical type9 uses P/16 and untriggered type10 uses 2*P/32.
    internal static byte ProjectPhysicalPower(byte nativePower, int multiplier) => (byte)Math.Min(byte.MaxValue, 8 * nativePower * multiplier / 20);

    internal static Ff7BattleDamageActor ProjectPhysicalSource(CrossoverFf8BattleKit kit) => new(Project(kit.Level, 7, 6, 100, 99), Project(kit.Stats.Strength, 17, 36, 255, 255), 0, 0, Project(kit.Stats.Luck, 16, 14, 255, 255), 0, 0, 0, 0);

    private static long HostTime(long tick) => checked(tick * TimeSpan.TicksPerSecond) / Ff7BattleSession.LogicUpdatesPerSecond;

    private static byte Project(int value, int from, int to, int sourceCap, int targetCap)
        => checked((byte)(value <= from ? value * to / from : to + (value - from) * (targetCap - to) / (sourceCap - from)));
}
