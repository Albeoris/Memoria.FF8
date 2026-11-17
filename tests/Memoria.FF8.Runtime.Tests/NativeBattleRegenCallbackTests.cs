using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRegenCallbackTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Theory]
    [InlineData("party", false, 500, 550, 3024)]
    [InlineData("party", true, 500, 450, 12000)]
    [InlineData("additional", false, 980, 1000, 3024)]
    [InlineData("additional", true, 20, 0, 12000)]
    [InlineData("enemy", false, 500, 550, 3024)]
    [InlineData("reinforcement", false, 980, 1000, 3024)]
    public void Due_regen_publishes_one_current_contact_and_preserves_gauge_across_the_queued_fork(string actorId, bool zombie, int hp, int expected, int gauge)
    {
        BattleSession battle = Create(actorId, hp, gauge);
        BattleSnapshot before = battle.Capture();
        DispatchTimers(battle);

        BattleExpandedAiCallbackRequest queued = Assert.IsType<BattleExpandedAiCallbackRequest>(Assert.Single(battle.QueuedActions).ExpandedAiCallback);
        Assert.Equal(actorId, queued.Actor.ActorId);
        Assert.Equal((byte)6, queued.Stage);
        Assert.Equal(hp, Actor(battle, actorId).CurrentHp);
        Assert.Equal((short)58, Actor(battle, actorId).NativeStatusTimers[4]);
        BattleSession fork = battle.ForkForContinuation();
        foreach (BattleSession logical in new[] { battle, fork })
        {
            BattleCombatant actor = Actor(logical, actorId);
            if (zombie)
            {
                actor.NativeStatus0 |= 0x40;
            }
            Assert.Equal(queued, Assert.Single(logical.QueuedActions).ExpandedAiCallback);
            if (actorId is "additional" or "reinforcement")
            {
                Assert.Null(actor.NativeActorIndex);
            }
            Bind(logical);

            Invoke(logical, "DispatchAction");
            BattlePresentationProcess process = logical.Presentation!;
            var header = Assert.Single(process.ActionPackets.PublishedHeaders());
            Assert.Equal((byte)0, header.Slot.Command);
            Assert.Equal((ushort)4, header.Slot.Action);
            Assert.Equal(logical.ReadPresentationModelOwner(actorId), header.Slot.Source);
            NativeBattleContactHalf contact = Assert.Single(process.ActionPackets.ReadHeaderContacts(header.Reference)).Primary;
            Assert.Equal(header.Slot.Source, contact.Recipient);
            Assert.Equal((ushort)50, contact.Amount6);
            Assert.Equal(zombie ? 0 : 1, contact.HitFlags & 1);
            Assert.Equal(expected, actor.CurrentHp);
            if (actor.IsPlayer)
            {
                Assert.Equal(hp, logical.ReadPresentedVital(actorId).HitPoints);
            }

            List<NativeBattlePresentationReceipt> receipts = AdvanceControlDescriptors(logical, process);
            NativeBattlePresentationContactReceived published = Assert.Single(receipts.OfType<NativeBattlePresentationContactReceived>());
            Assert.Equal(contact, published.Packet.Primary);
            Assert.Equal(0x47DDA0u, Assert.Single(receipts.OfType<NativeBattlePresentationCompletionRequested>()).Callback);
            if (actor.IsPlayer)
            {
                Assert.Equal(expected, logical.ReadPresentedVital(actorId).HitPoints);
            }
            Assert.Equal(new NativeBattleAtbState(12000, gauge), actor.NativeAtb);
            Assert.Equal(0u, logical.Capture().NativeClock!.ActionPause);
            Assert.Null(logical.ActiveAction);
            Assert.Empty(logical.QueuedActions);
            Assert.Single(logical.Events, entry => entry.Kind == "ActionComplete");
            Assert.Equal(before.Random!.Lane, logical.Capture().Random!.Lane);
            Assert.Equal(before.Random!.Indexes, logical.Capture().Random!.Indexes);
        }
    }

    [Fact]
    public void Queued_regen_is_discarded_if_its_actor_becomes_knocked_out()
    {
        BattleSession battle = Create("additional", 500, 3024);
        DispatchTimers(battle);
        BattleCombatant actor = Actor(battle, "additional");
        actor.CurrentHp = 0;
        actor.NativeStatus0 |= 1;
        Bind(battle);

        Invoke(battle, "DispatchAction");

        Assert.Equal(0, actor.CurrentHp);
        Assert.Empty(battle.QueuedActions);
        Assert.Empty(battle.Presentation!.ActionPackets.PublishedHeaders());
        Assert.Null(battle.ActiveAction);
        Assert.Equal(0u, battle.Capture().NativeClock!.ActionPause);
    }

    [Fact]
    public void Simultaneously_due_callbacks_preserve_current_occupied_order_after_slot_reuse()
    {
        BattleSession battle = Create("party", 500, 3024);
        BattleCombatant retired = Actor(battle, "enemy");
        retired.IsActive = false;
        retired.BindRosterSlot(null, bindNative: false);
        retired.NativeStatus1 |= 0x10;
        retired.NativeStatusTimers[4] = 60;

        BattleCombatant replacement = Actor(battle, "reinforcement");
        replacement.BindRosterSlot(BattleRosterSlot.Enemy(0), bindNative: true);
        replacement.NativeStatus1 |= 0x10;
        replacement.NativeStatusTimers[4] = 60;
        BattleCombatant additional = Actor(battle, "additional");
        additional.NativeStatus1 |= 0x10;
        additional.NativeStatusTimers[4] = 60;

        DispatchTimers(battle);
        DispatchTimers(battle);

        Assert.Equal(new[] { "party", "reinforcement", "additional" }, battle.QueuedActions.Select(command => command.ExpandedAiCallback!.Actor.ActorId));
        Assert.Equal((short)60, retired.NativeStatusTimers[4]);
        Assert.Equal((short)56, replacement.NativeStatusTimers[4]);
    }

    private static List<NativeBattlePresentationReceipt> AdvanceControlDescriptors(BattleSession logical, BattlePresentationProcess process)
    {
        List<NativeBattlePresentationReceipt> receipts = [];
        for (int frame = 0; frame < 8 && logical.ActiveAction is not null; frame++)
        {
            long? token = process.ActiveActionToken;
            NativeBattleModelLifecycleServices services = logical.CaptureModelLifecycleServices(process.Graphics.ActorModels.Roster);
            NativeBattleHostedTestFixture.SetField(process, "hostedFrameServices", services);
            try
            {
                NativeBattleGraphicsState graphics = Assert.IsType<NativeBattleGraphicsState>(Invoke(process, "AdvanceNativeDescriptors", process.Graphics));
                NativeBattleHostedTestFixture.SetProperty(process, nameof(BattlePresentationProcess.NativeGraphics), graphics);
            }
            finally
            {
                NativeBattleHostedTestFixture.SetField(process, "hostedFrameServices", null);
            }
            receipts.AddRange(services.ReadOrderedReceipts());
            Invoke(logical, "ApplyExpandedHostedFrameReceipts", services, token);
        }
        return receipts;
    }

    private static void Bind(BattleSession battle)
    {
        var (roster, aliases) = battle.ReadBattlePresentationPacketBindings();
        var bindings = battle.Combatants.Select(actor => new NativeBattleGraphicsBinding(actor.Id, actor.IsPlayer ? (byte)1 : (byte)16, new([0, 0]))).ToArray();
        NativeBattleGraphicsState graphics = NativeBattleHostedTestFixture.Graphics(roster, aliases, owner => new(0)
        {
            Flags8 = 0,
            ModelId4 = owner.Side == BattleSide.Allies ? (byte)1 : (byte)16
        }) with { EntryReady = true, EntryLifecycle = 4 };
        NativeBattleHostedTestFixture.BindProcess(battle, graphics, NativeBattleHostedTestFixture.CreateGraphicsDefinition(bindings));
        Invoke(battle, "InitializeExpandedPartyPresentation");
    }

    private static BattleSession Create(string actorId, int hp, int gauge)
    {
        BattleSession battle = new(new()
        {
            NativeClockRules = new(10),
            NativeBattleSpeed = 2,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            Combatants = [Combatant("party"), Combatant("second"), Combatant("third"), Combatant("additional"), Combatant("enemy", player: false), Combatant("reinforcement", player: false)]
        }, BattleAtbMode.Normal);
        Actor(battle, "reinforcement").BindRosterSlot(BattleRosterSlot.Enemy(4), bindNative: false);
        BattleSnapshot snapshot = battle.Capture();
        int row = Array.FindIndex(snapshot.Combatants, actor => actor.Id == actorId);
        NativeBattleAtbState atb = new(12000, gauge);
        snapshot.Combatants[row] = snapshot.Combatants[row] with { NativeAtb = atb, Atb = atb.Display };
        snapshot.Combatants[row].NativeStatus!.Timers[4] = 60;
        return BattleSession.Restore(snapshot);

        BattleCombatantDefinition Combatant(string id, bool player = true) => new()
        {
            Id = id,
            Name = id,
            IsPlayer = player,
            ModelId = player ? 1 : 16,
            MaximumHp = 1000,
            CurrentHp = id == actorId ? hp : 1000,
            NativeStatus1 = id == actorId ? 0x10u : 0u,
            NativeDeathCount = 0
        };
    }

    private static BattleCombatant Actor(BattleSession battle, string id) => battle.Combatants.Single(actor => actor.Id == id);

    private static void DispatchTimers(BattleSession battle) => Invoke(battle, "DispatchNativeStatusTimers");

    private static object? Invoke(object owner, string method, params object?[] arguments)
    {
        return owner.GetType().GetMethod(method, PrivateInstance)!.Invoke(owner, arguments);
    }
}
