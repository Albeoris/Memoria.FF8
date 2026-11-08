using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleComponentTerminalTextureTests(ITestOutputHelper output)
{
    [Fact]
    public void Original101_terminal_resident_texture_preserves_native_storage_and_independent_cold_next()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_COMPONENT_TERMINAL_SOURCE_SNAPSHOT");
        Assert.SkipUnless(directory is not null && path is not null, "Select original resources and an independent original101 component source for terminal texture validation.");
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        using GameAssets game = new(directory!);
        BattleSession battle = BattleSession.Restore(source);
        battle.AttachNativeEffectResourceResolver(id => game.ReadNativeEffectCatalogForEffects([id])[id - 1]);
        using IDisposable scope = BattleSceneVisibleArea.Begin(BattleSceneVisibleArea.Native);
        for (int callback = 0; callback < 900; callback++)
        {
            NativeBattleGraphicsState? beforeGraphics = battle.NativeGraphics;
            NativeBattleEffectMemory? beforeMemory = battle.NativeEffectMemory;
            BattleComponentTextureQuiescenceObservation? observation = battle.ObserveComponentTerminalTextures(101);
            Assert.Same(beforeGraphics, battle.NativeGraphics);
            Assert.Same(beforeMemory, battle.NativeEffectMemory);
            if (observation is not null)
            {
                Assert.True(observation.IsEligible);
                Assert.True(observation.Textures.IsTerminalQuiescent);
                Assert.Equal(new[] { "effect-or-auxiliary-task" }, battle.ObserveDiagnosticDrainBlockers());
                ObserveFingerprint("before-capture", battle, observation);
                BattleSnapshot terminal = battle.Capture();
                ObserveFingerprint("after-capture", battle, battle.ObserveComponentTerminalTextures(101));
                string frozen = JsonSerializer.Serialize(terminal);
                BattleSession direct = BattleSession.Restore(terminal);
                BattleSession cold = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(frozen)!);
                direct.AttachNativeEffectResourceResolver(id => game.ReadNativeEffectCatalogForEffects([id])[id - 1]);
                cold.AttachNativeEffectResourceResolver(id => game.ReadNativeEffectCatalogForEffects([id])[id - 1]);
                ObserveFingerprint("direct-restore-before-tick", direct, direct.ObserveComponentTerminalTextures(101));
                ObserveFingerprint("json-restore-before-tick", cold, cold.ObserveComponentTerminalTextures(101));
                byte[] vram = terminal.NativeEffectMemory!.Textures!.ReadVram();
                string auxiliary = JsonSerializer.Serialize(terminal.NativeGraphics!.Auxiliary);
                for (int tail = 0; tail < 3; tail++)
                {
                    direct.Tick();
                    cold.Tick();
                    BattleComponentTextureQuiescenceObservation next = Assert.IsType<BattleComponentTextureQuiescenceObservation>(direct.ObserveComponentTerminalTextures(101));
                    ObserveFingerprint("direct-after-tick-" + tail, direct, next);
                    ObserveFingerprint("json-after-tick-" + tail, cold, cold.ObserveComponentTerminalTextures(101));
                    Assert.Equal(observation.ResidentFingerprint, next.ResidentFingerprint);
                    Assert.True(next.GraphicsCallback > observation.GraphicsCallback);
                    BattleSnapshot nextState = direct.Capture();
                    Assert.Equal(JsonSerializer.Serialize(nextState), JsonSerializer.Serialize(cold.Capture()));
                    Assert.Equal(auxiliary, JsonSerializer.Serialize(nextState.NativeGraphics!.Auxiliary));
                    Assert.Equal(vram, nextState.NativeEffectMemory!.Textures!.ReadVram());
                    Assert.Equal(new[] { "effect-or-auxiliary-task" }, direct.ObserveDiagnosticDrainBlockers());
                }
                Assert.Equal(frozen, JsonSerializer.Serialize(terminal));
                return;
            }
            battle.Tick();
        }
        Assert.Fail("The selected original101 component did not reach its terminal resident texture boundary within the bounded callback window.");
    }

    private void ObserveFingerprint(string boundary, BattleSession battle, BattleComponentTextureQuiescenceObservation? observation)
    {
        NativeBattleGraphicsState graphics = battle.NativeGraphics!;
        NativeBattleActionPacketsState packets = battle.NativeActionPackets;
        object[] rows = observation?.Textures.Tasks.Select(row =>
        {
            NativeBattleAuxiliaryTask task = graphics.Auxiliary.Slots[row.Slot];
            NativeBattleGraphicsModel model = graphics.Models[row.ModelIndex!.Value];
            return (object)new
            {
                row.Slot,
                task.Flags,
                task.State,
                task.Next,
                task.Kind,
                task.UnknownByteMask,
                Payload = Convert.ToHexString(task.Payload.AsSpan()),
                ProgramOffset = task.Program?.Offset,
                ProgramLength = task.Program?.Source.Length,
                ProgramSha256 = task.Program is { } program ? Convert.ToHexStringLower(SHA256.HashData(program.Source.AsSpan())) : null,
                model.Index,
                model.Flags0,
                model.ResourceFlags44,
                model.Sequence,
                model.ActiveResourceRow100,
                model.ActiveStagedResourceRow100,
                model.ResourceMagicId,
                BodyPaletteRows = model.BodyTextureSlots is { PaletteRows.IsDefault: false } binding ? binding.PaletteRows.ToArray() : null
            };
        }).ToArray() ?? [];
        output.WriteLine(JsonSerializer.Serialize(new { Boundary = boundary, Eligible = observation?.IsEligible, GraphicsCallback = observation?.GraphicsCallback, ResidentFingerprint = observation?.ResidentFingerprint, graphics.Auxiliary.Head, graphics.Auxiliary.Tail, packets.ActionCount, packets.ContactCount, packets.ActiveHeaderIndex, packets.ActiveContactIndex, Rows = rows }));
    }
}
