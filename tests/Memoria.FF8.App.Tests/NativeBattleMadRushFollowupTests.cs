using System.Text.Json;
using System.Text.Json.Serialization;
using System.Reflection;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleMadRushFollowupTests
{
    [Theory]
    [InlineData("foreign-callback")]
    [InlineData("binding-roundtrip")]
    public void Original_party_aftercare_reaches_the_existing_conditional_texture_fixed_point(string fault)
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? recipe = Environment.GetEnvironmentVariable("MEMORIA_FF8_FOLLOWUP_RECIPE");
        Assert.SkipUnless(game is not null && recipe is not null && File.Exists(recipe), "Select installed resources and an external ordinary party-status recipe.");
        JsonSerializerOptions json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };
        DiagnosticBattleRequest request = JsonSerializer.Deserialize<DiagnosticBattleRequest>(File.ReadAllText(recipe!), json) ?? throw new InvalidDataException("Missing request.");
        request.Validate();
        DiagnosticBattleDriverRecipe driver = Assert.IsType<DiagnosticBattleDriverRecipe>(request.Driver);
        Assert.Equal((ushort)273, driver.ExpectedEffect);
        Assert.NotNull(driver.FreshEntry);
        GameSession session = GameWorld.CreateColdProcess(game!);
        session.StartNewGame();
        using GameWorld world = new(session, game!);
        BattleSession battle = world.StartDiagnosticBattle(request);
        BattleAudioPresentation audio = BattleAudioTestOutput.Create();
        int dispatchCount = 0;
        int followupCount = 0;
        battle.DiagnosticEffectDispatched += dispatch =>
        {
            dispatchCount++;
        };
        battle.DiagnosticInvocationReleased += IgnoreRelease;
        battle.DiagnosticInvocationFocalRetired += IgnoreRelease;
        battle.DiagnosticInvocationFollowupReleased += observed =>
        {
            followupCount++;
        };
        battle.ConfigureDiagnosticInvocationCompletion(driver.ExpectedEffect, driver.Input.ActorId!, driver.ExpectedNativeCommand, driver.ExpectedNativeAction ?? driver.Input.ActionId);
        if (request.PrepareEntryBeforePresentation)
        {
            world.PrepareDiagnosticEntryBeforePresentation(request);
        }
        for (int callback = 0; callback < 4096 && !Ready(); callback++)
        {
            Tick();
        }
        Assert.True(Ready());
        for (int callback = 0; callback < Math.Ceiling(driver.BaselineSeconds * 60); callback++)
        {
            Tick();
        }
        Assert.True(Ready());
        world.ApplyBattleInput(driver.Input);
        BattleComponentTextureQuiescenceObservation? fixedPoint = null;
        BattleDiagnosticAftercareBoundary? queuedBoundary = null;
        JsonElement state = default;
        for (int callback = 0; callback < 4096; callback++)
        {
            Tick();
            if (battle.ObserveDiagnosticAftercareBoundary() is { CallbackCompleted: false } queued)
            {
                queuedBoundary ??= queued;
            }
            state = JsonSerializer.SerializeToElement(battle.ObserveDiagnosticInvocationCompletionState(), json);
            if (state.GetProperty("followup_ambiguous").GetBoolean())
            {
                break;
            }
            JsonElement callbacks = state.GetProperty("callbacks");
            if (state.GetProperty("followup_retirement_captured").GetBoolean() && callbacks.ValueKind == JsonValueKind.Array && callbacks.GetArrayLength() == 1 && callbacks[0].GetProperty("stage").GetByte() == 2 && callbacks[0].GetProperty("completed").ValueKind == JsonValueKind.Number)
            {
                fixedPoint = battle.ObserveComponentTerminalTextures(driver.ExpectedEffect);
                if (fixedPoint is not null)
                {
                    break;
                }
            }
        }
        Assert.True(fixedPoint is not null, "Actual aftercare did not meet the existing native texture fixed-point contract: " + state.GetRawText());
        Assert.True(fixedPoint!.IsEligible);
        Assert.True(fixedPoint.Textures.IsTerminalQuiescent);
        Assert.Equal(0u, fixedPoint.CanonicalReturnedTask);
        Assert.Equal(0, fixedPoint.NativeActive);
        Assert.Equal(["effect-or-auxiliary-task"], fixedPoint.RawBlockers);
        Assert.True(fixedPoint.Owners.AuxiliaryHead);
        Assert.False(fixedPoint.Owners.ActionOrCommand);
        Assert.False(fixedPoint.Owners.TimedOrReaction);
        Assert.Single(fixedPoint.Textures.Tasks);
        Assert.Equal(driver.Input.ActorId, fixedPoint.Textures.Tasks[0].ModelActorId);
        Assert.Equal((byte)255, fixedPoint.Textures.Tasks[0].Deadline);
        BattleDiagnosticInvocationFollowupRelease release = Assert.IsType<BattleDiagnosticInvocationFollowupRelease>(battle.LastDiagnosticInvocationFollowupRelease);
        DiagnosticInvocationReleaseValidation.ValidateFollowup(release, release.FocalRetirement.Dispatch, battle.DiagnosticLifecycleId);
        Assert.False(release.RawDrained);
        Assert.Empty(release.TextureReleases);
        Assert.NotNull(battle.ObserveDiagnosticFollowupQuiescence(release));
        Assert.Null(battle.ObserveDiagnosticFollowupQuiescence(release with { }));
        BattleDiagnosticAftercareBoundary first = Assert.IsType<BattleDiagnosticAftercareBoundary>(queuedBoundary);
        BattleDiagnosticAftercareBoundary last = Assert.IsType<BattleDiagnosticAftercareBoundary>(battle.ObserveDiagnosticAftercareBoundary());
        Assert.False(first.CallbackCompleted);
        Assert.True(first.Owners.ActionOrCommand);
        Assert.True(last.CallbackCompleted);
        Assert.False(last.Owners.ActionOrCommand);
        Assert.Equal(first.ResidentFingerprint, last.ResidentFingerprint);
        Assert.Equal(last.GraphicsCallback - first.GraphicsCallback, last.ObservedFrames - first.ObservedFrames);
        BattleDiagnosticPlayerStage2NoOp noOp = Assert.IsType<BattleDiagnosticPlayerStage2NoOp>(release.QuiescentTextures!.NoOp);
        Assert.Equal(noOp.Inputs.Actor, noOp.Inputs.Attacker);
        Assert.InRange(noOp.Inputs.NativeActorIndex, (byte)0, (byte)2);
        Assert.Equal(noOp.Inputs.NativeActorIndex, noOp.Inputs.AttackerNativeIndex);
        Assert.Equal(0, noOp.HeaderCount);
        Assert.Equal(1, noOp.OperationCount);
        Assert.Equal(0, noOp.ProducedCommands);
        Assert.Equal(0x47DDA0u, noOp.CompletionCallback);
        Assert.Equal(release.FocalRetirement.CompletionToken + 1, noOp.CompletionToken);
        Assert.Equal(1, followupCount);
        int beforeDispatches = dispatchCount;
        long? beforeCallback = battle.NativeHostCallbacks;
        if (fault == "foreign-callback")
        {
            MethodInfo observeCallback = typeof(BattleSession).GetMethod("ObserveDiagnosticParticipantCallback", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("Missing participant observer.");
            BattleExpandedAiCallbackRequest foreign = new(new BattleEnemyActorReference("enemy-6"), 2);
            foreach (BattleParticipantCallbackPhase phase in new[] { BattleParticipantCallbackPhase.Queued, BattleParticipantCallbackPhase.Started, BattleParticipantCallbackPhase.Completed })
            {
                observeCallback.Invoke(battle, [new BattleParticipantCallbackReceipt(foreign, phase, [])]);
            }
        }
        else
        {
            BattlePresentationProcess process = battle.Presentation!;
            NativeBattleGraphicsState original = process.Graphics;
            BattlePartyActorModel owner = new(new(driver.Input.ActorId!));
            NativeBattleGraphicsModel model = original.ActorModels.Get(owner);
            PropertyInfo graphics = typeof(BattlePresentationProcess).GetProperty(nameof(BattlePresentationProcess.NativeGraphics)) ?? throw new InvalidOperationException("Missing graphics owner.");
            MethodInfo observeFrame = typeof(BattleSession).GetMethod("ObserveDiagnosticAftercareFrameEnded", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("Missing frame observer.");
            try
            {
                graphics.SetValue(process, original with { ActorModels = original.ActorModels.Set(owner, model with { ActiveResourceRow100 = (model.ActiveResourceRow100 ?? 0) + 1 }) });
                observeFrame.Invoke(battle, []);
            }
            finally
            {
                graphics.SetValue(process, original);
            }
            Assert.Equal(last.ResidentFingerprint, battle.ObserveComponentTerminalTextures(driver.ExpectedEffect)!.ResidentFingerprint);
            Assert.Null(battle.ObserveDiagnosticAftercareBoundary());
        }
        Assert.Equal(beforeDispatches, dispatchCount);
        Assert.Equal(beforeCallback, battle.NativeHostCallbacks);
        Assert.Null(battle.ObserveDiagnosticFollowupQuiescence(release));
        PropertyInfo observes = typeof(BattleSession).GetProperty("ObservingDiagnosticInvocationFollowup", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("Missing followup observer lifetime.");
        battle.CloseDiagnosticFollowupQuiescenceObservation(release);
        Assert.False(Assert.IsType<bool>(observes.GetValue(battle)));
        bool subsequentAttack = false;
        for (int callback = 0; callback < 128; callback++)
        {
            Tick();
            if (battle.ActiveAction?.NativeQueuedCommand == 1)
            {
                subsequentAttack = true;
                break;
            }
        }
        Assert.True(subsequentAttack, "Native Berserk continuation was not observed after the diagnostic receipt.");
        Assert.Null(battle.ObserveDiagnosticFollowupQuiescence(release));
        Assert.Equal(1, followupCount);

        bool Ready() => battle.NativeEntryReady && RecreateGame.DiagnosticCommandReady(battle, driver) && battle.ObserveDiagnosticDrainBlockers().Count == 0 && world.VerifyDiagnosticTarget() is not null;

        void Tick()
        {
            battle.Tick();
            audio.Update(battle);
        }
    }

    private static void IgnoreRelease(BattleDiagnosticInvocationRelease release)
    {
    }

}
