using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeGfJunctionRemovalTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LiveOffMenuRemovesTheSelectedCategoryWithoutHealingOrChangingStock(bool all)
    {
        using GameAssets assets = InstalledAssets();
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0).ToggleGuardianForceJunction(0, 0);
        NativeCharacterVital[] records = vitals.Records.ToArray();
        byte[] magic = new byte[20];
        magic[0] = 1;
        records[0] = records[0] with { HitPoints = new NativeHpWord(1, 1), Status = 4, Junction = records[0].Junction with { Commands = 21, Abilities = 39, MagicJunctions = magic } };
        vitals = vitals with { Records = records };
        string original = JsonSerializer.Serialize(vitals);
        NativeCharacterVitals source = vitals;
        List<PlayerAction> actions = [];
        MenuController controller = new(() => LiveMenuData.Build(new GameState { Scene = SceneKind.Field, CharacterVitals = vitals }), action =>
        {
            PlayerAction input = NativeJunctionMenuProjection.PlayerAction(action);
            actions.Add(input);
            vitals = NativeJunctionMenuInput.Apply(vitals, input);
        });
        controller.Open("junction-off");
        if (all)
        {
            controller.Navigate(MenuInput.Down);
        }
        controller.Navigate(MenuInput.Confirm);
        PlayerAction applied = Assert.Single(actions);
        Assert.Equal(PlayerActionKind.RemoveJunctions, applied.Kind);
        Assert.Equal(all ? "1" : "0", applied.Text);
        Assert.All(vitals.Records[0].Junction.MagicJunctions, spell => Assert.Equal(0, spell));
        Assert.Equal(all ? (ushort)0 : (ushort)1, vitals.Records[0].Junction.GuardianForces);
        Assert.Equal(all ? 0u : 21u, vitals.Records[0].Junction.Commands);
        Assert.Equal(all ? 0u : 39u, vitals.Records[0].Junction.Abilities);
        Assert.Equal(new NativeHpWord(1, 1), vitals.Records[0].HitPoints);
        Assert.Equal((ushort)4, vitals.Records[0].Status);
        Assert.Equal(source.Records[0].MagicInventory, vitals.Records[0].MagicInventory);
        Assert.Equal(source.Records.Skip(1), vitals.Records.Skip(1));
        Assert.Equal(original, JsonSerializer.Serialize(source));
        NativeCharacterVitals restored = JsonSerializer.Deserialize<NativeCharacterVitals>(JsonSerializer.Serialize(vitals))!;
        restored.Validate();
        Assert.Equal(vitals.Records[0], restored.Records[0]);
    }

    [Fact]
    public void JunctionRefreshesAcquiredGfMaximumWithoutHealingOrChangingLearning()
    {
        using GameAssets assets = InstalledAssets();
        NativeCharacterVitals source = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0);
        NativeGuardianForceVital[] gfs = source.GuardianForces.ToArray();
        gfs[0] = gfs[0] with { MaximumHitPoints = null, HitPoints = 0 };
        source = source with { GuardianForces = gfs };
        NativeCharacterVitals result = source.ToggleGuardianForceJunction(0, 0);
        Assert.Equal(result.Definitions.GuardianForceMaximum(0, result.GuardianForces[0]), result.GuardianForces[0].MaximumHitPoints);
        Assert.Equal(0, result.GuardianForces[0].HitPoints);
        Assert.Equal(source.GuardianForces[0].Learning, result.GuardianForces[0].Learning);
        Assert.Null(source.GuardianForces[0].MaximumHitPoints);
    }

    [Theory]
    [InlineData(-1, "0", null)]
    [InlineData(8, "1", null)]
    [InlineData(0, "2", null)]
    [InlineData(0, "All", null)]
    [InlineData(0, "0", 1)]
    public void MalformedRemovalInputIsRejected(int row, string category, int? quantity)
    {
        Assert.Throws<ArgumentException>(() => NativeJunctionMenuInput.Validate(new PlayerAction(PlayerActionKind.RemoveJunctions, row, category, Quantity: quantity)));
    }

    private static GameAssets InstalledAssets()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original GF/kernel resources are required.");
        return new GameAssets(directory);
    }
}
