using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeGfJunctionRemovalTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void LiveOffMenuRemovesTheSelectedCategoryWithoutHealingOrChangingStock(bool all, bool reserve)
    {
        using GameAssets assets = InstalledAssets();
        int owner = reserve ? 3 : 0;
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0).ToggleGuardianForceJunction(owner, 0);
        NativeCharacterVital[] records = vitals.Records.ToArray();
        byte[] magic = new byte[20];
        magic[0] = 1;
        records[owner] = records[owner] with { MemberFlags = (byte)(records[owner].MemberFlags | 1), HitPoints = new NativeHpWord(1, 1), Status = 4, Junction = records[owner].Junction with { Commands = 21, Abilities = 39, MagicJunctions = magic } };
        vitals = vitals with { Records = records, CombatParty = [0, 255, 255] };
        string original = JsonSerializer.Serialize(vitals);
        NativeCharacterVitals source = vitals;
        List<PlayerAction> actions = [];
        MenuController controller = new(() => LiveMenuData.Build(new GameState { Scene = SceneKind.Field, CharacterVitals = vitals }), action =>
        {
            PlayerAction input = NativeJunctionMenuProjection.PlayerAction(action);
            actions.Add(input);
            vitals = NativeJunctionMenuInput.Apply(vitals, input);
        });
        controller.Open();
        controller.Navigate(MenuInput.Confirm);
        int ownerIndex = controller.CharacterChoices.ToList().FindIndex(character => character.Id == $"character-{owner}");
        Assert.True(ownerIndex >= 0);
        for (int step = 0; step < ownerIndex; step++)
        {
            controller.Navigate(MenuInput.Down);
        }
        controller.BuildView();
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("junction", controller.ScreenId);
        Assert.Equal(!reserve, controller.Character!.InParty);
        controller.Navigate(MenuInput.Down);
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal("junction-off", controller.ScreenId);
        if (all)
        {
            controller.Navigate(MenuInput.Down);
        }
        controller.Navigate(MenuInput.Confirm);
        PlayerAction applied = Assert.Single(actions);
        Assert.Equal(PlayerActionKind.RemoveJunctions, applied.Kind);
        Assert.Equal(owner, applied.Value);
        Assert.Equal(all ? "1" : "0", applied.Text);
        Assert.All(vitals.Records[owner].Junction.MagicJunctions, spell => Assert.Equal(0, spell));
        Assert.Equal(all ? (ushort)0 : (ushort)1, vitals.Records[owner].Junction.GuardianForces);
        Assert.Equal(all ? 0u : 21u, vitals.Records[owner].Junction.Commands);
        Assert.Equal(all ? 0u : 39u, vitals.Records[owner].Junction.Abilities);
        Assert.Equal(new NativeHpWord(1, 1), vitals.Records[owner].HitPoints);
        Assert.Equal((ushort)4, vitals.Records[owner].Status);
        Assert.Equal(source.Records[owner].MagicInventory, vitals.Records[owner].MagicInventory);
        Assert.Equal(source.Records.Where((_, row) => row != owner), vitals.Records.Where((_, row) => row != owner));
        Assert.Equal(original, JsonSerializer.Serialize(source));
        NativeCharacterVitals restored = JsonSerializer.Deserialize<NativeCharacterVitals>(JsonSerializer.Serialize(vitals))!;
        restored.Validate();
        Assert.Equal(vitals.Records[owner], restored.Records[owner]);
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
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original GF/kernel resources are required.");
        return new GameAssets(directory);
    }
}
