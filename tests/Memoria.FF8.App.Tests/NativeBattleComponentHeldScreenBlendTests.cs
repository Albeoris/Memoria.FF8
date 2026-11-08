using System.Globalization;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleComponentHeldScreenBlendTests(ITestOutputHelper output)
{
    [Fact]
    public void Original_component_holds_its_native_overlay_and_preserves_independent_cold_next()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_COMPONENT_HELD_SOURCE_SNAPSHOT");
        string? selected = Environment.GetEnvironmentVariable("MEMORIA_FF8_COMPONENT_EFFECT_ID");
        Assert.SkipUnless(directory is not null && path is not null && selected is not null, "Select installed resources, one standalone component and its expected native effect.");
        ushort effect = ushort.Parse(selected!, CultureInfo.InvariantCulture);
        BattleSnapshot source = NativeBattleEffectFamilyRunner.LoadSnapshot(path!);
        using GameAssets game = new(directory!);
        BattleSession battle = BattleSession.Restore(source);
        Attach(battle);
        using IDisposable scope = BattleSceneVisibleArea.Begin(BattleSceneVisibleArea.Native);
        for (int callback = 0; callback < 1400; callback++)
        {
            NativeBattleGraphicsState? beforeGraphics = battle.NativeGraphics;
            NativeBattleEffectMemory? beforeMemory = battle.NativeEffectMemory;
            BattleComponentHeldScreenBlendObservation? observation = battle.ObserveComponentHeldScreenBlend(effect);
            Assert.Same(beforeGraphics, battle.NativeGraphics);
            Assert.Same(beforeMemory, battle.NativeEffectMemory);
            if (observation is not null)
            {
                Assert.True(observation.IsEligible);
                Assert.True(observation.ScreenBlend.IsHeld);
                Assert.Equal(new[] { "effect-or-auxiliary-task" }, battle.ObserveDiagnosticDrainBlockers());
                BattleSnapshot terminal = battle.Capture();
                string frozen = JsonSerializer.Serialize(terminal);
                BattleSession direct = BattleSession.Restore(terminal);
                BattleSession cold = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(frozen)!);
                Attach(direct);
                Attach(cold);
                string auxiliary = JsonSerializer.Serialize(terminal.NativeGraphics!.Auxiliary);
                for (int tail = 0; tail < 3; tail++)
                {
                    direct.Tick();
                    cold.Tick();
                    BattleComponentHeldScreenBlendObservation next = Assert.IsType<BattleComponentHeldScreenBlendObservation>(direct.ObserveComponentHeldScreenBlend(effect));
                    Assert.Equal(observation.ResidentFingerprint, next.ResidentFingerprint);
                    Assert.True(next.GraphicsCallback > observation.GraphicsCallback);
                    BattleSnapshot state = direct.Capture();
                    Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(cold.Capture()));
                    Assert.Equal(auxiliary, JsonSerializer.Serialize(state.NativeGraphics!.Auxiliary));
                    Assert.Equal(state.NativeEffectMemory!.Textures!.ReadVram(), cold.NativeEffectMemory!.Textures!.ReadVram());
                    Assert.Equal(new[] { "effect-or-auxiliary-task" }, direct.ObserveDiagnosticDrainBlockers());
                }
                Assert.Equal(frozen, JsonSerializer.Serialize(terminal));
                output.WriteLine(JsonSerializer.Serialize(new { Effect = effect, observation.GraphicsCallback, observation.ScreenBlend.Elapsed, observation.ScreenBlend.Duration, observation.ResidentFingerprint }));
                return;
            }
            battle.Tick();
        }
        Assert.Fail("The selected original component did not reach its native held-screen boundary within the bounded callback window.");

        void Attach(BattleSession active)
        {
            active.AttachNativeEffectResourceResolver(id => game.ReadNativeEffectCatalogForEffects([id])[id - 1]);
        }
    }
}
