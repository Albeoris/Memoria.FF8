using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticInvocationReleaseTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("missing-witness")]
    [InlineData("active-outcome")]
    [InlineData("living-enemy")]
    [InlineData("wrong-frame")]
    [InlineData("exceptional-outcome")]
    [InlineData("reused-armed-hold")]
    public void Prospective_main_texture_proof_requires_pristine_hold_and_actual_same_frame_native_victory(string fault)
    {
        BattleDiagnosticInvocationRelease release = ProspectiveTerminalTextureReceipt();
        BattleDiagnosticTerminalTextureRelease proof = release.TerminalTextures!;
        BattleDiagnosticNaturalVictory witness = proof.NaturalVictory!;
        release = fault switch
        {
            "missing-witness" => release with { TerminalTextures = proof with { NaturalVictory = null } },
            "active-outcome" => release with { TerminalTextures = proof with { NaturalVictory = witness with { Candidate = BattleOutcome.Active } } },
            "living-enemy" => release with { TerminalTextures = proof with { NaturalVictory = witness with { Combatants = witness.Combatants.SetItem(1, witness.Combatants[1] with { SavedStatus = 0, CurrentHp = 100 }) } } },
            "wrong-frame" => release with { TerminalTextures = proof with { NaturalVictory = witness with { Callback = witness.Callback + 1 } } },
            "exceptional-outcome" => release with { TerminalTextures = proof with { NaturalVictory = witness with { ExceptionalOutcome = true } } },
            "reused-armed-hold" => release with { TerminalTextures = proof with { Hold = proof.Hold with { Sequence = 1 } } },
            _ => release
        };
        if (fault == "valid")
        {
            DiagnosticInvocationReleaseValidation.Validate(release, release.Dispatch, release.LifecycleId);
        }
        else
        {
            Assert.Throws<InvalidDataException>(() => DiagnosticInvocationReleaseValidation.Validate(release, release.Dispatch, release.LifecycleId));
        }
    }

    internal static BattleDiagnosticInvocationRelease ProspectiveTerminalTextureReceipt()
    {
        BattleDiagnosticInvocationRelease main = TerminalTextureReceipt();
        BattleDiagnosticTerminalTextureRelease proof = main.TerminalTextures!;
        BattleDiagnosticTerminalHoldState armed = proof.Hold with { Phase = BattleDiagnosticTerminalHoldPhase.Armed, Sequence = 0, HeldCallback = null, HeldFrame = null, Receipts = [] };
        BattleDiagnosticNaturalVictory witness = new(main.LifecycleId, main.Callback, main.SimulationFrame, BattleOutcome.Victory, true, false, false, [new(main.Dispatch.ActorId!, 0, true, 1, 0, 0, 100), new("enemy-0", 3, false, 1, 1, 65536, 0)]);
        return main with { TerminalTextures = proof with { Hold = armed, NaturalVictory = witness } };
    }

    [Fact]
    public void Standalone_terminal_texture_release_keeps_the_exact_main_packet_and_victory_proofs()
    {
        BattleDiagnosticInvocationRelease release = TerminalTextureReceipt();
        DiagnosticInvocationReleaseValidation.Validate(release, release.Dispatch, release.LifecycleId);
        Assert.True(release.Released);
        Assert.Null(release.Aftercare);
        Assert.NotNull(release.ExecutingCompletion);
        Assert.Single(release.TerminalTextures!.Textures.Tasks);
    }

    [Theory]
    [InlineData("foreign-texture")]
    [InlineData("nonterminal-byte")]
    [InlineData("missing-hold")]
    [InlineData("wrong-candidate")]
    [InlineData("rearmed")]
    [InlineData("held-lifecycle")]
    [InlineData("command")]
    [InlineData("unbound-bank")]
    [InlineData("missing-certificate")]
    [InlineData("ordinary-boundary")]
    public void Standalone_texture_release_rejects_foreign_or_unproved_owners(string fault)
    {
        BattleDiagnosticInvocationRelease original = TerminalTextureReceipt();
        BattleDiagnosticTerminalTextureRelease proof = original.TerminalTextures!;
        BattleDiagnosticTextureQuiescence textures = proof.Textures;
        BattleDiagnosticInvocationRelease invalid = fault switch
        {
            "foreign-texture" => original with { TerminalTextures = proof with { Textures = textures with { Tasks = textures.Tasks.SetItem(0, textures.Tasks[0] with { ModelIndex = 3, ModelPointer = 0x1D972C0u + 156u * 3 }) } } },
            "nonterminal-byte" => original with { TerminalTextures = proof with { Textures = textures with { Tasks = textures.Tasks.SetItem(0, textures.Tasks[0] with { Deadline = 254 }) } } },
            "missing-hold" => original with { TerminalTextures = proof with { Hold = null! } },
            "wrong-candidate" => original with { TerminalTextures = proof with { Hold = proof.Hold with { Declaration = proof.Hold.Declaration with { Candidate = BattleOutcome.Defeat } } } },
            "rearmed" => original with { TerminalTextures = proof with { Hold = proof.Hold with { Rearms = 1 } } },
            "held-lifecycle" => original with { TerminalTextures = proof with { Hold = proof.Hold with { Receipts = [proof.Hold.Receipts[0] with { LifecycleId = Guid.NewGuid() }] } } },
            "command" => original with { Owners = original.Owners with { ActionOrCommand = true } },
            "unbound-bank" => original with { TerminalTextures = proof with { CommandBanks = proof.CommandBanks.SetItem(0, proof.CommandBanks[0] with { Links = proof.CommandBanks[0].Links.SetItem(0, new(0, 0, 1, null)) }) } },
            "missing-certificate" => original with { ExecutingCompletion = null },
            "ordinary-boundary" => original with { Boundary = BattleDiagnosticInvocationRelease.GraphicsFrameBoundary },
            _ => throw new ArgumentOutOfRangeException(nameof(fault))
        };
        Assert.Throws<InvalidDataException>(() => DiagnosticInvocationReleaseValidation.Validate(invalid, invalid.Dispatch, invalid.LifecycleId));
    }

    internal static BattleDiagnosticInvocationRelease TerminalTextureReceipt()
    {
        BattleDiagnosticInvocationRelease basis = PostFrameReceipt();
        BattleDiagnosticEffectDispatch focal = basis.Dispatch with { Ability = BattleAbility.LimitBreak, NativeCommand = 16, ActionId = 53, RequestedEffect = 288, ExecutedEffect = 288 };
        NativeBattleActionHeaderSlot header = basis.Header with { Payload = basis.Header.Payload.SetItem(1, 16).SetItem(4, 53).SetItem(6, 32).SetItem(7, 1) };
        BattleDiagnosticInvocationRelease certificate = basis.ExecutingCompletion! with { Dispatch = focal, Header = header, Owners = basis.ExecutingCompletion.Owners with { AuxiliaryHead = true }, Blockers = ["effect-or-auxiliary-task"] };
        BattleDiagnosticCombatant caster = new(focal.ActorId!, 100, 100, 0, 0, []) { NativeIndex = focal.NativeActorIndex, ModelId = 5 };
        BattleDiagnosticTerminalHoldDeclaration declaration = new("Single-use native victory texture cleanup", [focal.ActorId!], 100) { Candidate = BattleOutcome.Victory };
        BattleDiagnosticTerminalHoldReceipt admission = new(Guid.NewGuid(), basis.LifecycleId, "held", 1, basis.SimulationFrame, basis.Callback, BattleOutcome.Victory, null, false, 0, 0, new(NativeBattleRandomState.Initialize(0), null, null, null), [caster]);
        BattleDiagnosticTerminalHoldState hold = new(declaration, admission.HoldId, BattleDiagnosticTerminalHoldPhase.Held, 1, 0, admission.Frame, admission.Callback, [admission]);
        BattleDiagnosticTextureTask task = new(0, 1, 0, null, NativeBattleAuxiliaryKind.TextureAnimation5057D0, 0, 32, 0x1D972C0, 21, 0, 1, 255, 0, 3, 10, 21);
        BattleDiagnosticTextureQuiescence textures = new(16, 1, 0, 0, true, [task]);
        ImmutableArray<BattleDiagnosticCommandBankClosure> banks = Enumerable.Range(0, 3).Select(bank => new BattleDiagnosticCommandBankClosure((byte)bank, 255, Enumerable.Range(0, 11).Select(_ => new NativeBattleCommandLink(0, 0, 0, null)).ToImmutableArray())).ToImmutableArray();
        return basis with { Dispatch = focal, Header = header, Boundary = BattleDiagnosticInvocationRelease.TerminalTextureBoundary, ExecutingCompletion = certificate, Owners = basis.Owners with { AuxiliaryHead = true }, Blockers = ["effect-or-auxiliary-task"], TerminalTextures = new(textures, hold, banks) };
    }

    internal static BattleDiagnosticInvocationRelease Receipt(long callback = 3, long timestamp = 300)
    {
        BattleDiagnosticEffectDispatch dispatch = new(1, 1, "party-0", BattleAbility.Magic, 1, 2, 2, 2) { NativeActorIndex = 0, NativeHeaderIndex = 0, NativeHeaderGeneration = 1 };
        byte[] header = new byte[20];
        header[1] = 2;
        header[4] = 1;
        header[6] = 2;
        byte[] packet = new byte[12];
        packet[0] = 7;
        packet[1] = 128;
        packet[2] = 10;
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4), 0x47DDA0);
        return new(Guid.NewGuid(), callback, 2, timestamp, dispatch, new(header.ToImmutableArray(), 0, 0, null), new(packet.ToImmutableArray()), 0x47DDA0, 0, 0, 7, 0x47DDA0, true, true, true, true, new(1, 0, 0, 0, false, false, 0, false, 0, 0, false, 0, false, false, false, false) { ObservedOriginalEffect = true, SelectedReturnedDescriptor = 0, OriginalPools = [] }, []);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("lifecycle")]
    [InlineData("pool")]
    [InlineData("post-action")]
    [InlineData("ack")]
    [InlineData("generation")]
    [InlineData("serial")]
    [InlineData("packet")]
    [InlineData("packet-bytes")]
    [InlineData("packet-unknown")]
    [InlineData("packet-already-free")]
    [InlineData("header-bytes")]
    [InlineData("task")]
    [InlineData("contact")]
    [InlineData("pointer")]
    [InlineData("detached")]
    [InlineData("resource")]
    [InlineData("unknown-files")]
    [InlineData("action")]
    [InlineData("early")]
    public void Native_release_requires_exact_completion_and_conservative_empty_owners(string fault)
    {
        BattleDiagnosticInvocationRelease baseline = Receipt();
        DiagnosticInvocationReleaseValidation.Validate(baseline, baseline.Dispatch, baseline.LifecycleId);
        var invalid = fault switch
        {
            "owner" => baseline with { EventActorIndex = 1 },
            "lifecycle" => baseline with { LifecycleId = Guid.NewGuid() },
            "pool" => baseline with { Owners = baseline.Owners with { OriginalPools = [new(1, 2, 12, 1, true, true)] } },
            "post-action" => baseline with { NativeEventCompleted = false },
            "ack" => baseline with { PresentationAcknowledged = false },
            "generation" => baseline with { HeaderCapturedBeforeReset = false },
            "serial" => baseline with { Dispatch = baseline.Dispatch with { Serial = 2 } },
            "packet" => baseline with { CompletionPacketSerial = 8 },
            "packet-unknown" => baseline with { CompletionPacket = baseline.CompletionPacket! with { UnknownByteMask = 1 } },
            "packet-already-free" => baseline with { CompletionPacket = baseline.CompletionPacket! with { Bytes = baseline.CompletionPacket!.Bytes.SetItem(1, 0) } },
            "packet-bytes" => baseline with { CompletionPacket = baseline.CompletionPacket! with { Bytes = baseline.CompletionPacket!.Bytes.SetItem(2, 104) } },
            "header-bytes" => baseline with { Header = baseline.Header with { Payload = baseline.Header.Payload.SetItem(4, 3) } },
            "task" => baseline with { Owners = baseline.Owners with { NonPumpDescriptorTasks = 1 } },
            "contact" => baseline with { Owners = baseline.Owners with { PendingContacts = 1 } },
            "pointer" => baseline with { Owners = baseline.Owners with { ReturnedTaskPointer = 1 } },
            "detached" => baseline with { Owners = baseline.Owners with { DetachedReturnedTask = true } },
            "resource" => baseline with { Owners = baseline.Owners with { ReplacementResource = true } },
            "unknown-files" => baseline with { Owners = baseline.Owners with { Files = null } },
            "action" => baseline with { Owners = baseline.Owners with { ActionOrCommand = true } },
            _ => baseline with { Callback = 1 }
        };
        Assert.Throws<InvalidDataException>(() => DiagnosticInvocationReleaseValidation.Validate(invalid, baseline.Dispatch, baseline.LifecycleId));
    }

    private static BattleDiagnosticInvocationRelease PostFrameReceipt()
    {
        BattleDiagnosticInvocationRelease baseline = Receipt();
        BattleDiagnosticInvocationRelease certificate = baseline with
        {
            GraphicsCallback = 7,
            Owners = baseline.Owners with { AuxiliaryHead = true, ActionOrCommand = true },
            Blockers = ["effect-or-auxiliary-task", "action-or-reaction"]
        };
        return certificate with
        {
            Boundary = BattleDiagnosticInvocationRelease.GraphicsFrameBoundary,
            PacketRemoved = true,
            GraphicsCallback = 8,
            RemovedCompletionPacket = certificate.CompletionPacket! with { Bytes = certificate.CompletionPacket!.Bytes.SetItem(1, 0) },
            ExecutingCompletion = certificate,
            Timestamp = certificate.Timestamp + 1,
            Owners = baseline.Owners with { DescriptorPackets = 0 },
            Blockers = []
        };
    }

    [Fact]
    public void Post_frame_release_requires_actual_native_byte1_retirement_and_same_callback_certificate()
    {
        BattleDiagnosticInvocationRelease post = PostFrameReceipt();
        BattleDiagnosticInvocationRelease certificate = post.ExecutingCompletion!;
        Assert.Throws<InvalidDataException>(() => DiagnosticInvocationReleaseValidation.Validate(certificate, post.Dispatch, post.LifecycleId));
        DiagnosticInvocationReleaseValidation.Validate(post, post.Dispatch, post.LifecycleId);
        Assert.True(certificate.HeaderCapturedBeforeReset);
        Assert.True(certificate.NativeEventCompleted);
        Assert.True(certificate.PresentationAcknowledged);
        Assert.Equal(certificate.Callback, post.Callback);
        Assert.Equal(certificate.SimulationFrame, post.SimulationFrame);
        Assert.Equal(certificate.LifecycleId, post.LifecycleId);
        Assert.Equal(certificate.GraphicsCallback + 1, post.GraphicsCallback);
        Assert.Equal(128, certificate.CompletionPacket!.Bytes[1]);
        Assert.Equal(0, post.RemovedCompletionPacket!.Bytes[1]);
        Assert.Equal(certificate.CompletionPacket!.Bytes.SetItem(1, 0), post.RemovedCompletionPacket!.Bytes);
        Assert.Equal(1, certificate.Owners.DescriptorPackets);
        Assert.Equal(0, post.Owners.DescriptorPackets);
        Assert.True(certificate.Owners.AuxiliaryHead);
        Assert.True(certificate.Owners.ActionOrCommand);
        Assert.False(post.Owners.AuxiliaryHead);
        Assert.False(post.Owners.ActionOrCommand);
    }

    [Theory]
    [InlineData("unknown-mode")]
    [InlineData("missing-certificate")]
    [InlineData("not-removed")]
    [InlineData("missing-retired-packet")]
    [InlineData("retired-byte1-unchanged")]
    [InlineData("retired-byte1-nonzero")]
    [InlineData("retired-serial-reused")]
    [InlineData("retired-command-reused")]
    [InlineData("retired-argument-reused")]
    [InlineData("retired-unread-byte-reused")]
    [InlineData("retired-knownness-changed")]
    [InlineData("retired-empty")]
    [InlineData("next-host-callback")]
    [InlineData("next-simulation-frame")]
    [InlineData("foreign-lifecycle")]
    [InlineData("graphics-not-advanced")]
    [InlineData("graphics-retried-later")]
    [InlineData("graphics-unknown")]
    [InlineData("timestamp-before-certificate")]
    [InlineData("certificate-not-completed")]
    [InlineData("certificate-not-acknowledged")]
    [InlineData("certificate-not-captured")]
    [InlineData("certificate-no-removal-guarantee")]
    [InlineData("certificate-wrong-mode")]
    [InlineData("certificate-already-removed")]
    [InlineData("certificate-nested")]
    [InlineData("certificate-retired-packet")]
    [InlineData("certificate-no-blocker")]
    [InlineData("certificate-no-outstanding-owner")]
    [InlineData("certificate-wrong-count")]
    [InlineData("certificate-graphics-unknown")]
    [InlineData("certificate-graphics-negative")]
    [InlineData("certificate-stale-generation")]
    [InlineData("certificate-foreign-dispatch")]
    [InlineData("certificate-wrong-slot")]
    [InlineData("certificate-wrong-serial")]
    [InlineData("certificate-header-changed")]
    [InlineData("certificate-contact-index-changed")]
    [InlineData("certificate-header-data-changed")]
    [InlineData("certificate-packet-unread-byte-changed")]
    [InlineData("certificate-packet-knownness-changed")]
    [InlineData("certificate-packet-already-free")]
    [InlineData("post-descriptor-still-present")]
    [InlineData("post-descriptor-task")]
    [InlineData("post-auxiliary")]
    [InlineData("post-action")]
    [InlineData("post-contact")]
    [InlineData("post-module")]
    [InlineData("post-unknown-file-count")]
    [InlineData("post-live-pool")]
    public void Post_frame_release_rejects_mixed_modes_reuse_changed_certificate_or_live_owners(string fault)
    {
        BattleDiagnosticInvocationRelease baseline = PostFrameReceipt();
        BattleDiagnosticInvocationRelease certificate = baseline.ExecutingCompletion!;
        DiagnosticInvocationReleaseValidation.Validate(baseline, baseline.Dispatch, baseline.LifecycleId);
        BattleDiagnosticInvocationRelease invalid = fault switch
        {
            "unknown-mode" => baseline with { Boundary = "later-global-drain" },
            "missing-certificate" => baseline with { ExecutingCompletion = null },
            "not-removed" => baseline with { PacketRemoved = false },
            "missing-retired-packet" => baseline with { RemovedCompletionPacket = null },
            "retired-byte1-unchanged" => baseline with { RemovedCompletionPacket = certificate.CompletionPacket },
            "retired-byte1-nonzero" => baseline with { RemovedCompletionPacket = baseline.RemovedCompletionPacket! with { Bytes = baseline.RemovedCompletionPacket!.Bytes.SetItem(1, 1) } },
            "retired-serial-reused" => baseline with { RemovedCompletionPacket = baseline.RemovedCompletionPacket! with { Bytes = baseline.RemovedCompletionPacket!.Bytes.SetItem(0, 8) } },
            "retired-command-reused" => baseline with { RemovedCompletionPacket = baseline.RemovedCompletionPacket! with { Bytes = baseline.RemovedCompletionPacket!.Bytes.SetItem(2, 104) } },
            "retired-argument-reused" => baseline with { RemovedCompletionPacket = baseline.RemovedCompletionPacket! with { Bytes = baseline.RemovedCompletionPacket!.Bytes.SetItem(4, 0) } },
            "retired-unread-byte-reused" => baseline with { RemovedCompletionPacket = baseline.RemovedCompletionPacket! with { Bytes = baseline.RemovedCompletionPacket!.Bytes.SetItem(11, 1) } },
            "retired-knownness-changed" => baseline with { RemovedCompletionPacket = baseline.RemovedCompletionPacket! with { UnknownByteMask = 0x100 } },
            "retired-empty" => baseline with { RemovedCompletionPacket = baseline.RemovedCompletionPacket! with { Bytes = [] } },
            "next-host-callback" => baseline with { Callback = baseline.Callback + 1 },
            "next-simulation-frame" => baseline with { SimulationFrame = baseline.SimulationFrame + 1 },
            "foreign-lifecycle" => baseline with { LifecycleId = Guid.NewGuid() },
            "graphics-not-advanced" => baseline with { GraphicsCallback = certificate.GraphicsCallback },
            "graphics-retried-later" => baseline with { GraphicsCallback = certificate.GraphicsCallback + 2 },
            "graphics-unknown" => baseline with { GraphicsCallback = null },
            "timestamp-before-certificate" => baseline with { Timestamp = certificate.Timestamp - 1 },
            "certificate-not-completed" => baseline with { ExecutingCompletion = certificate with { NativeEventCompleted = false } },
            "certificate-not-acknowledged" => baseline with { ExecutingCompletion = certificate with { PresentationAcknowledged = false } },
            "certificate-not-captured" => baseline with { ExecutingCompletion = certificate with { HeaderCapturedBeforeReset = false } },
            "certificate-no-removal-guarantee" => baseline with { ExecutingCompletion = certificate with { CurrentCompletionPacketGuaranteedRemoval = false } },
            "certificate-wrong-mode" => baseline with { ExecutingCompletion = certificate with { Boundary = BattleDiagnosticInvocationRelease.GraphicsFrameBoundary } },
            "certificate-already-removed" => baseline with { ExecutingCompletion = certificate with { PacketRemoved = true } },
            "certificate-nested" => baseline with { ExecutingCompletion = certificate with { ExecutingCompletion = certificate } },
            "certificate-retired-packet" => baseline with { ExecutingCompletion = certificate with { RemovedCompletionPacket = baseline.RemovedCompletionPacket } },
            "certificate-no-blocker" => baseline with { ExecutingCompletion = certificate with { Blockers = [] } },
            "certificate-no-outstanding-owner" => baseline with { ExecutingCompletion = certificate with { Owners = Receipt().Owners } },
            "certificate-wrong-count" => baseline with { ExecutingCompletion = certificate with { Owners = certificate.Owners with { DescriptorPackets = 0 } } },
            "certificate-graphics-unknown" => baseline with { ExecutingCompletion = certificate with { GraphicsCallback = null } },
            "certificate-graphics-negative" => baseline with { ExecutingCompletion = certificate with { GraphicsCallback = -1 } },
            "certificate-stale-generation" => baseline with { ExecutingCompletion = certificate with { Dispatch = certificate.Dispatch with { NativeHeaderGeneration = 2 } } },
            "certificate-foreign-dispatch" => baseline with { ExecutingCompletion = certificate with { Dispatch = certificate.Dispatch with { Serial = 2 } } },
            "certificate-wrong-slot" => baseline with { ExecutingCompletion = certificate with { CompletionPacketSlot = 1 } },
            "certificate-wrong-serial" => baseline with { ExecutingCompletion = certificate with { CompletionPacketSerial = 8 } },
            "certificate-header-changed" => baseline with { ExecutingCompletion = certificate with { Header = certificate.Header with { Payload = certificate.Header.Payload.SetItem(2, 11) } } },
            "certificate-contact-index-changed" => baseline with { ExecutingCompletion = certificate with { Header = certificate.Header with { FirstContactIndex = 1 } } },
            "certificate-header-data-changed" => baseline with { ExecutingCompletion = certificate with { Header = certificate.Header with { Data = new(1, 2, "changed") } } },
            "certificate-packet-unread-byte-changed" => baseline with { ExecutingCompletion = certificate with { CompletionPacket = certificate.CompletionPacket! with { Bytes = certificate.CompletionPacket!.Bytes.SetItem(11, 1) } } },
            "certificate-packet-knownness-changed" => baseline with { ExecutingCompletion = certificate with { CompletionPacket = certificate.CompletionPacket! with { UnknownByteMask = 0x100 } } },
            "certificate-packet-already-free" => baseline with { ExecutingCompletion = certificate with { CompletionPacket = baseline.RemovedCompletionPacket } },
            "post-descriptor-still-present" => baseline with { Owners = baseline.Owners with { DescriptorPackets = 1 } },
            "post-descriptor-task" => baseline with { Owners = baseline.Owners with { NonPumpDescriptorTasks = 1 } },
            "post-auxiliary" => baseline with { Owners = baseline.Owners with { AuxiliaryHead = true } },
            "post-action" => baseline with { Owners = baseline.Owners with { ActionOrCommand = true } },
            "post-contact" => baseline with { Owners = baseline.Owners with { PendingContacts = 1 } },
            "post-module" => baseline with { Owners = baseline.Owners with { Modules = 1, ModuleHead = true } },
            "post-unknown-file-count" => baseline with { Owners = baseline.Owners with { Files = null } },
            "post-live-pool" => baseline with { Owners = baseline.Owners with { OriginalPools = [new(1, 2, 12, 1, true, true)] } },
            _ => throw new ArgumentOutOfRangeException(nameof(fault))
        };
        Assert.Throws<InvalidDataException>(() => DiagnosticInvocationReleaseValidation.Validate(invalid, baseline.Dispatch, baseline.LifecycleId));
    }

    [Theory]
    [InlineData("removed")]
    [InlineData("removed-packet")]
    [InlineData("certificate")]
    [InlineData("post-count")]
    [InlineData("unknown-mode")]
    public void Immediate_release_keeps_its_existing_contract_and_rejects_post_frame_fields(string fault)
    {
        BattleDiagnosticInvocationRelease baseline = Receipt();
        DiagnosticInvocationReleaseValidation.Validate(baseline, baseline.Dispatch, baseline.LifecycleId);
        BattleDiagnosticInvocationRelease invalid = fault switch
        {
            "removed" => baseline with { PacketRemoved = true },
            "removed-packet" => baseline with { RemovedCompletionPacket = baseline.CompletionPacket },
            "certificate" => baseline with { ExecutingCompletion = PostFrameReceipt().ExecutingCompletion },
            "post-count" => baseline with { Owners = baseline.Owners with { DescriptorPackets = 0 } },
            _ => baseline with { Boundary = "completed-later" }
        };
        Assert.Throws<InvalidDataException>(() => DiagnosticInvocationReleaseValidation.Validate(invalid, baseline.Dispatch, baseline.LifecycleId));
    }

    [Fact]
    public void Fresh_driver_requires_release_and_retains_actual_global_blockers_during_outside_gameplay()
    {
        var recipe = new DiagnosticBattleDriverRecipe(new(BattleInputKind.Action, ActorId: "party-0", TargetId: "party-0", Ability: BattleAbility.Magic, ActionId: 1), 2) { BaselineSeconds = 0, FreshEntry = new(DiagnosticFreshEntryDeclaration.SingleUse, [new("Data/battle.fs", new string('a', 64))]) };
        DiagnosticBattleDriver driver = new(recipe, 0);
        DiagnosticDriverObservation Observation(double seconds, long frame, bool drained) => new(seconds, frame, frame, true, drained, false, drained ? [] : ["action-or-command"]);
        driver.Advance(Observation(0, 0, true));
        Assert.True(driver.Advance(Observation(.1, 1, true)));
        BattleDiagnosticInvocationRelease receipt = Receipt();
        driver.ObserveDispatch(receipt.Dispatch);
        driver.Advance(Observation(.2, 2, true));
        Assert.Equal(DiagnosticDriverStage.Drain, driver.Stage);
        driver.ObserveInvocationRelease(receipt);
        driver.Advance(Observation(.3, 3, false));
        Assert.Equal(DiagnosticDriverStage.Tail, driver.Stage);
        Assert.Equal(new[] { "action-or-command" }, driver.LastDrainBlockers);
        driver.ObserveDispatch(new(2, 4, "party-0", BattleAbility.Attack, 0, 9, 9));
        driver.Advance(Observation(.4, 4, false));
        driver.Advance(Observation(.5, 5, false));
        Assert.Equal(DiagnosticDriverStage.Completed, driver.Stage);
        Assert.Single(driver.Dispatches);
    }
    internal static (BattleDiagnosticInvocationRelease Main, BattleDiagnosticInvocationAftercareReceipt Aftercare) AftercareFixture(BattleDiagnosticInvocationRelease? original = null)
    {
        BattleDiagnosticInvocationRelease initial = original ?? Receipt();
        BattleDiagnosticEffectDispatch focal = initial.Dispatch with { NativeTargetMask = 1 };
        BattleDiagnosticTextureTask texture = new(0, 1, 0, null, NativeBattleAuxiliaryKind.TextureAnimation5057D0, 0xF0, 32, 0x1D972C0, 11, 0, 1, 255, 0, 3, 10, 11);
        BattleDiagnosticTextureQuiescence textures = new(16, 1, 0, 0, true, [texture]);
        BattleDiagnosticInvocationRelease certificate = initial with { Dispatch = focal, GraphicsCallback = 7, Owners = initial.Owners with { AuxiliaryHead = true, ActionOrCommand = true }, Blockers = ["effect-or-auxiliary-task", "action-or-reaction"] };
        BattleDiagnosticInvocationRelease main = certificate with { Boundary = BattleDiagnosticInvocationRelease.AftercareBoundary, PacketRemoved = true, GraphicsCallback = 8, RemovedCompletionPacket = certificate.CompletionPacket! with { Bytes = certificate.CompletionPacket!.Bytes.SetItem(1, 0) }, ExecutingCompletion = certificate, Owners = certificate.Owners with { DescriptorPackets = 0 } };
        byte[] callbackBytes = new byte[12];
        callbackBytes[1] = 255;
        callbackBytes[4] = 2;
        BattleDiagnosticNoOpPlayerCallback callback = new(0, 0, 0, focal.ActorId!, 0, 0, 0, 2, new(callbackBytes.ToImmutableArray(), 0), new(255, 255, 0, null), focal.NativeHeaderIndex!.Value, focal.NativeHeaderGeneration!.Value);
        BattleDiagnosticRepairRandomState mainRandom = new(NativeBattleRandomState.ForecastPhase(2), new(7), new(8), new(9));
        main = main with { Aftercare = new(main.LifecycleId, focal.Serial, main.Callback, main.SimulationFrame, main.Timestamp, mainRandom, callback, textures) { CommandBanks = AftercareBanks(callback) } };
        byte[] releasedBank = callbackBytes.ToArray();
        releasedBank[0] = 255;
        byte[] child = new byte[12];
        child[0] = 8;
        child[1] = 128;
        child[2] = 10;
        BinaryPrimitives.WriteUInt32LittleEndian(child.AsSpan(4), 0x47DDA0);
        BattleDiagnosticRepairRandomState before = new(NativeBattleRandomState.ForecastPhase(3), new(10), new(11), new(12));
        BattleDiagnosticRepairRandomState after = new(before.Battle with { Indexes = before.Battle.Indexes.ToArray().ToImmutableArray() }, new(10), new(11), new(12));
        BattleDiagnosticInvocationAftercareReceipt receipt = new(main.LifecycleId, focal.Serial, main.Callback, main.Callback + 1, main.Callback + 1, main.Callback + 1, main.Callback + 2, main.SimulationFrame + 1, main.Timestamp + 100, callback, new(releasedBank.ToImmutableArray(), 0), callback.Link with { Previous = 0, Next = 0, State = 0 }, before, after, 0, 0, 0, focal.Serial, focal.Serial, 1, new(child.ToImmutableArray()), new(child.ToImmutableArray().SetItem(1, 0)), true, true, true, main.Owners with { ActionOrCommand = false }, textures);
        receipt = receipt with { CommandBanks = AftercareBanks(null) };
        return (main, receipt);
    }

    internal static ImmutableArray<BattleDiagnosticCommandBankClosure> AftercareBanks(BattleDiagnosticNoOpPlayerCallback? callback)
    {
        return Enumerable.Range(0, 3).Select(bank => new BattleDiagnosticCommandBankClosure((byte)bank, callback is not null && bank == 0 ? callback.Slot : (byte)255, Enumerable.Range(0, 11).Select(slot => callback is not null && bank == 0 && slot == callback.Slot ? callback.Link : new NativeBattleCommandLink(0, 0, 0, null)).ToImmutableArray())).ToImmutableArray();
    }

    [Fact]
    public void Scoped_aftercare_keeps_main_factual_random_and_requires_separate_real_handler_bank_and_child_completion()
    {
        var fixture = AftercareFixture();
        DiagnosticInvocationReleaseValidation.Validate(fixture.Main, fixture.Main.Dispatch, fixture.Main.LifecycleId);
        DiagnosticInvocationAftercareValidation.Validate(fixture.Main, fixture.Aftercare);
        Assert.True(fixture.Main.Owners.ActionOrCommand);
        Assert.True(fixture.Main.Owners.AuxiliaryHead);
        Assert.False(fixture.Aftercare.CleanupOwners.ActionOrCommand);
        Assert.True(fixture.Aftercare.CleanupOwners.AuxiliaryHead);
        Assert.NotEqual(fixture.Main.Aftercare!.MainRandom.Battle.Indexes[0], fixture.Aftercare.HandlerRandomBefore.Battle.Indexes[0]);
        Assert.Equal<byte>(fixture.Aftercare.HandlerRandomBefore.Battle.Indexes, fixture.Aftercare.HandlerRandomAfter.Battle.Indexes);
    }

    [Theory]
    [InlineData("lifecycle")]
    [InlineData("serial")]
    [InlineData("main-callback")]
    [InlineData("handler-before-selection")]
    [InlineData("bank-before-handler")]
    [InlineData("completion-before-bank")]
    [InlineData("native-event")]
    [InlineData("bank-release")]
    [InlineData("child-retirement")]
    [InlineData("produced-action")]
    [InlineData("produced-header")]
    [InlineData("produced-contact")]
    [InlineData("later-dispatch")]
    [InlineData("rng-index")]
    [InlineData("rng-lane")]
    [InlineData("rng-crt")]
    [InlineData("rng-ui")]
    [InlineData("rng-global")]
    [InlineData("bank-packet")]
    [InlineData("bank-link")]
    [InlineData("child-callback")]
    [InlineData("child-reused")]
    [InlineData("live-action")]
    [InlineData("unknown-files")]
    [InlineData("textures-missing")]
    [InlineData("banks-missing")]
    [InlineData("unbound-queued-row")]
    [InlineData("texture-replacement")]
    public void Scoped_aftercare_rejects_changed_callback_random_native_writer_output_or_cleanup(string fault)
    {
        var fixture = AftercareFixture();
        BattleDiagnosticInvocationAftercareReceipt baseline = fixture.Aftercare;
        BattleDiagnosticInvocationAftercareReceipt invalid = fault switch
        {
            "lifecycle" => baseline with { LifecycleId = Guid.NewGuid() },
            "serial" => baseline with { FocalSerial = baseline.FocalSerial + 1 },
            "main-callback" => baseline with { MainCallback = baseline.MainCallback + 1 },
            "handler-before-selection" => baseline with { HandlerCompletedCallback = baseline.DispatchCallback - 1 },
            "bank-before-handler" => baseline with { BankReleasedCallback = baseline.HandlerCompletedCallback - 1 },
            "completion-before-bank" => baseline with { CompletedCallback = baseline.BankReleasedCallback - 1 },
            "native-event" => baseline with { NativeEventCompleted = false },
            "bank-release" => baseline with { BankReleased = false },
            "child-retirement" => baseline with { PacketRemoved = false },
            "produced-action" => baseline with { ProducedActions = 1 },
            "produced-header" => baseline with { ProducedHeaders = 1 },
            "produced-contact" => baseline with { ProducedContacts = 1 },
            "later-dispatch" => baseline with { DispatchSerialAfter = baseline.DispatchSerialBefore + 1 },
            "rng-index" => baseline with { HandlerRandomAfter = baseline.HandlerRandomAfter with { Battle = baseline.HandlerRandomAfter.Battle with { Indexes = baseline.HandlerRandomAfter.Battle.Indexes.SetItem(0, 99) } } },
            "rng-lane" => baseline with { HandlerRandomAfter = baseline.HandlerRandomAfter with { Battle = baseline.HandlerRandomAfter.Battle with { Lane = 1 } } },
            "rng-crt" => baseline with { HandlerRandomAfter = baseline.HandlerRandomAfter with { Crt = new(99) } },
            "rng-ui" => baseline with { HandlerRandomAfter = baseline.HandlerRandomAfter with { Ui = null } },
            "rng-global" => baseline with { HandlerRandomAfter = baseline.HandlerRandomAfter with { Global = new(99) } },
            "bank-packet" => baseline with { ReleasedBankPacket = baseline.ReleasedBankPacket with { Bytes = baseline.ReleasedBankPacket.Bytes.SetItem(6, 1) } },
            "bank-link" => baseline with { ReleasedBankLink = baseline.ReleasedBankLink with { State = 1 } },
            "child-callback" => baseline with { CompletionPacket = baseline.CompletionPacket with { Bytes = baseline.CompletionPacket.Bytes.SetItem(4, 0) } },
            "child-reused" => baseline with { RemovedCompletionPacket = baseline.RemovedCompletionPacket with { Bytes = baseline.RemovedCompletionPacket.Bytes.SetItem(0, 9) } },
            "live-action" => baseline with { CleanupOwners = baseline.CleanupOwners with { ActionOrCommand = true } },
            "unknown-files" => baseline with { CleanupOwners = baseline.CleanupOwners with { Files = null } },
            "textures-missing" => baseline with { CleanupTextures = null },
            "banks-missing" => baseline with { CommandBanks = [] },
            "unbound-queued-row" => baseline with { CommandBanks = baseline.CommandBanks.SetItem(2, baseline.CommandBanks[2] with { Tail = 3, Links = baseline.CommandBanks[2].Links.SetItem(3, new(255, 255, 0, null)) }) },
            "texture-replacement" => baseline with { CleanupTextures = baseline.CleanupTextures! with { Tasks = baseline.CleanupTextures!.Tasks.SetItem(0, baseline.CleanupTextures!.Tasks[0] with { ProgramLength = 2, ProgramOffset = 1 }) } },
            _ => throw new ArgumentOutOfRangeException(nameof(fault))
        };
        Assert.Throws<InvalidDataException>(() => DiagnosticInvocationAftercareValidation.Validate(fixture.Main, invalid));
    }

    [Fact]
    public void Fresh_driver_waits_for_actual_aftercare_even_if_global_drain_looks_empty_then_counts_real_tail()
    {
        var fixture = AftercareFixture();
        var recipe = new DiagnosticBattleDriverRecipe(new(BattleInputKind.Action, ActorId: "party-0", TargetId: "party-0", Ability: BattleAbility.Magic, ActionId: 1), 2) { BaselineSeconds = 0, FreshEntry = new(DiagnosticFreshEntryDeclaration.SingleUse, [new("container.bin", new string('a', 64))]) };
        DiagnosticBattleDriver driver = new(recipe, 0);
        DiagnosticDriverObservation Observation(double seconds, long frame, bool drained) => new(seconds, frame, frame, true, drained, false, drained ? [] : ["action-or-command"]);
        driver.Advance(Observation(0, 0, true));
        Assert.True(driver.Advance(Observation(.1, 1, true)));
        driver.ObserveDispatch(fixture.Main.Dispatch);
        driver.ObserveInvocationRelease(fixture.Main);
        driver.Advance(Observation(.2, 3, true));
        Assert.Equal(DiagnosticDriverStage.Drain, driver.Stage);
        Assert.Null(driver.InvocationAftercare);
        driver.ObserveInvocationAftercare(fixture.Aftercare);
        driver.Advance(Observation(.3, 5, false));
        Assert.Equal(DiagnosticDriverStage.Tail, driver.Stage);
        driver.Advance(Observation(.4, 6, false));
        driver.Advance(Observation(.5, 7, false));
        Assert.Equal(DiagnosticDriverStage.Completed, driver.Stage);
        Assert.Equal(fixture.Main, driver.FocalRelease);
        Assert.Equal(fixture.Aftercare, driver.InvocationAftercare);
    }

    [Theory]
    [InlineData("model")]
    [InlineData("passives")]
    [InlineData("bank")]
    [InlineData("stage")]
    [InlineData("packet")]
    [InlineData("source-generation")]
    [InlineData("extra-blocker")]
    [InlineData("texture")]
    [InlineData("main-time")]
    [InlineData("main-unbound-queue")]
    public void Main_aftercare_plan_requires_exact_causal_noop_and_complete_terminal_texture_proof(string fault)
    {
        var fixture = AftercareFixture();
        BattleDiagnosticInvocationRelease baseline = fixture.Main;
        BattleDiagnosticInvocationAftercarePlan plan = baseline.Aftercare!;
        BattleDiagnosticInvocationRelease invalid = fault switch
        {
            "model" => baseline with { Aftercare = plan with { Callback = plan.Callback with { ModelId = 4 } } },
            "passives" => baseline with { Aftercare = plan with { Callback = plan.Callback with { PassiveFlags = 4 } } },
            "bank" => baseline with { Aftercare = plan with { Callback = plan.Callback with { Bank = 1 } } },
            "stage" => baseline with { Aftercare = plan with { Callback = plan.Callback with { Stage = 1 } } },
            "packet" => baseline with { Aftercare = plan with { Callback = plan.Callback with { Packet = plan.Callback.Packet with { Bytes = plan.Callback.Packet.Bytes.SetItem(1, 1) } } } },
            "source-generation" => baseline with { Aftercare = plan with { Callback = plan.Callback with { SourceHeaderGeneration = 2 } } },
            "extra-blocker" => baseline with { Blockers = baseline.Blockers.Add("pending-contact") },
            "texture" => baseline with { Aftercare = plan with { TerminalTextures = plan.TerminalTextures! with { Tasks = plan.TerminalTextures!.Tasks.SetItem(0, plan.TerminalTextures!.Tasks[0] with { Deadline = 254 }) } } },
            "main-time" => baseline with { Aftercare = plan with { MainTimestamp = plan.MainTimestamp + 1 } },
            "main-unbound-queue" => baseline with { Aftercare = plan with { CommandBanks = plan.CommandBanks.SetItem(1, plan.CommandBanks[1] with { Tail = 2, Links = plan.CommandBanks[1].Links.SetItem(2, new(255, 255, 0, null)) }) } },
            _ => throw new ArgumentOutOfRangeException(nameof(fault))
        };
        Assert.Throws<InvalidDataException>(() => DiagnosticInvocationReleaseValidation.Validate(invalid, baseline.Dispatch, baseline.LifecycleId));
    }

    [Fact]
    public void Terminal_texture_continuity_keeps_live_task_sources_while_allowing_current_valid_model_flags_or_actual_retirement()
    {
        var fixture = AftercareFixture();
        BattleDiagnosticTextureQuiescence textures = fixture.Aftercare.CleanupTextures!;
        BattleDiagnosticInvocationAftercareReceipt changedModel = fixture.Aftercare with { CleanupTextures = textures with { Tasks = textures.Tasks.SetItem(0, textures.Tasks[0] with { ModelFlags0 = 7, ResourceFlags44 = 8 }) } };
        DiagnosticInvocationAftercareValidation.Validate(fixture.Main, changedModel);
        BattleDiagnosticInvocationAftercareReceipt retired = fixture.Aftercare with { CleanupTextures = null, CleanupOwners = fixture.Aftercare.CleanupOwners with { AuxiliaryHead = false } };
        DiagnosticInvocationAftercareValidation.Validate(fixture.Main, retired);
    }

}
