using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeForegroundFogTests
{
    [Fact]
    public void ActualIfritDialogueShowsBothActorsBehindNativeAdditiveFogWithIndependentCompositionAndRestore()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original field corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        string source = Path.Combine(repository!.FullName, "../fixtures/external-fixture.json");
        byte[] original = File.ReadAllBytes(source);
        using var world = new GameWorld(new GameSession(GameSession.Load(source)), directory);
        world.EnsureField();
        world.SetRenderSize(320, 240);
        Assert.Equal(130, world.Events.FieldId);
        Assert.Equal(41, world.Events.Frame);
        JsonElement before = world.Events.Capture();
        FieldAssets field = world.Field!;
        ActorVisual[] actors = world.Events.Actors.Where(actor => actor.Active && actor.Visible && actor.ModelId >= 0).Select(actor => new ActorVisual(actor.ObjectId, actor.ModelId, actor.X + actor.DisplayOffset.X, actor.Y + actor.DisplayOffset.Y, actor.Z + actor.DisplayOffset.Z, actor.Direction, actor.AnimationId, actor.AnimationFrame, true, FirstControlRotation: actor.FirstControlRotation, RootTranslationZ: actor.RootTranslationZ, AdditionalControlDisabled: actor.AdditionalControlDisabled, PolygonColor: actor.PolygonColor)).ToArray();
        Assert.Equal(new[] { 0, 5 }, actors.Select(actor => actor.ObjectId));
        BackgroundTile[] fog = field.Background.Where(tile => tile.Z == 26).ToArray();
        Assert.Equal(315, fog.Length);
        Assert.All(fog, tile =>
        {
            Assert.True(tile.Blend);
            Assert.Equal(Albeoris.Games.FF8.Imaging.Map.MapBlendMode.Add, tile.BlendMode);
            Assert.All(tile.Image.Pixels, color => Assert.Equal(128, color.A));
        });
        BackgroundScrollFrame scroll = NormalCameraCertificate.Prepare(field, world.Events.Scene, world.Events.Actors)!;
        FieldAssets withoutFog = field with { Background = field.Background.Where(tile => tile.Z != 26).ToArray() };
        IReadOnlyDictionary<int, Vector3> shades = world.Events.GetBackgroundModuleShades();
        byte[] RenderWithoutFog(IEnumerable<ActorVisual> selected)
        {
            return new FieldRenderer(withoutFog, 320, 240).Render(world.Events.BackgroundStates, selected, TimeSpan.Zero, backgroundModuleShades: shades, backgroundScrollOffsets: scroll.LayerOffsets, actorScrollOffset: scroll.ActorOffset, nativeWrapFrame: scroll.NativeWrap);
        }
        byte[] background = RenderWithoutFog([]);
        foreach (ActorVisual actor in actors)
        {
            byte[] isolated = RenderWithoutFog([actor]);
            Assert.True(Enumerable.Range(0, 320 * 240).Count(pixel => !isolated.AsSpan(pixel * 4, 3).SequenceEqual(background.AsSpan(pixel * 4, 3))) > 100, $"Original actor{actor.ObjectId} has no visible projected mesh pixels.");
        }
        byte[] expected = RenderWithoutFog(actors);
        foreach (BackgroundTile tile in fog)
        {
            Assert.True(tile.Draw);
            Assert.Equal(world.Events.BackgroundStates[tile.AnimationId], tile.AnimationState);
            Vector3 shade = shades.GetValueOrDefault(tile.AnimationId, new Vector3(128));
            int left = tile.X + 160 + (int)scroll.LayerOffsets.GetValueOrDefault(tile.Layer).X;
            int top = tile.Y + 120 + (int)scroll.LayerOffsets.GetValueOrDefault(tile.Layer).Y;
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    if ((uint)(left + x) >= 320 || (uint)(top + y) >= 240)
                    {
                        continue;
                    }
                    var color = tile.Image[x, y];
                    int pixel = ((top + y) * 320 + left + x) * 4;
                    expected[pixel] = (byte)Math.Min(255, expected[pixel] + (int)(color.R * shade.X / 128));
                    expected[pixel + 1] = (byte)Math.Min(255, expected[pixel + 1] + (int)(color.G * shade.Y / 128));
                    expected[pixel + 2] = (byte)Math.Min(255, expected[pixel + 2] + (int)(color.B * shade.Z / 128));
                }
            }
        }
        Assert.Equal(expected, world.Render(TimeSpan.Zero));
        using var restored = new GameWorld(new GameSession(GameSession.Load(source)), directory);
        restored.EnsureField();
        restored.SetRenderSize(320, 240);
        Assert.True(JsonElement.DeepEquals(before, restored.Events.Capture()));
        Assert.Equal(expected, restored.Render(TimeSpan.Zero));
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
        Assert.Equal(original, File.ReadAllBytes(source));
    }
}
