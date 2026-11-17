using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDiagnosticContactProvenanceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Guardian_recomputation_observes_exact_retained_contact_without_changing_engine_state(bool admitted)
    {
        BattleSession battle = Create();
        BattleSession unobserved = BattleSession.Restore(battle.Capture());
        if (admitted)
        {
            BattleActionPresentation command = new("party", "requested", BattleActionPhase.Recovery, new BattleTargetRequest(new BattleTargetSelection.Explicit([new BattleEnemyActorReference("requested")]), BattleTargetEligibility.SavedStatus, false, BattleTargetReplacement.None), BattleAbility.GuardianForce, 0);
            typeof(BattleSession).GetProperty(nameof(BattleSession.ActiveAction))!.SetValue(battle, command);
            typeof(BattleSession).GetProperty(nameof(BattleSession.ActiveAction))!.SetValue(unobserved, command);
        }
        typeof(BattleSession).GetField("diagnosticDispatchSerial", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(battle, 5L);
        NativeBattleRetainedActionPackets original = battle.Presentation!.NativeActionPackets.ExportRetained();
        NativeBattleRawContactPacket retained = Assert.IsType<NativeBattleRawContactPacket>(original.Contacts[0]);
        NativeBattleRetainedActionHeaderSlot header = original.Headers[0];
        List<BattleDiagnosticResolution> resolutions = [];
        battle.DiagnosticResolution += resolutions.Add;

        Recompute(battle);
        Recompute(unobserved);

        Assert.Equal(JsonSerializer.Serialize(unobserved.Capture()), JsonSerializer.Serialize(battle.Capture()));
        BattleDiagnosticResolution resolution = Assert.Single(resolutions);
        Assert.Equal("recipient", resolution.OriginalTargetId);
        Assert.Equal("recipient", resolution.ActualTargetId);
        if (!admitted)
        {
            Assert.Null(resolution.NativeContact);
            return;
        }
        BattleDiagnosticNativeContact proof = Assert.IsType<BattleDiagnosticNativeContact>(resolution.NativeContact);
        Assert.Equal(0x4850A0u, proof.ProducerAddress);
        Assert.Equal(5L, proof.DispatchSerial);
        Assert.Equal("requested", proof.SubmittedTargetId);
        Assert.Equal("party", proof.ActorId);
        Assert.Equal((byte)0, proof.ActorIndex);
        Assert.Equal("recipient", proof.RecipientId);
        Assert.Equal((byte)4, proof.RecipientIndex);
        Assert.Equal(0, proof.HeaderIndex);
        Assert.Equal(0, proof.FirstContactIndex);
        Assert.Equal(0, proof.RetainedContactIndex);
        Assert.Equal(header.Payload, proof.HeaderPayload);
        Assert.Equal(header.UnknownByteMask, proof.HeaderUnknownByteMask);
        Assert.Equal(NativeBattleContactPacketCodec.Encode(retained), proof.ContactPayload.ToArray());
        Assert.Equal((byte)4, NativeBattleContactPacketCodec.Decode(proof.ContactPayload.AsSpan()).Primary.ActorIndex);
        Assert.Equal(4, battle.ObserveDiagnosticCombatants().Single(actor => actor.Id == "recipient").NativeIndex);
        BattleDiagnosticResolution restored = JsonSerializer.Deserialize<BattleDiagnosticResolution>(JsonSerializer.Serialize(resolution))!;
        Assert.Equal(JsonSerializer.Serialize(resolution), JsonSerializer.Serialize(restored));
        JsonObject serializedProof = JsonNode.Parse(JsonSerializer.Serialize(proof))!.AsObject();
        foreach (string property in serializedProof.Select(pair => pair.Key))
        {
            JsonObject missingProperty = serializedProof.DeepClone().AsObject();
            missingProperty.Remove(property);
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<BattleDiagnosticNativeContact>(missingProperty.ToJsonString()));
        }
    }

    private static BattleSession Create()
    {
        BattleNativeAction native = new(254, 64, 2, 0) { MagicId = 116, AttackType = 11, Power = 44, HitCount = 1, TargetFlags = 72, AttackFlags = 33, DamagePresentation = 5, Element = 0, ElementPercent = null, Hit = null, Critical = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, PowerModifier = 0, LevelModifier = 60, RawRecord = ImmutableArray.CreateRange(new byte[132]), SupplementalRecord = [] };
        BattleActionDefinition action = new(BattleAbility.GuardianForce, 0, "Synthetic kernel-layout GF", BattleActionEffect.Native, 44, false, Level: 12, DamagePercent: 20) { NativeAction = native, NativeGuardianBoostAvailable = false };
        BattleSession battle = new(new() { NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(), NativeCommandSequences = NativeBattleCommandSequenceFixture.Values, NativeActions = [native], ActionDefinitions = [action], Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "requested", Name = "Requested", MaximumHp = 10000, CurrentHp = 10000 }, new() { Id = "recipient", Name = "Recipient", MaximumHp = 10000, CurrentHp = 10000, Spirit = 5 }] }, BattleAtbMode.TurnBased);
        BattleSnapshot saved = battle.Capture();
        NativeBattleActionPacketsState packets = saved.ReadFixturePresentation().NativeActionPackets!;
        (packets, _) = packets.Prepare(new(packets.ModelAliases.DecodeLiteral(0), 254, 14, -1, 64, 116, null));
        (packets, _) = packets.AppendContact(new(new(packets.ModelAliases.DecodeLiteral(4), 5, 1, 0, 0, 0, 0), null));
        (packets, _) = packets.Publish(1);
        return BattleSession.Restore(saved.WithFixturePresentation(facts => facts with { NativeActionPackets = packets }));
    }

    private static void Recompute(BattleSession battle)
    {
        typeof(BattleSession).GetMethod("RecomputeNativeGuardianContacts", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(battle, [(byte)100]);
    }
}
