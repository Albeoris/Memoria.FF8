using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleDefinitionValidationTests
{
    [Fact]
    public void EqualDefinitionsAcceptTheSelectedSquallAndIrvineParty()
    {
        BattleDefinition current = SelectedParty();
        BattleDefinition saved = JsonSerializer.Deserialize<BattleDefinition>(JsonSerializer.Serialize(current))!;

        GameWorld.ValidateBattleDefinition(current, saved);
    }

    [Fact]
    public void ChangedPartyStatsIdentifyCombatants()
    {
        BattleDefinition current = SelectedParty();
        BattleDefinition saved = current with { Combatants = [current.Combatants[0] with { Attack = 99 }, current.Combatants[1]] };

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => GameWorld.ValidateBattleDefinition(current, saved));

        Assert.Contains(nameof(BattleDefinition.Combatants), exception.Message);
    }

    [Fact]
    public void ChangedFormationIdentifiesEncounterId()
    {
        BattleDefinition current = SelectedParty();

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => GameWorld.ValidateBattleDefinition(current, current with { EncounterId = 8 }));

        Assert.Contains(nameof(BattleDefinition.EncounterId), exception.Message);
    }

    [Fact]
    public void ChangedTuningIdentifiesTuning()
    {
        BattleDefinition current = SelectedParty();
        BattleDefinition saved = current with { Tuning = current.Tuning with { UnrestrictedScanRotation = true } };

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => GameWorld.ValidateBattleDefinition(current, saved));

        Assert.Contains(nameof(BattleDefinition.Tuning), exception.Message);
    }

    [Fact]
    public void TunedBattleSavesValidatesAndContinuesWithTheSelectedParty()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed battle corpus is required.");
        GameSession session = new(GameSession.Load(TestResources.FixturePath("battle-definition-tuning")));
        using GameWorld world = new(session, directory);
        world.EnsureField();
        BattleSnapshot original = world.Battle!.Capture();
        var request = world.Events.PendingInteractiveBattle!;
        BattleDefinition current = world.CreateEncounterBattle(request.Encounter, original.Definition.NativeSeed, request.Flags);

        Assert.True(session.State.GameplayPreferences.Tuning.UnrestrictedScanRotation);
        Assert.Equal(session.State.GameplayPreferences.Tuning, current.Tuning);
        Assert.Equal(original.Definition.Combatants.Where(actor => actor.IsPlayer).Select(actor => (actor.Id, actor.ModelId)), current.Combatants.Where(actor => actor.IsPlayer).Select(actor => (actor.Id, actor.ModelId)));
        Assert.Equal(world.CreateBattleParty().Select(actor => (actor.Id, actor.ModelId)), current.Combatants.Where(actor => actor.IsPlayer).Select(actor => (actor.Id, actor.ModelId)));

        string saveDirectory = Path.Combine(Path.GetTempPath(), "Memoria-battle-definition-" + Guid.NewGuid().ToString("N"));
        PlayerSaveStore saves = new(TestResources.ArtifactFile("research-checkpoint.json"), saveDirectory);
        string? temporary = null;
        try
        {
            session.SetEventState(world.Events.Capture());
            saves.Save(0, session, validateContinuation: candidate =>
            {
                using GameWorld continuation = new(new GameSession(candidate), directory);
                continuation.EnsureField();
            });
            temporary = saves.GetPath(0);
            GameSession restoredSession = new(saves.Load(0));
            using GameWorld restored = new(restoredSession, directory);
            restored.EnsureField();
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(original), JsonSerializer.SerializeToElement(restored.Battle!.Capture())));

            world.ApplyBattleInput(new BattleInput(BattleInputKind.Tick));
            restored.ApplyBattleInput(new BattleInput(BattleInputKind.Tick));

            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(world.Battle.Capture()), JsonSerializer.SerializeToElement(restored.Battle.Capture())));
        }
        finally
        {
            if (temporary is not null)
            {
                File.Delete(temporary);
            }
            if (Directory.Exists(saveDirectory))
            {
                Directory.Delete(saveDirectory);
            }
        }
    }

    private static BattleDefinition SelectedParty()
    {
        return new BattleDefinition
        {
            EncounterId = 7,
            Combatants =
            [
                new() { Id = "selected-squall", Name = "Squall", ModelId = 0, IsPlayer = true, Attack = 20 },
                new() { Id = "selected-irvine", Name = "Irvine", ModelId = 2, IsPlayer = true, Attack = 25 }
            ]
        };
    }
}
