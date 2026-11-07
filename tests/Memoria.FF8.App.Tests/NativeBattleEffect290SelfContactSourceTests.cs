using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect290SelfContactSourceTests
{
    [Fact]
    public void Original_death_selector_self_source_keeps_current_status_cache_and_cold_next()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed resources for the original109 self-target component.");
        string output = Path.Combine(Path.GetTempPath(), "self-contact290-" + Guid.NewGuid().ToString("N"));
        string variable = "MEMORIA_FF8_EFFECT290_SOURCE_SNAPSHOT";
        string? previous = Environment.GetEnvironmentVariable(variable);
        try
        {
            NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(NativeBattleEffect290PresentationTests.CreateOriginalRequest(), directory!, output);
            Assert.NotEqual("blocked", exported.Status);
            string path = Path.Combine(output, "after-entry.battle.json");
            BattleSnapshot original = NativeBattleEffectFamilyRunner.LoadSnapshot(path);
            Environment.SetEnvironmentVariable(variable, path);
            using GameAssets game = new(directory!);
            using BattleAssets assets = new(directory!);
            BattleSnapshot source = NativeBattleEffect290PresentationTests.PrepareSource(game, assets, output);
            NativeBattleActionPacketsState packets = source.NativeActionPackets!;
            int headerIndex = original.NativeActionPackets!.ActionCount;
            NativeBattleActionHeaderSlot header = packets.Headers[headerIndex];
            Assert.Equal((byte)3, header.ReadByte(0));
            Assert.Equal((byte)8, header.ReadByte(1));
            Assert.Equal((byte)14, header.ReadByte(2));
            Assert.Equal((ushort)291, NativeBattleActionGraphics.Word(header, 4));
            Assert.Equal((ushort)290, NativeBattleActionGraphics.Word(header, 6));
            Assert.Equal((byte)1, header.ReadByte(16));
            NativeBattleContactPacket contact = packets.ReadContact(header.FirstContactIndex!.Value);
            BattleCombatantSnapshot self = original.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == 3);
            Assert.Equal((byte)3, contact.Primary.ActorIndex);
            Assert.Equal((ushort)0, contact.Primary.Amount6);
            Assert.Equal(self.NativeStatus!.Presentation.Saved, contact.Primary.SavedStatuses4);
            Assert.Equal(self.NativeStatus.Presentation.Battle, contact.Primary.BattleStatuses8);
            Assert.Equal((ushort)8, NativeBattleActionGraphics.Normalize(packets, headerIndex).TargetMask);
            Assert.Equal(JsonSerializer.Serialize(original.Combatants), JsonSerializer.Serialize(source.Combatants));
            string frozen = JsonSerializer.Serialize(source);
            BattleSession direct = BattleSession.Restore(source);
            BattleSession cold = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(frozen)!);
            direct.AttachNativeEffectResourceResolver(id => game.ReadNativeEffectCatalogForEffects([id])[id - 1]);
            cold.AttachNativeEffectResourceResolver(id => game.ReadNativeEffectCatalogForEffects([id])[id - 1]);
            using IDisposable scope = BattleSceneVisibleArea.Begin(BattleSceneVisibleArea.Native);
            direct.Tick();
            cold.Tick();
            Assert.Equal(JsonSerializer.Serialize(direct.Capture()), JsonSerializer.Serialize(cold.Capture()));
            Assert.Equal(source.Combatants.Select(actor => (actor.Id, actor.CurrentHp, actor.NativeStatus!.Saved, actor.NativeStatus.Battle)), direct.Capture().Combatants.Select(actor => (actor.Id, actor.CurrentHp, actor.NativeStatus!.Saved, actor.NativeStatus.Battle)));
            Assert.Equal(frozen, JsonSerializer.Serialize(source));
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, previous);
            if (Directory.Exists(output))
            {
                Directory.Delete(output, true);
            }
        }
    }
}
