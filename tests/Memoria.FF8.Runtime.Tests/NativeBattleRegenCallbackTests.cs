using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRegenCallbackTests
{
    [Fact]
    public void RegenTimerQueuesItsActualCallbackWithoutChangingHpOrPublishingAFakeTimerFormula()
    {
        BattleSession battle = Create(false, 500);
        List<BattleDiagnosticResolution> resolutions = [];
        battle.DiagnosticResolution += resolutions.Add;
        Method("DispatchNativeStatusTimers").Invoke(battle, null);

        Assert.Equal(500, battle.Combatants[0].CurrentHp);
        Assert.Empty(resolutions);
        BattleNativeCallback callback = Assert.IsType<BattleNativeCallback>(Assert.Single(battle.QueuedActions).NativeCallback);
        Assert.Equal(new BattleNativeCallback(0, 6), callback);
        Assert.Equal((short)58, battle.Capture().Combatants[0].NativeStatus!.Timers[4]);
        callback.Validate();
        string saved = JsonSerializer.Serialize(battle.Capture());
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(saved)!);
        Assert.Equal(callback, Assert.Single(restored.QueuedActions).NativeCallback);
        Assert.Equal(saved, JsonSerializer.Serialize(restored.Capture()));
    }

    [Theory]
    [InlineData(false, 500, 550)]
    [InlineData(true, 500, 450)]
    [InlineData(false, 980, 1000)]
    [InlineData(true, 20, 0)]
    public void QueuedRegenProducesOneInternalContactAndTheExistingPresentationConsumerTracksActualHp(bool zombie, int hp, int expected)
    {
        BattleSession battle = Create(zombie, hp);
        Method("DispatchNativeStatusTimers").Invoke(battle, null);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        List<BattleDiagnosticResolution> resolutions = [];
        battle.DiagnosticResolution += resolutions.Add;

        NativeBattleContactHalf primary = Produce(battle);
        NativeBattleContactHalf restoredPrimary = Produce(restored);

        Assert.Equal(primary, restoredPrimary);
        Assert.Equal(expected, battle.Combatants[0].CurrentHp);
        Assert.Equal(expected, restored.Combatants[0].CurrentHp);
        Assert.Equal((byte)0, primary.ActorIndex);
        Assert.Equal((ushort)50, primary.Amount6);
        Assert.Equal(zombie ? 0 : 1, primary.HitFlags3 & 1);
        BattleDiagnosticResolution formula = Assert.Single(resolutions, resolution => resolution.ResolvedAmount == 50);
        Assert.Equal("party", formula.ActorId);
        Assert.Equal("party", formula.OriginalTargetId);
        Assert.Null(formula.PeriodicStatus);
        Assert.Equal(zombie ? "damage" : "healing", formula.Kind);

        Assert.Equal(hp, battle.ReadPresentedVital("party").HitPoints);
        Method("PublishNativePresentedContact").Invoke(battle, [primary]);
        Assert.Equal(expected, battle.ReadPresentedVital("party").HitPoints);
        Assert.Equal(1u, battle.Capture().NativeClock!.ActionPause);
        Assert.DoesNotContain(battle.QueuedActions, command => command.NativeCallback is not null);
        Assert.Single(battle.QueuedActions, command => command.NativeAction is { Command: 0, ActionId: 4 });

        Method("RunNativeDescriptorActionCallback").Invoke(battle, [battle.NativeGraphics!, 0x47DDA0u]);
        Assert.Equal(0u, battle.Capture().NativeClock!.ActionPause);
        Assert.Empty(battle.QueuedActions);
        Assert.Equal(expected, battle.Combatants[0].CurrentHp);
        Assert.Single(battle.Events, entry => entry.Kind == "ActionComplete");
    }

    [Fact]
    public void QueuedRegenTestsCurrentZombieStatusAtFormulaDispatch()
    {
        BattleSession battle = Create(false, 500);
        Method("DispatchNativeStatusTimers").Invoke(battle, null);
        battle.Combatants[0].NativeStatus0 |= 0x40;
        NativeBattleContactHalf primary = Produce(battle);
        Assert.Equal(450, battle.Combatants[0].CurrentHp);
        Assert.Equal(0, primary.HitFlags3 & 1);
    }

    [Theory]
    [InlineData(false, 550)]
    [InlineData(true, 450)]
    public void QueuedRegenBankZeroHeaderNormalizesAndCompletesItsGraphicalContactService(bool zombie, int expected)
    {
        BattleSession battle = Create(zombie, 500);
        Method("DispatchNativeStatusTimers").Invoke(battle, null);
        Produce(battle);

        // This focused graphical consumer fixture leaves model resources unloaded.
        // The native control service still normalizes and publishes its actual header contacts.
        BattleDefinition definition = (BattleDefinition)Field("definition").GetValue(battle)!;
        NativeBattleGraphicsRules rules = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, Enumerable.Repeat((sbyte)-1, 16).ToImmutableArray(), Enumerable.Repeat((sbyte)-1, 32).ToImmutableArray());
        Field("definition").SetValue(battle, definition with { NativeGraphics = new(rules, [], []) });
        ImmutableArray<NativeBattleGraphicsModel> models = NativeBattleGraphicsModels.ClearBank().ToImmutableArray();
        models = models.SetItem(0, models[0] with { Flags0 = 8, ModelId4 = 0 });
        NativeBattleGraphicsState graphics = new(models, ["party", null, null, "enemy", null, null, null], 0, 0);

        graphics = (NativeBattleGraphicsState)Method("BeginNativeActionGraphics").Invoke(battle, [graphics, 0x1D280C4u])!;
        Assert.Equal(0x50BD80u, graphics.ActionGraphics!.Callback);
        Assert.Equal((byte)0, battle.NativeActionPackets.Headers[0].ReadByte(17));
        Assert.Equal(500, battle.ReadPresentedVital("party").HitPoints);

        var result = ((NativeBattleGraphicsState Graphics, byte Phase, bool Complete))Method("PollNativeActionGraphics").Invoke(battle, [graphics, (byte)0])!;
        Assert.True(result.Complete);
        Assert.Equal((byte)1, result.Phase);
        Assert.Equal(expected, battle.ReadPresentedVital("party").HitPoints);
        Assert.Equal(expected, battle.Combatants[0].CurrentHp);
        Method("RunNativeDescriptorActionCallback").Invoke(battle, [result.Graphics, 0x47DDA0u]);
        Assert.Empty(battle.QueuedActions);
        Assert.Equal(expected, battle.Combatants[0].CurrentHp);
    }

    [Fact]
    public void QueuedRegenSkipsAnActorThatBecameKoBeforeCallbackDispatch()
    {
        BattleSession battle = Create(false, 500);
        Method("DispatchNativeStatusTimers").Invoke(battle, null);
        battle.Combatants[0].CurrentHp = 0;
        battle.Combatants[0].NativeStatus0 |= 1;
        TakeAndDispatch(battle);
        Assert.Equal(0, battle.Combatants[0].CurrentHp);
        Assert.Empty(battle.QueuedActions);
        Assert.Equal((byte)0, battle.NativeActionPackets.ActionCount);
        Assert.Equal(0u, battle.Capture().NativeClock!.ActionPause);
    }

    private static NativeBattleContactHalf Produce(BattleSession battle)
    {
        // The component FIFO roundtrip has no graphical definition or cache.
        // Attach this contact-consumer fixture only after that valid boundary.
        int hp = battle.Combatants[0].CurrentHp;
        Field("nativePartyPresentation").SetValue(battle, new NativeBattlePartyPresentation([new(checked((ushort)hp), 1000, null, null) { DisplayedHitPointsQ12 = hp << 12 }, null, null]));
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeGraphics))!.SetValue(battle, new NativeBattleGraphicsState([], [], 0, 0));
        TakeAndDispatch(battle);
        Assert.Equal((byte)1, battle.NativeActionPackets.ActionCount);
        Assert.Equal((byte)0, battle.NativeActionPackets.Headers[0].ReadByte(1));
        return battle.NativeActionPackets.ReadContact(0).Primary;
    }

    private static void TakeAndDispatch(BattleSession battle)
    {
        object?[] arguments = [null];
        Assert.True((bool)Method("TryTakeNativeBankCommand").Invoke(battle, arguments)!);
        BattleAttackCommand command = Assert.IsType<BattleAttackCommand>(arguments[0]);
        Assert.Equal(new BattleNativeCallback(0, 6), command.NativeCallback);
        try
        {
            Method("DispatchNativeCallback").Invoke(battle, [command.NativeCallback!]);
        }
        finally
        {
            Method("ReleaseNativeDispatchedCommand").Invoke(battle, null);
        }
    }

    private static BattleSession Create(bool zombie, int hp)
    {
        BattleSession battle = new(new()
        {
            NativeClockRules = new(10),
            NativeBattleSpeed = 2,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, ModelId = 0, MaximumHp = 1000, CurrentHp = hp, NativeStatus0 = zombie ? (ushort)0x40 : (ushort)0, NativeStatus1 = 0x10, NativeDeathCount = 0 },
                new() { Id = "enemy", Name = "Enemy", ModelId = 1, MaximumHp = 1000, CurrentHp = 1000 }
            ]
        }, BattleAtbMode.Normal);
        BattleSnapshot snapshot = battle.Capture();
        snapshot.Combatants[0].NativeStatus!.Timers[4] = 60;
        return BattleSession.Restore(snapshot);
    }

    private static MethodInfo Method(string name) => typeof(BattleSession).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static FieldInfo Field(string name) => typeof(BattleSession).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
}
