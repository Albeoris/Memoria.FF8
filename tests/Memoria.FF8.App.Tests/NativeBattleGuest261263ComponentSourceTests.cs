using System.Text.Json;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGuest261263ComponentSourceTests
{
    [Theory]
    [InlineData(261, 8, 0x187C20Cu)]
    [InlineData(262, 9, 0x187A2A4u)]
    [InlineData(263, 10, 0x1877DACu)]
    public void Installed_guest_limit_passes_original_actor_resource_and_cinematic_boundaries(ushort effect, byte character, uint directory)
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(game is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        GameSession session = GameWorld.CreateColdProcess(game!);
        session.StartNewGame();
        using GameWorld world = new(session, game!);
        string records = JsonSerializer.Serialize(session.State.CharacterVitals!.Records);
        Assert.Equal(NativeCharacterVitals.NativeRecordCount, session.State.CharacterVitals.Records.Length);
        BattleSession source = NativeBattleComponentSourceTests.PrepareComponentSource(world, NativeBattleComponentSourceTests.CreateComponentRequest(effect));
        BattleDefinition definition = source.ReadDefinition();
        Assert.NotNull(definition.NativeFieldBattleRules);
        Assert.Equal((definition.NativeFieldBattleRules.Value & 0x20) != 0, definition.NativeLimitsDisabled);
        Assert.Equal(records, JsonSerializer.Serialize(session.State.CharacterVitals!.Records));
        Assert.Equal(NativeCharacterVitals.NativeRecordCount, session.State.CharacterVitals.Records.Length);
        Assert.Null(world.Battle);
        NativeCharacterVitals temporary = Assert.IsType<NativeCharacterVitals>(source.MagicVitals);
        Assert.Equal(NativeCharacterVitals.ExtendedRecordCount, temporary.Records.Length);
        Assert.Equal(new[] { character }, temporary.GetBattlePartyRows(3));
        Assert.Equal(character, temporary.Records[character].CharacterId);
        Assert.Equal(session.State.CharacterVitals.Records.Select(record => record.CharacterId), temporary.Records.Take(NativeCharacterVitals.NativeRecordCount).Select(record => record.CharacterId));
        Assert.NotSame(session.State.CharacterVitals, temporary);
        BattleNativeAction action = Assert.Single(world.BattleResources.NativeActions, candidate => candidate.CharacterId == character && candidate.MagicId == effect && candidate.KernelSection == 18);
        BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, effect);
        Assert.Equal((byte)0, admission.OriginalActorIndex);
        Assert.Equal(action.Command, admission.Command);
        Assert.Equal(action.ActionId, admission.Action);
        BattlePresentationProcess process = source.Presentation!;
        BattleCombatant caster = Assert.Single(source.Combatants, actor => actor.NativeActorIndex == 0);
        Assert.Equal((int)character, caster.ModelId);
        BattleActionHeaderReference reference = new(admission.HeaderIndex);
        NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(reference);
        Assert.Equal(NativeBattleActionGraphics.ResolvePlayerSequence(action.Command, 0, action.ActionId, source.ReadDefinition().NativeCommandSequences), header.Sequence);
        var contacts = process.ActionPackets.ReadHeaderContacts(reference);
        Assert.Equal(action.HitCount, contacts.Length);
        foreach (NativeBattleContactPacket contact in contacts)
        {
            Assert.Equal(process.FindOriginalActor(NativeBattleModelSlot.Decode(3)), contact.Primary.Recipient);
            Assert.Equal(0, contact.Primary.Amount6);
            Assert.Equal(source.NativePresentationStatusMemory.Rows[3].Saved, contact.Primary.SavedStatuses4);
            Assert.Equal(source.NativePresentationStatusMemory.Rows[3].Battle, contact.Primary.BattleStatuses8);
            Assert.Null(contact.Secondary);
        }
        NativeBattleGraphicsState graphics = process.Graphics;
        NativeBattleStageState stage = process.NativeStage!;
        long? callback = source.NativeHostCallbacks;
        int events = source.Events.Count;
        var combat = (caster.CurrentHp, caster.NativeStatus0, caster.NativeStatus1, caster.NativePresentationStatus);
        BattleSession fork = source.ForkForContinuation();
        bool rebound = false;
        for (int frame = 0; frame < 1200 && !rebound; frame++)
        {
            fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
            NativeBattleGraphicsModel current = fork.Presentation!.Graphics.ActorModels.Get(admission.Actor);
            rebound = current.ActiveAnimationDirectory == directory && current.ActiveStagedResourceRow100 is not null;
        }
        Assert.True(rebound, "The original limit must bind its actual embedded actor DAT through the cinematic actor resource instruction.");
        long reboundCallback = fork.Presentation!.Graphics.Callback;
        fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
        NativeBattleGraphicsModel sampled = fork.Presentation.Graphics.ActorModels.Get(admission.Actor);
        Assert.True(fork.Presentation.Graphics.Callback > reboundCallback);
        Assert.Equal(directory, sampled.ActiveAnimationDirectory);
        Assert.NotNull(sampled.CurrentBodyBoneMatrices);
        Assert.NotNull(sampled.SequenceState!.Pose);
        bool entered = fork.Presentation.Graphics.OriginalEffect?.RequestedEffect == effect;
        bool completed = false;
        for (int frame = 0; frame < 2400 && !completed; frame++)
        {
            fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
            NativeBattleOriginalEffectState? current = fork.Presentation.Graphics.OriginalEffect;
            entered |= current?.RequestedEffect == effect;
            completed = entered && (current is null || current.ReturnedDescriptor == 0);
        }
        Assert.True(completed, "The actual guest cinematic must pass all native rig consumers and complete.");
        for (int tick = 0; tick < 3600 && fork.ReadyPlayerCommandOrder.Count == 0; tick++)
        {
            fork.Tick();
        }
        BattleCombatant ready = Assert.Single(fork.ReadyPlayerCommandOrder);
        Assert.Equal((int)character, ready.ModelId);
        Assert.NotNull(ready.NativeLimit);
        BattleHudController hud = new(fork, (_, _) => { }, () => { }, () => { });
        hud.BuildView();
        Assert.Equal(ready.Id, hud.SelectedActor!.Id);
        Assert.True(fork.HasNativeLimitSelection(ready.Id));
        NativeBattleRandomState selectedRandom = fork.ObserveDiagnosticSlotsFacts(ready.Id).Random;
        hud.BuildView();
        Assert.Equal(selectedRandom, fork.ObserveDiagnosticSlotsFacts(ready.Id).Random);
        Assert.Same(graphics, process.Graphics);
        Assert.Same(stage, process.NativeStage);
        Assert.Equal(callback, source.NativeHostCallbacks);
        Assert.Equal(events, source.Events.Count);
        Assert.Equal(header, process.ActionPackets.ReadHeader(reference));
        Assert.Equal(combat, (caster.CurrentHp, caster.NativeStatus0, caster.NativeStatus1, caster.NativePresentationStatus));
    }

    [Theory]
    [InlineData(261)]
    [InlineData(262)]
    [InlineData(263)]
    public void Guest_limit_rejects_a_different_caster_before_source_initialization(ushort effect)
    {
        Assert.Throws<InvalidDataException>(() => NativeBattleComponentSourceTests.PrepareOriginalGuest261263Component(null!, new(0, 0, effect)));
    }
}
