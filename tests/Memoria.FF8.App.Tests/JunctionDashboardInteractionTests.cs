using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class JunctionDashboardInteractionTests
{
    [Fact]
    public void ManualMoveCommitsOnceAndUndoRestoresAssignmentsWithoutHealing()
    {
        using Fixture fixture = new();
        NativeCharacterVitals before = fixture.Vitals.DeepCopy();
        fixture.Hit("slot-Str");
        fixture.Hit("candidate-magic-0-1");
        Assert.Equal(before.Records[0].Junction, fixture.Vitals.Records[0].Junction);
        fixture.Apply();
        Assert.Equal(0, fixture.Vitals.Records[0].Junction.MagicJunctions[0]);
        Assert.Equal(1, fixture.Vitals.Records[0].Junction.MagicJunctions[1]);
        NativeHpWord clamped = fixture.Vitals.Records[0].HitPoints;
        Assert.True(clamped.Maximum < before.Records[0].HitPoints.Maximum);
        fixture.Hit("junction-undo");
        Assert.Equal(2, fixture.Actions);
        Assert.Equal(before.Records[0].Junction, fixture.Vitals.Records[0].Junction);
        Assert.Equal(clamped, fixture.Vitals.Records[0].HitPoints);
        Assert.Equal(JsonSerializer.Serialize(before.MagicSystem), JsonSerializer.Serialize(fixture.Vitals.MagicSystem));
        Assert.Equal(before.Records[0].MagicInventory, fixture.Vitals.Records[0].MagicInventory);
        Assert.False(fixture.Menu.Entries.Single(entry => entry.Id == "junction-undo").Enabled);
    }

    [Fact]
    public void CancelAfterCandidateNavigationLeavesAllNestedOwnersUnchanged()
    {
        using Fixture fixture = new();
        string original = JsonSerializer.Serialize(fixture.Vitals);
        fixture.Hit("slot-Str");
        fixture.Hit("candidate-magic-0-1");
        fixture.Hit("candidate-magic-0-2");
        fixture.Menu.Navigate(MenuInput.Cancel);
        Assert.Equal("junction-dashboard", fixture.Menu.ScreenId);
        Assert.Equal(0, fixture.Actions);
        Assert.Equal(original, JsonSerializer.Serialize(fixture.Vitals));
    }

    [Fact]
    public void CardAlternateOpensActionsAndPinsWithoutChangingGameplay()
    {
        using Fixture fixture = new();
        string original = JsonSerializer.Serialize(fixture.Vitals);
        fixture.Menu.SelectEntry("junction-dashboard", "slot-Str");
        fixture.Menu.Navigate(MenuInput.Alternate);
        foreach (string control in new[] { "optimize", "fill-commands", "pin-stat", "clear-magic", "remove-junctions", "clear-party-magic" })
        {
            Assert.Contains(fixture.Menu.Entries, entry => entry.Id == control && entry.Enabled);
        }
        fixture.Hit("pin-stat");
        fixture.Menu.Navigate(MenuInput.Cancel);
        Assert.Equal(0, fixture.Actions);
        Assert.Equal(original, JsonSerializer.Serialize(fixture.Vitals));
    }

    [Fact]
    public void ExitAcknowledgmentSurvivesAnUnrelatedStatEdit()
    {
        using Fixture fixture = new();
        fixture.Menu.Navigate(MenuInput.Cancel);
        Assert.Contains(fixture.Menu.Entries, entry => entry.Id == "leave-configured");
        fixture.Hit("leave-configured");
        Assert.NotEqual("junction-dashboard", fixture.Menu.ScreenId);
        fixture.Menu.Open("junction");
        fixture.Hit("slot-Str");
        fixture.Hit("candidate-magic-0-2");
        fixture.Apply();
        fixture.Menu.Navigate(MenuInput.Cancel);
        Assert.NotEqual("junction-dashboard", fixture.Menu.ScreenId);
        Assert.Equal(1, fixture.Actions);
    }

    [Fact]
    public void SharedDepletedSpellRemainsJunctionableAndApplyUndoNeverRefillCasts()
    {
        using Fixture fixture = new(shared: true);
        fixture.Vitals.MagicSystem.SharedFieldMasks[1] = 15;
        fixture.Refresh();
        Assert.Equal(0, fixture.Data.Magic.Single(magic => magic.Id == "magic-0-1").RemainingUses);
        string history = JsonSerializer.Serialize(fixture.Vitals.MagicSystem);
        fixture.Hit("slot-Str");
        fixture.Hit("candidate-magic-0-1");
        fixture.Apply();
        fixture.Hit("junction-undo");
        Assert.Equal(2, fixture.Actions);
        Assert.Equal(history, JsonSerializer.Serialize(fixture.Vitals.MagicSystem));
        Assert.Equal(0, fixture.Data.Magic.Single(magic => magic.Id == "magic-0-1").RemainingUses);
        Assert.Equal(100, fixture.Vitals.GetMagicCount(0, 1));
    }

    [Fact]
    public void TransferringGfReviewsAndUpdatesTheHiddenDonorAsOneOperation()
    {
        using Fixture fixture = new();
        fixture.Vitals = fixture.Vitals.ToggleGuardianForceJunction(0, 0).ToggleGuardianForceJunction(7, 0).SetJunctionMagic(7, 0, 1).SetJunctionAbility(7, 0, 21, true);
        fixture.Vitals.Records[7] = fixture.Vitals.Records[7] with { MemberFlags = 0 };
        fixture.Refresh();
        Assert.DoesNotContain(fixture.Data.Characters, actor => actor.Id == "character-7");
        fixture.Hit("manage-gfs");
        fixture.Hit("candidate-gf-0");
        Assert.Equal(0, fixture.Actions);
        fixture.Apply();
        Assert.Contains(0, fixture.Vitals.Records[0].Junction.GuardianForceAssignments);
        Assert.DoesNotContain(0, fixture.Vitals.Records[7].Junction.GuardianForceAssignments);
        Assert.Equal(0u, fixture.Vitals.Records[7].Junction.Commands);
        Assert.Equal(0, fixture.Vitals.Records[7].Junction.MagicJunctions[0]);
        fixture.Menu.Navigate(MenuInput.Cancel);
        fixture.Hit("junction-undo");
        Assert.Contains(0, fixture.Vitals.Records[7].Junction.GuardianForceAssignments);
        Assert.Equal(21u, fixture.Vitals.Records[7].Junction.Commands);
        Assert.Equal(1, fixture.Vitals.Records[7].Junction.MagicJunctions[0]);
    }

    [Fact]
    public void EquippedMugUsesTheOriginalPassiveFlagsForItsBasicActionName()
    {
        using Fixture fixture = new();
        int mug = Enumerable.Range(58, 20).First(ability =>
        {
            NativeCharacterVitals candidate = fixture.Vitals.DeepCopy();
            candidate.Records[0] = candidate.Records[0] with { Junction = candidate.Records[0].Junction with { Abilities = (uint)ability } };
            return (fixture.Catalog.ReadStatRules(candidate, 0).NativeStatusPassiveFlags!.Value & 1) != 0;
        });
        fixture.Vitals = fixture.Vitals.TeachGuardianForceAbility(0, mug).SetJunctionAbility(0, 0, mug, false);
        fixture.Refresh();
        Assert.Equal(fixture.Catalog.CommandName(12), fixture.Data.Characters.Single(actor => actor.Id == "character-0").Commands[0]);
        fixture.Vitals = fixture.Vitals.SetJunctionAbility(0, 0, 0, false);
        fixture.Refresh();
        Assert.Equal(fixture.Catalog.CommandName(1), fixture.Data.Characters.Single(actor => actor.Id == "character-0").Commands[0]);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly GameAssets assets;
        public LiveMenuCatalog Catalog { get; }
        public NativeCharacterVitals Vitals { get; set; }
        public MenuData Data { get; private set; }
        public MenuController Menu { get; }
        public int Actions { get; private set; }

        public Fixture(bool shared = false)
        {
            Assert.SkipUnless(Directory.Exists(TestResources.GameDirectory), "Original menu/kernel resources must be explicitly configured.");
            assets = new GameAssets(TestResources.GameDirectory);
            Catalog = LiveMenuCatalog.ReadInstalled(assets);
            Vitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0);
            foreach (int ability in Enumerable.Range(1, 24))
            {
                Vitals = Vitals.TeachGuardianForceAbility(0, ability);
            }
            Vitals = Vitals.ToggleGuardianForceJunction(0, 0);
            for (int row = 0; row < 8; row++)
            {
                Vitals.Records[row] = Vitals.Records[row] with { MagicInventory = Enumerable.Range(1, 32).Select(spell => new NativeMagicStock((byte)spell, 100)).ToArray() };
            }
            Vitals = MagicModeCoordinator.Preview(Vitals, new GameplayPreferences { SharedMagicStock = shared, InfiniteMagic = true }).Vitals.SetJunctionMagic(0, 0, 1);
            Vitals = Vitals.WithSavedHitPoints(0, Vitals.GetMagicMaximumHitPoints(0));
            Data = Build();
            Menu = new MenuController(() => Data, action =>
            {
                Vitals = NativeJunctionMenuInput.Apply(Vitals, NativeJunctionMenuProjection.PlayerAction(action));
                Actions++;
                Data = Build();
            });
            Menu.Open("junction");
            Assert.Equal("junction-dashboard", Menu.ScreenId);
        }

        private MenuData Build() => LiveMenuData.Build(new GameState { Scene = SceneKind.Field, CharacterVitals = Vitals }, catalog: Catalog);

        public void Refresh()
        {
            Data = Build();
            Menu.InvalidateLiveData();
        }

        public void Hit(string control) => Menu.Execute(new MenuAction("junction-ui", control));

        public void Apply()
        {
            int before = Actions;
            Hit("apply-junction-preview");
            if (Actions == before)
            {
                Hit("apply-junction-preview");
            }
            Assert.Equal(before + 1, Actions);
        }

        public void Dispose() => assets.Dispose();
    }
}
