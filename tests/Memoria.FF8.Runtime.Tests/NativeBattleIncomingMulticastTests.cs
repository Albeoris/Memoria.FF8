using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleIncomingMulticastTests
{
    [Theory]
    [InlineData((byte)2, 0u)]
    [InlineData((byte)2, 0x20000u)]
    [InlineData((byte)2, 0x40000u)]
    [InlineData((byte)3, 0u)]
    [InlineData((byte)3, 0x20000u)]
    [InlineData((byte)3, 0x40000u)]
    public void Ai_action_clears_the_previous_dispatch_repeat_count_before_graphics_normalization(byte previousCastCount, uint recipientStatus)
    {
        BattleSession battle = CreateAiAction(previousCastCount, recipientStatus);

        Assert.Equal((byte)1, battle.NativeActionPackets.ActionCount);
        Assert.Equal((byte)0, battle.NativeActionPackets.ExportRetained().Headers[0].ReadOriginalByte(17));
        NativeBattleActionGraphicsState graphics = NativeBattleActionGraphics.Normalize(battle.NativeActionPackets, new BattleActionHeaderReference(0));
        Assert.Equal(new BattlePartyActorModel(new("recipient")), Assert.Single(graphics.TargetActors.Resolve(battle.NativeActionPackets.ModelAliases).Enumerate()));
        Assert.Equal(3_780, battle.Combatants.Single(actor => actor.Id == "recipient").CurrentHp);
        Assert.Equal(10_000, battle.Combatants.Single(actor => actor.Id == "caster").CurrentHp);
        Assert.Single(battle.Events, entry => entry.Kind == "Damage");
        BattleSession restored = BattleSession.Restore(battle.Capture());
        for (int frame = 0; frame < 60; frame++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Theory]
    [InlineData((byte)2)]
    [InlineData((byte)3)]
    public void Ai_reflection_clears_both_reused_single_action_headers(byte previousCastCount)
    {
        BattleSession battle = CreateAiAction(previousCastCount, 0x80, reflected: true);
        Assert.Equal((byte)2, battle.NativeActionPackets.ActionCount);
        Assert.Equal((byte)0, battle.NativeActionPackets.ExportRetained().Headers[0].ReadOriginalByte(17));
        Assert.Equal((byte)0, battle.NativeActionPackets.ExportRetained().Headers[1].ReadOriginalByte(17));
        Assert.Equal(new BattlePartyActorModel(new("recipient")), Assert.Single(NativeBattleActionGraphics.Normalize(battle.NativeActionPackets, new BattleActionHeaderReference(0)).TargetActors.Resolve(battle.NativeActionPackets.ModelAliases).Enumerate()));
        Assert.Equal(new BattleEnemyActorModel(new("caster")), Assert.Single(NativeBattleActionGraphics.Normalize(battle.NativeActionPackets, new BattleActionHeaderReference(1)).TargetActors.Resolve(battle.NativeActionPackets.ModelAliases).Enumerate()));
        BattleSession restored = BattleSession.Restore(battle.Capture());
        for (int frame = 0; frame < 60; frame++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Theory]
    [InlineData((byte)2, 0x20000u)]
    [InlineData((byte)2, 0x40000u)]
    [InlineData((byte)2, 0x60000u)]
    [InlineData((byte)3, 0x20000u)]
    [InlineData((byte)3, 0x40000u)]
    [InlineData((byte)3, 0x60000u)]
    [InlineData((byte)34, 0x20000u)]
    [InlineData((byte)34, 0x40000u)]
    [InlineData((byte)34, 0x60000u)]
    public void Recipient_multicast_status_does_not_change_incoming_contacts_or_rng(byte attackType, uint status)
    {
        BattleSession baseline = Create(0);
        BattleSession battle = Create(status);
        BattleSession restored = BattleSession.Restore(battle.Capture());
        ImmutableArray<NativeBattleContactPacket> expected = Resolve(baseline, attackType);
        ImmutableArray<NativeBattleContactPacket> actual = Resolve(battle, attackType);
        ImmutableArray<NativeBattleContactPacket> resumed = Resolve(restored, attackType);

        NativeBattleContactPacket contact = Assert.Single(actual);
        Assert.Equal(new BattlePartyActorModel(new("recipient")), contact.Primary.Recipient);
        Assert.Null(contact.Secondary);
        Assert.Equal(expected.ToArray(), actual.Select(packet => packet with { Primary = packet.Primary with { BattleStatuses8 = packet.Primary.BattleStatuses8 & ~0x60000u } }).ToArray());
        Assert.Equal(actual.ToArray(), resumed.ToArray());
        Assert.Equal(baseline.Combatants.Select(actor => actor.CurrentHp), battle.Combatants.Select(actor => actor.CurrentHp));
        Assert.Equal(10_000, battle.Combatants.Single(actor => actor.Id == "caster").CurrentHp);
        Assert.Equal(JsonSerializer.Serialize(baseline.Capture().Random), JsonSerializer.Serialize(battle.Capture().Random));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        Assert.Equal(status, battle.ReadStatusPresentation("recipient").Battle & 0x60000);
    }

    private static BattleSession Create(uint status)
    {
        return new(new BattleDefinition
        {
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            Combatants =
            [
                new() { Id = "recipient", Name = "Recipient", IsPlayer = true, MaximumHp = 10_000, CurrentHp = 5_000, NativeKillCount = 61, NativeStatus1 = status, NativeStatusResistances = new byte[40] },
                new() { Id = "caster", Name = "Caster", MaximumHp = 10_000, CurrentHp = 10_000, Magic = 20, NativeStatusResistances = new byte[40] }
            ]
        }, BattleAtbMode.Normal);
    }

    private static BattleSession CreateAiAction(byte previousCastCount, uint recipientStatus, bool reflected = false)
    {
        BattleNativeAction incoming = reflected ? Action(2) with { AttackFlags = 0x11 } : Action(34);
        NativeBattleAiDefinition ai = new([[0], [4, 0, 12, 0, 0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(8, 0, 1), 48).ToArray(), 0, []);
        BattleDefinition definition = Create(recipientStatus).Capture().Definition;
        BattleCombatantDefinition[] actors = definition.Combatants.ToArray();
        actors[1] = actors[1] with { InitialAtb = BattleSession.AtbMaximum, NativeAi = ai };
        BattleNativeAction[] actions = reflected ? [incoming, incoming with { Command = 247 }] : [incoming];
        BattleSession initial = new(definition with { Combatants = actors, NativeActions = actions, NativeCommandSequences = NativeBattleCommandSequenceFixture.Values }, BattleAtbMode.TurnBased);
        NativeBattleActionPacketsState packets = initial.NativeActionPackets;
        BattleModelOwner recipient = packets.ModelAliases.DecodeLiteral(0);
        BattleModelOwner caster = packets.ModelAliases.DecodeLiteral(3);
        for (int i = 0; i < previousCastCount; i++)
        {
            (packets, BattleActionHeaderReference header) = packets.Prepare(new(recipient, 2, 11, -1, 1, 0, null));
            (packets, _) = packets.AppendContact(new(new(i == 0 ? recipient : caster, 5, 0, 0, 0, i == 0 ? (ushort)77 : (ushort)99, 0), null));
            (packets, _) = packets.Publish(1);
            packets = packets.WritePlayerRepeatMode(header, previousCastCount);
        }
        packets = packets.ResetCounts();
        Assert.Equal(previousCastCount - 1, packets.ExportRetained().Headers[0].ReadOriginalByte(17));
        Assert.Equal(new[] { recipient, caster }, NativeBattleActionGraphics.Normalize(packets, new BattleActionHeaderReference(0)).TargetActors.Resolve(packets.ModelAliases).Enumerate());
        BattleSession battle = BattleSession.Restore(initial.Capture() with { NativeActionPackets = packets });
        for (int frame = 0; frame < 20 && battle.NativeActionPackets.ActionCount == 0; frame++)
        {
            battle.Tick();
        }
        return battle;
    }

    private static ImmutableArray<NativeBattleContactPacket> Resolve(BattleSession battle, byte attackType)
    {
        BattleNativeAction native = Action(attackType);
        BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == "caster");
        BattleCombatant recipient = battle.Combatants.Single(actor => actor.Id == "recipient");
        BattleActionPresentation command = new(caster.Id, recipient.Id, BattleActionPhase.Windup, 0, NativeBattleTargetRequestAdapter.Decode(1, BattleTargetEligibility.SavedStatus, BattleTargetReplacement.UseCurrentBattlePolicy, samplesOriginalSlots: true), BattleAbility.EnemyAbility, 1) { NativeAction = native, NativeQueuedCommand = 8 };
        BattleActionDefinition action = new(BattleAbility.EnemyAbility, 1, "Incoming action", BattleActionEffect.Native, 20, false) { NativeAction = native };
        typeof(BattleSession).GetMethod("BeginNativeReactionBatch", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, null);
        object produced = typeof(BattleSession).GetMethod("ProduceNativeInstalledContacts", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, [caster, recipient, command, action, null])!;
        return (ImmutableArray<NativeBattleContactPacket>)produced.GetType().GetProperty("Contacts")!.GetValue(produced)!;
    }

    private static BattleNativeAction Action(byte attackType)
    {
        return new(8, 1, 3, 1) { MagicId = 0, AttackType = attackType, Power = 20, HitCount = 1, TargetFlags = 0, AttackFlags = 1, DamagePresentation = 0, Element = 0, ElementPercent = 0, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = 255, Critical = 0, PowerModifier = 0, LevelModifier = 0, RawRecord = Enumerable.Repeat((byte)0, 20).ToImmutableArray(), SupplementalRecord = [] };
    }
}
