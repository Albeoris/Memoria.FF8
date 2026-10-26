using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class StatusLimitMenuTests
{
    [Theory]
    [InlineData(0, "Off", "On")]
    [InlineData(1, "On", "On")]
    [InlineData(32768, "Off", "Off")]
    [InlineData(32769, "On", "Off")]
    public void LiveSquallProjectsWeaponFinishersAndSavedOptions(int options, string auto, string indicator)
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed native menu resources are required.");
        using GameAssets assets = new(directory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals();
        NativeLimitProgress progress = assets.ReadNewGameLimitProgress() with { Options978 = (ushort)options };
        GameState state = new() { CharacterVitals = vitals, LimitProgress = progress };
        string original = JsonSerializer.Serialize(state);
        MenuData data = LiveMenuData.Build(state, catalog: catalog);
        MenuCharacter squall = data.Characters.Single(character => character.NativeId == 0);
        Assert.Equal(5, squall.NativeLimitCommand);
        Assert.Equal(catalog.CommandName(5), squall.LimitBreak);
        Assert.Equal(auto, squall.LimitSettings["Auto"]);
        Assert.Equal(indicator, squall.LimitSettings["Indicator"]);
        Assert.Equal(4, squall.LimitMoves.Count);
        int row = Array.FindIndex(vitals.Records, record => record.CharacterId == 0);
        byte finishers = catalog.ReadStatRules(vitals, row).WeaponRecord[2];
        for (int index = 0; index < squall.LimitMoves.Count; index++)
        {
            Assert.Equal((finishers & (1 << index)) != 0, squall.LimitMoves[index].Learned);
        }
        Assert.Contains(squall.LimitMoves, move => move.Learned == true);
        Assert.Contains(squall.LimitMoves, move => move.Learned == false);
        Assert.All(squall.LimitMoves, move =>
        {
            Assert.NotEmpty(move.Name);
            Assert.NotEmpty(move.Description);
            Assert.DoesNotContain("Fixture", move.Description);
        });
        MenuController menus = new(() => data);
        menus.Open("status-limit");
        Assert.Contains(menus.Entries, entry => entry.Destination == "status-renzokuken");
        UiElement[] elements = Descendants(menus.BuildView().Root).ToArray();
        Assert.Contains(elements.OfType<UiList>().SelectMany(list => list.Items), text => text.Contains(squall.LimitMoves[0].Name, StringComparison.Ordinal));
        menus.Open("status-renzokuken");
        Assert.Equal(4, menus.Entries.Count);
        Assert.Equal(squall.LimitMoves.Select(move => move.Name), menus.Entries.Select(entry => entry.Label));
        Assert.Contains(Descendants(menus.BuildView().Root).OfType<UiList>().SelectMany(list => list.Items), line => line == $"Auto: {auto}");
        Assert.All(menus.Entries, entry => Assert.Null(entry.Action));
        Assert.Equal(original, JsonSerializer.Serialize(state));
    }

    [Theory]
    [InlineData(1, 11, 10)]
    [InlineData(2, 14, 8)]
    [InlineData(3, 15, 16)]
    [InlineData(4, 19, 8)]
    [InlineData(5, 16, 6)]
    public void OtherLimitFamiliesProjectSavedLearningFlagsWithoutFixtureActions(int id, int command, int count)
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed native menu resources are required.");
        using GameAssets assets = new(directory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeLimitProgress initial = assets.ReadNewGameLimitProgress();
        foreach (bool learned in new[] { false, true })
        {
            NativeLimitProgress progress = initial with { DuelFlags = learned ? ushort.MaxValue : (ushort)0, BlueMagicFlags = learned ? ushort.MaxValue : (ushort)0, AngeloFlags = learned ? byte.MaxValue : (byte)0, SpecialMagicFlags = learned ? (byte)63 : (byte)0 };
            GameState state = new() { LimitProgress = progress, ShotAvailability = assets.ReadNewGameShotAvailability() with { Flags770 = learned ? byte.MaxValue : (byte)0 } };
            MenuCharacter character = new() { Id = "character", NativeId = id, InParty = true };
            catalog.ProjectLimit(character, 0, state);
            Assert.Equal(command, character.NativeLimitCommand);
            Assert.Equal(count, character.LimitMoves.Count);
            Assert.All(character.LimitMoves, move => Assert.Equal(learned, move.Learned));
            MenuData data = new();
            data.Characters.Add(character);
            MenuController menus = new(() => data);
            menus.Open("status-limit");
            Assert.NotEmpty(menus.Entries);
            foreach (string destination in menus.Entries.Select(entry => entry.Destination!).ToArray())
            {
                menus.Open(destination);
                Assert.All(menus.Entries, entry => Assert.Null(entry.Action));
            }
        }
    }

    [Fact]
    public void NativeCommandIdentityRoutesLocalizedLimitNames()
    {
        MenuData data = new();
        data.Characters.Add(new MenuCharacter { Id = "squall", NativeId = 0, NativeLimitCommand = 5, Name = "Squall", LimitBreak = "Localized limit", InParty = true });
        MenuController menus = new(() => data);
        menus.Open("status-limit");
        Assert.Contains(menus.Entries, entry => entry.Destination == "status-renzokuken");
    }

    [Fact]
    public void SelectedLiveSnapshotRendersNativeLimitInformation()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_STATUS_LIMIT_SNAPSHOT");
        Assert.SkipUnless(source is not null && File.Exists(source), "Select an optional live Status snapshot.");
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed native menu resources are required.");
        using GameAssets assets = new(directory);
        GameState state = GameSession.Load(source!);
        string original = JsonSerializer.Serialize(state);
        MenuData data = LiveMenuData.Build(state, catalog: LiveMenuCatalog.ReadInstalled(assets));
        MenuController menus = new(() => data);
        menus.Open("status-limit");
        menus.Execute(new MenuAction("select-character", data.Characters.Single(character => character.NativeId == 0).Id));
        Assert.Equal(5, menus.Character?.NativeLimitCommand);
        Assert.NotEmpty(menus.Character!.LimitMoves);
        foreach (MenuPresentation presentation in new[] { MenuPresentation.Classic, MenuPresentation.Vector })
        {
            menus.Presentation = presentation;
            foreach (string screen in new[] { "status-limit", "status-renzokuken" })
            {
                menus.Open(screen);
                UiView view = menus.BuildView();
                view.Resize(1280, 896);
                UiElement[] elements = Descendants(view.Root).ToArray();
                string[] text = elements.OfType<UiLabel>().Select(label => label.Text).Concat(elements.OfType<UiList>().SelectMany(list => list.Items)).ToArray();
                Assert.DoesNotContain(text, line => line.Contains("Limit: Unavailable", StringComparison.Ordinal));
                Assert.Contains(text, line => line.Contains(menus.Character.LimitMoves[0].Name, StringComparison.Ordinal));
                using SkiaSharp.SKBitmap bitmap = new(1280, 896);
                using BattlePreviewCanvas canvas = new(bitmap, directory);
                view.Draw(canvas, menus.CreateTheme());
                if (Environment.GetEnvironmentVariable("MEMORIA_FF8_STATUS_LIMIT_OUTPUT") is string output)
                {
                    Directory.CreateDirectory(output);
                    bitmap.SavePng(Path.Combine(output, $"{presentation.ToString().ToLowerInvariant()}-{screen}.png"));
                }
            }
        }
        Assert.Equal(original, JsonSerializer.Serialize(state));
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
