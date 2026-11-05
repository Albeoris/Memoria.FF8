using System.Numerics;
using System.Reflection;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events.Tests;

internal static partial class Program
{
    private static void GameplaySaveFieldActorContract(string gameDirectory)
    {
        using GameAssets assets = new(gameDirectory);
        FieldAssets field = assets.LoadField(919);
        NativeModelPartsDefinition originalDefinition = field.Models[13].NativeParts!;
        (Jsm.Opcode, int)[] initialize = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.SETMODEL, 13), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.PSHN_L, 3), (Jsm.Opcode.DOFFSET, 0), (Jsm.Opcode.PSHN_L, 64), (Jsm.Opcode.SHADELEVEL, 0), (Jsm.Opcode.PSHN_L, 16), (Jsm.Opcode.SHADESET, 0), (Jsm.Opcode.PSHN_L, 42), (Jsm.Opcode.FOOTSTEP, 13), (Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.POPM_L, 500), (Jsm.Opcode.RET, 8)];
        byte[] content = SyntheticObjects([1], [initialize, [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]]);
        EventEngine Create(bool changed = false, byte[]? selectedProgram = null)
        {
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
            session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
            NativeModelPartsDefinition definition = changed ? originalDefinition with { SourceHash = new string('b', 64) } : originalDefinition;
            EventEngine engine = new(session)
            {
                AnimationFrameCount = (model, animation) => field.GetAnimationFrameCount(model, animation) + (changed ? 2 : 0),
                ReadNativeModelParts = model => model == 13 ? definition : null
            };
            engine.LoadField(106, selectedProgram ?? content, [], nativeRuntimeProfile: assets.NativeRuntimeProfile);
            return engine;
        }
        EventEngine source = Create();
        source.Step();
        ActorState original = source.Actors[0];
        SetGameplayActorValue(original, nameof(ActorState.NativeMovementCompletion542), (ushort)2);
        SetGameplayActorValue(original, nameof(ActorState.NativeLandingTriangle508), (ushort)0x8123);
        SetGameplayActorValue(original, nameof(ActorState.NativeTurnAccumulator474), (short)-123);
        SetGameplayActorValue(original, nameof(ActorState.NativeMovementAccelerationTarget), (short)321);
        SetGameplayActorValue(original, nameof(ActorState.NativeMovementGroundContact600), (byte)1);
        SetGameplayActorValue(original, nameof(ActorState.NativeTurnMode580), (byte)3);
        SetGameplayActorValue(original, nameof(ActorState.NativeMotionAuthority), new NativeMotionAuthority(1, 77, 2));
        SetGameplayActorValue(original, nameof(ActorState.NativeLadderAnimation), new NativeLadderAnimationState(5, 6, 7));
        SetGameplayActorValue(original, nameof(ActorState.Active), false);
        NativeModelPartResourceState settled = new(new() { [1] = new(1, 10, 20, 30, 10, 20, 30, 1, 4) });
        settled.Validate(originalDefinition);
        SetGameplayActorValue(source.Scene, nameof(SceneEventState.ModelParts), source.Scene.ModelParts! with { Resources = new() { [13] = settled } });
        GameplaySaveFieldActors captured = source.CaptureGameplaySaveFieldActors();
        GameplaySaveFieldActor savedActor = captured.Actors[0];
        captured.Actors[0] = savedActor with { Pose = savedActor.Pose with { Position = new(123.25f, -45.5f, 7), FixedPosition = new(504832, -186368, 28672) } };
        string encoded = JsonSerializer.Serialize(captured);
        GameplaySaveFieldActors detached = captured.Detach();
        Require(JsonSerializer.Serialize(detached) == encoded && detached.Actors[0].Pose.Position.X == 123.25f && detached.Actors[0].Appearance.PolygonColor!.Value.X == 128, "Production codec options lost scalar actor vectors, color or raw fixed position.");
        Require(!encoded.Contains("ProgramHash", StringComparison.Ordinal) && !encoded.Contains("SourceHash", StringComparison.Ordinal) && !encoded.Contains("NativeSetLineReceipt", StringComparison.Ordinal) && !encoded.Contains("Command", StringComparison.Ordinal), "Semantic actor DTO embedded resource identities or executed receipts.");
        EventEngine target = Create(changed: true);
        SetGameplayActorValue(target.Scene, nameof(SceneEventState.NativeFieldInitialization), target.Scene.NativeFieldInitialization! with { Origin = NativeFieldInitializationOrigin.GameplayContinuation, Stage = NativeFieldInitializationStage.Completed });
        int[] partyBindings = target.Scene.FieldPartyNativeActorIds.ToArray();
        PartyTraceState? trace = target.Scene.PartyTrace;
        target.RestoreGameplaySaveFieldActors(detached);
        Require(target.Frame == 0 && target.Actors[0].GameplayReconstruction is not null && !target.Actors[0].Active && target.Scene.NativeActorObjectIds.SequenceEqual(new[] { 0 }), "Actor restoration executed a callback or dropped the retained inactive resource instance.");
        Require(target.Scene.FieldPartyNativeActorIds.SequenceEqual(partyBindings) && ReferenceEquals(trace, target.Scene.PartyTrace), "Actor restoration replaced scene-owned party bindings or trace.");
        Require(target.Actors[0].NativeLadderAnimation == new NativeLadderAnimationState(5, 6, 7) && target.Actors[0].NativeMovementCompletion542 == 2 && target.Actors[0].NativeLandingTriangle508 == 0x8123 && target.Actors[0].NativeTurnAccumulator474 == -123 && target.Actors[0].NativeMovementAccelerationTarget == 321 && target.Actors[0].NativeMotionAuthority == new NativeMotionAuthority(1, 77, 2), "Shared logical movement cells were reset during resource reconstruction.");
        Require(target.Actors[0].NativeFootstepPair is { First392: 42, Second393: 13, Source.Kind: "GameplaySave", Source.ScriptId: -1, Source.Operation: -1 } && target.Actors[0].NativeShadowSet is null && target.Actors[0].NativeShadowLevel is null && target.Actors[0].NativeShadowAllocation is null && target.Actors[0].NativeDisplayOffset is { Command: null }, "Semantic reconstruction fabricated an executed footstep, shadow or display-offset command.");
        Require(target.Actors[0].ResolvedShadowLevel == 64 && target.Actors[0].ResolvedShadowRadii!.SequenceEqual(Enumerable.Repeat((byte)4, 8)), "Renderer logical shadow inputs changed after removing executed receipts.");
        Require(target.Scene.ModelParts!.Draws[0].Origin == NativeModelPartDrawOrigin.GameplayReconstruction && target.GetNativeModelPartTranslations(0)!.Values.Contains(new Vector3(10, 20, 30)), "Settled model-part state did not rebuild a current-resource skin sample without a callback.");
        Require(target.Actors[0].NativeAnimationHeader!.Terminal == (field.GetAnimationFrameCount(13, detached.Actors[0].Animation.Selector) + 2) * 16, "Restoration copied the previous resource's animation terminal.");
        typeof(EventEngine).GetField("defaultsStarted", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, true);
        GameplaySaveFieldActors roundTrip = target.CaptureGameplaySaveFieldActors();
        Require(JsonSerializer.Serialize(roundTrip) == encoded, "Actor capture/rebuild changed durable logical values or fixed position.");
        Require(JsonSerializer.Serialize(detached) == encoded, "Actor rebuild mutated the source component.");

        foreach (string member in new[] { nameof(ActorState.MovementTarget), nameof(ActorState.RotationTarget), nameof(ActorState.NativePushContactPending) })
        {
            EventEngine active = Create();
            active.Step();
            SetGameplayActorValue(active.Actors[0], member, member == nameof(ActorState.MovementTarget) ? new Vector3(1, 2, 3) : member == nameof(ActorState.RotationTarget) ? (object)1f : true);
            bool rejected = false;
            try
            {
                active.CaptureGameplaySaveFieldActors();
            }
            catch (NotSupportedException)
            {
                rejected = true;
            }
            Require(rejected, "Semantic actors accepted an active transition or pending contact.");
        }
        List<(Jsm.Opcode Opcode, int Parameter)[]> doorScripts = [initialize, [(Jsm.Opcode.LBL, 1), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)]];
        doorScripts.Add([(Jsm.Opcode.LBL, 8), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 100), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.PSHN_L, 0), (Jsm.Opcode.SETLINE, 0), (Jsm.Opcode.RET, 8)]);
        for (int script = 9; script < 16; script++)
        {
            doorScripts.Add([(Jsm.Opcode.LBL, script), (Jsm.Opcode.RET, 8)]);
        }
        byte[] doorProgram = SyntheticObjects([(8 << 7) | 7, 1], doorScripts);
        doorProgram[1] = 1;
        doorProgram[3] = 1;
        EventEngine sourceDoor = Create(selectedProgram: doorProgram);
        sourceDoor.Step();
        GameplaySaveFieldActors doorSaved = sourceDoor.CaptureGameplaySaveFieldActors();
        EventEngine targetDoor = Create(selectedProgram: doorProgram);
        SetGameplayActorValue(targetDoor.Scene, nameof(SceneEventState.NativeFieldInitialization), targetDoor.Scene.NativeFieldInitialization! with { Origin = NativeFieldInitializationOrigin.GameplayContinuation, Stage = NativeFieldInitializationStage.Completed });
        targetDoor.RestoreGameplaySaveFieldActors(doorSaved);
        ActorState restoredLine = targetDoor.Actors.Single(actor => actor.LineStart is not null);
        Require(restoredLine.NativeSetLineReceipt is null && restoredLine.LineOperation == -1 && (bool)typeof(EventEngine).GetMethod("ValidNativeDoorState", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(targetDoor, [restoredLine, targetDoor.Frame])!, "Logical door geometry required a fabricated SETLINE script site.");

        GameplaySaveFieldActors malformed = captured with { RegisteredActors = [0, 0] };
        bool invalidRejected = false;
        try
        {
            malformed.Validate();
        }
        catch (InvalidDataException)
        {
            invalidRejected = true;
        }
        Require(invalidRejected, "Semantic actor validation accepted aliased retained object IDs.");
    }

    private static void SetGameplayActorValue(object actor, string name, object value)
    {
        actor.GetType().GetProperty(name)!.SetValue(actor, value);
    }
}
