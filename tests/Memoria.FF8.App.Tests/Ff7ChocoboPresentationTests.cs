using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;
using Memoria.FF8.Assets;
using System.Text.Json;
using Microsoft.Xna.Framework.Graphics;
using GameTime = Microsoft.Xna.Framework.GameTime;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class Ff7ChocoboPresentationTests
{
    [Fact]
    public void OriginalDepartureRendersResourceBackedRaceThroughNormalSceneUpdates()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF7_CHOCOBO_ENTRY_SAVE");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(root) && Directory.Exists(root), "Select original FFVII resources.");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(source) && File.Exists(source), "Select an original Chocobo departure save.");
        byte[] sourceBytes = File.ReadAllBytes(source!);
        using MemoryStream stream = new(sourceBytes, false);
        CombinedSessionSave departure = CombinedSessionCodec.Read(stream);
        Ff7Installation installation = Ff7Installation.Discover(root!);
        using GpuTestDevice device = new();
        using Ff7Scene scene = new(device.Device, installation, "", new(50, 32000), _ => throw new InvalidOperationException("The selected Chocobo departure unexpectedly requested a movie."), audioEnabled: false);
        string? ff8Root = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        if (!string.IsNullOrWhiteSpace(ff8Root))
        {
            scene.ConfigureCharacterIdentity(ff8Root);
        }
        scene.Continue(departure);
        using GameAssets? appearanceAssets = !string.IsNullOrWhiteSpace(ff8Root) && departure.Mappings.Length > 0 ? new(ff8Root) : null;
        IFf7ActorAppearance? mappedAppearance = Ff7AppearanceSelection.Create(departure.Mappings, installation, appearanceAssets);
        TrackedAppearance? tracked = mappedAppearance is null ? null : new(mappedAppearance);
        scene.SetAppearance(departure.Mappings, tracked);
        Assert.Null(scene.Error);
        using SpriteBatch batch = new(device.Device);
        using RenderTarget2D target = device.CreateTarget(320, 240);
        TimeSpan step = TimeSpan.FromMilliseconds(34);
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF7_CHOCOBO_RENDER_OUTPUT");
        for (int update = 1; update <= 320; update++)
        {
            scene.Update(new GameTime(step * update, step), default);
            Assert.Null(scene.Error);
            if (update is not (64 or 320))
            {
                continue;
            }
            Ff7ChocoboRuntime race = Assert.IsType<Ff7ChocoboRuntime>(scene.Session?.ChocoboRace);
            string beforeRender = JsonSerializer.Serialize(race.Capture());
            device.Device.SetRenderTarget(target);
            scene.Draw(batch, new Rectangle(0, 0, 320, 240));
            Assert.Null(scene.Error);
            Assert.Equal(beforeRender, JsonSerializer.Serialize(race.Capture()));
            var pixels = GpuTestDevice.Read(target);
            Assert.True(pixels.Select(pixel => pixel.PackedValue).Distinct().Count() > 60, $"Chocobo scene update {update} has insufficient framebuffer color variety.");
            Assert.True(pixels.Count(pixel => pixel.R > 4 || pixel.G > 4 || pixel.B > 4) > 5000, $"Chocobo scene update {update} has insufficient visible scene coverage.");
            if (update == 320)
            {
                scene.InterpolatePresentation = false;
                device.Device.SetRenderTarget(target);
                scene.Draw(batch, new Rectangle(0, 0, 320, 240));
                var nativePixels = GpuTestDevice.Read(target);
                Assert.NotEqual(pixels.Select(pixel => pixel.PackedValue), nativePixels.Select(pixel => pixel.PackedValue));
                Assert.Equal(beforeRender, JsonSerializer.Serialize(race.Capture()));
                scene.InterpolatePresentation = true;
                device.Device.SetRenderTarget(target);
                scene.Draw(batch, new Rectangle(0, 0, 320, 240));
                pixels = GpuTestDevice.Read(target);
            }
            if (!string.IsNullOrWhiteSpace(output))
            {
                Directory.CreateDirectory(output);
                using FileStream capture = File.Create(Path.Combine(output, $"chocobo-scene-{update}.png"));
                target.SaveAsPng(capture, target.Width, target.Height);
            }
        }
        Assert.Equal(sourceBytes, File.ReadAllBytes(source!));
        if (tracked is not null)
        {
            Assert.True(tracked.SceneCalls > 0);
            Assert.True(tracked.MappedTriangles > 0);
            Assert.Equal(0, tracked.StoryCharacter);
        }
    }

    private sealed class TrackedAppearance(IFf7ActorAppearance selected) : IFf7ActorAppearance
    {
        internal int SceneCalls { get; private set; }
        internal int MappedTriangles { get; private set; }
        internal int StoryCharacter { get; private set; } = -1;

        public bool TryCreate(Ff7ActorVisual actor, PcFieldModelDescriptor descriptor, Ff7FieldModelResources original, out IReadOnlyList<Ff7AppearanceTriangle> triangles)
        {
            bool mapped = selected.TryCreate(actor, descriptor, original, out triangles);
            if (actor.PoseDomain == Ff7ActorPoseDomain.Scene)
            {
                SceneCalls++;
                MappedTriangles += triangles.Count;
                StoryCharacter = actor.PartyCharacter;
            }
            return mapped;
        }
    }

    [Fact]
    public void OriginalPresentationTablesResolveAllModelAndBillboardDependencies()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrEmpty(root) && Directory.Exists(root), "Selected original FFVII resources are required.");
        Ff7Installation installation = Ff7Installation.Discover(root!);
        Ff7ChocoboPresentation presentation = new(installation);
        Assert.NotNull(presentation);
        foreach (bool shortCourse in new[] { false, true })
        {
            Ff7ChocoboPresentationData resources = Ff7PcExecutableData.LoadChocoboPresentationData(installation, shortCourse);
            PcChocoboCourse course = Ff7ChocoboResources.LoadCourse(installation, shortCourse);
            Assert.Equal(shortCourse ? 35 : 31, resources.Models.Count);
            Assert.Equal(shortCourse ? 19 : 15, resources.Scenery.Count);
            Assert.Equal(shortCourse ? 90 : 255, resources.SpriteMap.Count);
            Assert.Equal(shortCourse ? 7 : 36, resources.SpriteTextures.Count);
            Assert.Equal("CD.hrc", resources.Models[0].Hrc);
            Assert.Equal("EA.hrc", resources.Models[6].Hrc);
            Assert.Equal("MI.hrc", resources.Models[7].Hrc);
            Assert.Equal("NC.hrc", resources.Models[8].Hrc);
            Assert.Equal("LP.hrc", resources.Models[9].Hrc);
            Assert.Equal("DH.hrc", resources.Models[10].Hrc);
            Assert.All(resources.SpriteMap, index => Assert.InRange(index, 0, resources.SpriteTextures.Count - 1));
            Assert.All(resources.Scenery, descriptor => Assert.InRange(descriptor.Model, (byte)16, (byte)(resources.Models.Count - 1)));
            Assert.Equal(resources.SpriteMap.Count * 40, course.Sprites.Length);
            Assert.Contains(resources.Scenery, descriptor => descriptor.Frame != 0 || descriptor.Increment != 0);
            Assert.All(resources.Models.Take(6), model => Assert.Equal(6, model.Animations.Count));
            Assert.Equal(5, resources.Models[9].Animations.Count);
            Assert.Equal(5, resources.Models[10].Animations.Count);
        }
    }
}
