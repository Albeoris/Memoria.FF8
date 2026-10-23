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
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "fixture", "queued-action104.battle.json"));
        Assert.Equal(new[] { 0, 1, 2 }, source.Definition.Combatants.Where(actor => actor.IsPlayer).Select(actor => actor.ModelId).ToArray());
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([190])[189];
        Assert.Equal(NativeBattleChainGun190Profile.Entry, binding.Entry);
        Assert.Equal(NativeBattleChainGun190Profile.Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag149.tim", NativeBattleChainGun190Profile.InitializerGlobal), binding.InitializerResource);
        Assert.Equal(68128, binding.InitializerAsset!.Data.Length);
        Assert.Same(game.ReadNativeChainGun190Resources(), binding.ChainGun190Resources);
        NativeBattleActionPacketsState packets = source.NativeActionPackets!;
        Assert.Equal((byte)1, packets.ActionCount);
        Assert.Equal((byte)1, packets.ContactCount);
        NativeBattleActionHeaderSlot header = packets.Headers[0];
        Assert.Equal(0, header.FirstContactIndex);
        Assert.Equal((byte)3, header.ReadByte(0));
        Assert.Equal((byte)8, header.ReadByte(1));
        Assert.Equal((byte)14, header.ReadByte(2));
        NativeBattleContactPacket first = packets.ReadContact(0);
        Assert.Equal((byte)0, first.Primary.ActorIndex);
        Assert.Equal((ushort)7, first.Primary.Amount6);
        byte[] coldThird = source.Definition.NativeRetainedMemory!.ActionPackets.ReadContact(2).Encode();
        Assert.Equal(new byte[24], coldThird);
        Assert.Equal(coldThird, packets.ReadContact(2).Encode());
        BattleCombatantSnapshot[] actors = source.Combatants;
        for (int actor = 1; actor < contactCount; actor++)
        {
            BattleCombatantSnapshot recipient = actors.Single(item => item.NativeParticipant!.ActorIndex == actor);
            NativeBattleStatusSnapshot status = recipient.NativeStatus!;
            actors = actors.Select(item => item.Id == recipient.Id ? item with { NativeStatus = status with { Presentation = new(status.Saved, status.Battle) } } : item).ToArray();
            NativeBattleContactPacket declared = first with { Primary = first.Primary with { ActorIndex = checked((byte)actor), SavedStatuses4 = status.Saved, BattleStatuses8 = status.Battle } };
            (packets, int index) = packets.AppendContact(declared);
            Assert.Equal(actor, index);
        }
        header = header with { Payload = header.Payload.SetItem(16, checked((byte)contactCount)) };
        packets = packets with { Headers = packets.Headers.SetItem(0, header) };
        packets.Validate();
        Assert.Equal(contactCount, packets.ReadHeaderContacts506BA0(0).Length);
        if (contactCount == 2)
        {
            Assert.Equal(coldThird, packets.ReadContact(2).Encode());
        }
        File.WriteAllText(Path.Combine(output, "controlled-contacts.json"), JsonSerializer.Serialize(new { Scenario = "Original ChainGun190 command8/action154/sequence14 and formation155; original party models0/1/2 loaded through the fixture pipeline; explicitly declared seven-HP counted presentation contacts with logical HP/status retained, no ordinary AOE formula or AI admission", Count = contactCount, Count2ThirdPhysicalOwner = contactCount == 2 ? "Retained actual loader-backed inactive contact ordinal2, untouched by this fixture" : null, ColdThird = coldThird, PhysicalThird = packets.ReadContact(2), Contacts = packets.ReadHeaderContacts506BA0(0) }));
        return source with { Combatants = actors, NativeActionPackets = packets };
    }
}
