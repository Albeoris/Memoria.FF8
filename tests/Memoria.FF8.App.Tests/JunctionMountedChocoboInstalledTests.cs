using System.Numerics;
using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class JunctionMountedChocoboInstalledTests
{
    [Fact]
    public void MountedStepsUseTheSelectedWorldTexturesAndContinueAfterReload()
    {
        string directory = TestResources.GameDirectory;
        string source = TestResources.FixturePath("world-mounted-chocobo");
        string archive = TestResources.FixturePath("junction-world-textures");
        string mods = Directory.GetParent(Path.GetDirectoryName(archive)!)!.FullName;
        using GameResourceHost host = new(directory, TestResources.ArtifactDirectory, watch: false, modsDirectory: mods);
        GameSession session = new(PlayerStateLoader.Load(source, directory, resources: host.Resources));
        using GameWorld world = new(session, directory, resources: host.Resources);
        Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
        WorldMapScene scene = Assert.IsType<WorldMapScene>(world.WorldScene);
        Assert.NotNull(scene.State.NativeChocoboRuntime);
        using WorldAssets selected = new(directory, host.Resources);
        using WorldAssets original = new(directory);
        HashSet<PixelImage> stepTextures = [];
        foreach (byte type in new byte[] { 17, 18 })
        {
            for (int frame = 0; frame < selected.ForestTables.GetEffect(type).Frames; frame++)
            {
                PixelImage texture = selected.GetForestTexture(type, frame);
                Assert.True(texture.Width > original.GetForestTexture(type, frame).Width);
                stepTextures.Add(texture);
            }
        }

        StepDrawRecorder recorder = new(stepTextures);
        HashSet<byte> emitted = [];
        for (int callback = 0; callback < 72; callback++)
        {
            Assert.True(world.Apply(RideWest(scene.State)).Accepted);
            NativeWorldForestParticle[] particles = scene.State.NativeChocoboRuntime!.Objects.Forest.Particles;
            foreach (NativeWorldForestParticle particle in particles.Where(particle => particle.Type is 17 or 18 && particle.Visible))
            {
                emitted.Add(particle.Type);
            }
            if (emitted.Count == 2)
            {
                break;
            }
        }
        Assert.Equal(new byte[] { 17, 18 }, emitted.Order().ToArray());
        scene.Render(640, 448, recorder);
        Assert.True(recorder.Triangles >= 8);
        Assert.DoesNotContain(host.Mods.Diagnostics, diagnostic => diagnostic.Code is "ffnx-unsupported-target" or "replacement-failed" or "resource-rejected");

        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_JUNCTION_OUTPUT") ?? TestResources.ArtifactDirectory;
        string snapshot = Path.Combine(output, "mounted-chocobo-steps.ff8save");
        session.Save(snapshot, compact: true);
        GameSession restored = new(GameSession.Load(snapshot));
        Assert.Equal(session.State.WorldProcess!.MountWaterStepEdges, restored.State.WorldProcess!.MountWaterStepEdges);
        Assert.Equal(session.State.WorldProcess.CompanionWaterStepEdges, restored.State.WorldProcess.CompanionWaterStepEdges);
        using WorldMapScene cold = new(restored, directory, host.Resources);
        Assert.Equal(scene.Render(640, 448), cold.Render(640, 448));
        PlayerAction next = RideWest(scene.State);
        Assert.Equal(scene.Apply(next), cold.Apply(next));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State.WorldProcess), JsonSerializer.SerializeToElement(restored.State.WorldProcess)));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(scene.State), JsonSerializer.SerializeToElement(cold.State)));
    }

    private static PlayerAction RideWest(WorldState state)
    {
        float angle = state.CameraYaw * MathF.Tau / 4096;
        return new(PlayerActionKind.Move, Horizontal: -MathF.Cos(angle), Vertical: MathF.Sin(angle));
    }

    private sealed class StepDrawRecorder(HashSet<PixelImage> textures) : ITriangleRenderer
    {
        public int Triangles { get; private set; }

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }

        public void End()
        {
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            if (material.Texture is { } texture && textures.Contains(texture))
            {
                Triangles++;
            }
        }
    }
}
