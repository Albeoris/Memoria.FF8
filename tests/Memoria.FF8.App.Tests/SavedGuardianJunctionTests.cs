using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class SavedGuardianJunctionTests
{
    [Fact]
    public void ConfiguredSaveAllowsGuardianRemovalAttachmentAndTransferThroughTheDashboard()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_MEMORIA_SAVE_FIXTURE");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(path) && File.Exists(path), "Configure an optional standalone save.");
        byte[] original = File.ReadAllBytes(path!);
        using GameAssets assets = new(TestResources.GameDirectory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        GameState saved = PlayerStateLoader.Load(path!, TestResources.GameDirectory, sharedAssets: assets);
        NativeCharacterVitals source = saved.CharacterVitals!;
        MenuData initial = LiveMenuData.Build(saved, catalog: catalog);
        int checkedAssignments = 0;
        foreach (MenuCharacter character in initial.Characters.Where(character => character.InParty))
        {
            foreach (MenuGuardianForce force in initial.GuardianForces)
            {
                GameState current = saved with { CharacterVitals = source.DeepCopy() };
                MenuData data = LiveMenuData.Build(current, catalog: catalog);
                MenuJunctionPlan plan = data.JunctionPlanner!.Plan(force.Owner is { } owner && owner.Id != character.Id ? new("junction-gf-reassign", force.Id, character.Id, owner.Id) : new("junction-gf", force.Id, character.Id));
                Assert.True(plan.HasChanges, $"{character.Id}/{force.Id}: {plan.UnavailableReason}");
                int actions = 0;
                MenuController menu = new(() => data, action =>
                {
                    current = current with { CharacterVitals = NativeJunctionMenuInput.Apply(current.CharacterVitals!, NativeJunctionMenuProjection.PlayerAction(action)) };
                    data = LiveMenuData.Build(current, catalog: catalog);
                    actions++;
                });
                menu.Open("junction");
                menu.Execute(new("select-character", character.Id));
                menu.Execute(new("junction-ui", "gf-portrait-" + force.Id));
                if (force.Owner is { } donor && donor.Id != character.Id)
                {
                    Assert.Equal(0, actions);
                    menu.Execute(new("junction-ui", "junction-transfer-yes"));
                }
                Assert.Equal(1, actions);
                Assert.Equal(plan.Result.Records.Select(record => record.Junction.GuardianForces), current.CharacterVitals!.Records.Select(record => record.Junction.GuardianForces));
                checkedAssignments++;
            }
        }
        Assert.True(checkedAssignments > 0);
        Assert.Equal(original, File.ReadAllBytes(path!));
        Console.WriteLine($"Checked {checkedAssignments} independent guardian dashboard edits without changing the source save.");
    }
}
