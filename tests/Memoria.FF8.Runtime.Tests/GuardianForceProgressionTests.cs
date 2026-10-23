using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class GuardianForceProgressionTests
{
    [Fact]
    public void ExternalCompatibilityProgressUsesSavedIdentityMappingAndSurvivesLoad()
    {
        NativeCharacterVitals source = MagicSystemFixture.Create();
        var native = source.MenuDefinitions.Learning.SetItem(0, source.MenuDefinitions.Learning[0].SetItem(0, new(1, 255, 100)));
        source = source with
        {
            MenuDefinitions = source.MenuDefinitions with
            {
                Learning = native
            }
        };
        var templates = source.ResolveGuardianForceTemplates().SetItem(0, [new(100, false, 20), new(101, false, 20)]);
        source = source.ApplyGuardianForceTemplates(templates);
        source.GuardianForces[0] = source.GuardianForces[0] with
        {
            AbilityProgress = source.GuardianForces[0].AbilityProgress.SetItem(0, 9),
            Learning = 101,
            LearnedBank = source.GuardianForces[0].LearnedBank with
            {
                High = 1UL << (101 - 64)
            }
        };
        GameSession session = new(new GameState { CharacterVitals = source });
        Assert.Equal(9, session.CharacterVitals!.GuardianForceAbilities(0).Single(entry => entry.Ability == 100).Progress);
        Assert.True(session.CharacterVitals.GuardianForces[0].HasLearnedAbility(101));
        Assert.Equal(100, session.CharacterVitals.GuardianForces[0].Learning);
        Assert.NotEmpty(session.CharacterVitals.GuardianForceCompatibilityDiagnostics);
        Assert.False(session.CharacterVitals.GuardianForceOpaqueBaseline);
    }

    [Fact]
    public void AmbiguousExternalStructureBecomesAnOpaqueBaselineWithoutReplayingTheProfile()
    {
        NativeCharacterVitals source = MagicSystemFixture.Create();
        var templates = source.ResolveGuardianForceTemplates().SetItem(0, [new(100, false, 20)]);
        GuardianForceAbilityPatchDocument document = new()
        {
            DocumentId = "prior"
        };
        source = source.ApplyGuardianForceTemplates(templates) with
        {
            GuardianForceProfile = new(source.ResolveGuardianForceTemplates(), "source", [new("Memoria/prior", document)], [], [])
        };
        source = source with
        {
            MenuDefinitions = source.MenuDefinitions with
            {
                Learning = source.MenuDefinitions.Learning.SetItem(0, source.MenuDefinitions.Learning[0].SetItem(0, new(1, 255, 101)))
            }
        };
        source.GuardianForces[0] = source.GuardianForces[0] with
        {
            AbilityProgress = source.GuardianForces[0].AbilityProgress.SetItem(0, 7)
        };
        NativeCharacterVitals recovered = new GameSession(new GameState { CharacterVitals = source }).CharacterVitals!;
        Assert.True(recovered.GuardianForceOpaqueBaseline);
        Assert.Null(recovered.GuardianForceProfile);
        Assert.Equal("prior", recovered.GuardianForceRecoveryProfile!.AppliedLayers[0].Document.DocumentId);
        Assert.Contains(recovered.GuardianForceAbilities(0), entry => entry.Ability == 101 && entry.Progress == 7);
        Assert.DoesNotContain(recovered.ResolveGuardianForceTemplates()[0], entry => entry.Ability == 100);
        Assert.Contains(recovered.GuardianForceRecoveryTemplates[0], entry => entry.Ability == 100);
        Assert.NotNull(recovered.GuardianForceRecoveryProgression[0]);
        Assert.Contains(recovered.GuardianForces[0].Progression!.Abilities, entry => entry.Ability == 100 && !entry.Active);
        Assert.False(recovered.GuardianForces[0].HasLearnedAbility(100));
    }

    [Fact]
    public void FlagOnlyAcquisitionInitializesFinalManagedDefaultsAtTheSessionBoundary()
    {
        NativeCharacterVitals source = MagicSystemFixture.Create();
        source.GuardianForces[15] = source.GuardianForces[15] with
        {
            Flags = 0
        };
        source = source with
        {
            MenuDefinitions = source.MenuDefinitions with
            {
                InitialStates = source.GuardianForces.Select(gf => new NativeGuardianForceInitialState(gf.LearnedBank, gf.AbilityProgress, gf.Learning, gf.ForgottenAbilities)).ToImmutableArray()
            }
        };
        var templates = source.ResolveGuardianForceTemplates().SetItem(15, [new(100, true, 20)]);
        source = source.ApplyGuardianForceTemplates(templates);
        GameSession session = new(new GameState { CharacterVitals = source });
        NativeCharacterVitals acquisition = session.CharacterVitals!.DeepCopy();
        acquisition.GuardianForces[15] = acquisition.GuardianForces[15] with
        {
            Flags = 1
        };
        session.SetCharacterVitals(acquisition);
        Assert.True(session.CharacterVitals!.GuardianForces[15].HasLearnedAbility(100));
        Assert.True(session.CharacterVitals.GuardianForces[15].Progression!.Initialized);
    }

    [Fact]
    public void AuthoredDisplayPermutationDoesNotChangeLearningPriority()
    {
        NativeCharacterVitals source = MagicSystemFixture.Create();
        var templates = source.ResolveGuardianForceTemplates().SetItem(0, [new(101, false, 20, LearningPriority: 2), new(100, false, 20, LearningPriority: 1)]);
        source = source.ApplyGuardianForceTemplates(templates);
        Assert.Equal(new[] { 101, 100 }, source.GuardianForceAbilities(0).Select(entry => entry.Ability));
        Assert.Equal(100, source.GuardianForces[0].Learning);
    }

    [Fact]
    public void ProfileVersionAndContributionOwnershipAreValidated()
    {
        NativeCharacterVitals source = MagicSystemFixture.Create();
        GuardianForceAbilityPatchDocument document = new()
        {
            DocumentId = "converted"
        };
        GuardianForceAbilityProfile profile = new(source.ResolveGuardianForceTemplates(), "source", [new("Memoria/provider", document)], [document], ["converted"]);
        profile.Validate();
        Assert.Throws<InvalidDataException>(() => (profile with { SchemaVersion = 2 }).Validate());
        Assert.Throws<InvalidDataException>(() => (profile with { ExcludedDocuments = [] }).Validate());
        Assert.Throws<InvalidDataException>(() => (profile with { LocalDocuments = [document, document] }).Validate());
        Assert.Throws<InvalidDataException>(() => (profile with { AppliedLayers = [new("Memoria/provider", document), new("Memoria/duplicate", document)] }).Validate());
    }

    [Fact]
    public void ExplicitResetReviewNamesExactLostApAndJunctionAssignments()
    {
        NativeCharacterVitals source = MagicSystemFixture.Create();
        var templates = source.ResolveGuardianForceTemplates().SetItem(0, [new(21, true, 10), new(100, false, 20)]);
        source = source.ApplyGuardianForceTemplates(templates).SelectGuardianForceLearning(0, 100).AwardGuardianForceAbilityPoints(0, 8);
        source.Records[0] = source.Records[0] with
        {
            Junction = source.Records[0].Junction with
            {
                GuardianForces = 1,
                Commands = 21
            }
        };
        GameSession session = new(new GameState { CharacterVitals = source });
        var updated = templates.SetItem(0, [new(100, false, 20)]);
        var preview = session.PreviewGuardianForceAbilities(updated, resets: [new(0, 100, false)]);
        Assert.Contains(preview.Consequences, text => text.Contains("AP=8 -> Learned=False, Forgotten=False, AP=0", StringComparison.Ordinal));
        Assert.Contains(preview.Consequences, text => text.Contains("Character 0: command slot 1, ability 21 -> 0", StringComparison.Ordinal));
        Assert.Equal(8, session.CharacterVitals!.GuardianForceAbilities(0).Single(entry => entry.Ability == 100).Progress);
    }

    [Theory]
    [InlineData("{\"TransitionRequested\":true}")]
    [InlineData("{\"PartyJoin\":{}}")]
    [InlineData("{\"PartySplit\":{}}")]
    [InlineData("{\"Scene\":{\"NativeTutorial\":{}}}")]
    [InlineData("{\"Scene\":{\"NativeMenuTutorial\":{}}}")]
    public void SessionRejectsUnsafeScriptBoundariesBeforePreparingACommit(string checkpoint)
    {
        GameSession initial = GameSession.CreateColdProcess();
        initial.StartNewGame();
        NativeCharacterVitals source = MagicSystemFixture.Create();
        JsonObject events = JsonNode.Parse(checkpoint)!.AsObject();
        events["EventMemory"] = JsonSerializer.SerializeToNode(initial.State.EventMemory);
        GameSession session = new(initial.State with { Scene = SceneKind.Field, CharacterVitals = source, EventState = JsonSerializer.SerializeToElement(events) });
        GuardianForceAbilityApplyPreview preview = session.PreviewGuardianForceAbilities(source.ResolveGuardianForceTemplates());
        string before = JsonSerializer.Serialize(session.State);
        Assert.False(session.CanApplyGuardianForceAbilities(out string? reason));
        Assert.False(string.IsNullOrEmpty(reason));
        Assert.Throws<InvalidOperationException>(() => session.PrepareGuardianForceAbilities(preview));
        Assert.Equal(before, JsonSerializer.Serialize(session.State));
    }

    [Theory]
    [InlineData("{\"TransitionRequested\":true}")]
    [InlineData("{\"PartyJoin\":{}}")]
    [InlineData("{\"PartySplit\":{}}")]
    [InlineData("{\"Scene\":{\"NativeTutorial\":{}}}")]
    [InlineData("{\"Scene\":{\"NativeMenuTutorial\":{}}}")]
    public void WorldMapIgnoresHistoricalFieldMutationBoundaries(string checkpoint)
    {
        GameSession initial = GameSession.CreateColdProcess();
        initial.StartNewGame();
        NativeCharacterVitals source = MagicSystemFixture.Create();
        JsonObject events = JsonNode.Parse(checkpoint)!.AsObject();
        events["EventMemory"] = JsonSerializer.SerializeToNode(initial.State.EventMemory);
        JsonElement historical = JsonSerializer.SerializeToElement(events);
        GameSession session = new(initial.State with { Scene = SceneKind.WorldMap, WorldArrival = new WorldTransition(0, 0, 0), CharacterVitals = source, EventState = historical });
        var templates = source.ResolveGuardianForceTemplates().SetItem(0, [new(100, true, 10)]);
        GuardianForceAbilityApplyPreview preview = session.PreviewGuardianForceAbilities(templates);
        Assert.True(session.CanApplyGuardianForceAbilities(out string? reason));
        Assert.Null(reason);
        Assert.Null(session.MagicMutationUnavailableReason);
        GuardianForceAbilityPreparedApply prepared = session.PrepareGuardianForceAbilities(preview);
        session.CommitPreparedGuardianForceAbilities(prepared);
        Assert.True(session.CharacterVitals!.GuardianForceAbilities(0).Single().Learned);
        Assert.True(JsonElement.DeepEquals(historical, session.State.EventState!.Value));
    }

    [Fact]
    public void WorldTransitionFixtureAllowsGuardianChangesAndPreservesWorldAcrossSaveLoad()
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("guardian-force-world-transition")));
        GameState before = session.State;
        Assert.Equal(SceneKind.WorldMap, before.Scene);
        Assert.True(before.EventState!.Value.GetProperty("TransitionRequested").GetBoolean());
        Assert.Null(session.MagicMutationUnavailableReason);
        Assert.True(session.CanApplyGuardianForceAbilities(out _));
        NativeCharacterVitals source = session.CharacterVitals!;
        var templates = source.ResolveGuardianForceTemplates();
        GuardianForceAbilityTemplate ability = templates[0][0];
        templates = templates.SetItem(0, templates[0].SetItem(0, ability with { RequiredPoints = ability.RequiredPoints + 1 }));
        GuardianForceAbilityApplyPreview preview = session.PreviewGuardianForceAbilities(templates);
        GuardianForceAbilityPreparedApply prepared = session.PrepareGuardianForceAbilities(preview);
        session.CommitPreparedGuardianForceAbilities(prepared);
        Assert.Equal(ability.RequiredPoints + 1, session.CharacterVitals!.ResolveGuardianForceTemplates()[0][0].RequiredPoints);
        Assert.Equal(before.World, session.State.World);
        Assert.True(session.State.EventState!.Value.GetProperty("TransitionRequested").GetBoolean());
        NativeCharacterVitals eventOwner = session.State.EventState.Value.GetProperty("CharacterVitals").Deserialize<NativeCharacterVitals>()!;
        Assert.Equal(ability.RequiredPoints + 1, eventOwner.ResolveGuardianForceTemplates()[0][0].RequiredPoints);
        string path = TestResources.ArtifactFile("guardian-force-world-transition.json");
        session.Save(path, compact: true);
        GameSession restored = new(GameSession.Load(path));
        Assert.True(restored.CanApplyGuardianForceAbilities(out _));
        Assert.Equal(ability.RequiredPoints + 1, restored.CharacterVitals!.ResolveGuardianForceTemplates()[0][0].RequiredPoints);
        Assert.True(restored.State.EventState!.Value.GetProperty("TransitionRequested").GetBoolean());
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(before.World), JsonSerializer.SerializeToElement(restored.State.World)));
        Assert.True(JsonElement.DeepEquals(session.State.EventState.Value, restored.State.EventState.Value));
    }

    [Fact]
    public void DormantLearnedAbilitiesConferNoShopOrDirectReaderEffects()
    {
        NativeCharacterVitals source = MagicSystemFixture.Create();
        var templates = source.ResolveGuardianForceTemplates().SetItem(0, [new(97, true, 10)]);
        NativeCharacterVitals current = source.ApplyGuardianForceTemplates(templates);
        Assert.True(current.GuardianForces[0].HasLearnedAbility(97));
        Assert.NotEqual(0u, current.ShopAbilityMask() & (1u << 5));
        current = current.ApplyGuardianForceTemplates(templates.SetItem(0, []));
        Assert.False(current.GuardianForces[0].HasLearnedAbility(97));
        Assert.Equal(0u, current.ShopAbilityMask() & (1u << 5));
        // A compatibility mirror cannot override managed progression.
        current.GuardianForces[0] = current.GuardianForces[0] with
        {
            LearnedBank = new(0, 1UL << (97 - 64))
        };
        Assert.False(current.GuardianForces[0].HasLearnedAbility(97));
        Assert.Equal(0u, current.ShopAbilityMask() & (1u << 5));
        NativeCharacterVitals restored = current.ApplyGuardianForceTemplates(templates);
        Assert.True(restored.GuardianForces[0].HasLearnedAbility(97));
    }

    [Fact]
    public void ExceptionalUnacquiredProgressDoesNotMaterializeNewDefaultsBeforeAcquisition()
    {
        NativeCharacterVitals source = MagicSystemFixture.Create();
        source.GuardianForces[0] = source.GuardianForces[0] with
        {
            Flags = 0
        };
        source = source with
        {
            MenuDefinitions = source.MenuDefinitions with
            {
                InitialStates = source.GuardianForces.Select(gf => new NativeGuardianForceInitialState(gf.LearnedBank, gf.AbilityProgress, gf.Learning, gf.ForgottenAbilities)).ToImmutableArray()
            }
        };
        source.GuardianForces[0] = source.GuardianForces[0] with
        {
            LearnedBank = source.GuardianForces[0].LearnedBank with
            {
                High = 1UL << (100 - 64)
            }
        };
        var templates = source.ResolveGuardianForceTemplates().SetItem(0, [new(101, true, 10)]);
        NativeCharacterVitals current = source.ApplyGuardianForceTemplates(templates);
        Assert.DoesNotContain(current.GuardianForces[0].Progression!.Abilities, entry => entry.Ability == 101);
        current = current.ApplyGuardianForceTemplates(templates.SetItem(0, [new(102, true, 10)])).AcquireGuardianForce(0);
        Assert.DoesNotContain(current.GuardianForces[0].Progression!.Abilities, entry => entry.Ability == 101);
        Assert.True(current.GuardianForces[0].HasLearnedAbility(102));
    }

    [Fact]
    public void UntouchedUnacquiredDefaultsMaterializeOnlyOnAcquisition()
    {
        NativeCharacterVitals source = MagicSystemFixture.Create();
        source.GuardianForces[0] = source.GuardianForces[0] with
        {
            Flags = 0
        };
        source = source with
        {
            MenuDefinitions = source.MenuDefinitions with
            {
                InitialStates = source.GuardianForces.Select(gf => new NativeGuardianForceInitialState(gf.LearnedBank, gf.AbilityProgress, gf.Learning, gf.ForgottenAbilities)).ToImmutableArray()
            }
        };
        var templates = source.ResolveGuardianForceTemplates().SetItem(0, [new(100, true, 10)]);
        NativeCharacterVitals unacquired = source.ApplyGuardianForceTemplates(templates);
        Assert.False(unacquired.GuardianForces[0].Progression!.Initialized);
        Assert.Empty(unacquired.GuardianForces[0].Progression!.Abilities);
        var updated = templates.SetItem(0, [new(101, true, 10)]);
        NativeCharacterVitals acquired = unacquired.ApplyGuardianForceTemplates(updated).AcquireGuardianForce(0);
        Assert.True(acquired.GuardianForces[0].Progression!.Initialized);
        Assert.True(acquired.GuardianForceAbilities(0).Single().Learned);
        Assert.Equal(101, acquired.GuardianForceAbilities(0).Single().Ability);
    }

    [Fact]
    public void AcquisitionMaterializesOnlySelectedTemplateWithoutRebuildingSavedOwners()
    {
        NativeCharacterVitals source = MagicSystemFixture.Create();
        source.GuardianForces[0] = source.GuardianForces[0] with { Flags = 0x80, HitPoints = 60000, MaximumHitPoints = 1 };
        source.Records[0] = source.Records[0] with { HitPoints = new NativeHpWord(60000, 60000) };
        source = source with
        {
            MenuDefinitions = source.MenuDefinitions with
            {
                Templates = source.ResolveGuardianForceTemplates().SetItem(0, [new(100, true, 10)])
            }
        };
        string original = JsonSerializer.Serialize(source);

        NativeCharacterVitals acquired = source.AcquireGuardianForce(0);

        Assert.Equal((byte)0x81, acquired.GuardianForces[0].Flags);
        Assert.True(acquired.GuardianForces[0].Progression!.Initialized);
        Assert.True(acquired.GuardianForces[0].HasLearnedAbility(100));
        Assert.Equal((ushort)60000, acquired.GuardianForces[0].HitPoints);
        Assert.Equal((ushort)1, acquired.GuardianForces[0].MaximumHitPoints);
        Assert.Equal(source.GuardianForces.Skip(1), acquired.GuardianForces.Skip(1));
        Assert.Equal(source.Records, acquired.Records);
        Assert.Equal(source.CombatParty, acquired.CombatParty);
        Assert.Equal(original, JsonSerializer.Serialize(source));
        NativeCharacterVitals restored = JsonSerializer.Deserialize<NativeCharacterVitals>(JsonSerializer.Serialize(acquired))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(acquired), JsonSerializer.Serialize(restored.AcquireGuardianForce(0)));
    }

    [Fact]
    public void SessionPreviewIsPureAndApplyChecksRevisionAndUpdatesSnapshotOwners()
    {
        NativeCharacterVitals source = MagicSystemFixture.Create();
        JsonElement checkpoint = JsonSerializer.SerializeToElement(new
        {
            CharacterVitals = source
        });
        GameSession session = new(new GameState { CharacterVitals = source, EventState = checkpoint, DepartingFieldState = checkpoint });
        string before = JsonSerializer.Serialize(session.State);
        var templates = source.ResolveGuardianForceTemplates().SetItem(0, [new(100, true, 10)]);
        GuardianForceAbilityApplyPreview preview = session.PreviewGuardianForceAbilities(templates, sourceRevision: "source", modGeneration: 7);
        Assert.Equal(before, JsonSerializer.Serialize(session.State));
        Assert.Throws<InvalidOperationException>(() => session.ApplyGuardianForceAbilities(preview, "source", 8));
        Assert.Equal(before, JsonSerializer.Serialize(session.State));
        GuardianForceAbilityPreparedApply prepared = session.PrepareGuardianForceAbilities(preview, "source", 7);
        Assert.Equal(before, JsonSerializer.Serialize(session.State));
        Assert.True(session.CanApplyGuardianForceAbilities(out _));
        session.CommitPreparedGuardianForceAbilities(prepared);
        Assert.True(session.CharacterVitals!.GuardianForceAbilities(0).Single().Learned);
        NativeCharacterVitals eventOwner = session.State.EventState!.Value.GetProperty("CharacterVitals").Deserialize<NativeCharacterVitals>()!;
        NativeCharacterVitals departingOwner = session.State.DepartingFieldState!.Value.GetProperty("CharacterVitals").Deserialize<NativeCharacterVitals>()!;
        Assert.True(eventOwner.GuardianForceAbilities(0).Single().Learned);
        Assert.True(departingOwner.GuardianForceAbilities(0).Single().Learned);
        Assert.Throws<InvalidOperationException>(() => session.ApplyGuardianForceAbilities(preview, "source", 7));
    }

    [Fact]
    public void ExpandedAbilitiesLearnWithoutCapacityAndPreserveDormantProgress()
    {
        NativeCharacterVitals source = MagicSystemFixture.Create();
        ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> templates = source.ResolveGuardianForceTemplates();
        templates = templates.SetItem(0, Enumerable.Range(1, 115).Select(id => new GuardianForceAbilityTemplate(id, id < 90, 10, LearningPriority: id)).ToImmutableArray());
        NativeCharacterVitals expanded = source.ApplyGuardianForceTemplates(templates);
        Assert.True(expanded.GuardianForceAbilities(0).Count > 22);
        expanded = expanded.SelectGuardianForceLearning(0, 100).AwardGuardianForceAbilityPoints(0, 4);
        Assert.Equal(4, expanded.GuardianForceAbilities(0).Single(entry => entry.Ability == 100).Progress);
        NativeCharacterVitals absent = expanded.ApplyGuardianForceTemplates(templates.SetItem(0, templates[0].Where(entry => entry.Ability != 100).ToImmutableArray()));
        Assert.DoesNotContain(absent.GuardianForceAbilities(0), entry => entry.Ability == 100);
        NativeCharacterVitals restored = absent.ApplyGuardianForceTemplates(templates);
        Assert.Equal(4, restored.GuardianForceAbilities(0).Single(entry => entry.Ability == 100).Progress);
        Assert.Equal(0, source.GuardianForces[0].AbilityProgress[0]);
    }

    [Fact]
    public void ReplacingAndChangingDefaultsNeverTransfersOrResetsEarnedProgress()
    {
        NativeCharacterVitals source = MagicSystemFixture.Create();
        var templates = source.ResolveGuardianForceTemplates().SetItem(0, [new(100, false, 20), new(101, false, 20, LearningPriority: 1)]);
        NativeCharacterVitals current = source.ApplyGuardianForceTemplates(templates).SelectGuardianForceLearning(0, 100).AwardGuardianForceAbilityPoints(0, 8);
        var changed = templates.SetItem(0, [new(101, true, 3)]);
        current = current.ApplyGuardianForceTemplates(changed);
        Assert.False(current.GuardianForceAbilities(0).Single().Learned);
        Assert.Equal(0, current.GuardianForceAbilities(0).Single().Progress);
        current = current.ApplyGuardianForceTemplates(templates.SetItem(0, [new(100, true, 3)]));
        NativeGuardianForceAbility ability = current.GuardianForceAbilities(0).Single();
        Assert.False(ability.Learned);
        Assert.Equal(8, ability.Progress);
        current = current.AwardGuardianForceAbilityPoints(0, 1);
        Assert.True(current.GuardianForceAbilities(0).Single().Learned);
    }

    [Fact]
    public void ForgetReteachAndSerializationRetainIdentity()
    {
        NativeCharacterVitals source = MagicSystemFixture.Create();
        var templates = source.ResolveGuardianForceTemplates().SetItem(0, [new(100, true, 10)]);
        NativeCharacterVitals current = source.ApplyGuardianForceTemplates(templates).ForgetGuardianForceAbility(0, 100);
        Assert.Empty(current.GuardianForceAbilities(0));
        current = current.TeachGuardianForceAbility(0, 100);
        Assert.True(current.GuardianForceAbilities(0).Single().Learned);
        NativeCharacterVitals restored = JsonSerializer.Deserialize<NativeCharacterVitals>(JsonSerializer.Serialize(current))!;
        restored.Validate();
        Assert.True(restored.GuardianForceAbilities(0).Single().Learned);
    }

    [Fact]
    public void LearningPriorityIsIndependentOfDisplayOrder()
    {
        NativeCharacterVitals source = MagicSystemFixture.Create();
        var templates = source.ResolveGuardianForceTemplates().SetItem(0, [new(101, false, 1, LearningPriority: 2), new(100, false, 1, LearningPriority: 1)]);
        NativeCharacterVitals current = source.ApplyGuardianForceTemplates(templates);
        Assert.Equal(100, current.GuardianForces[0].Learning);
        current = current.AwardGuardianForceAbilityPoints(0, 1);
        Assert.Equal(101, current.GuardianForces[0].Learning);
    }
}
