using System.Collections.Immutable;
using System.Text.Json;
using System.Security.Cryptography;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ActiveBattleEncounterPolicyCompatibilityTests
{
    [Fact]
    public void New_encounters_apply_current_publication_while_saved_policy_absence_stays_absent()
    {
        BattleDefinition original = new() { Name = "Original", EncounterId = 7 };
        EncounterBindingContext context = new("Classic");
        int publications = 0;
        BattleDefinition Current(BattleDefinition definition, EncounterBindingContext selected)
        {
            publications++;
            Assert.Same(context, selected);
            return definition with { Name = "Current publication" };
        }
        BattleDefinition entered = GameWorld.ApplyEncounterBattlePolicy(original, context, null, Current);
        Assert.Equal("Current publication", entered.Name);
        Assert.Equal(1, publications);
        BattleDefinition restored = GameWorld.ApplyEncounterBattlePolicy(original, context, original, Current);
        Assert.Same(original, restored);
        Assert.Null(restored.EncounterMod);
        Assert.Equal(1, publications);
    }

    [Fact]
    public void Retained_compiled_policy_rebinds_native_AI_and_initial_properties_without_weaker_core_validation()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        NativeBattleStatusAllocation allocation = assets.ReadEnemyNativeStatusData(88);
        BattleDefinition baseline = new()
        {
            EncounterId = 136,
            StageId = 147,
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, Attack = 20 },
                new() { Id = "enemy-0", Name = "Original", ModelId = 88, SceneSlot = 0, MaximumHp = 1000, CurrentHp = 1000, NativeAi = assets.ReadEnemyAi(88, 1), NativeTemplate = assets.ReadEnemyTemplate(88, 1), NativeStatusResistances = allocation.Resistances.ToArray() }
            ]
        };
        EncounterBindingContext context = new("Classic", 768, 136, 18);
        EncounterBindingResult binding = EncounterBindingResolver.Resolve(EncounterBindingResolver.Disc1Edea, baseline, context);
        EncounterCompiledDefinition compiled = new(EncounterBindingResolver.Disc1Edea, "Classic", "entered-publication", binding.OriginalActorId, binding.CastingSequence,
            ImmutableDictionary<string, EncounterCompiledPolicy>.Empty, ImmutableDictionary<string, string>.Empty, ImmutableDictionary<string, EncounterCompiledTemplate>.Empty,
            ImmutableDictionary<string, ImmutableArray<string>>.Empty, ImmutableDictionary<string, EncounterCompiledPresentation>.Empty, ImmutableDictionary<string, EncounterCompiledDamageRoute>.Empty, ImmutableDictionary<string, EncounterCompiledEventGate>.Empty, ImmutableDictionary<string, EncounterCompiledPlacement>.Empty,
            ImmutableDictionary<string, double>.Empty.Add("original/stats.maximumHpMultiplier", 1.25).Add("original/" + EncounterActorProperties.ReflectResistanceMultiplier, 0));
        BattleDefinition saved = GameResourceHost.ApplyCompiledEncounterDefinition(baseline, binding, compiled);
        int publications = 0;
        BattleDefinition Current(BattleDefinition definition, EncounterBindingContext selected)
        {
            publications++;
            throw new InvalidOperationException("Current publication must not replace an entered battle.");
        }
        BattleDefinition restored = GameWorld.ApplyEncounterBattlePolicy(baseline, context, saved, Current);
        Assert.Equal(0, publications);
        Assert.Same(compiled, restored.EncounterMod);
        Assert.Equal(1250, restored.Combatants[1].MaximumHp);
        Assert.Equal(1250, restored.Combatants[1].CurrentHp);
        Assert.Equal((byte)0, restored.Combatants[1].NativeStatusResistances[15]);
        Assert.Equal(allocation.Resistances[15], baseline.Combatants[1].NativeStatusResistances[15]);
        GameWorld.ValidateBattleDefinition(restored, saved);
        BattleDefinition changedParty = baseline with { Combatants = [baseline.Combatants[0] with { Attack = 99 }, baseline.Combatants[1]] };
        BattleDefinition invalid = GameWorld.ApplyEncounterBattlePolicy(changedParty, context, saved, Current);
        Assert.Contains(nameof(BattleDefinition.Combatants), Assert.Throws<InvalidDataException>(() => GameWorld.ValidateBattleDefinition(invalid, saved)).Message);
        BattleDefinition changedResource = baseline with { StageId = 1 };
        Assert.Throws<InvalidDataException>(() => GameWorld.ApplyEncounterBattlePolicy(changedResource, context, saved, Current));
        BattleDefinition changedBinding = saved with { EncounterMod = compiled with { OriginalActorId = "enemy-1" } };
        Assert.Throws<InvalidDataException>(() => GameWorld.ApplyEncounterBattlePolicy(baseline, context, changedBinding, Current));
    }

    [Theory]
    [InlineData("App.Tests.ActiveBattleEncounterPolicyCompatibilityTests.1")]
    [InlineData("App.Tests.ActiveBattleEncounterPolicyCompatibilityTests.2")]
    public void Installed_active_battles_keep_entered_policy_live_state_and_next_callback_after_publication_changes(string resourceId)
    {
        string directory = TestResources.GameDirectory;
        string path = TestResources.FixturePath(resourceId);
        byte[] originalHash;
        using (FileStream original = File.OpenRead(path))
        {
            originalHash = SHA256.HashData(original);
        }
        using GameResourceHost selected = new(directory, watch: false);
        selected.ConnectEncounterMods();
        using GameAssets assets = new(directory, resources: selected.Resources, modelOverrides: selected);
        GameSession session = new(PlayerStateLoader.Load(path, directory, sharedAssets: assets, resources: selected.Resources));
        BattleSnapshot saved = Assert.IsType<BattleSnapshot>(session.State.Battle);
        Assert.Equal(EncounterBindingResolver.Disc1Edea, saved.Definition.EncounterMod!.Encounter);
        int publications = 0;
        BattleDefinition Current(BattleDefinition definition, EncounterBindingContext context)
        {
            publications++;
            return selected.ApplyEncounterMods(definition, context);
        }
        using GameWorld source = new(session, directory, resources: selected.Resources, sharedAssets: assets, modelOverrides: selected, readSceneMods: () => selected.SceneMods, readGameplayPolicy: () => selected.GameplayPolicy, applyEncounterMods: Current, validateWorldPresentation: false);
        source.EnsureField();
        Assert.Equal(0, publications);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(saved), JsonSerializer.SerializeToElement(source.Battle!.Capture())));
        var request = source.Events.PendingInteractiveBattle!;
        BattleDefinition nextEncounter = source.CreateEncounterBattle(request.Encounter, saved.Definition.NativeSeed, request.Flags);
        Assert.Equal(1, publications);
        if (selected.EncounterMods.Layers.IsEmpty)
        {
            Assert.Null(nextEncounter.EncounterMod);
        }
        GameSession copySession = new(session.PrepareSnapshot());
        using GameWorld copy = new(copySession, directory, resources: selected.Resources, sharedAssets: assets, modelOverrides: selected, readSceneMods: () => selected.SceneMods, readGameplayPolicy: () => selected.GameplayPolicy, applyEncounterMods: Current, validateWorldPresentation: false);
        copy.EnsureField();
        Assert.Equal(1, publications);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(source.Battle.Capture()), JsonSerializer.SerializeToElement(copy.Battle!.Capture())));
        source.ApplyBattleInput(new(BattleInputKind.Tick));
        copy.ApplyBattleInput(new(BattleInputKind.Tick));
        Assert.Equal(1, publications);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(source.Battle.Capture()), JsonSerializer.SerializeToElement(copy.Battle.Capture())));
        using FileStream untouched = File.OpenRead(path);
        Assert.Equal(originalHash, SHA256.HashData(untouched));
    }
}
