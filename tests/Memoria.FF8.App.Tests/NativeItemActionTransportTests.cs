using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Events;

namespace Memoria.FF8.App.Tests;

public sealed class NativeItemActionTransportTests
{
    [Theory]
    [InlineData(PlayerActionKind.UseFieldItem, 1, 0)]
    [InlineData(PlayerActionKind.RefineItems, 97, 5, "0:0")]
    [InlineData(PlayerActionKind.BuyShopItem, 191, 7)]
    [InlineData(PlayerActionKind.SellShopItem, 191, 3)]
    [InlineData(PlayerActionKind.SelectWeaponShopCharacter, 4, null)]
    [InlineData(PlayerActionKind.RemodelWeapon, 3, null)]
    public void HeadlessActionsAndControlQueuePreserveExplicitInputs(PlayerActionKind kind, int value, int? quantity, string? text = null)
    {
        string json = JsonSerializer.Serialize(new { Kind = kind.ToString(), Value = value, Quantity = quantity, Text = text });
        PlayerAction expected = new(kind, value, Text: text, Quantity: quantity);
        Assert.Equal(expected, Assert.Single(HeadlessPlayer.ReadActions("[" + json + "]")));
        string root = Path.Combine(Path.GetTempPath(), "memoria-item-transport-" + Guid.NewGuid().ToString("N"));
        List<PlayerAction> dispatched = [];
        ControlQueue queue = new(root, request =>
        {
            PlayerAction action = Assert.IsType<PlayerAction>(request.Action);
            dispatched.Add(action);
            return action;
        });
        try
        {
            File.WriteAllText(Path.Combine(root, "requests", "action.json"), "{\"Id\":\"item-action\",\"Command\":\"action\",\"Action\":" + json + "}");
            queue.Poll();
            Assert.Equal(expected, Assert.Single(dispatched));
            using JsonDocument response = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "responses", "action.json")));
            Assert.True(response.RootElement.GetProperty("Ok").GetBoolean());
            JsonSerializerOptions options = new() { Converters = { new JsonStringEnumConverter() } };
            Assert.Equal(expected, response.RootElement.GetProperty("Result").Deserialize<PlayerAction>(options));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
