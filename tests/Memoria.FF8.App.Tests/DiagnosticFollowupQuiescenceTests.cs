using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticFollowupQuiescenceTests
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, Converters = { new JsonStringEnumConverter(), new DiagnosticBattleActorReferenceJsonConverter() } };

    [Fact]
    public void Older_actual_drain_receipt_without_optional_quiescence_fields_remains_valid()
    {
        BattleDiagnosticInvocationFollowupRelease release = DiagnosticInvocationReleaseTests.FollowupReceipt();
        JsonObject wire = Assert.IsType<JsonObject>(JsonSerializer.SerializeToNode(release, Json));
        wire.Remove("quiescent_textures");
        wire.Remove("completion_kind");
        wire.Remove("raw_drained");
        BattleDiagnosticInvocationFollowupRelease restored = wire.Deserialize<BattleDiagnosticInvocationFollowupRelease>(Json)!;
        DiagnosticInvocationReleaseValidation.ValidateFollowup(restored, release.FocalRetirement.Dispatch, release.FocalRetirement.LifecycleId);
        Assert.Null(restored.QuiescentTextures);
        Assert.True(restored.RawDrained);
        Assert.Equal("native-followup-drain", restored.CompletionKind);
    }

    [Fact]
    public void Resident_receipt_retains_real_nonempty_owners_and_survives_serialization()
    {
        BattleDiagnosticInvocationFollowupRelease release = Receipt();
        DiagnosticInvocationReleaseValidation.ValidateFollowup(release, release.FocalRetirement.Dispatch, release.FocalRetirement.LifecycleId);
        Assert.False(release.RawDrained);
        Assert.Equal("native-followup-resident-texture-no-op-aftercare", release.CompletionKind);
        Assert.True(release.FinalDrain.Owners.AuxiliaryHead);
        Assert.Empty(release.TextureReleases);
        BattleDiagnosticInvocationFollowupRelease restored = JsonSerializer.Deserialize<BattleDiagnosticInvocationFollowupRelease>(JsonSerializer.Serialize(release, Json), Json)!;
        DiagnosticInvocationReleaseValidation.ValidateFollowup(restored, release.FocalRetirement.Dispatch, release.FocalRetirement.LifecycleId);
        DiagnosticInvocationReleaseValidation.ValidateAftercarePresentationTail(restored, Tail(restored));
    }

    [Theory]
    [InlineData("pending-action")]
    [InlineData("pending-module")]
    [InlineData("pending-contact")]
    [InlineData("pending-reaction")]
    [InlineData("changed-generation")]
    [InlineData("changed-dispatch")]
    [InlineData("mismatch-packet")]
    [InlineData("missing-stage2")]
    [InlineData("other-stage")]
    [InlineData("foreign-callback")]
    [InlineData("unproved-texture")]
    [InlineData("changed-program")]
    [InlineData("changed-row")]
    [InlineData("missing-resident")]
    [InlineData("missing-quiescence")]
    [InlineData("fabricated-release")]
    [InlineData("default-releases")]
    [InlineData("null-resident")]
    [InlineData("missing-noop")]
    [InlineData("native-enemy-index")]
    [InlineData("non-self")]
    [InlineData("header-output")]
    [InlineData("model-control")]
    [InlineData("produced-command")]
    [InlineData("wrong-completion")]
    [InlineData("wrong-completion-token")]
    [InlineData("blocked-noop-input")]
    [InlineData("no-completed-witness")]
    [InlineData("different-owners")]
    public void Resident_receipt_rejects_unproved_lineage_or_other_native_owners(string fault)
    {
        BattleDiagnosticInvocationFollowupRelease release = Receipt();
        BattleDiagnosticInvocationFollowupQuiescence proof = release.QuiescentTextures!;
        BattleDiagnosticPlayerStage2NoOp noOp = proof.NoOp ?? throw new InvalidDataException("Missing synthetic native no-op witness.");
        BattleComponentTextureQuiescenceObservation observation = proof.Observation;
        BattleDiagnosticInvocationRelease focal = release.FocalRetirement;
        BattleDiagnosticInvocationFollowupRelease changed = fault switch
        {
            "pending-action" => release with { FinalDrain = release.FinalDrain with { Owners = release.FinalDrain.Owners with { ActionOrCommand = true } } },
            "pending-module" => release with { FinalDrain = release.FinalDrain with { Owners = release.FinalDrain.Owners with { Modules = 1 } } },
            "pending-contact" => release with { FinalDrain = release.FinalDrain with { Owners = release.FinalDrain.Owners with { PendingContacts = 1 } } },
            "pending-reaction" => release with { FinalDrain = release.FinalDrain with { Owners = release.FinalDrain.Owners with { TimedOrReaction = true } } },
            "changed-generation" => release with { QuiescentTextures = proof with { HeaderGeneration = proof.HeaderGeneration + 1 } },
            "changed-dispatch" => release with { QuiescentTextures = proof with { DispatchSerial = proof.DispatchSerial + 1 } },
            "mismatch-packet" => release with { FocalRetirement = focal with { CompletionPacketSerial = unchecked((byte)(focal.CompletionPacketSerial + 1)) } },
            "missing-stage2" => release with { Callbacks = [] },
            "other-stage" => release with { Callbacks = [release.Callbacks[0] with { Stage = 3 }] },
            "foreign-callback" => release with { Callbacks = [release.Callbacks[0] with { Actor = new BattlePartyActorReference("other") }] },
            "unproved-texture" => release with { QuiescentTextures = proof with { Observation = observation with { Textures = observation.Textures with { Tasks = [observation.Textures.Tasks[0] with { Deadline = 254 }] } } } },
            "changed-program" => release with { QuiescentTextures = proof with { ResidentTextures = [proof.ResidentTextures[0] with { ProgramSha256 = new string('b', 64) }] } },
            "changed-row" => release with { QuiescentTextures = proof with { Observation = observation with { Textures = observation.Textures with { Tasks = [observation.Textures.Tasks[0] with { ProgramLength = 2 }] } } } },
            "missing-resident" => release with { QuiescentTextures = proof with { ResidentTextures = [] } },
            "missing-quiescence" => release with { QuiescentTextures = null },
            "fabricated-release" => release with { TextureReleases = [new(proof.ResidentTextures[0], 4, focal.SimulationFrame, focal.GraphicsCallback!.Value, focal.Timestamp + 1, 3, 0, 8, true, true)] },
            "default-releases" => release with { TextureReleases = default },
            "null-resident" => release with { QuiescentTextures = proof with { ResidentTextures = [null!] } },
            "missing-noop" => release with { QuiescentTextures = proof with { NoOp = null } },
            "native-enemy-index" => release with { QuiescentTextures = proof with { NoOp = noOp with { Inputs = noOp.Inputs with { NativeActorIndex = 3 } } } },
            "non-self" => release with { QuiescentTextures = proof with { NoOp = noOp with { Inputs = noOp.Inputs with { Attacker = new("other") } } } },
            "header-output" => release with { QuiescentTextures = proof with { NoOp = noOp with { HeaderCount = 1 } } },
            "model-control" => release with { QuiescentTextures = proof with { NoOp = noOp with { OperationCount = 2 } } },
            "produced-command" => release with { QuiescentTextures = proof with { NoOp = noOp with { ProducedCommands = 1 } } },
            "wrong-completion" => release with { QuiescentTextures = proof with { NoOp = noOp with { CompletionCallback = 0x47DE70 } } },
            "wrong-completion-token" => release with { QuiescentTextures = proof with { NoOp = noOp with { CompletionToken = noOp.CompletionToken + 1 } } },
            "blocked-noop-input" => release with { QuiescentTextures = proof with { NoOp = noOp with { Inputs = noOp.Inputs with { SavedStatus = 1 } } } },
            "no-completed-witness" => release with { QuiescentTextures = proof with { NoOp = noOp with { CompletedTimestamp = null } } },
            _ => release with { QuiescentTextures = proof with { Observation = observation with { Owners = observation.Owners with { RetainedActiveHeader = 1 } } } }
        };
        Assert.Throws<InvalidDataException>(() => DiagnosticInvocationReleaseValidation.ValidateFollowup(changed, focal.Dispatch, focal.LifecycleId));
    }

    [Theory]
    [InlineData("too-few-presents")]
    [InlineData("duplicate-present")]
    [InlineData("nonincreasing-time")]
    [InlineData("changed-fingerprint")]
    [InlineData("unknown-row")]
    [InlineData("new-owner")]
    [InlineData("new-completion")]
    [InlineData("changed-epoch")]
    [InlineData("frame-gap")]
    [InlineData("zero-native-progress")]
    [InlineData("aftercare-first")]
    [InlineData("not-completed-end")]
    [InlineData("after-start-first")]
    [InlineData("bad-header")]
    [InlineData("first-raw-empty")]
    [InlineData("new-action")]
    public void Spanning_tail_requires_real_presentations_exact_noop_and_continuous_actual_owners(string fault)
    {
        BattleDiagnosticInvocationFollowupRelease release = Receipt();
        BattleDiagnosticAftercareTail tail = Tail(release);
        BattleDiagnosticAftercarePresent first = tail.First;
        BattleDiagnosticAftercarePresent last = tail.Last;
        tail = fault switch
        {
            "too-few-presents" => tail with { PresentedFrames = 1 },
            "duplicate-present" => tail with { Last = last with { PresentedFrame = first.PresentedFrame } },
            "nonincreasing-time" => tail with { Last = last with { Timestamp = first.Timestamp } },
            "changed-fingerprint" => tail with { Last = last with { Boundary = last.Boundary with { ResidentFingerprint = new string('b', 64) } } },
            "unknown-row" => tail with { First = first with { Boundary = first.Boundary with { Textures = first.Boundary.Textures with { Tasks = [first.Boundary.Textures.Tasks[0] with { UnknownByteMask = 0x200 }] } } } },
            "new-owner" => tail with { Last = last with { Boundary = last.Boundary with { Owners = last.Boundary.Owners with { Effects = 1 } } } },
            "new-completion" => tail with { Last = last with { Boundary = last.Boundary with { CompletionIssued = last.Boundary.CompletionIssued + 1 } } },
            "changed-epoch" => tail with { Last = last with { Boundary = last.Boundary with { DispatchSerial = last.Boundary.DispatchSerial + 1 } } },
            "frame-gap" => tail with { Last = last with { Boundary = last.Boundary with { ObservedFrames = last.Boundary.ObservedFrames + 1 } } },
            "zero-native-progress" => tail with { Last = last with { Boundary = last.Boundary with { GraphicsCallback = first.Boundary.GraphicsCallback, ObservedFrames = first.Boundary.ObservedFrames } } },
            "aftercare-first" => tail with { First = first with { Boundary = first.Boundary with { CallbackCompleted = true } } },
            "not-completed-end" => tail with { Last = last with { Boundary = last.Boundary with { CallbackCompleted = false } } },
            "after-start-first" => tail with { First = first with { Timestamp = release.QuiescentTextures!.NoOp!.Timestamp + 1 } },
            "bad-header" => tail with { Last = last with { Boundary = last.Boundary with { HeaderGeneration = last.Boundary.HeaderGeneration + 1 } } },
            "first-raw-empty" => tail with { First = first with { Boundary = first.Boundary with { Owners = first.Boundary.Owners with { ActionOrCommand = false } } } },
            _ => tail with { Last = last with { Boundary = last.Boundary with { ActionIssued = last.Boundary.ActionIssued + 1 } } }
        };
        Assert.Throws<InvalidDataException>(() => DiagnosticInvocationReleaseValidation.ValidateAftercarePresentationTail(release, tail));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Driver_accepts_only_current_authenticated_residency_without_claiming_raw_drain(bool current)
    {
        BattleDiagnosticInvocationFollowupRelease release = Receipt();
        BattleDiagnosticEffectDispatch dispatch = release.FocalRetirement.Dispatch;
        BattleAbility ability = dispatch.Ability ?? throw new InvalidDataException("Synthetic dispatch lacks its ability.");
        int action = dispatch.ActionId ?? throw new InvalidDataException("Synthetic dispatch lacks its action.");
        DiagnosticBattleDriverRecipe recipe = new(new(BattleInputKind.Action, ActorId: dispatch.ActorId, TargetId: dispatch.ActorId, Ability: ability, ActionId: action), dispatch.RequestedEffect)
        {
            BaselineSeconds = 0,
            TailFrames = 2,
            FreshEntry = new(DiagnosticFreshEntryDeclaration.SingleUse, [new("Data/battle.fs", new string('a', 64))])
        };
        DiagnosticBattleDriver driver = new(recipe, 0);
        driver.Advance(new(0, 0, 0, true, true, false, []));
        driver.Advance(new(0, 1, 1, true, true, false, []));
        driver.ObserveDispatch(dispatch);
        driver.ObserveInvocationFollowupRelease(release);
        BattleDiagnosticAftercareTail tail = Tail(release);
        driver.Advance(new(1, release.FinalDrain.Callback, tail.Last.PresentedFrame, true, false, false, ["effect-or-auxiliary-task"], AftercareTail: current ? tail : null));
        Assert.Equal(current ? DiagnosticDriverStage.Completed : DiagnosticDriverStage.Drain, driver.Stage);
        Assert.Equal(["effect-or-auxiliary-task"], driver.LastDrainBlockers);
        Assert.False(release.RawDrained);
    }

    private static BattleDiagnosticInvocationFollowupRelease Receipt()
    {
        BattleDiagnosticInvocationFollowupRelease basis = DiagnosticInvocationReleaseTests.FollowupReceipt();
        BattleDiagnosticEffectDispatch dispatch = basis.FocalRetirement.Dispatch;
        BattlePartyActorReference actor = new(dispatch.ActorId!);
        BattleDiagnosticInvocationPendingTexture pending = new(0, actor, 0, 7, 0, 1, new string('a', 64), 255, 3, 0, 7);
        BattleDiagnosticInvocationRelease retirement = basis.FocalRetirement with
        {
            Owners = basis.FocalRetirement.Owners with { AuxiliaryHead = true },
            Blockers = ["effect-or-auxiliary-task", "action-or-reaction"],
            PendingTextures = [pending],
            PendingFollowups = [new(actor, 2, basis.FocalRetirement.PendingFollowups[0].QueuedCallback)]
        };
        BattleDiagnosticInvocationFinalDrain boundary = basis.FinalDrain with { Owners = basis.FinalDrain.Owners with { AuxiliaryHead = true } };
        BattleDiagnosticTextureTask row = new(0, 1, 0, null, NativeBattleAuxiliaryKind.TextureAnimation5057D0, 0, 32, NativeBattleGraphicsModelAddress.EncodeOriginalCell(NativeBattleModelSlot.Decode(0)), 7, 0, 1, 255, 0, 3, 0, 7) { ModelActorId = actor.ActorId };
        BattleComponentTextureQuiescenceObservation observation = new(dispatch.RequestedEffect, boundary.GraphicsCallback, 0, 0, boundary.Owners, new(16, 1, 0, 0, true, [row]), new string('a', 64), ["effect-or-auxiliary-task"]);
        BattleDiagnosticInvocationFollowupCallback callback = basis.Callbacks[0] with { Actor = actor };
        BattleDiagnosticPlayerStage2SelfInputs inputs = new(actor, 0, actor, 0, 0, 0, 0, 0, true);
        BattleDiagnosticPlayerStage2NoOp noOp = new(inputs, callback.StartedCallback, retirement.Timestamp + 10, retirement.CompletionToken + 1, 0x47DDA0, 0, 1, 0) { CompletedCallback = callback.CompletedCallback, CompletedTimestamp = retirement.Timestamp + 20 };
        return new(retirement, boundary, [callback]) { QuiescentTextures = new(observation, [pending], dispatch.Serial, dispatch.NativeHeaderGeneration!.Value) { NoOp = noOp } };
    }

    private static BattleDiagnosticAftercareTail Tail(BattleDiagnosticInvocationFollowupRelease release)
    {
        BattleDiagnosticInvocationRelease focal = release.FocalRetirement;
        BattleDiagnosticInvocationFollowupQuiescence proof = release.QuiescentTextures!;
        BattleDiagnosticPlayerStage2NoOp noOp = proof.NoOp ?? throw new InvalidDataException("Missing synthetic native no-op witness.");
        BattleDiagnosticAftercareBoundary first = new(focal.LifecycleId, focal.Callback, focal.SimulationFrame, focal.GraphicsCallback!.Value, focal.Timestamp + 2, focal.Dispatch.Serial, proof.HeaderGeneration, 1, focal.CompletionToken, focal.CompletionToken, 1, 0, 0, noOp.Inputs, false, focal.Owners, proof.Observation.Textures, proof.Observation.ResidentFingerprint);
        BattleDiagnosticAftercareBoundary last = first with { Callback = release.FinalDrain.Callback, SimulationFrame = release.FinalDrain.SimulationFrame, GraphicsCallback = release.FinalDrain.GraphicsCallback, Timestamp = release.FinalDrain.Timestamp + 1, CompletionIssued = noOp.CompletionToken, CompletionAcknowledged = noOp.CompletionToken, ObservedFrames = checked((int)(1 + release.FinalDrain.GraphicsCallback - first.GraphicsCallback)), CallbackCompleted = true, Owners = release.FinalDrain.Owners };
        return new(BattleDiagnosticAftercareTail.Phase, 2, new(10, focal.Timestamp + 5, first), new(11, release.FinalDrain.Timestamp + 2, last));
    }
}
