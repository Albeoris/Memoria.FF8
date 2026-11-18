using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect77ComponentSourceTests
{
    [Fact]
    public void Installed_component_retains_original_formation_self_geometry_and_unknown_outcome_until_C1()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(game is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        GameSession session = GameWorld.CreateColdProcess(game!);
        session.StartNewGame();
        using GameWorld world = new(session, game!);
        EffectPreviewRequest request = NativeBattleComponentSourceTests.CreateComponentRequest(77);
        Assert.Equal(126, request.EnemyId);
        Assert.Equal(142, request.StageId);
        Assert.Throws<InvalidDataException>(() => NativeBattleComponentSourceTests.PrepareOriginalEffect77Component(world, request with { StageId = 0 }));
        BattleSession source = NativeBattleComponentSourceTests.PrepareComponentSource(world, request);
        Assert.Equal(981, source.ReadDefinition().EncounterId);
        Assert.Equal(142, source.ReadDefinition().StageId);
        BattlePresentationProcess process = source.Presentation!;
        BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, 77);
        Assert.Equal((byte)3, admission.OriginalActorIndex);
        Assert.Equal((byte)8, admission.Command);
        Assert.Equal((ushort)356, admission.Action);
        BattleEnemyActorModel owner = Assert.IsType<BattleEnemyActorModel>(admission.Actor);
        BattleCombatant caster = Assert.Single(source.Combatants, actor => actor.Id == owner.Actor.ActorId);
        Assert.Equal(126, caster.ModelId);
        NativeBattlePoseSource pose = world.BattleResources.ReadNativeGraphicsResource(source.GetCombatantDefinition(caster.Id)).PoseSource!;
        Assert.Equal(71, pose.BoneCount);
        Assert.Equal(71, NativeBattleBodyBoneMatrices.Require(process.Graphics.ActorModels.Get(owner), pose).Length);
        BattleActionHeaderReference reference = new(admission.HeaderIndex);
        NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(reference);
        Assert.Equal((byte)18, header.Sequence);
        NativeBattleContactPacket contact = Assert.Single(process.ActionPackets.ReadHeaderContacts(reference));
        Assert.Equal(owner, contact.Primary.Recipient);
        Assert.Equal(0, contact.Primary.Amount6);
        NativeBattleOutcomeState outcome = Assert.IsType<NativeBattleOutcomeState>(process.Graphics.NativeOutcome);
        Assert.Equal(new NativeBattleOutcomeState(), outcome);
        NativeBattleGraphicsState graphics = process.Graphics;
        long? callback = source.NativeHostCallbacks;
        NativeBattlePresentationStatusMemory statusMemory = source.NativePresentationStatusMemory;
        var combat = (caster.CurrentHp, caster.NativeStatus0, caster.NativeStatus1, caster.NativePresentationStatus);
        BattleCombatant player = Assert.Single(source.Combatants, actor => actor.IsPlayer);
        player.NativeStatus1 |= 0x80000000;
        uint playerFlags = player.NativeCombatantFlags;
        uint playerStatuses = player.NativeStatus1;
        BattleSnapshot invalidInput = source.CaptureForContinuation();
        invalidInput = invalidInput with { PresentationContinuation = invalidInput.PresentationContinuation! with { Invocation = invalidInput.PresentationContinuation.Invocation! with { DiagnosticOutcomeEntryIssued = true } } };
        BattleSession invalid = InstalledBattlePresentation.Restore(invalidInput, world.ValidationAssets, world.BattleResources);
        BattlePresentationSnapshot invalidPresentation = invalid.CaptureForContinuation().PresentationContinuation!;
        NativeBattlePhysicalPartyStatuses invalidBefore = invalid.Presentation!.ReadPhysicalPartyBattleStatuses();
        NativeBattleDiagnosticOutcomeEntry staleStatuses = new(invalidPresentation.Invocation!, invalidPresentation.DiagnosticGenerations![reference.Ordinal], owner)
        {
            OriginalStatuses = invalidPresentation.PhysicalPartyBattleStatuses!,
            BeforeStatuses = invalidBefore,
            AfterStatuses = invalidBefore.ClearHighBits()
        };
        BattleCombatant invalidPlayer = Assert.Single(invalid.Combatants, actor => actor.IsPlayer);
        invalidPlayer.NativeStatus1 ^= 1;
        uint changedStatus = invalidPlayer.NativeStatus1;
        System.Reflection.MethodInfo acknowledge = typeof(BattleSession).GetMethod("AcknowledgeDiagnosticOutcomeEntry", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        System.Reflection.TargetInvocationException rejection = Assert.Throws<System.Reflection.TargetInvocationException>(() => acknowledge.Invoke(invalid, [staleStatuses, invalidPresentation.Invocation!.Token]));
        Assert.IsType<InvalidDataException>(rejection.InnerException);
        Assert.False(invalid.CaptureForContinuation().PresentationContinuation!.Invocation!.DiagnosticOutcomeEntryAcknowledged);
        Assert.True(invalidPresentation.PhysicalPartyBattleStatuses!.SameValues(invalid.CaptureForContinuation().PresentationContinuation!.PhysicalPartyBattleStatuses!));
        Assert.Equal(changedStatus, invalidPlayer.NativeStatus1);
        BattleSnapshot controlled = source.CaptureForContinuation();
        NativeBattlePhysicalPartyStatuses physical = NativeBattlePhysicalPartyStatuses.Unknown().Observe(1, 0x80000045);
        controlled = controlled with { PresentationContinuation = controlled.PresentationContinuation! with { PhysicalPartyBattleStatuses = physical } };
        BattleSession fork = InstalledBattlePresentation.Restore(controlled, world.ValidationAssets, world.BattleResources);
        BattleCombatant forkPlayer = Assert.Single(fork.Combatants, actor => actor.IsPlayer);
        int eventsBefore = fork.Events.Count;
        bool selfGeometry = false;
        for (int frame = 0; frame < 1200 && fork.Presentation!.Graphics.NativeOutcome?.ExitModeDE8 is null; frame++)
        {
            NativeBattleClockState clock = fork.Capture().NativeClock!;
            int gauge = forkPlayer.NativeAtb!.Current;
            fork.AdvanceNativeGraphicsPresentation(PresentationTiming.SimulationStep);
            NativeBattleGraphicsState current = fork.Presentation!.Graphics;
            if (current.ActionContext is { } geometry)
            {
                selfGeometry |= geometry.IsSelfTarget(current.ActorModels, current.NativeBindings);
            }
            if (current.NativeOutcome?.ExitModeDE8 is not null)
            {
                Assert.Equal(clock.Active, fork.Capture().NativeClock!.Active);
                Assert.Equal(clock.AtbAdmitted, fork.Capture().NativeClock!.AtbAdmitted);
                Assert.True(forkPlayer.NativeAtb!.Current >= gauge);
            }
        }
        Assert.True(selfGeometry);
        NativeBattleOutcomeState published = Assert.IsType<NativeBattleOutcomeState>(fork.Presentation!.Graphics.NativeOutcome);
        Assert.Equal((byte)0, published.ActionOwnerB0D);
        Assert.Equal((byte)2, published.ExitModeDE8);
        Assert.Equal((byte)1, published.AdmissionDFD);
        Assert.Null(published.ResultByte6E7);
        Assert.Null(published.FadeStyleE01);
        Assert.Null(published.VictoryQueuedDFE);
        Assert.Equal(BattleOutcome.Active, fork.Outcome);
        Assert.DoesNotContain(fork.Events.Skip(eventsBefore), item => item.Kind is "Damage" or "Healing" or "Victory" or "BattleRewards");
        Assert.Equal(playerStatuses & 0x7FFFFFFF, forkPlayer.NativeStatus1);
        Assert.Equal(playerFlags, forkPlayer.NativeCombatantFlags);
        BattleSnapshot entered = fork.CaptureForContinuation();
        NativeBattlePhysicalPartyStatuses cleared = entered.PresentationContinuation!.PhysicalPartyBattleStatuses!;
        Assert.Equal(new NativeBattlePhysicalStatusDword(uint.MaxValue, playerStatuses & 0x7FFFFFFF), cleared.Rows[0]);
        Assert.Equal(new NativeBattlePhysicalStatusDword(uint.MaxValue, 0x45), cleared.Rows[1]);
        Assert.Equal(new NativeBattlePhysicalStatusDword(0x80000000, 0), cleared.Rows[2]);
        Assert.Null(process.FindOriginalActor(NativeBattleModelSlot.Decode(1)));
        Assert.Null(process.FindOriginalActor(NativeBattleModelSlot.Decode(2)));
        Assert.Equal(new NativeBattlePhysicalStatusDword(0, 0), process.ReadPhysicalPartyBattleStatuses().Rows[1]);
        BattlePresentationProcess.HostedPresentationInvocation invocation = entered.PresentationContinuation!.Invocation!;
        Assert.True(invocation.DiagnosticOutcomeEntryIssued);
        Assert.True(invocation.DiagnosticOutcomeEntryAcknowledged);
        NativeBattleDiagnosticOutcomeEntry replay = new(invocation, entered.PresentationContinuation.DiagnosticGenerations![reference.Ordinal], owner);
        Assert.Throws<InvalidDataException>(() => fork.Presentation!.ValidateDiagnosticOutcomeEntry(replay));
        BattleSession continued = fork.ForkForContinuation();
        Assert.True(continued.CaptureForContinuation().PresentationContinuation!.Invocation!.DiagnosticOutcomeEntryAcknowledged);
        Assert.True(cleared.SameValues(continued.Presentation!.ReadPhysicalPartyBattleStatuses()));
        Assert.Equal(playerStatuses, player.NativeStatus1);
        Assert.Equal(playerFlags, player.NativeCombatantFlags);
        Assert.Same(graphics, process.Graphics);
        Assert.Equal(callback, source.NativeHostCallbacks);
        Assert.Equal(header, process.ActionPackets.ReadHeader(reference));
        Assert.Same(outcome, process.Graphics.NativeOutcome);
        Assert.Same(statusMemory, source.NativePresentationStatusMemory);
        Assert.Equal(combat, (caster.CurrentHp, caster.NativeStatus0, caster.NativeStatus1, caster.NativePresentationStatus));
    }
}
