using System.Numerics;
using System.Reflection;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleEffect207PilotPresentationTests
{
    [Fact]
    public void Installed_screen_warp_keeps_native_positions_depths_and_UVs_when_smoothing_begins()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_EFFECT207_OPENING_SNAPSHOT");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory) && input is not null && File.Exists(input), "Select original resources and a pre-action battle snapshot for effect207 presentation.");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(input!));
        BattleSnapshot snapshot = document.RootElement.TryGetProperty("Battle", out _) ? GameSession.Load(input!).Battle ?? throw new InvalidDataException("The selected standalone save has no active battle.") : document.RootElement.Deserialize<BattleSnapshot>() ?? throw new InvalidDataException("The selected battle snapshot is empty.");
        using GameAssets game = new(directory!);
        BattleSession active = BattleSession.Restore(snapshot);
        active.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
        FieldInfo hold = typeof(BattleSession).GetField("diagnosticHoldEnemyCommands", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new MissingFieldException(typeof(BattleSession).FullName, "diagnosticHoldEnemyCommands");
        hold.SetValue(active, true);
        BattleAudioPresentation audio = new(_ => { }, playModule: _ => { }, stopSounds: () => { });
        BattleDiagnosticEnemyAbility selection = new(100, 2, 1, 2, 8, 338, 11, 204);
        string actor = active.Combatants.Single(candidate => !candidate.IsPlayer && candidate.ModelId == selection.Model && candidate.SceneSlot == selection.SceneSlot).Id;
        for (int step = 0; step < 4096 && !active.CanSubmitDiagnosticEnemyAbility(actor, selection); step++)
        {
            Tick();
        }
        Assert.True(active.CanSubmitDiagnosticEnemyAbility(actor, selection), "The selected original enemy ability did not reach its ready boundary.");
        active.SubmitDiagnosticEnemyAbility(actor, selection, 207);
        Capture capture = new();
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = capture };
        NativeBattlePrimitiveCapabilities capabilities = snapshot.Definition.NativeGraphics!.PrimitiveCapabilities!;
        long? previousCallback = null;
        int frames = 0;
        int packets = 0;
        for (int step = 0; step < 4096 && frames < 60; step++)
        {
            Tick();
            NativeBattlePreparedPresentation? earlier = active.PreparedPresentation;
            if (earlier is null || earlier.Callback == previousCallback)
            {
                continue;
            }
            previousCallback = earlier.Callback;
            if (earlier.PilotEffect != 207 || active.NativeGraphics?.OriginalEffect is not { Binding.Effect: 207, ReturnedDescriptor: > 0 })
            {
                continue;
            }
            frames++;
            foreach (NativeBattleLogicalResolvedCommand command in earlier.Primitives.LastCommands.Where(command => command.Pass > 0 && command.Command.PilotGeometry is { ScreenSpace: true, Identity.Effect: 207 }))
            {
                NativeBattlePreparedPresentation isolated = earlier with { Primitives = earlier.Primitives with { LastCommands = [command] } };
                NativeBattlePreparedPresentation later = isolated with { Callback = isolated.Callback + 1 };
                renderer.RenderNativePrimitiveFrame(isolated.Primitives, isolated.Textures, capabilities: capabilities, textureWindowCache: isolated.TextureWindows);
                TriangleVertex[] expected = capture.Vertices.ToArray();
                renderer.RenderNativePilotPrimitiveFrame(isolated, later, 0.5f, capabilities: capabilities);
                string location = $"Effect207 frame {frames}, packet {command.Command.Packet}, opcode {command.Command.Opcode:X2}";
                Assert.True(expected.Length == capture.Vertices.Count, $"{location}: vertex count {expected.Length}/{capture.Vertices.Count}.");
                for (int corner = 0; corner < expected.Length; corner++)
                {
                    TriangleVertex actual = capture.Vertices[corner];
                    Assert.True(Vector2.Distance(expected[corner].Position, actual.Position) < 0.001f, $"{location}, corner {corner}: position {expected[corner].Position}/{actual.Position}.");
                    Assert.True(Math.Abs(expected[corner].Depth - actual.Depth) < 0.000001f, $"{location}, corner {corner}: depth {expected[corner].Depth}/{actual.Depth}.");
                    Assert.True(expected[corner].ReciprocalW == actual.ReciprocalW, $"{location}, corner {corner}: reciprocal W {expected[corner].ReciprocalW}/{actual.ReciprocalW}.");
                    Assert.True(expected[corner].Uv == actual.Uv, $"{location}, corner {corner}: UV {expected[corner].Uv}/{actual.Uv}.");
                }
                packets++;
            }
        }
        Assert.Equal(60, frames);
        Assert.True(packets > 0, "The opening did not submit any effect207 presentation geometry.");

        void Tick()
        {
            active.Tick();
            audio.Update(active);
        }
    }

    private sealed class Capture : ITriangleRenderer
    {
        internal List<TriangleVertex> Vertices { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            Vertices.Clear();
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Vertices.Add(a);
            Vertices.Add(b);
            Vertices.Add(c);
        }

        public void End()
        {
        }
    }
}
