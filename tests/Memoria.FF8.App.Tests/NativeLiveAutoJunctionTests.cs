using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeLiveAutoJunctionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Party_magic_off_is_one_action_and_auto_only_has_conservative_priorities(bool shared)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals source = assets.ReadNewGameCharacterVitals();
        source = MagicModeCoordinator.Preview(source, new GameplayPreferences { SharedMagicStock = shared }).Vitals;
        for (int row = 0; row < 8; row++)
        {
            source.Records[row] = source.Records[row] with { Junction = source.Records[row].Junction with { MagicJunctions = Enumerable.Repeat((byte)1, 20).ToArray() } };
        }
        string original = JsonSerializer.Serialize(source);
        NativeCharacterVitals party = source;
        List<PlayerAction> actions = [];
        MenuController controller = new(() => LiveMenuData.Build(new GameState { Scene = SceneKind.Field, CharacterVitals = party }), action =>
        {
            PlayerAction input = NativeJunctionMenuProjection.PlayerAction(action);
            actions.Add(input);
            PlayerAction restored = JsonSerializer.Deserialize<PlayerAction>(JsonSerializer.Serialize(input))!;
            party = NativeJunctionMenuInput.Apply(party, restored);
        });
        controller.Open("junction-auto");
        Assert.Equal(new[] { "Attack", "Magic", "Defense" }, controller.Entries.Select(entry => entry.Label));
        controller.Open("junction-off");
        Assert.Equal(new[] { "Magic", "All", "Entire party magic" }, controller.Entries.Select(entry => entry.Label));
        controller.Navigate(MenuInput.Down);
        controller.Navigate(MenuInput.Down);
        controller.Navigate(MenuInput.Confirm);

        Assert.Equal(new PlayerAction(PlayerActionKind.RemoveAllMagicJunctions), Assert.Single(actions));
        Assert.Equal(JsonSerializer.Serialize(source.RemoveAllMagicJunctions()), JsonSerializer.Serialize(party));
        Assert.Equal(original, JsonSerializer.Serialize(source));
    }

    [Fact]
    public void Obsolete_party_auto_action_cannot_be_replayed_as_party_magic_removal()
    {
        PlayerAction obsolete = JsonSerializer.Deserialize<PlayerAction>("{\"Kind\":33,\"Value\":0,\"Text\":\"0\"}")!;
        Assert.False(NativeJunctionMenuInput.IsMenuAction(obsolete.Kind));
        Assert.Throws<ArgumentException>(() => NativeJunctionMenuInput.Validate(obsolete));
        Assert.Equal(34, (int)PlayerActionKind.ReassignJunctionMagic);
        Assert.Equal(35, (int)PlayerActionKind.SwitchPartyMembers);
        Assert.Equal(36, (int)PlayerActionKind.RemoveAllMagicJunctions);
    }

    [Theory]
    [InlineData(1, null, null, 1)]
    [InlineData(0, "0", null, 1)]
    [InlineData(0, null, 1, 1)]
    [InlineData(0, null, null, 2)]
    public void Party_magic_off_rejects_character_category_quantity_or_multi_frame_operands(int value, string? text, int? quantity, int frames)
    {
        Assert.Throws<ArgumentException>(() => NativeJunctionMenuInput.Validate(new PlayerAction(PlayerActionKind.RemoveAllMagicJunctions, value, text, Frames: frames, Quantity: quantity)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Live_auto_emits_one_atomic_action_and_matches_the_proved_native_priority(int priority)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeTutorialCatalog catalog = assets.ReadNativeTutorialCatalog();
        LiveMenuCatalog names = LiveMenuCatalog.ReadInstalled(assets);
        NativeCharacterVitals source = catalog.CreateIsolatedParty(1);
        NativeCharacterVitals party = source;
        string original = JsonSerializer.Serialize(source);
        NativeCharacterVitals expected = catalog.AutoJunction(source, 0, priority);
        List<PlayerAction> actions = [];
        int projections = 0;
        MenuController controller = new(() => LiveMenuData.Build(new GameState { Scene = SceneKind.Field, CharacterVitals = party }, catalog: names), action =>
        {
            PlayerAction input = NativeJunctionMenuProjection.PlayerAction(action);
            actions.Add(input);
            PlayerAction restored = JsonSerializer.Deserialize<PlayerAction>(JsonSerializer.Serialize(input))!;
            party = NativeJunctionMenuInput.Apply(party, restored, (vitals, row, selectedPriority) =>
            {
                projections++;
                return catalog.AutoJunction(vitals, row, selectedPriority);
            });
        });
        controller.Open("junction-auto");
        for (int index = 0; index < priority; index++)
        {
            controller.Navigate(MenuInput.Down);
        }
        controller.Navigate(MenuInput.Confirm);

        PlayerAction applied = Assert.Single(actions);
        Assert.Equal(PlayerActionKind.AutoJunction, applied.Kind);
        Assert.Equal(0, applied.Value);
        Assert.Equal(priority.ToString(System.Globalization.CultureInfo.InvariantCulture), applied.Text);
        Assert.Null(applied.Quantity);
        Assert.Equal(1, projections);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(party));
        Assert.Equal(original, JsonSerializer.Serialize(source));
        Assert.Equal(source.Records[0].MagicInventory, party.Records[0].MagicInventory);
        Assert.Equal(source.Records[0].Junction.GuardianForces, party.Records[0].Junction.GuardianForces);
    }

    [Theory]
    [InlineData(-1, "0", null)]
    [InlineData(8, "0", null)]
    [InlineData(0, "3", null)]
    [InlineData(0, "Attack", null)]
    [InlineData(0, "0", 1)]
    public void Auto_rejects_malformed_transport(int row, string priority, int? quantity)
    {
        Assert.Throws<ArgumentException>(() => NativeJunctionMenuInput.Validate(new PlayerAction(PlayerActionKind.AutoJunction, row, priority, Quantity: quantity)));
    }

    [Fact]
    public void Auto_requires_an_original_resource_provider_before_publication()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals source = assets.ReadNewGameCharacterVitals();
        string original = JsonSerializer.Serialize(source);
        Assert.Throws<NotSupportedException>(() => NativeJunctionMenuInput.Apply(source, new PlayerAction(PlayerActionKind.AutoJunction, 0, "0")));
        Assert.Equal(original, JsonSerializer.Serialize(source));
    }
}
