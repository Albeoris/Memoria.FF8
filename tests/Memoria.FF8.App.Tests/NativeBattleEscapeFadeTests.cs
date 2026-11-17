using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class NativeBattleEscapeFadeTests
{
    [Theory]
    [InlineData(480, 320)]
    [InlineData(640, 360)]
    public void Expanded_escape_fade_uses_the_original_packets_and_survives_json(int width, int height)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleDefinition definition = new()
        {
            StageId = 0,
            ExpandedFormationFlags = 0,
            InitialRandom = NativeBattleRandomState.ForecastPhase(1),
            Combatants =
            [
                .. Enumerable.Range(0, 11).Select(index => new BattleCombatantDefinition { Id = $"ally-{index}", Name = $"Ally {index}", IsPlayer = true, ModelId = index, Speed = 1, InitialAtb = BattleSession.AtbMaximum }),
                .. Enumerable.Range(0, 11).Select(index => new BattleCombatantDefinition { Id = $"enemy-{index}", Name = $"Enemy {index}", ModelId = 1, SceneSlot = index, Speed = 1, Information254 = 0 })
            ]
        };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        BattleScenePresentation scene = new(assets, definition);
        battle.PublishNativeEscapeButtons(3);
        for (int frame = 0; frame < 60; frame++)
        {
            battle.Tick();
        }
        Assert.Equal((byte)40, battle.Escape.ExpandedOutcomeFrames);
        Assert.Null(battle.ExpandedEscapeScreenFade);
        battle.PublishNativeEscapeButtons(0);
        for (int frame = 0; frame < 26; frame++)
        {
            battle.Tick();
        }
        Assert.Equal((byte)14, battle.Escape.ExpandedOutcomeFrames);
        Assert.Null(battle.ExpandedEscapeScreenFade);
        battle.Tick();
        Assert.Equal((byte?)128, battle.ExpandedEscapeScreenFade);
        for (int frame = 0; frame < 6; frame++)
        {
            battle.Tick();
        }
        Assert.Equal((byte?)68, battle.ExpandedEscapeScreenFade);

        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!;
        BattleSession restored = BattleSession.Restore(snapshot);
        Assert.Equal(battle.ExpandedEscapeScreenFade, restored.ExpandedEscapeScreenFade);
        battle.Tick();
        restored.Tick();
        Assert.Equal((byte?)59, restored.ExpandedEscapeScreenFade);
        byte[] faded = scene.Render(battle, width, height).ToArray();
        Assert.Equal(faded, new BattleScenePresentation(assets, definition).Render(restored, width, height).ToArray());

        var fade = NativeBattleOutcomeCallbacks.PrepareFade501D10(restored.ExpandedEscapeScreenFade!.Value, assets.ReadNativePrimitiveTables());
        var quad = Assert.Single(fade.LastCommands, command => command.Pass == 2 && command.Command.Opcode == 0x2A);
        Assert.Equal(2, quad.DrawMode.BlendMode);
        Assert.Equal(NativeBattleScreenCoverage.Full, quad.Command.ScreenCoverage);
        Assert.Equal(new uint[] { 0x2A454545, 0, 320, 216u << 16, 320 | 216u << 16 }, quad.Command.Words);

        // Keep the same scene pose while examining whether the fade reached the viewport edges.
        BattleSession beforeFade = BattleSession.Restore(restored.Capture() with { Escape = restored.Escape with { ExpandedOutcomeFrames = 14 } });
        byte[] before = scene.Render(beforeFade, width, height).ToArray();
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        scene.Render(restored, width, height, gpu);
        Microsoft.Xna.Framework.Color[] gpuPixels = GpuTestDevice.Read(gpu.Target);
        foreach (int pixel in new[] { 0, width - 1, width * (height - 1), width * height - 1 })
        {
            Assert.True(faded[pixel * 4] < before[pixel * 4]);
            Assert.InRange(Math.Abs(faded[pixel * 4] - gpuPixels[pixel].R), 0, 1);
            Assert.InRange(Math.Abs(faded[pixel * 4 + 1] - gpuPixels[pixel].G), 0, 1);
            Assert.InRange(Math.Abs(faded[pixel * 4 + 2] - gpuPixels[pixel].B), 0, 1);
        }

        for (int frame = 0; frame < 6; frame++)
        {
            restored.Tick();
        }
        Assert.Equal((byte?)0, restored.ExpandedEscapeScreenFade);
        Assert.Equal(BattleOutcome.Escaped, restored.Outcome);
        Assert.Equal((byte?)2, restored.NativeResult);
        Assert.True(restored.CanReturnToField);
    }
}
