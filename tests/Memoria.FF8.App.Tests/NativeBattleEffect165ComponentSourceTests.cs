using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect165ComponentSourceTests
{
    [Fact]
    public void Installed_component_retains_all_seventeen_original_Lion_Heart_contact_writers()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(game is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        GameSession session = GameWorld.CreateColdProcess(game!);
        session.StartNewGame();
        using GameWorld world = new(session, game!);
        BattleSession source = NativeBattleComponentSourceTests.PrepareComponentSource(world, NativeBattleComponentSourceTests.CreateComponentRequest(165));
        BattleDiagnosticComponentAdmission admission = GameWorld.ValidateEffectComponentSource(source, 165);
        Assert.Equal((byte)0, admission.OriginalActorIndex);
        Assert.Equal((byte)249, admission.Command);
        Assert.Equal((ushort)3, admission.Action);
        BattlePresentationProcess process = source.Presentation!;
        Assert.Equal(0, Assert.Single(source.Combatants, actor => actor.IsPlayer && actor.NativeActorIndex == 0).ModelId);
        BattleModelOwner target = process.FindOriginalActor(NativeBattleModelSlot.Decode(3))!;
        BattleActionHeaderReference reference = new(admission.HeaderIndex);
        NativeBattleActionHeaderSlot header = process.ActionPackets.ReadHeader(reference);
        Assert.Equal((byte)0, header.Sequence);
        NativeBattleContactPacket[] contacts = process.ActionPackets.ReadHeaderContacts(reference).ToArray();
        Assert.Equal(17, contacts.Length);
        Assert.All(contacts, contact =>
        {
            Assert.Equal(target, contact.Primary.Recipient);
            Assert.Equal(0, contact.Primary.Amount6);
            Assert.Equal(source.NativePresentationStatusMemory.Rows[3].Saved, contact.Primary.SavedStatuses4);
            Assert.Equal(source.NativePresentationStatusMemory.Rows[3].Battle, contact.Primary.BattleStatuses8);
            Assert.Null(contact.Secondary);
        });
        NativeBattleGraphicsState graphics = process.Graphics;
        NativeBattleStageState stage = process.NativeStage!;
        long? callback = source.NativeHostCallbacks;
        int events = source.Events.Count;
        var combat = source.Combatants.Select(actor => (actor.Id, actor.CurrentHp, actor.NativeStatus0, actor.NativeStatus1, actor.IsActive, actor.Hidden, actor.Untargetable, actor.Suspended)).ToArray();
        BattleSession fork = source.ForkForContinuation();
        List<NativeBattleContactPacket> consumed = [];
        for (int frame = 0; frame < 2400 && consumed.Count < 17; frame++)
        {
            fork.AdvanceNativeGraphicsPresentation(BattlePresentationTiming.ElapsedForCallbacks(1));
            if (fork.Presentation!.LastFrameServices is { } services)
            {
                consumed.AddRange(services.ReadOrderedReceipts().OfType<NativeBattlePresentationContactReceived>().Select(receipt => receipt.Packet));
            }
        }
        Assert.Equal(17, consumed.Count);
        Assert.All(consumed, contact => Assert.Equal(contacts[0], contact));
        Assert.Same(graphics, process.Graphics);
        Assert.Same(stage, process.NativeStage);
        Assert.Equal(callback, source.NativeHostCallbacks);
        Assert.Equal(events, source.Events.Count);
        Assert.Equal(header, process.ActionPackets.ReadHeader(reference));
        Assert.Equal(contacts, process.ActionPackets.ReadHeaderContacts(reference));
        Assert.Equal(combat, source.Combatants.Select(actor => (actor.Id, actor.CurrentHp, actor.NativeStatus0, actor.NativeStatus1, actor.IsActive, actor.Hidden, actor.Untargetable, actor.Suspended)).ToArray());
    }
}
