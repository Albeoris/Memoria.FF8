using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class EncounterPolicyRuntimeTests
{
    [Fact]
    public void CanonicalMagicIdsResolveSelectedNativeRecordsIndependentlyOfDisplayName()
    {
        EncounterCompiledDefinition compiled = Compile("""
            {"id":"scan","when":{"ownTurn":3},"action":{"kind":"Cast","spell":"Scan","target":"randomOpponent","casts":"nativeDouble"}}
            """, [Magic(50, "Localized display")]);
        EncounterCompiledRule rule = compiled.Policies["mod/policy"].Select(3);
        Assert.Equal("Scan", rule.Action.Spells.Single().CanonicalName);
        Assert.Equal("Localized display", rule.Action.Spells.Single().NativeAction.Name);
        Assert.Equal(50, rule.Action.Spells.Single().NativeAction.ActionId);
        Assert.True(rule.Action.NativeDouble);
    }

    [Fact]
    public void MissingSelectedSpellFailsBeforePublishingACompiledDefinition()
    {
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => Compile("""
            {"id":"scan","when":{"ownTurn":3},"action":{"kind":"Cast","spell":"Scan","target":"randomOpponent"}}
            """, []));
        Assert.Contains("2/50", error.Message);
        Assert.Contains("scan", error.Message);
    }

    [Fact]
    public void OwnTurnRangesUseExplicitPriorityAndDoNotWrapAfter255()
    {
        EncounterCompiledDefinition compiled = Compile("""
            {"id":"all","priority":1,"when":{"ownTurnAtLeast":1},"action":{"kind":"Cast","spell":"Scan","target":"randomOpponent"}},
            {"id":"later","priority":2,"when":{"ownTurnAtLeast":5},"action":{"kind":"Cast","spell":"Haste","target":"self"}}
            """, [Magic(50), Magic(35)]);
        EncounterCompiledPolicy policy = compiled.Policies["mod/policy"];
        Assert.Equal("all", policy.Select(4).Id);
        Assert.Equal("later", policy.Select(5).Id);
        Assert.Equal("later", policy.Select(256).Id);
        Assert.Equal("later", policy.Select(ulong.MaxValue).Id);
    }

    [Fact]
    public void UncoveredOwnTurnFailsInsteadOfExecutingUnrelatedNativeNormalAi()
    {
        EncounterCompiledDefinition compiled = Compile("""
            {"id":"one","when":{"ownTurn":1},"action":{"kind":"Cast","spell":"Scan","target":"self"}}
            """, [Magic(50)]);
        Assert.Throws<InvalidDataException>(() => compiled.Policies["mod/policy"].Select(2));
    }

    [Fact]
    public void AllOpponentsOverrideSingleTargetNativeFlagsLocally()
    {
        EncounterCompiledDefinition compiled = Compile("""
            {"id":"blind","when":{"ownTurn":1},"action":{"kind":"Cast","spell":"Blind","target":"allOpponents"}}
            """, [Magic(38)]);
        EncounterPolicyDecision decision = Select(compiled, [Enemy(), Party("z"), Party("a"), Party("inactive") with { Active = false }, Party("dead") with { Living = false }])!;
        Assert.Equal(new[] { "a", "z" }, decision.TargetIds);
        Assert.True(decision.OverrideNativeTargeting);
        Assert.Equal((byte?)0x10, decision.Spell!.NativeAction.TargetFlags);
    }

    [Fact]
    public void ResolvingRepeatedCastDoesNotAdvanceMainTurnAndCanChooseANewOpponent()
    {
        EncounterCompiledDefinition compiled = Compile("""
            {"id":"scan","when":{"ownTurn":3},"action":{"kind":"Cast","spell":"Scan","target":"randomOpponent","casts":"nativeDouble"}}
            """, [Magic(50)]);
        EncounterActionCursor cursor = new(2);
        ImmutableArray<EncounterPolicyActor> actors = [Enemy(), Party("a"), Party("b")];
        EncounterPolicyDecision first = EncounterPolicyRuntime.Select(compiled, "mod/policy", cursor, "enemy", actors, new(), _ => 0)!;
        EncounterCompiledRule rule = compiled.Policies[first.PolicyId].Rules.Single(candidate => candidate.Id == first.RuleId);
        EncounterPolicyDecision second = EncounterPolicyRuntime.Resolve(compiled, first.PolicyId, rule, first.OwnTurn, "enemy", actors, new(), count => count - 1)!;
        Assert.Equal("a", first.TargetIds.Single());
        Assert.Equal("b", second.TargetIds.Single());
        Assert.Equal(3ul, second.OwnTurn);
        Assert.Equal(2ul, cursor.CommittedTurns);
    }

    [Fact]
    public void ScanAwareSelectionReceivesOnlyObservedKnowledgeAndUsesNativeElementIdentity()
    {
        EncounterCompiledDefinition compiled = Compile("""
            {"id":"offense","when":{"ownTurnAtLeast":5},"action":{"kind":"CastFromPool","spells":["Firaga","Blizzaga"],"targetPolicy":"scanKnownWeakestElseRandom"}}
            """, [Magic(3, element: 1), Magic(6, element: 2)]);
        BattleScanKnowledge knowledge = new BattleScanKnowledge().Observe(new(BattleObserverSide.Enemy, "party", "enemy", 1, Enumerable.Repeat(BattleKnownAffinity.FromNativeDefense(800), 8).Select((affinity, index) => index == 0 ? BattleKnownAffinity.FromNativeDefense(700) : affinity).ToImmutableArray()));
        EncounterPolicyDecision selected = EncounterPolicyRuntime.Select(compiled, "mod/policy", new(4), "enemy", [Enemy(), Party("party")], knowledge, _ => 0)!;
        Assert.Equal("Firaga", selected.Spell!.CanonicalName);
        Assert.Equal("party", selected.TargetIds.Single());
    }

    [Fact]
    public void InvalidRandomIndexIsDiagnosedAndNoLiveTargetsProduceNoDecision()
    {
        EncounterCompiledDefinition compiled = Compile("""
            {"id":"scan","when":{"ownTurn":1},"action":{"kind":"Cast","spell":"Scan","target":"randomOpponent"}}
            """, [Magic(50)]);
        Assert.Null(Select(compiled, [Enemy()]));
        Assert.Throws<InvalidDataException>(() => EncounterPolicyRuntime.Select(compiled, "mod/policy", new(), "enemy", [Enemy(), Party("party")], new(), count => count));
    }

    [Fact]
    public void CompiledGraphRoundTripsThroughTheExistingJsonSnapshotSerializer()
    {
        EncounterCompiledDefinition compiled = Compile("""
            {"id":"scan","when":{"ownTurnAtLeast":1},"action":{"kind":"Cast","spell":"Scan","target":"randomOpponent","casts":"nativeDouble"}}
            """, [Magic(50)]);
        string json = JsonSerializer.Serialize(compiled);
        EncounterCompiledDefinition restored = JsonSerializer.Deserialize<EncounterCompiledDefinition>(json)!;
        EncounterPolicyDecision selected = Select(restored, [Enemy(), Party("party")])!;
        Assert.Equal(compiled.GenerationIdentity, restored.GenerationIdentity);
        EncounterPatchOrigin origin = Assert.Single(restored.Origins);
        Assert.Equal("package", origin.PackageId);
        Assert.Equal("gameplay/encounters/test.json", origin.PayloadPath);
        Assert.Equal("mod", origin.DocumentId);
        Assert.Equal("define", origin.OperationId);
        Assert.Equal("mod/policy", origin.Target);
        EncounterPatchOutcome outcome = Assert.Single(restored.Outcomes);
        Assert.Equal(origin, outcome.Origin);
        Assert.Equal("Applied", outcome.Status);
        Assert.Equal(50, selected.Spell!.NativeAction.ActionId);
        Assert.True(selected.NativeDouble);
    }

    [Fact]
    public void CompleteTemplateGraphCompilesGroupMembershipPlacementArrivalAndRouting()
    {
        EncounterPatchComposition composition = SyntheticGraph();
        BattleDefinition definition = new() { Combatants = [new() { Id = "enemy" }], NativeActions = [Magic(29)] };
        EncounterCompiledDefinition compiled = EncounterPolicyRuntime.Compile(composition, definition, new("ff8.disc1.edea", "enemy", 11, 11, [8, 9, 10], true));
        Assert.Equal(new[] { "mod/copy", "original" }, compiled.Groups["mod/family"]);
        Assert.Equal("mod/support", compiled.Templates["mod/copy"].NormalActionPolicy);
        Assert.Equal(28, compiled.Presentations["mod/arrival"].EndFrame);
        Assert.Equal("mod/left", compiled.Placements["mod/left"].Id);
        Assert.Equal("original", compiled.DamageRoutes["mod/link"].SourceActor);
        Assert.Equal("mod/copy", compiled.DamageRoutes["mod/link"].Recipients.Single());
        EncounterPolicyDecision summon = EncounterPolicyRuntime.Select(compiled, "mod/main", new(), "enemy", [Enemy(), Party("party")], new(), _ => 0)!;
        Assert.Equal(EncounterCompiledActionKind.SummonGroup, summon.Kind);
        Assert.Equal(new EncounterCompiledSpawn("mod/copy", "mod/left"), summon.Spawns.Single());
        EncounterPolicyDecision protect = EncounterPolicyRuntime.Select(compiled, "mod/support", new(), "copy-instance", [Enemy(), new("copy-instance", "mod/copy", false, true, true, false), new("unrelated-ally", "other-mod", false, true, true, false)], new(), _ => 0)!;
        Assert.Equal(new[] { "copy-instance", "enemy" }, protect.TargetIds);
        string json = JsonSerializer.Serialize(compiled);
        Assert.Equal(0, JsonSerializer.Deserialize<EncounterCompiledDefinition>(json)!.Presentations["mod/arrival"].OpacityFrom);
    }

    [Fact]
    public void FifthActorIsRejectedBeforeAnyTemplateIsPublished()
    {
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => SyntheticGraph(["original", "other-a", "other-b", "other-c"]));
        Assert.Contains("requires 5", error.Message);
        Assert.Contains("no actors were evicted", error.Message);
    }

    private static EncounterPatchComposition SyntheticGraph(ImmutableArray<string> baselineActors = default)
    {
        const string json = """
            {
              "schemaVersion":1,"documentId":"mod","encounter":"ff8.disc1.edea","atomic":true,"sourceProfile":"Classic","requiredActorCapacity":2,
              "placements":[{"id":"left","x":-100,"y":0,"z":0}],
              "operations":[
                {"id":"family","kind":"EnsureGroup","group":"family","members":["original"]},
                {"id":"arrival","kind":"AddPresentation","presentation":"arrival","clip":"002","frameNumbering":"zeroBased","startFrame":8,"endFrame":28,"opacity":{"from":0,"to":1,"curve":"linear"},"loop":false,"after":"nativeIdle"},
                {"id":"copy","kind":"AddActorTemplate","actorTemplate":"copy","inheritFrom":"original","inherit":["model","resolvedCombatStats","drawDefinitions"],"level":{"fromActor":"original","at":"spawn"},"maximumHp":{"fromActor":"original","at":"spawn"},"initialHp":"maximum","initialAtb":0,"transientState":"fresh","group":"family","normalActionPolicy":"support","nativeLifecycle":"ordinarySummonedActor","rewardPolicy":"none","arrival":"arrival"},
                {"id":"support","kind":"AddActionPolicy","policy":"support","rules":[{"id":"protect","when":{"ownTurnAtLeast":1},"action":{"kind":"Cast","spell":"Protect","target":{"group":"family","select":"allLiving"}}}]},
                {"id":"main","kind":"AddActionPolicy","policy":"main","rules":[{"id":"summon","when":{"ownTurn":1},"action":{"kind":"SummonGroup","casterPresentation":"nativeCast","simultaneous":true,"atomic":true,"actors":[{"template":"copy","anchor":"left"}]}}]},
                {"id":"assign","kind":"SetNormalActionPolicy","actor":"original","expectedOwner":"nativeNormalAction","policy":"main","preserveNativeLifecycle":true},
                {"id":"link","kind":"AddDamageRoute","route":"link","sourceActor":"original","recipients":["copy"],"recipientSelection":"livingActivatedInstances","mode":"redirectSplitEqually","damageKinds":"allPositiveHpDamage","mitigation":"alreadyResolved","remainder":"discard","overkill":"discard","withoutRecipients":"normalDamage"},
                {"id":"gate","kind":"GateNativeEvent","actor":"original","event":"nativeTaunts","ownTurnAtLeast":5,"preserveOrder":true,"maximumQueuedMessagesPerTurn":1}
              ]
            }
            """;
        return EncounterPatchComposer.Compose(new("ff8.disc1.edea", "Classic", baselineActors.IsDefault ? ["original"] : baselineActors), [new("package", "gameplay/encounters/test.json", EncounterPatchDocument.Parse(json))]);
    }
    [Fact]
    public void AuthoredPoolWeightsCompileAndUseOneBoundedWeightedSpellDraw()
    {
        EncounterCompiledDefinition compiled = Compile("""
            {"id":"pool","when":{"ownTurnAtLeast":1},"action":{"kind":"CastFromPool","spells":["Fire","Blizzard"],"spellWeights":{"Fire":3},"target":"self"}}
            """, [Magic(1), Magic(4)]);
        List<int> draws = [];
        EncounterPolicyDecision selected = EncounterPolicyRuntime.Select(compiled, "mod/policy", new(), "enemy", [Enemy()], new(), count =>
        {
            draws.Add(count);
            return 2;
        })!;
        Assert.Equal("Fire", selected.Spell!.CanonicalName);
        Assert.Equal(3, selected.Spell.Weight);
        Assert.Equal(new[] { 4 }, draws);
    }

    [Theory]
    [InlineData("{\"Fire\":0}")]
    [InlineData("{\"Fire\":256}")]
    [InlineData("{\"Fire\":255,\"Blizzard\":2}")]
    [InlineData("{\"Scan\":2}")]
    public void InvalidOrOutOfPoolSpellWeightsAreRejected(string weights)
    {
        string rules = "{\"id\":\"pool\",\"when\":{\"ownTurn\":1},\"action\":{\"kind\":\"CastFromPool\",\"spells\":[\"Fire\",\"Blizzard\"],\"spellWeights\":" + weights + ",\"target\":\"self\"}}";
        Assert.Throws<InvalidDataException>(() => Compile(rules, [Magic(1), Magic(4)]));
    }

    [Fact]
    public void ExistingSceneActorRoleIsPreservedInsteadOfBeingScopedAsATemplate()
    {
        const string json = """
            {"schemaVersion":1,"documentId":"mod","encounter":"ff8.disc1.edea","atomic":true,"operations":[
              {"id":"link","kind":"AddDamageRoute","route":"link","sourceActor":"scene-1","recipients":["original"],"recipientSelection":"livingActivatedInstances","mode":"redirectSplitEqually","damageKinds":"allPositiveHpDamage","mitigation":"alreadyResolved","remainder":"discard","overkill":"discard","withoutRecipients":"normalDamage"}
            ]}
            """;
        EncounterPatchComposition composition = EncounterPatchComposer.Compose(new("ff8.disc1.edea", "Classic", ["original", "scene-1"]), [new("package", "gameplay/encounters/test.json", EncounterPatchDocument.Parse(json))]);
        BattleDefinition definition = new() { Combatants = [new() { Id = "enemy-0" }, new() { Id = "enemy-1" }] };
        EncounterCompiledDefinition compiled = EncounterPolicyRuntime.Compile(composition, definition, new("ff8.disc1.edea", "enemy-0", 11, 11, [8, 9, 10], true));
        Assert.Equal("scene-1", compiled.DamageRoutes["mod/link"].SourceActor);
        Assert.Equal("original", compiled.DamageRoutes["mod/link"].Recipients.Single());
    }

    [Fact]
    public void ScanPairSelectionRejectsNativeAllTargetSpellsInsteadOfChangingTheirCardinality()
    {
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => Compile("""
            {"id":"quake","when":{"ownTurnAtLeast":1},"action":{"kind":"CastFromPool","spells":["Quake"],"targetPolicy":"scanKnownWeakestElseRandom"}}
            """, [Magic(17, element: 8) with { TargetFlags = 0x40 }]));
        Assert.Contains("single-target cardinality", error.Message);
        Assert.Contains("nativeOpponentTargeting", error.Message);
    }

    [Fact]
    public void NativeOpponentTargetingPreservesQuakeAllTargetCardinality()
    {
        EncounterCompiledDefinition compiled = Compile("""
            {"id":"quake","when":{"ownTurnAtLeast":1},"action":{"kind":"CastFromPool","spells":["Quake"],"target":"nativeOpponentTargeting"}}
            """, [Magic(17, element: 8) with { TargetFlags = 0x40 }]);
        EncounterPolicyDecision decision = Select(compiled, [Enemy(), Party("a"), Party("b")])!;
        Assert.Equal(new[] { "a", "b" }, decision.TargetIds);
        Assert.False(decision.OverrideNativeTargeting);
        Assert.True(decision.Spell!.NativeAllTargets);
    }
    [Fact]
    public void ScanPrefersUnknownAndNextMainTurnAvoidsPreviousTarget()
    {
        EncounterCompiledDefinition compiled = Compile("""
            {"id":"scan","when":{"ownTurnAtLeast":1},"action":{"kind":"Cast","spell":"Scan","target":"randomOpponent"}}
            """, [Magic(50)]);
        BattleScanKnowledge knowledge = new BattleScanKnowledge().Observe(new(BattleObserverSide.Enemy, "a", "enemy", 1, Enumerable.Repeat(BattleKnownAffinity.FromNativeDefense(800), 8).ToImmutableArray()));
        ImmutableArray<EncounterPolicyActor> actors = [Enemy(), Party("a"), Party("b"), Party("c")];
        EncounterPolicyDecision first = EncounterPolicyRuntime.Select(compiled, "mod/policy", new(), "enemy", actors, knowledge, _ => 0)!;
        Assert.Equal("b", first.TargetIds.Single());
        EncounterActionCursor cursor = new EncounterActionCursor().Admit(first.TargetIds);
        EncounterActionCursor restored = JsonSerializer.Deserialize<EncounterActionCursor>(JsonSerializer.Serialize(cursor))!;
        Assert.Equal("c", EncounterPolicyRuntime.Select(compiled, "mod/policy", restored, "enemy", actors, knowledge, _ => 0)!.TargetIds.Single());
    }

    [Fact]
    public void StatusBuffPrefersAllyMissingStatusAndRetainsSoleEligibleTarget()
    {
        EncounterCompiledDefinition compiled = Compile("""
            {"id":"haste","when":{"ownTurnAtLeast":1},"action":{"kind":"Cast","spell":"Haste","target":"randomAlly"}}
            """, [Magic(35) with { Statuses1 = 2 }]);
        ImmutableArray<EncounterPolicyActor> actors = [Enemy() with { BattleStatuses = 2 }, new("copy", "copy", false, true, true, false)];
        Assert.Equal("copy", Select(compiled, actors)!.TargetIds.Single());
        Assert.Equal("enemy", EncounterPolicyRuntime.Select(compiled, "mod/policy", new EncounterActionCursor().Admit(["enemy"]), "enemy", [Enemy()], new(), _ => 0)!.TargetIds.Single());
    }

    [Theory]
    [InlineData("Protect", 29, "randomAlly", 0x20u, 0)]
    [InlineData("Shell", 30, "randomAlly", 0x40u, 0)]
    [InlineData("Haste", 35, "randomAlly", 2u, 0)]
    [InlineData("Slow", 36, "randomOpponent", 4u, 0)]
    [InlineData("Blind", 38, "randomOpponent", 0u, 8)]
    public void ExhaustedDistinctStatusTargetsRepeatWithinAuthoredSide(string spell, int spellId, string target, uint battleStatus, int savedStatus)
    {
        string rules = "{\"id\":\"status\",\"when\":{\"ownTurnAtLeast\":1},\"action\":{\"kind\":\"Cast\",\"spell\":\"" + spell + "\",\"target\":\"" + target + "\",\"casts\":\"nativeDouble\"}}";
        EncounterCompiledDefinition compiled = Compile(rules, [Magic(checked((ushort)spellId)) with { Statuses1 = battleStatus, Statuses0 = checked((ushort)savedStatus) }]);
        ImmutableArray<EncounterPolicyActor> actors = [Enemy() with { BattleStatuses = battleStatus, SavedStatuses = checked((ushort)savedStatus) }, Party("party") with { BattleStatuses = battleStatus, SavedStatuses = checked((ushort)savedStatus) }];
        string expected = target == "randomAlly" ? "enemy" : "party";
        EncounterPolicyDecision repeated = EncounterPolicyRuntime.Resolve(compiled, "mod/policy", compiled.Policies["mod/policy"].Select(1), 1, "enemy", actors, new(), _ => 0, [expected], distinctTargets: true)!;
        Assert.Equal(new[] { expected }, repeated.TargetIds.ToArray());
        Assert.Equal(1ul, repeated.OwnTurn);
    }

    [Fact]
    public void TripleCompilesAsCanonicalKernelSpell34()
    {
        EncounterCompiledDefinition compiled = Compile("""
            {"id":"triple","when":{"ownTurnAtLeast":1},"action":{"kind":"Cast","spell":"Triple","target":"self"}}
            """, [Magic(34)]);
        Assert.Equal(34, Select(compiled, [Enemy()])!.Spell!.NativeAction.ActionId);
    }

    private static EncounterPolicyDecision? Select(EncounterCompiledDefinition compiled, ImmutableArray<EncounterPolicyActor> actors) => EncounterPolicyRuntime.Select(compiled, "mod/policy", new(), "enemy", actors, new(), _ => 0);
    private static EncounterPolicyActor Enemy() => new("enemy", "original", false, true, true, false);
    private static EncounterPolicyActor Party(string id) => new(id, id, true, true, true, false);

    private static EncounterCompiledDefinition Compile(string rules, BattleNativeAction[] actions)
    {
        string json = "{\"schemaVersion\":1,\"documentId\":\"mod\",\"encounter\":\"ff8.disc1.edea\",\"atomic\":true,\"operations\":[{\"id\":\"define\",\"kind\":\"AddActionPolicy\",\"policy\":\"policy\",\"rules\":[" + rules + "]}]}";
        EncounterPatchComposition composition = EncounterPatchComposer.Compose(new("ff8.disc1.edea", "Classic", ["original"]), [new("package", "gameplay/encounters/test.json", EncounterPatchDocument.Parse(json))]);
        BattleDefinition definition = new() { Combatants = [new() { Id = "enemy", IsPlayer = false }], NativeActions = actions };
        return EncounterPolicyRuntime.Compile(composition, definition, new("ff8.disc1.edea", "enemy", 11, 11, [8, 9, 10], true));
    }

    private static BattleNativeAction Magic(ushort id, string? name = null, byte element = 0)
    {
        return new(2, id, 1, id)
        {
            Name = name,
            MagicId = id,
            AttackType = 2,
            Power = 0,
            HitCount = 1,
            TargetFlags = 0x10,
            AttackFlags = 0x10,
            DamagePresentation = 0,
            Element = element,
            ElementPercent = 100,
            StatusAttack = 0,
            Statuses0 = 0,
            Statuses1 = 0,
            Hit = 255,
            Critical = null,
            PowerModifier = null,
            LevelModifier = null,
            RawRecord = [0],
            SupplementalRecord = []
        };
    }
}
