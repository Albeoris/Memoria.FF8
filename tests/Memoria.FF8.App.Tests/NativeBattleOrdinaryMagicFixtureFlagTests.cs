using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleOrdinaryMagicFixtureFlagTests
{
    [Fact]
    public void Original_enemy_flags_survive_ordinary_admission_and_a_cold_next_callback()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? selectedOutput = Environment.GetEnvironmentVariable("MEMORIA_FF8_ORDINARY_FIXTURE_FLAGS_OUTPUT");
        Assert.SkipUnless(directory is not null && selectedOutput is not null, "Select installed resources and a fresh external fixture output directory.");
        string output = Path.GetFullPath(selectedOutput!);
        Assert.False(Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any());
        Directory.CreateDirectory(output);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleNativeAction action = Assert.Single(assets.NativeActions, row => row.Command == 2 && row.MagicId == 223);
        string fixture = Path.Combine(output, "input");
        NativeEffectFixtureRequest request = new("ordinary-flags", 223, 2, action.ActionId, 3, 800, 240) { OrdinaryMagicAdmission = true, AdmissionProbeMaximumCallbacks = 800 };
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, directory!, fixture);
        Assert.True(exported.Status != "blocked", exported.FirstBlocker);
        BattleSnapshot queued = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(fixture, "ordinary-produced-action104.battle.json"));
        object report = VerifyBoundaries(fixture, queued, assets, game, action);
        File.WriteAllText(Path.Combine(output, "fixture-flags.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        DeleteFixtureSnapshots(fixture, exported);
    }

    internal static object VerifyBoundaries(string fixture, BattleSnapshot queued, BattleAssets assets, GameAssets game, BattleNativeAction action)
    {
        BattleSnapshot beforeEntry = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(fixture, "before-entry.battle.json"));
        BattleSnapshot beforeInput = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(fixture, "before-ordinary-input.battle.json"));
        BattleCombatantSnapshot initialEnemy = beforeEntry.Combatants.Single(actor => actor.NativeParticipant?.ActorIndex == 3);
        BattleCombatantDefinition enemyDefinition = beforeEntry.Definition.Combatants.Single(actor => actor.Id == initialEnemy.Id);
        uint originalFlags = assets.ReadEnemyNativeCombatantFlags(enemyDefinition.ModelId);
        Assert.Equal(originalFlags, enemyDefinition.NativeCombatantFlags);
        NativeBattleParticipantSnapshot participant = initialEnemy.NativeParticipant!;
        Assert.True(participant.Active);
        uint entryFlags = participant.NativeCombatantFlags!.Value;
        Assert.Equal(originalFlags, entryFlags & ~0xC3u);
        NativeBattleAtbState gauge = initialEnemy.NativeAtb!;
        NativeBattleStatusSnapshot status = initialEnemy.NativeStatus!;
        NativeBattleGaugeRegistration registration = NativeBattleGaugeRegistrations.Evaluate4842B0(gauge with { Current = gauge.Maximum }, entryFlags, status.Saved, status.Battle);
        Assert.Equal(NativeBattleGaugeRegistrationKind.DeferredScript, registration.Kind);
        foreach (BattleSnapshot boundary in new[] { beforeInput, queued })
        {
            BattleCombatantSnapshot enemy = boundary.Combatants.Single(actor => actor.Id == initialEnemy.Id);
            Assert.Equal(originalFlags, enemy.NativeParticipant!.NativeCombatantFlags!.Value & originalFlags);
            NativeBattleUiServices ui = boundary.NativeGraphics!.UiServices!;
            Assert.All(ui.Commands.Queue, index => Assert.InRange(ui.Commands.Packets[index].Bytes[0], (byte)0, (byte)2));
        }
        string party = queued.NativeGraphics!.ActorIds[0]!;
        Assert.Equal(2, beforeInput.Combatants.Single(actor => actor.Id == party).NativeMagicInventory!.Single(stock => stock.SpellId == action.ActionId).Count);
        Assert.Equal(1, queued.Combatants.Single(actor => actor.Id == party).NativeMagicInventory!.Single(stock => stock.SpellId == action.ActionId).Count);
        int descriptor = Assert.Single(queued.NativeGraphics.Descriptors.Queue, index => queued.NativeGraphics.Descriptors.Packets[index].Command == 104);
        uint offset = queued.NativeGraphics.Descriptors.Packets[descriptor].Argument - 0x1D280C4u;
        Assert.Equal(0u, offset % 20);
        NativeBattleActionHeaderSlot header = queued.NativeActionPackets!.Headers[checked((int)(offset / 20))];
        Assert.Equal((byte)2, header.ReadByte(1));
        Assert.Equal(action.ActionId, NativeBattleActionGraphics.Word(header, 4));
        Assert.Equal((ushort)223, NativeBattleActionGraphics.Word(header, 6));
        Assert.Equal((byte)10, header.ReadByte(0x10));
        string frozen = JsonSerializer.Serialize(queued);
        BattleSession direct = BattleSession.Restore(queued);
        BattleSession cold = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(frozen)!);
        direct.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        cold.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        direct.Tick();
        cold.Tick();
        Assert.Equal(JsonSerializer.Serialize(direct.Capture()), JsonSerializer.Serialize(cold.Capture()));
        Assert.Equal(frozen, JsonSerializer.Serialize(queued));
        BattleCombatant nextEnemy = direct.Combatants.Single(actor => actor.Id == initialEnemy.Id);
        Assert.Equal(originalFlags, nextEnemy.NativeCombatantFlags & originalFlags);
        return new { enemyDefinition.ModelId, physicalSlot = 3, originalFlags, entryFlags, fullGaugeRegistration = registration.Kind.ToString(), beforeInput = beforeInput.NativeGraphics!.Callback, produced = queued.NativeGraphics.Callback, next = direct.NativeGraphics!.Callback, action.Command, action.ActionId, contacts = header.ReadByte(0x10), stockBefore = 2, stockAfter = 1, coldNextEqual = true };
    }

    internal static void DeleteFixtureSnapshots(string fixture, NativeEffectFixtureExportResult exported)
    {
        foreach (NativeEffectFixtureArtifact artifact in exported.Artifacts)
        {
            string path = Path.GetFullPath(Path.Combine(fixture, artifact.File));
            Assert.StartsWith(fixture + Path.DirectorySeparatorChar, path, StringComparison.OrdinalIgnoreCase);
            File.Delete(path);
        }
    }
}
