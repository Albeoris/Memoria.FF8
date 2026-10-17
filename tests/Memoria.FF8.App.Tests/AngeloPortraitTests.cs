using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class AngeloPortraitTests
{
    [Fact]
    public void Naming_renders_original_dog_portrait_and_keeps_it_after_changing_the_draft()
    {
        string game = TestResources.GameDirectory;
        NativeMenuArtwork artwork = NativeMenuArtwork.Load(game);
        Assert.Equal(64, artwork.AngeloPortrait.Width);
        Assert.Equal(96, artwork.AngeloPortrait.Height);
        Assert.All(artwork.AngeloPortrait.Pixels, pixel => Assert.Equal(byte.MaxValue, pixel.A));
        for (int id = 0; id < NativeMenuArtwork.CharacterCount; id++)
        {
            Assert.False(artwork.AngeloPortrait.Pixels.SequenceEqual(artwork.GetCharacterPortrait(id).Pixels));
        }
        MenuArtworkProvider provider = new(game);
        UiImageSource source = new(UiImageKind.AngeloPortrait, 0);
        Assert.True(artwork.AngeloPortrait.Pixels.SequenceEqual(provider.Read(source).Pixels));
        Assert.Same(provider.Read(source), provider.Read(source));
        MenuController menus = new();
        menus.OpenNamePrompt(2, "Angelo", _ => { });
        menus.Execute(new MenuAction("name-clear"));
        menus.EnterNameText("Custom dog");
        UiView view = menus.BuildView();
        view.Resize(640, 448);
        UiImage image = Assert.Single(Descendants(view.Root).OfType<UiImage>());
        Assert.Equal(source, image.Source);
        using System.Drawing.Bitmap bitmap = new(640, 448);
        using BattlePreviewCanvas canvas = new(bitmap, game);
        view.Draw(canvas, new UiTheme());
        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_NAMING_EVIDENCE") is string directory)
        {
            Directory.CreateDirectory(directory);
            bitmap.Save(Path.Combine(directory, "angelo-name-input.png"), System.Drawing.Imaging.ImageFormat.Png);
        }
    }

    [Fact]
    public void Confirmed_dog_name_survives_standalone_reload_and_next_wait()
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.AngeloPortraitTests.1")));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        MenuController menus = new();
        menus.OpenNamePrompt(2, "Angelo", name => world.Rename(2, name));
        menus.Execute(new MenuAction("name-save"));
        session.SetEventState(world.Events.Capture());
        GameSession restored = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using GameWorld loaded = new(restored, TestResources.GameDirectory);
        loaded.EnsureField();
        Assert.Equal("Angelo", loaded.Events.Scene.Names[2]);
        Assert.Equal(world.Apply(new PlayerAction(PlayerActionKind.Wait)), loaded.Apply(new PlayerAction(PlayerActionKind.Wait)));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), loaded.Events.Capture()));
        restored.SetEventState(loaded.Events.Capture());
        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_NAMING_EVIDENCE") is string directory)
        {
            Directory.CreateDirectory(directory);
            restored.Save(Path.Combine(directory, "angelo-name-continued.json"));
        }
    }

    private static IEnumerable<UiElement> Descendants(UiElement element)
    {
        yield return element;
        foreach (UiElement child in element.Children)
        {
            foreach (UiElement descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
