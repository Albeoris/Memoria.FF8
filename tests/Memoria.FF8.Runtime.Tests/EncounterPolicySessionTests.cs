using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class EncounterPolicySessionTests
{
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public void NativeCallbackAdmitsOneMainActionAndEnemyDoubleCastsWithoutStocks(bool doubleActive, int expectedCasts)
    {
        BattleSession battle = Create(doubleActive);
        for (int frame = 0; frame < 40 && !battle.Events.Any(entry => entry.Kind == "EncounterPolicy"); frame++)
        {
            battle.Tick();
        }
        Assert.Equal(1ul, battle.EncounterPolicies!.Cursors["enemy"].CommittedTurns);
        Assert.Equal(expectedCasts, battle.Events.Count(entry => entry.Kind == "Scan" && entry.ActorId == "enemy"));
        Assert.Equal(expectedCasts, battle.NativeActionPackets.ActionCount);
        if (doubleActive)
        {
            Assert.Equal((byte)1, battle.NativeActionPackets.Headers[0].ReadByte(17));
        }
        Assert.Empty(battle.Combatants.Single(actor => actor.Id == "enemy").MagicStocks);
        Assert.Single(battle.ScanKnowledge.Observations);
    }

    [Fact]
    public void AdmittedMainActionCursorAndCompiledPolicySurviveJsonSnapshotRestore()
    {
        BattleSession battle = Create(true);
        for (int frame = 0; frame < 40 && !battle.Events.Any(entry => entry.Kind == "EncounterPolicy"); frame++)
        {
            battle.Tick();
        }
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!;
        BattleSession restored = BattleSession.Restore(snapshot);
        Assert.Equal(battle.ReadDefinition().EncounterMod!.GenerationIdentity, restored.ReadDefinition().EncounterMod!.GenerationIdentity);
        Assert.Equal(1ul, restored.EncounterPolicies!.Cursors["enemy"].CommittedTurns);
        Assert.Equal(2ul, restored.EncounterPolicies.Cursors["enemy"].NextOwnTurn);
        Assert.Single(restored.ScanKnowledge.Observations);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public void EnemyPolicyReflectionPreservesCasterMainTurnAndRepeatCountAcrossSnapshot(bool doubleActive, int casts)
    {
        BattleSession battle = Create(doubleActive, true);
        for (int frame = 0; frame < 40 && !battle.Events.Any(entry => entry.Kind == "Reflect"); frame++)
        {
            battle.Tick();
        }
        Assert.Equal(casts, battle.Events.Count(entry => entry.Kind == "Reflect"));
        Assert.Equal(casts, battle.Events.Count(entry => entry.Kind == "Damage" && entry.ActorId == "enemy" && entry.TargetId == "enemy"));
        Assert.Equal(1000, battle.Combatants.Single(actor => actor.Id == "party").CurrentHp);
        Assert.Equal(casts * 2, battle.NativeActionPackets.ActionCount);
        if (doubleActive)
        {
            Assert.Equal((byte)1, battle.NativeActionPackets.Headers[0].ReadByte(17));
        }
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!;
        BattleSession restored = BattleSession.Restore(snapshot);
        Assert.Equal(battle.Combatants.Single(actor => actor.Id == "enemy").CurrentHp, restored.Combatants.Single(actor => actor.Id == "enemy").CurrentHp);
        Assert.Equal(1ul, restored.EncounterPolicies!.Cursors["enemy"].CommittedTurns);
        Assert.Empty(restored.Combatants.Single(actor => actor.Id == "enemy").MagicStocks);
        restored.Tick();
        Assert.Equal(casts, restored.Events.Count(entry => entry.Kind == "Reflect"));
    }

    [Fact]
    public void EnemyReflectedFreeMagicPacketSurvivesTheSharedInventorySnapshotValidator()
    {
        BattleDefinition source = Create(false, true).ReadDefinition();
        BattleDefinition definition = source with
        {
            EncounterMod = null,
            Combatants = source.Combatants.Select(actor => actor.Id == "enemy" ? actor with { NativeAi = null, Abilities = [BattleAbility.Magic], MagicStocks = [new(1, 1)] } : actor).ToArray()
        };
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        battle.QueueAction("enemy", "party", BattleAbility.Magic, 1);
        for (int frame = 0; frame < 40 && !battle.Events.Any(entry => entry.Kind == "Reflect"); frame++)
        {
            battle.Tick();
        }
        Assert.Single(battle.Events, entry => entry.Kind == "Reflect");
        Assert.True(battle.QueuedActions.Any(command => command.NativeQueuedCommand == 247 && command.FreeNativeMagic) || battle.ActiveAction is { NativeQueuedCommand: 247, FreeNativeMagic: true });
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!;
        BattleSession restored = BattleSession.Restore(snapshot);
        Assert.Single(restored.Events, entry => entry.Kind == "Damage" && entry.ActorId == "enemy" && entry.TargetId == "enemy");
        restored.Tick();
        Assert.Single(restored.Events, entry => entry.Kind == "Reflect");
    }
    [Fact]
    public void EnemyOrdinaryMagicCannotClaimThePlayerFreeMagicSnapshotFlag()
    {
        BattleSession battle = Create(false);
        for (int frame = 0; frame < 40 && !battle.Events.Any(entry => entry.Kind == "EncounterPolicy"); frame++)
        {
            battle.Tick();
        }
        BattleSnapshot snapshot = battle.Capture();
        Assert.True(snapshot.QueuedActions.Any(command => command.NativeQueuedCommand == 2) || snapshot.ActiveAction?.NativeQueuedCommand == 2);
        BattleSnapshot invalid = snapshot with
        {
            QueuedActions = snapshot.QueuedActions.Select(command => command.NativeQueuedCommand == 2 ? command with { FreeNativeMagic = true } : command).ToArray(),
            ActiveAction = snapshot.ActiveAction?.NativeQueuedCommand == 2 ? snapshot.ActiveAction with { FreeNativeMagic = true } : snapshot.ActiveAction
        };
        Assert.Contains("free magic", Assert.Throws<InvalidDataException>(() => BattleSession.Restore(invalid)).Message);
    }
    [Theory]
    [InlineData("Protect", 0x20u)]
    [InlineData("Shell", 0x40u)]
    [InlineData("Regen", 0x10u)]
    [InlineData("Reflect", 0x80u)]
    [InlineData("Haste", 2u)]
    public void NamedFamilyStatusesExcludeUnrelatedFourthActor(string spellName, uint status)
    {
        BattleDefinition source = Create(false).ReadDefinition();
        BattleNativeAction spell = source.NativeActions[0] with { AttackType = 2, StatusAttack = 255, Statuses1 = status, TargetFlags = 16, MagicId = 0 };
        EncounterCompiledDefinition mod = source.EncounterMod!;
        EncounterCompiledAction action = new(EncounterCompiledActionKind.Cast, [new(spellName, spell, -1, false, false)], spellName == "Haste" ? EncounterCompiledTargetKind.RandomAlly : EncounterCompiledTargetKind.GroupAllLiving, "family", false, false, null, []);
        EncounterCompiledPolicy policy = new(mod.NormalActionPolicies["original"], [new("family", 1, ulong.MaxValue, 0, action)]);
        BattleCombatantDefinition enemy = source.Combatants.Single(actor => actor.Id == "enemy");
        BattleCombatantDefinition[] actors = source.Combatants.Concat(new[] { "support", "assault", "fourth" }.Select((id, index) => enemy with { Id = id, Name = id, SceneSlot = index + 1, InitialAtb = 0, NativeAi = null, Speed = 1 })).ToArray();
        byte seed = Enumerable.Range(0, 256).Select(value => (byte)value).First(value =>
        {
            NativeBattleRandomState.Initialize(value).Draw(out byte draw);
            return draw % 4 == 2;
        });
        BattleSession battle = new(source with { NativeStatusRules = new(0, Enumerable.Repeat((byte)30, 14).ToImmutableArray()), NativeSeed = seed, Combatants = actors, NativeActions = [spell], ActionDefinitions = [new(BattleAbility.Magic, spell.ActionId, spellName, BattleActionEffect.Native, 0, true) { NativeAction = spell }], EncounterMod = mod with { Policies = mod.Policies.SetItem(policy.Id, policy), Groups = mod.Groups.SetItem("family", ["original", "support", "assault"]) } }, BattleAtbMode.Normal);
        for (int frame = 0; frame < 40 && !battle.Events.Any(entry => entry.Kind == "EncounterPolicy"); frame++)
        {
            battle.Tick();
        }
        Assert.Equal(1ul, battle.EncounterPolicies!.Cursors["enemy"].CommittedTurns);
        if (spellName == "Haste")
        {
            Assert.Equal((byte)1, battle.NativeActionPackets.ContactCount);
            Assert.NotEqual(0u, battle.Capture().Combatants.Single(actor => actor.Id == "fourth").NativeStatus!.Battle & status);
            return;
        }
        Assert.Equal((byte)3, battle.NativeActionPackets.ContactCount);
        foreach (string id in new[] { "enemy", "support", "assault" })
        {
            Assert.NotEqual(0u, battle.Capture().Combatants.Single(actor => actor.Id == id).NativeStatus!.Battle & status);
        }
        Assert.Equal(0u, battle.Capture().Combatants.Single(actor => actor.Id == "fourth").NativeStatus!.Battle & status);
        Assert.DoesNotContain(battle.Events, entry => entry.TargetId == "fourth" && entry.Kind == "Damage");
    }

    [Fact]
    public void AcceptedSummonControlRestoresWindupAndFactualHeaderThenCompletesBoundedly()
    {
        BattleDefinition source = Create(false).ReadDefinition();
        NativeBattleEnemyLevel level = new(1000, 10, 10, 10, 10, 10, Enumerable.Repeat(800, 8).ToArray(), [], [], 0);
        NativeBattleEnemyTemplate template = new(1, Enumerable.Repeat(level, 101).ToArray()) { NativeStatusAllocation = new(0, 0, Enumerable.Repeat((byte)100, 40).ToImmutableArray()) };
        EncounterCompiledDefinition mod = source.EncounterMod!;
        EncounterCompiledAction action = new(EncounterCompiledActionKind.SummonGroup, [], EncounterCompiledTargetKind.Self, null, false, false, null, [new("support", "anchor")]);
        EncounterCompiledPolicy policy = new(mod.NormalActionPolicies["original"], [new("summon", 1, ulong.MaxValue, 0, action)]);
        BattleSession battle = new(source with
        {
            Combatants = source.Combatants.Select(actor => actor.Id == "enemy" ? actor with { NativeTemplate = template, Level = 1, Speed = 10, Attack = 10, Magic = 10, Defense = 10, Spirit = 10, SceneSlot = 0 } : actor).ToArray(),
            EncounterMod = mod with { Policies = mod.Policies.SetItem(policy.Id, policy), Templates = mod.Templates.SetItem("support", new("support", "original", "family", policy.Id, "arrival")) }
        }, BattleAtbMode.Normal);
        bool sawControl = false;
        bool sawHeader = false;
        for (int frame = 0; frame < 100 && battle.EncounterAllocation is null; frame++)
        {
            battle.Tick();
            if (battle.ActiveAction?.NativeAction?.PresentationOnly == true || battle.QueuedActions.Any(command => command.NativeAction?.PresentationOnly == true))
            {
                sawControl = true;
                sawHeader |= battle.ActiveAction?.NativeHeaderIndex is not null || battle.QueuedActions.Any(command => command.NativeHeaderIndex is not null);
                BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!;
                battle = BattleSession.Restore(snapshot);
                Assert.Equal(1ul, battle.EncounterPolicies!.Cursors["enemy"].CommittedTurns);
                Assert.Throws<InvalidDataException>(() => BattleSession.Restore(snapshot with { EncounterPolicies = snapshot.EncounterPolicies! with { PendingSummon = null } }));
            }
        }
        Assert.True(sawControl);
        Assert.True(sawHeader);
        Assert.Equal(BattleEncounterAllocationPhase.Ready, battle.EncounterAllocation!.Phase);
        Assert.DoesNotContain(battle.Events, entry => entry.Kind == "Damage" || entry.Kind == "DamageText");
        Assert.Equal(1000, battle.Combatants.Single(actor => actor.Id == "enemy").CurrentHp);
    }

    [Theory]
    [InlineData(0x20000u, 0u, 2, 3)]
    [InlineData(0x40000u, 0u, 3, 3)]
    [InlineData(0u, 0x40000u, 3, 3)]
    [InlineData(0x40000u, 0x20000u, 3, 3)]
    [InlineData(0u, 0u, 1, 3)]
    [InlineData(0x40000u, 0u, 3, 2)]
    [InlineData(0x40000u, 0u, 3, 1)]
    public void MirrorUsesOwnOrOriginalMulticastAndScansDistinctOpponents(uint originalStatus, uint mirrorStatus, int casts, int opponents)
    {
        BattleDefinition source = Create(false).ReadDefinition();
        EncounterCompiledDefinition mod = source.EncounterMod!;
        BattleCombatantDefinition original = source.Combatants.Single(actor => actor.Id == "enemy");
        BattleCombatantDefinition party = source.Combatants.Single(actor => actor.Id == "party");
        BattleDefinition definition = source with
        {
            Combatants = [party, party with { Id = "party-b" }, party with { Id = "party-c" }, original with { NativeStatus1 = originalStatus, InitialAtb = 0, NativeAi = null, SceneSlot = 0 }, original with { Id = "mirror", NativeStatus1 = mirrorStatus, SceneSlot = 1 }],
            EncounterMod = mod with
            {
                NormalActionPolicies = mod.NormalActionPolicies.Remove("original"),
                Templates = mod.Templates.SetItem("mirror", new("mirror", "original", "family", "mod/policy", "arrival"))
            }
        };
        definition = definition with { Combatants = definition.Combatants.Where(actor => opponents == 3 || actor.Id != "party-c" && (opponents == 2 || actor.Id != "party-b")).ToArray() };
        BattleSession battle = new(definition, BattleAtbMode.Normal);
        for (int frame = 0; frame < 40 && !battle.Events.Any(entry => entry.Kind == "EncounterPolicy"); frame++)
        {
            battle.Tick();
        }
        Assert.Equal(casts, battle.Events.Count(entry => entry.Kind == "Scan" && entry.ActorId == "mirror"));
        Assert.Equal(Math.Min(casts, opponents), battle.ScanKnowledge.Observations.Length);
        Assert.Equal(casts, battle.NativeActionPackets.ActionCount);
        Assert.Equal(Math.Min(casts, opponents), battle.EncounterPolicies!.Cursors["mirror"].LastTargetIds.Length);
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        Assert.Equal(battle.EncounterPolicies.Cursors["mirror"].LastTargetIds.ToArray(), restored.EncounterPolicies!.Cursors["mirror"].LastTargetIds.ToArray());
    }

    [Theory]
    [InlineData(0u, 1, 3)]
    [InlineData(0x20000u, 2, 3)]
    [InlineData(0x40000u, 3, 3)]
    [InlineData(0x40000u, 3, 2)]
    public void MirrorGroupSpellRepeatsWithinNamedAlliesAfterDistinctRecipientsAreExhausted(uint originalStatus, int casts, int allies)
    {
        BattleDefinition source = Create(false).ReadDefinition();
        EncounterCompiledDefinition mod = source.EncounterMod!;
        BattleCombatantDefinition original = source.Combatants.Single(actor => actor.Id == "enemy");
        EncounterCompiledPolicy policy = mod.Policies["mod/policy"];
        EncounterCompiledRule rule = policy.Rules[0];
        rule = rule with { Action = rule.Action with { Target = EncounterCompiledTargetKind.GroupAllLiving, Group = "family" } };
        BattleSession battle = new(source with
        {
            Combatants = [source.Combatants[0], original with { NativeStatus1 = originalStatus, InitialAtb = 0, NativeAi = null, SceneSlot = 0 }, original with { Id = "mirror", SceneSlot = 1 }, original with { Id = "other", InitialAtb = 0, NativeAi = null, SceneSlot = 2 }],
            EncounterMod = mod with
            {
                NormalActionPolicies = mod.NormalActionPolicies.Remove("original"),
                Policies = mod.Policies.SetItem(policy.Id, policy with { Rules = [rule] }),
                Templates = mod.Templates.SetItem("mirror", new("mirror", "original", "family", policy.Id, "arrival")),
                Groups = mod.Groups.SetItem("family", allies == 3 ? ["original", "mirror", "other"] : ["original", "mirror"])
            }
        }, BattleAtbMode.Normal);
        for (int frame = 0; frame < 40 && !battle.Events.Any(entry => entry.Kind == "EncounterPolicy"); frame++)
        {
            battle.Tick();
        }
        Assert.Equal(casts, battle.Events.Count(entry => entry.Kind == "Scan"));
        Assert.Equal(Math.Min(casts, allies), battle.ScanKnowledge.Observations.Length);
        Assert.Equal(casts, battle.NativeActionPackets.ActionCount);
        Assert.Equal(Math.Min(casts, allies), battle.EncounterPolicies!.Cursors["mirror"].LastTargetIds.Length);
        Assert.DoesNotContain(battle.Events, entry => entry.Kind == "Scan" && entry.TargetId == "party");
        if (allies == 2)
        {
            Assert.DoesNotContain(battle.Events, entry => entry.Kind == "Scan" && entry.TargetId == "other");
        }
    }

    [Theory]
    [InlineData(EncounterCompiledTargetKind.RandomAlly, "enemy")]
    [InlineData(EncounterCompiledTargetKind.RandomOpponent, "party")]
    public void WaterDamagesItsSelectedSideEvenWhenTripleRepeatsOneRecipient(EncounterCompiledTargetKind targetKind, string recipientId)
    {
        BattleDefinition source = Create(false, true).ReadDefinition();
        BattleNativeAction water = source.NativeActions[0] with { ActionId = 10, KernelIndex = 10, Name = "Water", Element = 64 };
        EncounterCompiledDefinition mod = source.EncounterMod!;
        EncounterCompiledPolicy policy = mod.Policies["mod/policy"];
        EncounterCompiledRule rule = policy.Rules[0];
        rule = rule with { Action = rule.Action with { Spells = [new("Water", water, 6, true, false)], Target = targetKind } };
        BattleSession battle = new(source with
        {
            Combatants = source.Combatants.Select(actor => actor with { NativeStatus1 = actor.IsPlayer ? 0 : 0x40000u }).ToArray(),
            NativeActions = [water],
            ActionDefinitions = [new(BattleAbility.Magic, 10, "Water", BattleActionEffect.Native, water.Power, false) { NativeAction = water }],
            EncounterMod = mod with { Policies = mod.Policies.SetItem(policy.Id, policy with { Rules = [rule] }) }
        }, BattleAtbMode.Normal);
        for (int frame = 0; frame < 40 && !battle.Events.Any(entry => entry.Kind == "EncounterPolicy"); frame++)
        {
            battle.Tick();
        }
        Assert.Equal(3, battle.Events.Count(entry => entry.Kind == "Damage" && entry.ActorId == "enemy" && entry.TargetId == recipientId && entry.Amount > 0));
        Assert.DoesNotContain(battle.Events, entry => entry.Kind == "Miss" || entry.Kind == "Reflect");
        Assert.Equal(3, battle.NativeActionPackets.ActionCount);
        Assert.Equal(new[] { recipientId }, battle.EncounterPolicies!.Cursors["enemy"].LastTargetIds.ToArray());
        string otherId = recipientId == "enemy" ? "party" : "enemy";
        Assert.Equal(1000, battle.Combatants.Single(actor => actor.Id == otherId).CurrentHp);
    }

    [Fact]
    public void RestoredTargetHistoryRequiresDistinctAllocatedIdentities()
    {
        BattleSnapshot source = Create(false).Capture();
        foreach (ImmutableArray<string> targets in new ImmutableArray<string>[] { default, ["unknown"], ["party", "party"] })
        {
            BattleEncounterPolicyState state = new(source.Definition.EncounterMod!.GenerationIdentity, ImmutableDictionary<string, EncounterActionCursor>.Empty.Add("enemy", new(1) { LastTargetIds = targets }));
            Assert.Throws<InvalidDataException>(() => BattleSession.Restore(source with { EncounterPolicies = state }));
        }
    }

    private static BattleSession Create(bool doubleActive, bool reflect = false)
    {
        BattleNativeAction scan = new(2, 50, 1, 50)
        {
            MagicId = 1,
            AttackType = 12,
            Power = 0,
            HitCount = 1,
            TargetFlags = 0x50,
            AttackFlags = 1,
            DamagePresentation = 0,
            Element = 0,
            ElementPercent = null,
            StatusAttack = 0,
            Statuses0 = 0,
            Statuses1 = 0,
            Hit = null,
            Critical = null,
            PowerModifier = null,
            LevelModifier = null,
            RawRecord = [0],
            SupplementalRecord = []
        };
        if (reflect)
        {
            scan = scan with { ActionId = 1, KernelIndex = 1, AttackType = 2, Power = 10, AttackFlags = 0x11 };
        }
        NativeBattleAiDefinition ai = new([[0], [0], [0], [0], [0]], Enumerable.Repeat(new NativeBattleAiAbility(2, 0, 50), 48).ToArray(), 0, []);
        BattleDefinition definition = new()
        {
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeActions = reflect ? [scan, scan with { Command = 247 }] : [scan],
            ActionDefinitions = [new(BattleAbility.Magic, scan.ActionId, reflect ? "Fire" : "Scan", BattleActionEffect.Native, 0, false) { NativeAction = scan }],
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, MaximumHp = 1000, CurrentHp = 1000, NativeStatus1 = reflect ? 0x80u : 0, ElementDefenses = [700, 800, 850, 900, 1000, 800, 800, 800] },
                new() { Id = "enemy", Name = "Enemy", ModelId = 88, MaximumHp = 1000, CurrentHp = 1000, NativeAi = ai, NativeCombatantFlags = 0x11, InitialAtb = BattleSession.AtbMaximum, NativeStatus1 = doubleActive ? 0x20000u : 0 }
            ]
        };
        const string json = """
            {"schemaVersion":1,"documentId":"mod","encounter":"fixture.encounter","atomic":true,"operations":[
              {"id":"policy","kind":"AddActionPolicy","policy":"policy","rules":[{"id":"scan","when":{"ownTurnAtLeast":1},"action":{"kind":"Cast","spell":"Scan","target":"randomOpponent","casts":"nativeDouble"}}]},
              {"id":"assign","kind":"SetNormalActionPolicy","actor":"original","expectedOwner":"nativeNormalAction","policy":"policy","preserveNativeLifecycle":true}
            ]}
            """;
        string payload = reflect ? json.Replace("\"Scan\"", "\"Fire\"", StringComparison.Ordinal) : json;
        EncounterPatchComposition composition = EncounterPatchComposer.Compose(new("fixture.encounter", "Classic", ["original"]), [new("package", "gameplay/encounters/test.json", EncounterPatchDocument.Parse(payload))]);
        EncounterCompiledDefinition compiled = EncounterPolicyRuntime.Compile(composition, definition, new("fixture.encounter", "enemy", 11, 11, [8, 9, 10], true));
        return new(definition with { EncounterMod = compiled }, BattleAtbMode.Normal);
    }
}
