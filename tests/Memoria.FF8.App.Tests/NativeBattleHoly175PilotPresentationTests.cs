using System.Numerics;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleHoly175PilotPresentationTests
{
    [Fact]
    public void Installed_capture_screen_packets_keep_native_positions_depths_and_UVs_when_smoothing_begins()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select original resources for Holy presentation.");
        GameSession session = GameWorld.CreateColdProcess(directory!);
        session.StartNewGame();
        using GameWorld world = new(session, directory!);
        BattleSession active = world.StartEffectPreview(new(1, 0) { AbilityId = 14, CommandId = 2 });
        active.PublishDiagnosticAbilityPreview(2, 14);
        TimeSpan callback = NativeBattleGraphicsPresentationClock.ElapsedForCallbacks(1) + TimeSpan.FromTicks(1);
        Capture capture = new();
        BattleRenderer renderer = new(320, 216) { TriangleRenderer = capture };
        NativeBattlePrimitiveCapabilities capabilities = active.Capture().Definition.NativeGraphics!.PrimitiveCapabilities!;
        int frames = 0;
        int packets = 0;
        int offscreenSourcePackets = 0;
        int vertices = 0;
        for (int index = 0; index < 140 && frames < 40; index++)
        {
            active.AdvanceNativeGraphicsPresentation(callback);
            NativeBattlePreparedPresentation earlier = active.PreparedPresentation!;
            if (earlier.PilotEffect != 175 || active.NativeGraphics?.OriginalEffect is not { Binding.Effect: 175, ReturnedDescriptor: > 0 })
            {
                continue;
            }
            frames++;
            foreach (NativeBattleLogicalResolvedCommand command in earlier.Primitives.LastCommands.Where(command => command.Pass > 0 && command.Command.PilotGeometry is { ScreenSpace: true }))
            {
                NativeBattlePreparedPresentation isolated = earlier with { Primitives = earlier.Primitives with { LastCommands = [command] } };
                NativeBattlePreparedPresentation later = isolated with { Callback = isolated.Callback + 1 };
                renderer.RenderNativePrimitiveFrame(isolated.Primitives, isolated.Textures, capabilities: capabilities, textureWindowCache: isolated.TextureWindows);
                TriangleVertex[] expected = capture.Vertices.ToArray();
                if (command.Command.SceneCapture is { Kind: NativeBattleSceneCaptureKind.Source } source && (source.X >= 320 || source.Y >= 216))
                {
                    Assert.Empty(expected);
                    offscreenSourcePackets++;
                }
                renderer.RenderNativePilotPrimitiveFrame(isolated, later, 0.5f, capabilities: capabilities);
                Assert.True(expected.Length == capture.Vertices.Count, $"Holy frame {frames}, packet {command.Command.Packet}, opcode {command.Command.Opcode:X2}: vertex count {expected.Length}/{capture.Vertices.Count}.");
                for (int corner = 0; corner < expected.Length; corner++)
                {
                    Assert.True(Vector2.Distance(expected[corner].Position, capture.Vertices[corner].Position) < 0.001f, $"Holy frame {frames}, packet {command.Command.Packet}, opcode {command.Command.Opcode:X2}: position {expected[corner].Position}/{capture.Vertices[corner].Position}.");
                    Assert.True(Math.Abs(expected[corner].Depth - capture.Vertices[corner].Depth) < 0.000001f, $"Holy frame {frames}, packet {command.Command.Packet}, opcode {command.Command.Opcode:X2}: depth {expected[corner].Depth}/{capture.Vertices[corner].Depth}.");
                    Assert.Equal(expected[corner].ReciprocalW, capture.Vertices[corner].ReciprocalW);
                    Assert.Equal(expected[corner].Uv, capture.Vertices[corner].Uv);
                }
                vertices += expected.Length;
                packets++;
            }
        }
        Assert.Equal(40, frames);
        Assert.True(packets > 1000);
        Assert.True(offscreenSourcePackets > 0);
        Assert.True(vertices > 3000);
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
