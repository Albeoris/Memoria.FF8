using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect233ProjectionResourceTests
{
    [Fact]
    public void Original_wave_mesh_particles_and_shadow_keep_projection_limits_after_json_continuation()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT233_SOURCE_SNAPSHOT");
        Assert.SkipUnless(directory is not null && source is not null, "Select original resources and the earthquake component fixture.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(directory!);
        BattleSnapshot initial = NativeBattleEffectFamilyRunner.LoadSnapshot(source!);
        NativeBattleEffect233SourceAdmissionTests.ValidateSource(initial, assets);
        BattleSession battle = NativeBattleEffect233PresentationTests.Restore(NativeBattleEffect233PresentationTests.QueueComponent(initial, game, assets), game);
        BattleScenePresentation scene = new(assets, initial.Definition, true) { SmoothCompatibleMotion = true };
        HashSet<byte> saturatedProducers = [];
        bool restored = false;
        int groundQuads = 0;
        int callbacks = 0;
        for (; callbacks < 180; callbacks++)
        {
            battle.Tick();
            NativeBattlePreparedPresentation prepared = battle.PreparedPresentation!;
            NativeBattleLogicalCommand[] commands = ModuleCommands(prepared);
            foreach (NativeBattleLogicalCommand command in commands)
            {
                NativeBattlePilotVisualGeometry geometry = command.PilotGeometry!;
                Assert.True(geometry.ProjectionScreenBounds == new Vector4(-8192, -8192, 8184, 8184), $"Callback {callbacks}, opcode {command.Opcode:X2}, {geometry.Identity} lacks its module projection bounds.");
                NativeBattleCameraProjection projection = geometry.FocalLengthOverride is ushort focal ? prepared.PilotProjection! with { FocalLength = focal } : prepared.PilotProjection!;
                for (int corner = 0; corner < geometry.CornerCount; corner++)
                {
                    Vector3 point = NativeBattlePilotVisualGeometry.Project(geometry.Corner(corner), projection);
                    Assert.True(float.IsFinite(point.X) && float.IsFinite(point.Y));
                    Assert.True(point.Z < ushort.MaxValue, "This fixture must exercise screen saturation without the separate far-depth boundary.");
                    if (point.X < -8192 || point.X > 8184 || point.Y < -8192 || point.Y > 8184)
                    {
                        saturatedProducers.Add((byte)(command.Opcode & 0xFC));
                    }
                }
            }
            groundQuads = Math.Max(groundQuads, commands.Where(command => command.PilotGeometry!.ScreenScale == 0.125f).Select(command => command.PilotGeometry!.Identity).Distinct().Count());
            byte[] pixels = scene.Render(battle, 320, 224, null, 0.5f);
            if (callbacks is 28 or 32 && Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT233_EVIDENCE") is { } output)
            {
                Directory.CreateDirectory(output);
                File.WriteAllBytes(Path.Combine(output, $"smooth-{callbacks:D3}.rgba"), pixels);
            }
            Assert.Equal(320 * 224 * 4, pixels.Length);
            Assert.Equal(0, scene.LastPilotFallbackCount);
            if (!restored && saturatedProducers.Count == 3)
            {
                VerifyColdNext(battle.Capture(), game, assets);
                restored = true;
            }
            if (NativeBattleEffect233PresentationTests.Released(battle.Capture()))
            {
                break;
            }
        }
        Assert.True(callbacks < 180, "The original earthquake must finish within the component callback budget.");
        Assert.True(restored);
        Assert.Equal(496, groundQuads);
        Assert.Equal(new byte[] { 0x2C, 0x30, 0x3C }, saturatedProducers.Order().ToArray());
        Assert.True(battle.NativeEntryReady);
    }

    private static NativeBattleLogicalCommand[] ModuleCommands(NativeBattlePreparedPresentation prepared)
    {
        return prepared.Primitives.LastCommands.Select(item => item.Command).Where(command => command.PilotGeometry is { Identity.Effect: 233, ScreenSpace: false } && (command.Opcode & 0xE0) == 0x20).ToArray();
    }

    private static void VerifyColdNext(BattleSnapshot snapshot, GameAssets game, BattleAssets assets)
    {
        string json = JsonSerializer.Serialize(snapshot);
        Assert.DoesNotContain("ProjectionScreenBounds", json);
        BattleSnapshot serialized = JsonSerializer.Deserialize<BattleSnapshot>(json)!;
        BattleSession direct = NativeBattleEffect233PresentationTests.Restore(snapshot, game);
        BattleSession cold = NativeBattleEffect233PresentationTests.Restore(serialized, game);
        direct.Tick();
        cold.Tick();
        Assert.Equal(JsonSerializer.Serialize(direct.Capture()), JsonSerializer.Serialize(cold.Capture()));
        NativeBattleLogicalCommand[] commands = ModuleCommands(cold.PreparedPresentation!);
        Assert.NotEmpty(commands);
        Assert.All(commands, command => Assert.Equal(new Vector4(-8192, -8192, 8184, 8184), command.PilotGeometry!.ProjectionScreenBounds));
        Assert.Equal(new BattleScenePresentation(assets, snapshot.Definition, true).Render(direct, 320, 224), new BattleScenePresentation(assets, snapshot.Definition, true).Render(cold, 320, 224));
    }
}
