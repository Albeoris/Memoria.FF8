using System.Text.Json;
using Memoria.FF7.Runtime;
using Memoria.FF7.Resources;
using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7PlayerMenuTests
{
    [Fact]
    public void OrdinaryFieldMenuDoesNotRequireSavePermissionAndRejectsNativeInteractions()
    {
        Ff7FieldProcess idle = new();
        Assert.True(Ff7Scene.CanOpenFieldPlayerMenu(idle, 0));
        Assert.False(Ff7Scene.CanOpenFieldPlayerMenu(idle, 2));
        Assert.False(Ff7Scene.CanOpenFieldPlayerMenu(idle with { MenuDisabled = true }, 0));
        Assert.False(Ff7Scene.CanOpenFieldPlayerMenu(idle with { PlayerLocked = true }, 0));
        Assert.False(Ff7Scene.CanOpenFieldPlayerMenu(idle with { GlobalCommand = Ff7GlobalCommand.Menu }, 0));
        Assert.False(Ff7Scene.CanOpenFieldPlayerMenu(idle with { Dialogues = [new(1, 0, 0, null, null, 0)] }, 0));
    }

    [Fact]
    public void WorldMenuIsIndependentOfTerrainAndSaveBitButKeepsNativeControlAndFadeGates()
    {
        Ff7WorldProcess idle = new(0, 0, 1, null!, [], 0, true, 0, 0, null, [], -1, new(), 0, 0, MainState: 1, FadeAlpha: 0, FadeDisplayedAlpha: 0);
        string before = JsonSerializer.Serialize(idle);
        Assert.True(Ff7Scene.CanOpenWorldPlayerMenu(idle, 0));
        Assert.False(Ff7Scene.CanOpenWorldPlayerMenu(idle with { MovementEnabled = false }, 0));
        Assert.False(Ff7Scene.CanOpenWorldPlayerMenu(idle with { ControlLock = 1 }, 0));
        Assert.False(Ff7Scene.CanOpenWorldPlayerMenu(idle with { FadeDisplayedAlpha = 1 }, 0));
        Assert.False(Ff7Scene.CanOpenWorldPlayerMenu(idle with { Dialogue = new(1, 0, 0, null, null, 0) }, 0));
        Assert.False(Ff7Scene.CanOpenWorldPlayerMenu(idle, 2));
        Assert.Equal(before, JsonSerializer.Serialize(idle));
    }

    [Fact]
    public void InventoryAndMappedPartyProjectionPreservesNativeProgressAndMainPartyOrder()
    {
        Ff7Progress progress = new() { InitialKernelData = [1], Characters = [new(0, "Cloud", 6, 100, 314, 54, new byte[132]), new(1, "Barret", 7, 120, 400, 30, new byte[132])], Party = [1, 0, 255], Inventory = [new(0, 3), new(1, 0)], Materia = [], Gil = 240 };
        string before = JsonSerializer.Serialize(progress);
        Ff7CharacterPresentationIdentity identity = new([new(SessionGame.Ff7, 0, SessionGame.Ff8, 0, Ff7ModelAppearance.GeneralFieldProfileVersion)]);
        MenuData menu = Ff7PlayerMenuProjection.Create(progress, progress.Party, identity, new Dictionary<int, string> { [0] = "Potion" }, new Dictionary<int, string> { [0] = "Restores HP by 100" }, "field", TimeSpan.FromSeconds(12), false, value => new(value.PortraitGame == SessionGame.Ff8 ? UiImageKind.CharacterPortrait : UiImageKind.HostArtwork, value.PortraitCharacter));
        Assert.Equal(new[] { "Barret", "Squall" }, menu.Characters.Select(character => character.Name));
        Assert.Equal(new UiImageSource(UiImageKind.CharacterPortrait, 0), menu.Characters[1].Portrait);
        Assert.Equal("Potion", Assert.Single(menu.Items).Name);
        Assert.Equal("Restores HP by 100", menu.Items[0].Description);
        Assert.Equal(3, menu.Items[0].Quantity);
        Assert.False(menu.SaveAvailable);
        Assert.False(menu.ShowSeeDRank);
        Assert.True(new MenuData().ShowSeeDRank);
        Assert.DoesNotContain("item-use", menu.SupportedActions);
        Assert.Equal(before, JsonSerializer.Serialize(progress));
    }

    [Fact]
    public void SharedSettingsProjectionReadsCurrentValuesAndBrowsingNeverWrites()
    {
        UiPreferences preferences = new() { FieldMovementSpeedPercent = 170, WorldMovementSpeedPercent = 180, FieldFps = 60, MenuPresentation = MenuPresentation.Vector };
        int writes = 0;
        MenuData data = new();
        MenuController menu = new(() => data, _ => writes++);
        menu.ConnectPreferences(() => preferences, _ => writes++);
        menu.PopulateHostSettingValues(data, ["Field movement speed", "World movement speed", "Field FPS", "UI presentation", "Anti-aliasing"]);
        Assert.Equal("170%", data.Settings["Field movement speed"]);
        Assert.Equal("180%", data.Settings["World movement speed"]);
        Assert.Equal("60", data.Settings["Field FPS"]);
        Assert.Equal("Vector", data.Settings["UI presentation"]);
        Assert.Equal(0, writes);
        Assert.Contains("config:Field movement speed", data.SupportedActions);
    }

    [Fact]
    public void ReserveMembershipAndMaximumHpUseSavedNativeOwners()
    {
        byte[] record = new byte[132];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(56), 432);
        Ff7Progress progress = new() { InitialKernelData = [1], Characters = [new(0, "Cloud", 6, 100, 314, 54, record), new(1, "Barret", 7, 120, 400, 30, record), new(2, "Tifa", 1, 0, 100, 20, record)], Party = [0, 255, 255], Inventory = [], Materia = [], AvailableCharacters = 2 };
        MenuData data = Ff7PlayerMenuProjection.Create(progress, progress.Party, new([]), new Dictionary<int, string>(), new Dictionary<int, string>(), "field", TimeSpan.Zero, false, _ => new(UiImageKind.HostArtwork, 0));
        Assert.Equal(new[] { "Cloud", "Barret" }, data.Characters.Select(character => character.Name));
        Assert.True(data.Characters[0].InParty);
        Assert.False(data.Characters[1].InParty);
        Assert.All(data.Characters, character => Assert.Equal(432, character.MaximumHp));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(56), ushort.MaxValue);
        MenuData uncached = Ff7PlayerMenuProjection.Create(progress, progress.Party, new([]), new Dictionary<int, string>(), new Dictionary<int, string>(), "field", TimeSpan.Zero, false, _ => new(UiImageKind.HostArtwork, 0));
        Assert.All(uncached.Characters, character => Assert.Null(character.MaximumHp));
    }

    [Fact]
    public void SelectedKernelLabelsUseOriginalItemAndEquipmentText()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(root), "Select original FFVII kernel menu resources.");
        Ff7KernelTextResources text = Ff7KernelTextResources.Load(Ff7Installation.Discover(root!));
        Assert.Equal("Potion", text.ItemNames[0]);
        Assert.Equal("Restores HP by 100", text.ItemDescriptions[0]);
        Assert.False(string.IsNullOrWhiteSpace(text.ItemNames[128]));
        Assert.False(string.IsNullOrWhiteSpace(text.ItemDescriptions[128]));
    }
}
