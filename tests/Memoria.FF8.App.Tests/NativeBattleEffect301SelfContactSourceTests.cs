using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect301SelfContactSourceTests(ITestOutputHelper outputWriter)
{
    [Fact]
    public void Original_init_selector_allocated_self_source_keeps_current_status_cache_and_cold_next()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed resources for the original94 self-target component.");
        string output = Path.Combine(Path.GetTempPath(), "self-contact301-" + Guid.NewGuid().ToString("N"));
        string variable = "MEMORIA_FF8_EFFECT301_SOURCE_SNAPSHOT";
        string? previous = Environment.GetEnvironmentVariable(variable);
        try
        {
            NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(NativeBattleEffect301PresentationTests.CreateOriginalRequest(), directory!, output);
            Assert.NotEqual("blocked", exported.Status);
            string path = Path.Combine(output, "after-entry.battle.json");
            BattleSnapshot original = NativeBattleEffectFamilyRunner.LoadSnapshot(path);
            Environment.SetEnvironmentVariable(variable, path);
            using GameAssets game = new(directory!);
            using BattleAssets assets = new(directory!);
            BattleSnapshot source = NativeBattleEffect301PresentationTests.PrepareSource(game, assets, output);
            original = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output, "allocation-ready-source.battle.json"));
            NativeBattleActionPacketsState packets = source.NativeActionPackets!;
            int headerIndex = Assert.Single(Enumerable.Range(0, packets.ActionCount), index => NativeBattleActionGraphics.Word(packets.Headers[index], 6) == 301);
            NativeBattleActionHeaderSlot header = packets.Headers[headerIndex];
            byte actualCaster = header.ReadByte(0);
            Assert.InRange(actualCaster, (byte)3, (byte)6);
            Assert.Equal((byte)110, source.NativeGraphics!.Models[actualCaster].ModelId4);
            Assert.Equal((byte)8, header.ReadByte(1));
            Assert.Equal((byte)16, header.ReadByte(2));
            Assert.Equal((ushort)265, NativeBattleActionGraphics.Word(header, 4));
            Assert.Equal((ushort)301, NativeBattleActionGraphics.Word(header, 6));
            Assert.Equal((byte)1, header.ReadByte(16));
            NativeBattleContactPacket contact = packets.ReadContact(header.FirstContactIndex!.Value);
            BattleCombatantSnapshot self = source.Combatants.Single(actor => actor.NativeParticipant!.ActorIndex == actualCaster);
            Assert.Equal(actualCaster, contact.Primary.ActorIndex);
            Assert.Equal(self.NativeStatus!.Presentation.Saved, contact.Primary.SavedStatuses4);
            Assert.Equal(self.NativeStatus.Presentation.Battle, contact.Primary.BattleStatuses8);
            Assert.Equal((ushort)(1 << actualCaster), NativeBattleActionGraphics.Normalize(packets, headerIndex).TargetMask);
            Assert.True(source.NativeEffectMemory!.Effect301!.ScriptSourcePending);
            Assert.Equal(JsonSerializer.Serialize(original.NativeEffectMemory!.Effect301), JsonSerializer.Serialize(source.NativeEffectMemory.Effect301));
            string frozen = JsonSerializer.Serialize(source);
            BattleSession direct = BattleSession.Restore(source);
            BattleSession cold = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(frozen)!);
            direct.AttachNativeEffectResourceResolver(id => game.ReadNativeEffectCatalogForEffects([id])[id - 1]);
            cold.AttachNativeEffectResourceResolver(id => game.ReadNativeEffectCatalogForEffects([id])[id - 1]);
            List<BattleDiagnosticEffectDispatch> directDispatches = [];
            List<BattleDiagnosticEffectDispatch> coldDispatches = [];
            direct.DiagnosticEffectDispatched += dispatch => directDispatches.Add(dispatch);
            cold.DiagnosticEffectDispatched += dispatch => coldDispatches.Add(dispatch);
            using IDisposable scope = BattleSceneVisibleArea.Begin(BattleSceneVisibleArea.Native);
            direct.Tick();
            cold.Tick();
            Assert.Equal(JsonSerializer.Serialize(direct.Capture()), JsonSerializer.Serialize(cold.Capture()));
            Assert.Equal(source.Combatants.Select(actor => (actor.Id, actor.CurrentHp, actor.NativeStatus!.Saved, actor.NativeStatus.Battle)), direct.Capture().Combatants.Select(actor => (actor.Id, actor.CurrentHp, actor.NativeStatus!.Saved, actor.NativeStatus.Battle)));
            int callbacks = 1;
            while (callbacks < 360 && (direct.ObserveDiagnosticDrainBlockers().Count != 0 || cold.ObserveDiagnosticDrainBlockers().Count != 0))
            {
                direct.Tick();
                cold.Tick();
                callbacks++;
            }
            Assert.True(callbacks < 360, "The actual INIT-produced301 must finish all native and logical owners without a duplicate invocation.");
            Assert.Empty(direct.ObserveDiagnosticDrainBlockers());
            Assert.Empty(cold.ObserveDiagnosticDrainBlockers());
            BattleDiagnosticEffectDispatch actual = Assert.Single(directDispatches, dispatch => dispatch.ExecutedEffect == 301);
            outputWriter.WriteLine(JsonSerializer.Serialize(new { ActualDispatch = actual, Callbacks = callbacks, SourceCallback = source.NativeGraphics!.Callback, Blockers = direct.ObserveDiagnosticDrainBlockers() }));
            Assert.Equal((byte)0, actual.NativeAiCallbackStage);
            Assert.Equal((byte)8, actual.NativeCommand);
            Assert.Equal(265, actual.ActionId);
            Assert.Equal(actualCaster, actual.NativeActorIndex);
            Assert.Equal((ushort)(1 << actualCaster), actual.NativeTargetMask);
            Assert.Equal(headerIndex, actual.NativeHeaderIndex);
            Assert.Equal("2B42F29F6048B9B6304B866835D42B34A2D0A72D892DCA00B29FB39A2E2F2F47", actual.NativeAiScriptSha256, ignoreCase: true);
            Assert.Single(coldDispatches, dispatch => dispatch.ExecutedEffect == 301);
            Assert.Equal(JsonSerializer.Serialize(direct.Capture()), JsonSerializer.Serialize(cold.Capture()));
            Assert.Equal(0x103CFB8u, direct.NativeEffectMemory!.Effect301!.ReadDword(NativeBattleEffect301State.ScriptPointerAddress));
            Assert.False(direct.NativeEffectMemory.Effect301.ScriptSourcePending);
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
