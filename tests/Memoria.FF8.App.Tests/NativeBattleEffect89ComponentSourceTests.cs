using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect89ComponentSourceTests
{
    [Fact]
    public void Installed_component_retains_Rinoa_rig_and_eight_contacts_through_both_original_actor_bone_selectors()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(game is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        GameSession session = GameWorld.CreateColdProcess(game!);
        session.StartNewGame();
        using GameWorld world = new(session, game!);
        NativeCharacterVitals originalVitals = session.State.CharacterVitals!;
        int row = Assert.Single(originalVitals.Records.Select((record, index) => (record, index)), candidate => candidate.record.CharacterId == 4).index;
        string originalRecords = JsonSerializer.Serialize(originalVitals.Records);
        EffectPreviewRequest request = NativeBattleComponentSourceTests.CreateComponentRequest(89);
        Assert.Equal((byte)4, request.AllyId);
        Assert.Equal(0, request.EnemyId);
        Assert.Equal(0, request.StageId);
        Assert.Throws<InvalidDataException>(() => NativeBattleComponentSourceTests.PrepareOriginalEffect89Component(world, request with { AllyId = 0 }));
        BattleSession source = NativeBattleComponentSourceTests.PrepareComponentSource(world, request);
        Assert.Equal(new byte[] { checked((byte)row), 255, 255 }, session.State.CharacterVitals!.CombatParty);
        Assert.Equal(originalRecords, JsonSerializer.Serialize(session.State.CharacterVitals.Records));
        BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, 89);
        Assert.Equal((byte)0, admission.OriginalActorIndex);
        Assert.Equal((byte)19, admission.Command);
        Assert.Equal((ushort)3, admission.Action);
        BattlePresentationProcess process = source.Presentation!;
        BattleCombatant caster = Assert.Single(source.Combatants, actor => actor.NativeActorIndex == 0);
        Assert.Equal(4, caster.ModelId);
        NativeBattlePoseSource pose = world.BattleResources.ReadNativeGraphicsResource(source.GetCombatantDefinition(caster.Id)).PoseSource!;
        Assert.True(pose.BoneCount > 25);
        Assert.Equal(pose.BoneCount, NativeBattleBodyBoneMatrices.Require(process.Graphics.ActorModels.Get(admission.Actor), pose).Length);
        BattleActionHeaderReference reference = new(admission.HeaderIndex);
        NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(reference);
        Assert.Equal((byte)21, header.Sequence);
        Assert.Equal((byte)8, header.ReadContactCount());
        NativeBattleContactPacket[] contacts = process.ActionPackets.ReadHeaderContacts(reference).ToArray();
        Assert.Equal(8, contacts.Length);
        BattleModelOwner recipient = process.FindOriginalActor(NativeBattleModelSlot.Decode(3))!;
        Assert.All(contacts, contact =>
        {
            Assert.Equal(recipient, contact.Primary.Recipient);
            Assert.Equal(0, contact.Primary.Amount6);
            Assert.Equal(source.NativePresentationStatusMemory.Rows[3].Saved, contact.Primary.SavedStatuses4);
            Assert.Equal(source.NativePresentationStatusMemory.Rows[3].Battle, contact.Primary.BattleStatuses8);
            Assert.Null(contact.Secondary);
        });
        Assert.Equal(8, contacts.Select(contact => contact.Occurrence).Distinct().Count());
        NativeBattleGraphicsState graphics = process.Graphics;
        NativeBattleStageState stage = process.NativeStage!;
        NativeBattleCameraState camera = process.Camera;
        long? callback = source.NativeHostCallbacks;
        var combat = (caster.CurrentHp, caster.NativeStatus0, caster.NativeStatus1, caster.NativePresentationStatus);
        BattleSession fork = source.ForkForContinuation();
        int eventsBefore = fork.Events.Count;
        bool sampled = false;
        for (int frame = 0; frame < 1200 && !sampled; frame++)
        {
            fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
            NativeBattleEffectTaskPool? body = fork.Presentation!.Graphics.OriginalEffect?.Pools.SingleOrDefault(pool => pool.DescriptorAddress == NativeBattleRinoa89Controller.Body);
            if (body is not null && (body.Slots[0].Flags & 1) != 0 && body.Slots[0].Callback == 0x6FA780 && body.Slots[0].ReadWord(12) >= 15)
            {
                Assert.Equal(checked(body.BankAddress + 248), fork.Presentation.Graphics.ActorModels.Get(admission.Actor).ActiveStagedResourceRow100);
                sampled = true;
            }
        }
        Assert.True(sampled, "The original body must pass ages7/14 and their actor bone25/24 emissions.");
        long sampledCallback = fork.Presentation!.Graphics.Callback;
        fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
        Assert.True(fork.Presentation.Graphics.Callback > sampledCallback);
        NativeBattleGraphicsModel nextPose = fork.Presentation.Graphics.ActorModels.Get(admission.Actor);
        Assert.NotNull(nextPose.CurrentBodyBoneMatrices);
        Assert.NotNull(nextPose.SequenceState!.Pose);
        Assert.NotEqual(camera.Projection, fork.Presentation!.Camera.Projection);
        Assert.DoesNotContain(fork.Events.Skip(eventsBefore), item => item.Kind is "Damage" or "Healing" or "Victory" or "BattleRewards");
        Assert.Same(graphics, process.Graphics);
        Assert.Same(stage, process.NativeStage);
        Assert.Same(camera, process.Camera);
        Assert.Equal(callback, source.NativeHostCallbacks);
        Assert.Equal(header, process.ActionPackets.ReadHeader(reference));
        Assert.Equal(combat, (caster.CurrentHp, caster.NativeStatus0, caster.NativeStatus1, caster.NativePresentationStatus));
    }
}
