using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public readonly record struct Ff7BattleCameraView(Ff7BattleCameraPoint Position, Ff7BattleCameraPoint Focus)
{
    public SceneCameraPlacement ToPlacement() => new(Position.ToVector(), Focus.ToVector());
}

public sealed record Ff7BattleCameraPresentation(Ff7BattleCameraScriptState Scripts, Ff7BattleCameraView Output, Ff7BattleCameraView RenderOutput, int Attacker = 0, int Target = 0, ushort TargetMask = 0, ushort Options = 0, short MenuPhase = -1, sbyte PrimaryQueueState = 0, Ff7BattleOutcome? TerminalOutcome = null);

public sealed partial class Ff7BattleSession
{
    private readonly Ff7BattleCameraScriptResources? cameraScripts;
    private bool cameraAdvanced;
    private List<int>? cameraTargetRows;

    public SceneCameraPlacement CameraOutput => state.Camera?.Output.ToPlacement() ?? Data.Cameras[0];
    public SceneCameraPlacement RenderCameraOutput => state.Camera?.RenderOutput.ToPlacement() ?? Data.Cameras[0];
    public SceneCameraPlacement GuestRenderCameraOutput
    {
        get
        {
            SceneCameraPlacement camera = RenderCameraOutput;
            float translation = (guestAttack as IFf7BattleGuestCamera)?.ViewTranslationY ?? 0;
            Vector3 offset = BattleCameraBasis.Create(camera).Up * translation;
            return new(camera.Position - offset, camera.Focus - offset);
        }
    }
    public byte CameraPresentationCounter => state.Camera?.Scripts.PresentationCounter ?? 0;

    public void SetCameraMenuPhase(short phase)
    {
        if (state.Camera is { } camera)
        {
            state = state with { Camera = camera with { MenuPhase = phase } };
        }
    }

    public void SetCameraJointDeferral(int slot, bool deferred)
    {
        SetActorModelDeferral(slot, deferred);
    }

    private void InitializeCamera(byte counter, bool fresh)
    {
        if (cameraScripts is null || state.Camera is not null)
        {
            return;
        }
        if (animations is null)
        {
            throw new InvalidOperationException("FFVII native camera presentation requires original actor animation resources.");
        }
        SceneCameraPlacement initial = Data.Cameras[0];
        Ff7BattleCameraScriptState scripts = Ff7NativeBattleCameraScripts.Create(fresh ? (short)-2 : (short)-1, Formation.InitialCamera, initial, counter);
        state = state with { Camera = new(scripts, CameraView(initial), CameraView(initial)), Intro = fresh ? new() : null };
        if (!fresh)
        {
            PublishActorFrames(false);
        }
    }

    private void BeginCameraPresentation()
    {
        cameraAdvanced = false;
        actorModelCursor = 0;
        deferredActorSubmissions.Clear();
        actorDeferAfterSubmission.Clear();
        if (state.Camera is { } camera)
        {
            state = state with { Camera = camera with { RenderOutput = camera.Output } };
        }
    }

    private void AdvanceCameraPresentation()
    {
        if (cameraAdvanced)
        {
            return;
        }
        AdvanceActorReactionModels(actorModelCursor, 10);
        actorModelCursor = 10;
        AdvanceActorMotionPresentation();
        PublishActorFrames(false);
        cameraAdvanced = true;
        if (state.Camera is not { } camera)
        {
            return;
        }
        if (Outcome != Ff7BattleOutcome.Fighting && camera.TerminalOutcome != Outcome)
        {
            short terminal = Outcome == Ff7BattleOutcome.Victory ? (short)-3 : (short)-1;
            camera = camera with { Scripts = Ff7NativeBattleCameraScripts.Start(camera.Scripts, terminal), TerminalOutcome = Outcome };
            state = state with { Camera = camera };
        }
        camera = state.Camera!;
        Ff7BattleCameraActors actors = new(camera.Attacker, camera.Target,
            (slot, selector) => animations!.JointSelector(ActorModel(slot), selector),
            (slot, bone) => CameraJoint(slot, bone),
            slot => CameraActor(slot),
            CameraGroupCenter, cameraScripts!.Math);
        Ff7BattleCameraScriptUpdate update = Ff7NativeBattleCameraScripts.Advance(camera.Scripts, true, false, cameraScripts.Script, Data.Cameras, camera.Output.ToPlacement(), cameraScripts.Math.Cosine, actors);
        sbyte phase = Outcome == Ff7BattleOutcome.Victory ? (sbyte)100 : (sbyte)0;
        Ff7BattleCameraOutput selected = Ff7NativeBattleCameraOutput.Select(update.State, new(unchecked((ushort)Formation.LocationId), camera.Options, phase, camera.MenuPhase, camera.PrimaryQueueState), Data.Cameras, camera.Output.ToPlacement());
        state = state with { Camera = camera with { Scripts = selected.State, Output = CameraView(selected.Camera), Options = selected.Options, Target = update.TargetWrite ?? camera.Target } };
    }

    private void FinishCameraPresentation()
    {
        PublishActorFrames(true);
        if (state.Camera is not { } camera)
        {
            return;
        }
        state = state with { Camera = camera with { Scripts = Ff7NativeBattleCameraScripts.AdvanceOuterCallback(camera.Scripts), PrimaryQueueState = NativeActorModelsReady && !HasPrimaryPresentationCallbacks ? (sbyte)1 : (sbyte)0 } };
    }

    private Ff7BattleCameraPoint CameraJoint(int slot, int bone)
    {
        Ff7BattleActorModelState? model = state.ActorModels.SingleOrDefault(model => model.Slot == slot);
        if (model?.Joints is not { } joints || (uint)bone >= joints.Length)
        {
            throw new NotSupportedException($"FFVII camera joint {slot}:{bone} has no initialized native actor cache.");
        }
        return joints[bone].ToCameraPoint();
    }

    private Ff7BattleCameraActorInput CameraActor(int slot)
    {
        Ff7BattleActorState actor = Actor(slot);
        short action = state.ActorModels.Length != 0 ? ActorModelState(slot).Action : checked((short)actor.Actor.IdleAction);
        return new(animations!.CameraModelParameters(ActorModel(slot)), 0x1000, unchecked((short)VisualPlacement(slot).Yaw), unchecked((short)HomePlacement(slot).Yaw), action);
    }

    private Ff7BattleCameraPoint CameraGroupCenter()
    {
        Ff7BattleCameraPoint[] points = state.Actors.Where(actor => (state.Camera!.TargetMask & 1 << actor.Actor.Slot) != 0).Select(actor => CameraPoint(VisualPlacement(actor.Actor.Slot).Position)).ToArray();
        if (points.Length == 0)
        {
            return default;
        }
        return new(unchecked((short)(((int)points.Min(point => point.X) + (int)points.Max(point => point.X)) / 2)), 0, unchecked((short)(((int)points.Min(point => point.Z) + (int)points.Max(point => point.Z)) / 2)));
    }

    private void SetCameraActiveAction()
    {
        if (state.Camera is { } camera)
        {
            state = state with { Camera = camera with { Scripts = camera.Scripts with { ActiveAction = 1 } } };
        }
    }

    private void SetCameraEntry(byte initialCamera)
    {
        if (state.Camera is { } camera)
        {
            state = state with { Camera = camera with { Scripts = Ff7NativeBattleCameraScripts.Start(camera.Scripts with { InitialCamera = initialCamera }, -2) } };
        }
    }

    private ushort BeginCameraAction(Ff7BattleQueuedCommand command)
    {
        int[] rows = cameraTargetRows?.ToArray() ?? [];
        cameraTargetRows = null;
        state = state with { ModelPrimaryTarget = Ff7NativeBattleCamera.InitialTarget(rows) ?? state.ModelPrimaryTarget };
        ushort mask = 0;
        foreach (int row in rows)
        {
            mask |= (ushort)(1 << row);
        }
        if (state.Camera is not { } camera)
        {
            return mask;
        }
        Ff7BattleActionCameras pair = CameraSelection(command, rows.Length > 1);
        short selected = unchecked((short)(rows.Length > 1 ? pair.MultipleTargets : pair.SingleTarget));
        state = state with { Camera = camera with { Scripts = Ff7NativeBattleCameraScripts.Start(camera.Scripts, selected), Attacker = command.ActorSlot, Target = Ff7NativeBattleCamera.InitialTarget(rows) ?? camera.Target, TargetMask = mask } };
        return mask;
    }

    private Ff7BattleActionCameras CameraSelection(Ff7BattleQueuedCommand command, bool multiple)
    {
        if (command.GuestAttack)
        {
            return new(ushort.MaxValue, ushort.MaxValue);
        }
        Ff7BattleAttack? attack = command.CommandId switch
        {
            0x20 => Data.Attacks.Single(attack => attack.Slot == command.Argument && attack.Id == command.AttackId).Data,
            (byte)Ff7BattleCommand.Magic => ReadKernelAttack(command.Argument),
            (byte)Ff7BattleCommand.Limit => limits[command.Argument - 0x80],
            _ => null
        };
        if (command.CommandId == (byte)Ff7BattleCommand.Item)
        {
            return Ff7NativeBattleCamera.SelectItem(kernel[4].Data.Span.Slice(command.Argument * 28, 28));
        }
        if (command.CommandId == (byte)Ff7BattleCommand.Attack)
        {
            int weapon = characters[command.ActorSlot]!.NativeRecord[28];
            return Ff7NativeBattleCamera.SelectWeapon(kernel[5].Data.Span.Slice(weapon * 44, 44));
        }
        if (attack is null)
        {
            return new(ushort.MaxValue, ushort.MaxValue);
        }
        ushort over = command.ActorSlot >= 4 ? Enemy(command.ActorSlot).Attacks.First(attack => attack.AttackId == (command.AttackId ?? command.Argument)).CameraOverride : ushort.MaxValue;
        return Ff7NativeBattleCamera.SelectAction(attack, over, command.CommandId == (byte)Ff7BattleCommand.Limit, multiple);
    }

    private static Ff7BattleCameraView CameraView(SceneCameraPlacement placement) => new(CameraPoint(placement.Position), CameraPoint(placement.Focus));

    private static Ff7BattleCameraPoint CameraPoint(Vector3 point) => new(unchecked((short)(int)point.X), unchecked((short)(int)point.Y), unchecked((short)(int)point.Z));

    private static Ff7BattleCameraPresentation CopyCamera(Ff7BattleCameraPresentation camera) => camera with { Scripts = Ff7NativeBattleCameraScripts.Copy(camera.Scripts) };

    private void ValidateCamera(Ff7BattleSnapshot snapshot)
    {
        if (snapshot.Intro is { Callbacks: > 63 })
        {
            throw new InvalidDataException("FFVII battle startup cursor exceeds its resident-resource protocol.");
        }
        if (snapshot.Camera is not { } camera)
        {
            return;
        }
        if (cameraScripts is null || animations is null || camera.Scripts is null || camera.PrimaryQueueState is < 0 or > 1 || camera.Attacker is < 0 or > 9 || camera.Target is < 0 or > 15 || camera.Target is > 9 and < 15)
        {
            throw new InvalidDataException("FFVII battle camera snapshot lost its native resource, selector or joint-cache owner.");
        }
        Ff7NativeBattleCameraScripts.Validate(camera.Scripts, cameraScripts.Script);
    }
}
