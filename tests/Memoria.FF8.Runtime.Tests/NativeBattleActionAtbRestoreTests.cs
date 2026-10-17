using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleActionAtbRestoreTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FactualQueuedOrActiveHeaderRetainsItsOriginalNonzeroGaugeAcrossJson(bool active)
    {
        BattleSnapshot snapshot = Snapshot(active);
        string json = JsonSerializer.Serialize(snapshot);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(json)!);
        Assert.Equal(json, JsonSerializer.Serialize(restored.Capture()));
        Assert.Equal(new NativeBattleAtbState(12000, 153), restored.Combatants[0].NativeAtb);
        Assert.Equal(127, restored.Combatants[0].Atb);
        Assert.True(restored.Combatants[0].IsBusy);
    }

    [Fact]
    public void NativeGaugeStillRequiresItsKnownOwnerAndMatchingDisplay()
    {
        BattleSnapshot snapshot = Snapshot(true);
        BattleCombatantSnapshot[] missing = snapshot.Combatants.ToArray();
        missing[0] = missing[0] with { NativeAtb = null };
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { Combatants = missing }));
        BattleCombatantSnapshot[] wrongDisplay = snapshot.Combatants.ToArray();
        wrongDisplay[0] = wrongDisplay[0] with { Atb = 128 };
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { Combatants = wrongDisplay }));
    }

    [Fact]
    public void RetainedGaugeDoesNotBypassPhysicalActorHeaderValidation()
    {
        BattleSnapshot snapshot = Snapshot(true);
        NativeBattleActionPacketsState packets = snapshot.NativeActionPackets!;
        NativeBattleActionHeaderSlot header = packets.Headers[0];
        header = header with { Payload = header.Payload.SetItem(0, 3) };
        packets = packets with { Headers = packets.Headers.SetItem(0, header) };
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { NativeActionPackets = packets }));
        Assert.Contains("physical actor/header", error.Message);
    }

    [Fact]
    public void MissingPhysicalHeaderKeepsTheOriginalZeroAtbActionInvariant()
    {
        BattleSnapshot snapshot = Snapshot(true);
        BattleActionPresentation action = snapshot.ActiveAction! with { NativeHeaderIndex = null };
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { ActiveAction = action }));
        Assert.Contains("Invalid queued or active", error.Message);
    }

    [Fact]
    public void ComponentGaugeAndUnownedScaffoldActionAreNotTreatedAsFactualHeaders()
    {
        BattleDefinition definition = new() { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] };
        BattleSnapshot snapshot = new BattleSession(definition, BattleAtbMode.Normal).Capture();
        BattleCombatantSnapshot[] actors = snapshot.Combatants.ToArray();
        actors[0] = actors[0] with { Atb = 127 };
        BattleActionPresentation action = new("party", "enemy", BattleActionPhase.Windup, 1);
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { Combatants = actors, ActiveAction = action }));
    }

    [Theory]
    [InlineData(BattleAbility.Attack, false)]
    [InlineData(BattleAbility.Attack, true)]
    [InlineData(BattleAbility.Magic, false)]
    [InlineData(BattleAbility.Magic, true)]
    public void OrdinaryInstalledAttackAndMagicUsePhysicalHeaderOwnershipWithoutInlineNativeAction(BattleAbility ability, bool active)
    {
        BattleSnapshot snapshot = Snapshot(active, ability);
        Assert.Null(active ? snapshot.ActiveAction!.NativeAction : snapshot.QueuedActions[0].NativeAction);
        string json = JsonSerializer.Serialize(snapshot);
        Assert.Equal(json, JsonSerializer.Serialize(BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(json)!).Capture()));
    }

    [Fact]
    public void DistinctPhysicalHeadersCanRetainTheSameActorWhileScaffoldBusyUniquenessStaysSeparate()
    {
        BattleSnapshot snapshot = Snapshot(true, BattleAbility.Magic);
        NativeBattleActionPacketsState packets = snapshot.NativeActionPackets!;
        (packets, _) = packets.Prepare(new(0, 1, 0, 0, 0, 0, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        BattleAttackCommand queued = new("party", "enemy") { NativeHeaderIndex = 1, NativeQueuedCommand = 1 };
        snapshot = snapshot with { NativeActionPackets = packets, QueuedActions = [queued] };
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(BattleSession.Restore(snapshot).Capture()));
    }

    [Fact]
    public void OrdinaryPhysicalHeaderDoesNotInventAnUnownedComponentGauge()
    {
        BattleSnapshot snapshot = Snapshot(true, BattleAbility.Attack, false);
        Assert.Null(snapshot.Combatants[0].NativeAtb);
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot));
    }

    [Fact]
    public void OwnedGaugeAndHeaderWithoutAnInstalledAttackProducerRemainStrict()
    {
        BattleSnapshot snapshot = Snapshot(true, BattleAbility.Attack);
        BattleCombatantDefinition[] actors = snapshot.Definition.Combatants.ToArray();
        actors[0] = actors[0] with { NativeAttack = null };
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { Definition = snapshot.Definition with { Combatants = actors } }));
    }

    [Fact]
    public void OrdinaryFactualMagicStillRequiresItsCatalogAlliesAndAbilityContract()
    {
        BattleSnapshot snapshot = Snapshot(true, BattleAbility.Magic);
        BattleActionPresentation action = snapshot.ActiveAction! with { TargetId = "party" };
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { ActiveAction = action }));
        BattleDefinition missingAction = snapshot.Definition with { ActionDefinitions = [] };
        Assert.Throws<NotSupportedException>(() => BattleSession.Restore(snapshot with { Definition = missingAction }));
        BattleCombatantDefinition[] actors = snapshot.Definition.Combatants.ToArray();
        actors[0] = actors[0] with { Abilities = [BattleAbility.Attack] };
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { Definition = snapshot.Definition with { Combatants = actors } }));
    }

    private static BattleSnapshot Snapshot(bool active, BattleAbility? ordinary = null, bool nativeGauge = true)
    {
        BattleNativeAction magic = new(2, 4, 1, 4) { MagicId = 144, AttackType = 2, Power = 0, HitCount = 1, TargetFlags = 0, AttackFlags = 1, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = [] };
        BattleDefinition definition = new()
        {
            NativeClockRules = nativeGauge ? new(10) : null,
            NativeBattleSpeed = 2,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            ActionDefinitions = [new(BattleAbility.Magic, 4, "Blizzard", BattleActionEffect.Native, 0, false) { NativeAction = magic }],
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true, ModelId = 0, MaximumHp = 100, CurrentHp = 100, Abilities = [BattleAbility.Attack, BattleAbility.Magic], NativeAttack = magic with { Command = 1, ActionId = 0 }, MagicStocks = [new(4, 3)] }, new() { Id = "enemy", Name = "Enemy", ModelId = 1, MaximumHp = 100, CurrentHp = 100 }]
        };
        BattleSnapshot snapshot = new BattleSession(definition, BattleAtbMode.Normal).Capture();
        NativeBattleActionPacketsState packets = snapshot.NativeActionPackets!;
        byte command = ordinary is null ? (byte)252 : ordinary == BattleAbility.Magic ? (byte)2 : (byte)1;
        int actionId = ordinary == BattleAbility.Magic ? 4 : ordinary is null ? 1 : 0;
        (packets, _) = packets.Prepare(new(0, command, 0, 0, (ushort)actionId, ordinary == BattleAbility.Magic ? (ushort)144 : (ushort)0, null));
        (packets, _) = packets.AppendContact(new(new(3, 0, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.Publish(1);
        NativeBattleAtbState gauge = new NativeBattleAtbState(12000, 0).Advance(123, 10, 9, 0, 0);
        BattleCombatantSnapshot[] actors = snapshot.Combatants.ToArray();
        actors[0] = actors[0] with { NativeAtb = nativeGauge ? gauge : null, Atb = gauge.Display };
        BattleNativeGraphicCommand? graphic = ordinary is null ? new(1) : null;
        BattleAttackCommand queued = new("party", "enemy", ordinary ?? BattleAbility.EnemyAbility, actionId) { NativeHeaderIndex = 0, NativeQueuedCommand = command, NativeGraphic = graphic };
        BattleActionPresentation running = new("party", "enemy", BattleActionPhase.Windup, 1, ordinary ?? BattleAbility.EnemyAbility, actionId) { NativeHeaderIndex = 0, NativeQueuedCommand = command, NativeGraphic = graphic };
        return snapshot with { Combatants = actors, NativeActionPackets = packets, QueuedActions = active ? [] : [queued], ActiveAction = active ? running : null };
    }
}
