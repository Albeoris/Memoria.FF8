using System.Globalization;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.Events;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class WorldDrawPointInteractionTests
{
    [Fact]
    public void Installed_world_draw_markers_are_native_sprites()
    {
        string directory = TestResources.GameDirectory;
        using var assets = new WorldAssets(directory);
        string message = assets.ComposeWorldMessage(2, assets.ReadWorldMessage(42), "Character", assets.ReadWorldMessage(48))
            + assets.ComposeWorldMessage(2, assets.ReadWorldMessage(42), "Character", assets.ReadWorldMessage(47));
        Assert.Contains("{Key 64}", message);
        Assert.Contains("{Key JunctionMarker}", message);
        NativeMenuArtwork artwork = NativeMenuArtwork.Load(directory);
        foreach (bool originalCompatible in new[] { true, false })
        {
            DialogueDocument document = DialogueTextFormatter.Parse(message, originalCompatible: originalCompatible);
            Assert.DoesNotContain("{Key", document.Text);
            Assert.Equal([7, 192], document.Pages.SelectMany(page => page.Lines).SelectMany(line => line.Runs).Where(run => run.Icon.HasValue).Select(run => run.Icon!.Value));
        }
        Assert.True(artwork.GetIcon(7).Width > 0);
        Assert.True(artwork.GetIcon(192).Width > 0);
    }

    [Theory]
    [InlineData("A", 197)]
    [InlineData("B", 239)]
    public void Saved_world_point_accepts_confirm_and_commits_stock_and_depletion_once(string fixture, int expectedPoint)
    {
        string directory = TestResources.GameDirectory;
        string path = TestResources.FixturePath("App.Tests.WorldDrawPointInteractionTests." + fixture);
        var session = new GameSession(PlayerStateLoader.Load(path, directory));
        using IResourceSource source = GameDataSourceFactory.Open(directory);
        ResourceService resources = new(source);
        using var world = new GameWorld(session, directory, resources: resources);
        world.EnsureField();
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 24)).Accepted);
        var confirm = new PlayerAction(PlayerActionKind.Confirm);
        PlayerAction input = ControllerGameplayInput.CreateWorldAction(default, session.State, 0x40);
        Assert.True(world.Apply(input).Accepted);
        NativeWorldDrawPoint point = Assert.IsType<NativeWorldDrawPoint>(session.State.World!.DrawPoint);
        Assert.Equal(expectedPoint, point.PointIndex);
        Assert.Equal(NativeWorldDrawPointPhase.Found, point.Phase);
        string originalSpell = world.ValidationAssets.ReadNativeDrawPointCatalog().MagicNames[point.MagicId];
        Assert.Contains(originalSpell, world.CurrentDialogue!.Text, StringComparison.OrdinalIgnoreCase);
        string magicKey = LocalizationSourceExporter.IndexedKey(resources.Source.Language, LocalizationSourceExporter.KernelTable("MagicSpells", "Name"), point.MagicId);
        resources.PrepareLocalization(new LocalizationCatalog(new Dictionary<string, LocalizationValue>
        {
            [WorldKey(1)] = Translation("Station name"),
            [WorldKey(3)] = Translation("Airstation name"),
            [WorldKey(4)] = Translation("Get off?{Line}Yes{Line}No"),
            [WorldKey(20)] = Translation("Draw point discovered!{Line}"),
            [WorldKey(24)] = Translation("Select the recipient{Line}"),
            [WorldKey(25)] = Translation("Skip this point"),
            [WorldKey(26)] = Translation(LocalizationValue.InvalidTarget),
            [WorldKey(27)] = Translation(" added to stock "),
            [magicKey] = Translation("Localized spell")
        })).Commit();
        DialogueState found = Assert.IsType<DialogueState>(world.CurrentDialogue);
        Assert.Equal(-2, found.MessageId);
        Assert.StartsWith("Draw point discovered!{Line}Localized spell", found.Text);
        Assert.True(world.Apply(confirm).Accepted);
        Assert.Equal(NativeWorldDrawPointPhase.Found, session.State.World.DrawPoint!.Phase);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.True(world.Apply(confirm).Accepted);
        DialogueState choice = Assert.IsType<DialogueState>(world.CurrentDialogue);
        Assert.Equal(-2, choice.MessageId);
        Assert.StartsWith("Select the recipient{Line}Skip this point", choice.Text);
        Assert.Equal(choice.LastChoice!.Value + 1, choice.Text.Split("{Line}").Length);
        Assert.Equal(1, choice.CancelChoice);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Choose, choice.CancelChoice!.Value)).Accepted);
        Assert.Null(session.State.World.DrawPoint);
        Assert.True(session.State.EventRandom!.DrawPoints!.GetDepletion(point.PointIndex) < 2);

        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.True(world.Apply(confirm).Accepted);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.True(world.Apply(confirm).Accepted);
        NativeCharacterVitals before = session.State.CharacterVitals!;
        var catalog = world.ValidationAssets.ReadNativeDrawPointCatalog();
        byte[] fieldParty = NativeWorldSaveCompatibility.InitialParty(session.State);
        byte[] party = fieldParty.Where(identity => identity != 255).Select(identity => before.CombatParty.Single(row => row != 255 && before.Records[row].CharacterId == identity)).ToArray();
        int slot = Array.FindIndex(party, row => catalog.HasDrawCommand(before.RebuildParty().Records[row]) && before.GetMagicCapacity(row, point.MagicId) == 0);
        Assert.True(slot >= 0);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Choose, slot + 2)).Accepted);
        NativeWorldDrawPoint acquisition = session.State.World.DrawPoint!;
        Assert.True(acquisition.Quantity > 0);
        int stock = before.GetMagicCount(party[slot], point.MagicId) + acquisition.Quantity;
        Assert.Equal(stock, session.State.CharacterVitals!.GetMagicCount(party[slot], point.MagicId));
        Assert.Equal((point.Definition & 0x40) != 0 ? 2 : 3, session.State.EventRandom.DrawPoints!.GetDepletion(point.PointIndex));
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: NativeWorldDrawPoint.AcquisitionCallbacks)).Accepted);
        Assert.Equal(NativeWorldDrawPointPhase.Acquired, session.State.World.DrawPoint!.Phase);
        DialogueState acquired = Assert.IsType<DialogueState>(world.CurrentDialogue);
        Assert.Equal(-2, acquired.MessageId);
        Assert.Contains(" added to stock ", acquired.Text);
        Assert.Contains("Localized spell", acquired.Text);
        Assert.Contains(acquisition.Quantity.ToString(CultureInfo.InvariantCulture), acquired.Text);
        Assert.DoesNotContain("[", acquired.Text);
        Assert.True(world.Apply(confirm).Accepted);
        Assert.Null(session.State.World.DrawPoint);
        if (fixture == "A" && Environment.GetEnvironmentVariable("MEMORIA_FF8_WORLD_DRAW_OUTPUT") is { Length: > 0 } output)
        {
            session.Save(output, compact: true);
            var resumed = new GameSession(GameSession.Load(output));
            using var restored = new GameWorld(resumed, directory);
            restored.EnsureField();
            Assert.Null(restored.CurrentDialogue);
            Assert.Equal(stock, resumed.State.CharacterVitals!.GetMagicCount(party[slot], point.MagicId));
            Assert.True(restored.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        }
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.True(world.Apply(confirm).Accepted);
        Assert.Equal(NativeWorldDrawPointPhase.Depleted, session.State.World.DrawPoint!.Phase);
        Assert.Equal(19, world.CurrentDialogue!.MessageId);
        Assert.Equal(stock, session.State.CharacterVitals!.GetMagicCount(party[slot], point.MagicId));

        string WorldKey(int message) => LocalizationSourceExporter.IndexedKey(resources.Source.Language, "messages_world", message);

        static LocalizationValue Translation(string value) => new(value, "fixture", "fixture");
    }
}
