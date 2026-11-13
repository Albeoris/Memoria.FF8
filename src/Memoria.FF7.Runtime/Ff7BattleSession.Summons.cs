using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed record Ff7BattleSummonPresentation(int Age = 0, bool FatChocobo = false, bool FirstCameraVariant = false, int TargetZ = 0, int Yaw = 0, ushort PartyVisible = 0, Ff7BattleChocoMogCameraState? Camera = null, Ff7BattleCameraView? CameraView = null, Ff7BattleSummonModelDraw? Model = null, bool Completed = false);

public sealed partial class Ff7BattleSession
{
    private readonly IReadOnlyList<Ff7BattleSummon>?[] nativeBattleSummons = new IReadOnlyList<Ff7BattleSummon>?[3];

    private bool HasChocoMogResources => animations is { ActorResources.SummonCameras: not null };

    private IReadOnlyList<Ff7BattleSummon> NativeBattleSummons(int slot) => nativeBattleSummons[slot] ??= Ff7NativeBattleSummons.Read(characters[slot]!, kernel);

    private static int SummonUses(Ff7BattleActorState actor, Ff7BattleSummon summon) => actor.SummonUses?.GetValueOrDefault(summon.AttackId, summon.InitialUses) ?? summon.InitialUses;

    private byte? ResolveSummon(Ff7BattleQueuedCommand command)
    {
        int slot = command.ActorSlot;
        Ff7BattleActorState source = Actor(slot);
        Ff7BattleSummon? summon = NativeBattleSummons(slot).SingleOrDefault(candidate => candidate.AttackId == command.Argument);
        if (summon is not { AttackId: 0x38 } || command.TargetMask == 0 || !HasChocoMogResources)
        {
            return null;
        }
        int uses = SummonUses(source, summon);
        Ff7BattleActionVitals vitals = new(source.Actor.HitPoints, source.Actor.MaxHitPoints, source.Actor.MagicPoints, source.Actor.MaxMagicPoints, source.Actor.Statuses);
        if (uses <= 0 || (source.Actor.Statuses & 0x80) != 0 || !Ff7NativeBattleActionResolver.TrySpendCastingCost(vitals, summon.Attack, out vitals))
        {
            return null;
        }

        Ff7BattleRng transaction = new(kernel[2].Data.Span, random.Capture());
        byte selected = Ff7NativeBattleSummons.SelectChocoMogAttack(summon, transaction);
        Ff7BattleAttack record = ReadKernelAttack(selected) with { AdditionalEffect = byte.MaxValue };
        foreach (Ff7BattleGuestSlotState guest in state.GetGuestSlotStates())
        {
            if ((command.TargetMask & (1 << guest.Binding.HostSlot)) != 0)
            {
                ValidateGuestIncoming(command, guest.Binding.HostSlot, record);
            }
        }

        Dictionary<byte, int> remaining = source.SummonUses is null ? [] : new(source.SummonUses);
        remaining[summon.AttackId] = uses - 1;
        Replace(source with { Defending = false, SummonUses = remaining, Actor = source.Actor with { MagicPoints = vitals.Mp } });
        random.Adopt(transaction);
        Ff7BattleDamageAction damage = Ff7BattleDamageAction.FromAttack(record) with { MpTurboLevel = summon.MpTurboLevel };
        Ff7BattleQueuedCommand resolved = command with { AttackId = selected };
        foreach (Ff7BattleActorState target in state.Actors)
        {
            if ((command.TargetMask & (1 << target.Actor.Slot)) != 0)
            {
                ApplyHit(resolved, target.Actor.Slot, damage, record, record.Elements);
            }
        }
        return selected;
    }

    private Ff7BattleActionState StartSummonPresentation(Ff7BattleActionState action)
    {
        if (action.Summon is not null || action.ResolvedSummonAttack is not (0x38 or 0x60))
        {
            return action;
        }
        ushort visible = Mask(state.Actors.Where(actor => actor.Actor.Slot < 3 && LiveActorModelOutput(actor.Actor.Slot).Visible));
        foreach (Ff7BattleActorState member in state.Actors.Where(actor => (visible & (1 << actor.Actor.Slot)) != 0))
        {
            BeginActorFade(member.Actor.Slot, false, 14);
        }
        AppendSounds(Ff7NativeBattleSound.Emit(state.Sounds ?? Ff7BattleSoundState.Empty, 41, 64));
        return action with { Summon = new(FatChocobo: action.ResolvedSummonAttack == 0x60, PartyVisible: visible) };
    }

    private Ff7BattleActionState AdvanceSummonPresentation(Ff7BattleActionState action)
    {
        Ff7BattleSummonPresentation summon = action.Summon!;
        int age = summon.Age;
        if (age < 15)
        {
            return action with { Summon = summon with { Age = age + 1 } };
        }

        int cinematicAge = age - 15;
        if (summon.Camera is null)
        {
            int center = TargetRootCenter(action.Command.TargetMask).Z;
            summon = summon with
            {
                FirstCameraVariant = (random.NextCrt() & 0x100) != 0,
                TargetZ = center,
                Yaw = center < VisualPlacement(action.Command.ActorSlot).Position.Z ? 0x800 : 0,
                Camera = new()
            };
        }

        int impactAge = summon.FatChocobo ? 91 : 135;
        if (cinematicAge == impactAge)
        {
            action = PublishTargetReactions(action, state.Actors.Where(actor => (action.Command.TargetMask & (1 << actor.Actor.Slot)) != 0).Select(actor => actor.Actor.Slot));
        }
        if (cinematicAge == (summon.FatChocobo ? 1 : 5))
        {
            AppendSounds(Ff7NativeBattleSound.Emit(state.Sounds ?? Ff7BattleSoundState.Empty, summon.FatChocobo ? (ushort)507 : (ushort)589, 64, 1));
        }
        if (!summon.FatChocobo && cinematicAge == 65)
        {
            AppendSounds(Ff7NativeBattleSound.Emit(state.Sounds ?? Ff7BattleSoundState.Empty, 590, 64, 2));
        }

        Ff7BattleSummonModelDraw? model = SummonModel(summon, cinematicAge);
        Ff7BattleChocoMogCameraState camera = summon.Camera!;
        Ff7BattleCameraView? cameraView = null;
        if (!camera.Retired)
        {
            Ff7BattleSummonCameraResources resources = animations!.ActorResources!.SummonCameras!;
            IReadOnlyList<short> script = resources.Script(summon.FatChocobo, summon.FirstCameraVariant);
            camera = Ff7BattleChocoMogCamera.Advance(camera, script);
            cameraView = Ff7BattleChocoMogCamera.View(camera, action.Command.ActorSlot, (slot, bone) =>
            {
                if (slot != 3)
                {
                    return bone < 0 ? CameraPoint(VisualPlacement(slot).Position) : CameraJoint(slot, bone);
                }
                Ff7BattleSummonModelDraw live = model ?? new(summon.FatChocobo, 0, 0, new(0, 0, summon.TargetZ), summon.Yaw);
                Ff7BattleSummonModel resource = animations.GetSummonModel(summon.FatChocobo);
                Vector3 joint = BattleRigidPose.BonePosition(resource.Model, resource.Frame(live.Clip, live.Frame), live.Position.ToVector(), live.Yaw * (MathF.Tau / 4096), bone);
                return CameraPoint(joint);
            });
        }
        if (camera.Retired && !summon.Camera!.Retired)
        {
            foreach (Ff7BattleActorState member in state.Actors.Where(actor => (summon.PartyVisible & (1 << actor.Actor.Slot)) != 0))
            {
                BeginActorFade(member.Actor.Slot, true, 14);
            }
        }

        bool completed = camera.Retired && NativeActorModelsReady;
        return action with { Summon = summon with { Age = age + 1, Camera = camera, CameraView = camera.Retired ? null : cameraView, Model = model, Completed = completed } };
    }

    // TODO(TD-028) Consume the remaining stage, smoke, and summoned model script callbacks.
    private static Ff7BattleSummonModelDraw? SummonModel(Ff7BattleSummonPresentation summon, int age)
    {
        if (summon.FatChocobo)
        {
            if (age is < 30 or >= 91)
            {
                return null;
            }
            int local = age - 30;
            int y = local < 15 ? -10000 + (local + 1) * 666 : 0;
            return new(true, local < 15 ? 0 : 1, local, new(0, y, summon.TargetZ), summon.Yaw);
        }
        if (age is < 4 or >= 133)
        {
            return null;
        }
        int frame = Math.Max(0, age - 5);
        int yPosition = frame < 79 ? 0 : -750;
        int z = frame < 60 ? frame * 250 - 15000 : frame < 79 ? -250 : -5000;
        int yaw = summon.Yaw + 0x800 + (frame >= 79 ? 1024 : 0);
        if (summon.Yaw != 0)
        {
            z = -z;
        }
        return new(false, frame < 79 ? 0 : 1, frame, new(0, yPosition, summon.TargetZ + z), yaw);
    }

    private void ValidateSummonSnapshot(Ff7BattleSnapshot snapshot)
    {
        foreach (Ff7BattleActorState actor in snapshot.Actors)
        {
            if (actor.SummonUses is not { } uses)
            {
                continue;
            }
            if (actor.Actor.Slot >= 3 || uses.Any(pair => pair.Key != 0x38 || pair.Value < 0 || !NativeBattleSummons(actor.Actor.Slot).Any(summon => summon.AttackId == pair.Key && pair.Value <= summon.InitialUses)))
            {
                throw new InvalidDataException("FFVII summon stock does not match its original equipped materia.");
            }
        }
        if (snapshot.Action is not { } action)
        {
            return;
        }
        if (action.ResolvedSummonAttack is byte selected && (selected is not (0x38 or 0x60) || action.Command is not { CommandId: (byte)Ff7BattleCommand.Summon, Argument: 0x38 } || action.Command.AttackId != selected))
        {
            throw new InvalidDataException("FFVII battle snapshot has an invalid resolved summon owner.");
        }
        if (action.Summon is { } effect && (action.ResolvedSummonAttack is not (0x38 or 0x60) || effect.FatChocobo != (action.ResolvedSummonAttack == 0x60) || effect.Age is < 0 or > 200 || (effect.PartyVisible & ~7) != 0 || effect.Yaw is not (0 or 0x800) || effect.Camera is { ProgramCounter: < 0 or > 80 } || effect.Model is { Clip: < 0 or > 1 } || effect.Model is { Frame: < 0 }))
        {
            throw new InvalidDataException("FFVII battle snapshot has an invalid summon presentation cursor.");
        }
    }

    private void EmitSummonSound(ushort sound, int actor)
    {
        AppendSounds(Ff7NativeBattleSound.Emit(state.Sounds ?? Ff7BattleSoundState.Empty, sound, Ff7NativeBattleSound.ActorPan(SoundScreenX(actor))));
    }
}
