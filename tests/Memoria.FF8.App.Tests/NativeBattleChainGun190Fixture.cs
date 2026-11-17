using static Memoria.FF8.App.Tests.NativeBattleOriginalModelAssertions;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class NativeBattleChainGun190Fixture
{
    internal static BattleSnapshot Prepare(GameAssets game, BattleAssets assets, int contactCount, string output)
    {
        Assert.Contains(contactCount, new[] { 2, 3 });
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 8 && row.ActionId == 154 && row.MagicId == 190 && row.KernelSection == 3 && row.KernelIndex == 154);
        BattleEncounter formation = assets.LoadEncounter(155);
        Assert.Equal(40, formation.StageId);
        BattleEnemyPlacement caster = Assert.Single(NativeBattleEffectFixtureExporter.SelectEnemies(formation, true));
        Assert.Equal(0, caster.Slot);
        Assert.Equal(8, caster.ModelId);
        Assert.Equal(255, caster.Level);
        Assert.Equal(new NativeBattleVector(0, 0, -700), caster.NativePosition);
        Assert.False(caster.Hidden);
        Assert.False(caster.InitiallySuspended);
        Assert.False(caster.Untargetable);
        Assert.Equal((byte)14, NativeBattleEffectFixtureExporter.SelectEnemySequence(assets.ReadEnemyAi(caster.ModelId, caster.Level), action));
        NativeEffectFixtureRequest request = new("chain-gun190-count" + contactCount, 190, 8, 154, 0, 800, 240) { EncounterId = formation.Id, CasterActorIndex = 3, PreserveOriginalFormationEnemies = true, ControlledContactValue = 7, AdditionalPartyModelIds = [1, 2] };
        NativeBattleEffectFixtureExporter.Validate(request);
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? throw new InvalidDataException("Select installed original resources."), Path.Combine(output, "fixture"));
        Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        BattleSnapshot source = NativeBattleFixtureInspection.ReadLogicalSnapshot(Path.Combine(output, "fixture", "queued-action104.battle.json"));
        Assert.Equal(new[] { 0, 1, 2 }, source.Definition.Combatants.Where(actor => actor.IsPlayer).Select(actor => actor.ModelId).ToArray());
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([190])[189];
        Assert.Equal(NativeBattleChainGun190Profile.Entry, binding.Entry);
        Assert.Equal(NativeBattleChainGun190Profile.Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag149.tim", NativeBattleChainGun190Profile.InitializerGlobal), binding.InitializerResource);
        Assert.Equal(68128, binding.InitializerAsset!.Data.Length);
        Assert.Same(game.ReadNativeChainGun190Resources(), binding.ChainGun190Resources);
        NativeBattleActionPacketsState packets = source.ReadFixturePresentation().NativeActionPackets!;
        Assert.Equal((byte)1, packets.ActionCount);
        Assert.Equal((byte)1, packets.ContactCount);
        NativeBattleActionHeaderSlot header = packets.ReadHeader(new BattleActionHeaderReference(0));
        Assert.Equal(0, header.FirstContact.Ordinal);
        Assert.Equal((byte)3, ReadOriginalModelOperand(packets.ModelAliases, header.Source));
        Assert.Equal((byte)8, header.Command);
        Assert.Equal((byte)14, header.Sequence);
        NativeBattleContactPacket first = packets.ReadContact(new BattleContactOccurrence(0));
        Assert.Equal((byte)0, ReadOriginalModelOperand(packets.ModelAliases, first.Primary.Recipient));
        Assert.Equal((ushort)7, first.Primary.Amount6);
        byte[] coldThird = NativeBattleContactPacketCodec.Encode(source.Definition.NativeRetainedMemory!.ActionPackets.Contacts[2]!);
        Assert.Equal(new byte[24], coldThird);
        Assert.Equal(coldThird, NativeBattleContactPacketCodec.Encode(packets.ExportRetained().Contacts[2]!));
        Assert.Null(packets.ActiveHeader);
        packets = NativeBattleActionPacketsState.Create(packets.Roster, packets.ExportRetained(), packets.ModelAliases);
        (packets, BattleActionHeaderReference reference) = packets.Prepare(new(header.Source, header.Command, header.Sequence, header.CameraChoice, header.Action, header.Effect, header.Data));
        (packets, _) = packets.AppendContact(first);
        BattleCombatantSnapshot[] actors = source.Combatants;
        for (int actor = 1; actor < contactCount; actor++)
        {
            BattleCombatantSnapshot recipient = actors.Single(item => item.NativeParticipant!.OriginalSlot?.Offset == actor);
            NativeBattleStatusSnapshot status = recipient.NativeStatus!;
            actors = actors.Select(item => item.Id == recipient.Id ? item with { NativeStatus = status with { Presentation = new(status.Saved, status.Battle) } } : item).ToArray();
            NativeBattleContactPacket declared = first with { Primary = first.Primary with { Recipient = packets.ModelAliases.DecodeLiteral(checked((byte)actor)), SavedStatuses4 = status.Saved, BattleStatuses8 = status.Battle } };
            (packets, BattleContactOccurrence occurrence) = packets.AppendContact(declared);
            Assert.Equal(new BattleContactOccurrence(actor), occurrence);
        }
        (packets, _) = packets.Publish(contactCount);
        packets.Validate();
        Assert.Equal(contactCount, packets.ReadHeaderContacts(reference).Length);
        if (contactCount == 2)
        {
            Assert.Equal(coldThird, NativeBattleContactPacketCodec.Encode(packets.ExportRetained().Contacts[2]!));
        }
        File.WriteAllText(Path.Combine(output, "controlled-contacts.json"), JsonSerializer.Serialize(new { Scenario = "Original ChainGun190 command8/action154/sequence14 and formation155; original party models0/1/2 loaded through the fixture pipeline; explicitly declared seven-HP counted presentation contacts with logical HP/status retained, no ordinary AOE formula or AI admission", Count = contactCount, Count2ThirdPhysicalOwner = contactCount == 2 ? "Retained actual loader-backed inactive contact ordinal2, untouched by this fixture" : null, ColdThird = coldThird, PhysicalThird = packets.ExportRetained().Contacts[2], Contacts = packets.ReadHeaderContacts(reference) }));
        return (source with { Combatants = actors}).WithFixturePresentation(facts => facts with { NativeActionPackets = packets });
    }
}
