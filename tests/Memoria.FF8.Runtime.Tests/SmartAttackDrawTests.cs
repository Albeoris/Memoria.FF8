using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class SmartAttackDrawTests
{
    [Fact]
    public void DrawOnlyCommandUsesRevealedSpellAndKeepsSourceSeparateFromRecipient()
    {
        BattleSession battle = Create([Spell(1, 40)], [], drawOnly: true);
        BattleSmartAttackPreview preview = battle.PreviewSmartAttack("party", "enemy");
        Assert.Equal(BattleAbility.Draw, preview.Ability);
        Assert.Equal(1, preview.ActionId);
        Assert.Equal(1, preview.CastCount);
        Assert.Equal("enemy", preview.TargetId);
        Assert.Equal("source", preview.DrawSourceId);
        Assert.Equal(battle.Combatants.Single(actor => actor.Id == "source").NativeIndex, preview.DrawSourceIndex);
        battle.QueueSmartAttack(preview);
        BattleAttackCommand queued = Assert.Single(battle.QueuedActions);
        Assert.Equal(BattleAbility.Draw, queued.Ability);
        Assert.True(queued.DrawCast);
        Assert.Equal("source", queued.TargetId);
        Assert.Equal("enemy", queued.DrawCastTargetId);
    }

    [Fact]
    public void UnrevealedSpellAndUnknownKnowledgeBankCannotBecomeDrawCandidates()
    {
        BattleSession hidden = Create([Spell(1, 40)], [], known: []);
        Assert.Equal(BattleAbility.Attack, hidden.PreviewSmartAttack("party", "enemy").Ability);
        BattleSession unknown = Create([Spell(1, 40)], [], knownBank: false);
        Assert.Equal(BattleAbility.Attack, unknown.PreviewSmartAttack("party", "enemy").Ability);
    }

    [Fact]
    public void RevealedWeakerSpellWinsOverUnrevealedStrongerSourceEntry()
    {
        BattleSession battle = Create([Spell(1, 20), Spell(2, 100)], [], known: [1], sourceSlots: [new(1, 0), new(2, 0), new(0, 0), new(0, 0)]);
        Assert.Equal(1, battle.PreviewSmartAttack("party", "enemy").ActionId);
    }

    [Fact]
    public void ReliableDrawKillTakesPriorityOverReliableStockKill()
    {
        BattleSession battle = Create([Spell(1, 40), Spell(2, 80)], [new(2, 100)], enemyHp: 10);
        BattleSmartAttackPreview preview = battle.PreviewSmartAttack("party", "enemy");
        Assert.Equal(BattleAbility.Draw, preview.Ability);
        Assert.True(preview.KillProbability > 0.9);
    }

    [Fact]
    public void ReliableStockKillTakesPriorityOverUnreliableDraw()
    {
        BattleSession battle = Create([Spell(1, 20), Spell(2, 80)], [new(2, 100)], enemyHp: 300);
        BattleSmartAttackPreview preview = battle.PreviewSmartAttack("party", "enemy");
        Assert.Equal(BattleAbility.Magic, preview.Ability);
        Assert.Equal(2, preview.ActionId);
        Assert.True(preview.KillProbability > 0.9);
    }

    [Theory]
    [InlineData(80, 10, BattleAbility.Draw, 1)]
    [InlineData(10, 80, BattleAbility.Magic, 2)]
    public void NoReliableKillChoosesGreaterExpectedDamageAcrossDrawAndStock(byte drawPower, byte stockPower, BattleAbility expectedAbility, int expectedSpell)
    {
        BattleSession battle = Create([Spell(1, drawPower), Spell(2, stockPower)], [new(2, 100)]);
        BattleSmartAttackPreview preview = battle.PreviewSmartAttack("party", "enemy");
        Assert.Equal(expectedAbility, preview.Ability);
        Assert.Equal(expectedSpell, preview.ActionId);
        Assert.Equal(0, preview.KillProbability);
    }

    [Fact]
    public void NativeQuantityFailureAndDamageMultiplierUseSequentialRandomPhases()
    {
        BattleSession battle = Create([Spell(1, 40)], [], sourceSlots: [new(1, 22), new(0, 0), new(0, 0), new(0, 0)]);
        BattleCombatant actor = battle.Combatants[0];
        BattleCombatant source = battle.Combatants.Single(entry => entry.Id == "source");
        BattleCombatant target = battle.Combatants.Single(entry => entry.Id == "enemy");
        BattleDamageForecast expected = BattleActionForecaster.FromNativePhases(draw =>
        {
            int quantity = Math.Clamp(((((actor.Level - source.Level + 10) >> 1) + (draw() & 31) + 1 + actor.Magic) / 5) - 22, 0, 9);
            if (quantity == 0)
            {
                return 0;
            }
            int damage = NativeBattleDamage.Magic(actor.Magic, target.Spirit, 40, draw(), false);
            return damage * (draw() + 10) / 150;
        }, target.CurrentHp);
        BattleSmartAttackPreview actual = battle.PreviewSmartAttack("party", "enemy");
        Assert.Equal(BattleAbility.Draw, actual.Ability);
        Assert.Equal(expected.ExpectedDamage, actual.ExpectedDamage, 12);
        Assert.Equal(expected.KillProbability, actual.KillProbability, 12);
        Assert.True(expected.Distribution[0] > 0);
    }

    [Fact]
    public void PreviewIsPureAndForcePhysicalBypassesDraw()
    {
        BattleSession battle = Create([Spell(1, 80), Spell(2, 10)], [new(2, 100)], infinite: true);
        string before = JsonSerializer.Serialize(battle.Capture());
        Assert.Equal(BattleAbility.Draw, battle.PreviewSmartAttack("party", "enemy").Ability);
        Assert.Equal(BattleAbility.Attack, battle.PreviewSmartAttack("party", "enemy", true).Ability);
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
    }

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, false, false, true)]
    public void DrawRequiresEquippedAbilityAndLivingActiveTargetableEnemy(bool equipped, bool dead, bool inactive, bool untargetable)
    {
        BattleSession battle = Create([Spell(1, 40)], [], equippedDraw: equipped);
        BattleCombatant source = battle.Combatants.Single(actor => actor.Id == "source");
        if (dead)
        {
            source.CurrentHp = 0;
            source.NativeStatus0 |= 1;
        }
        source.IsActive = !inactive;
        source.Untargetable = untargetable;
        Assert.Equal(BattleAbility.Attack, battle.PreviewSmartAttack("party", "enemy").Ability);
    }

    [Fact]
    public void SourceMutationInvalidatesCachedPreviewAndRejectsStaleSubmission()
    {
        BattleSession battle = Create([Spell(1, 40)], []);
        BattleSmartAttackPreview original = battle.PreviewSmartAttack("party", "enemy");
        BattleCombatant source = battle.Combatants.Single(actor => actor.Id == "source");
        source.RestoreDrawSlots([new(1, 50), new(0, 0), new(0, 0), new(0, 0)]);
        BattleSmartAttackPreview changed = battle.PreviewSmartAttack("party", "enemy");
        Assert.NotEqual(original.Revision, changed.Revision);
        Assert.Equal(BattleAbility.Attack, changed.Ability);
        Assert.Throws<InvalidOperationException>(() => battle.QueueSmartAttack(original));
        Assert.Empty(battle.QueuedActions);
    }

    [Fact]
    public void UnsupportedSpellShapesAreExcludedFromDraw()
    {
        BattleNativeAction pure = Spell(1, 40);
        BattleNativeAction[] excluded = [pure with { Statuses0 = 1 }, pure with { Statuses1 = 1 }, pure with { TargetFlags = 0x30 }, pure with { HitCount = 2 }, pure with { AttackType = 6 }];
        foreach (BattleNativeAction spell in excluded)
        {
            Assert.Equal(BattleAbility.Attack, Create([spell], []).PreviewSmartAttack("party", "enemy").Ability);
        }
    }

    [Fact]
    public void ZeroDamageEqualityPreservesPhysicalCommand()
    {
        BattleSession battle = Create([Spell(1, 40), Spell(2, 40)], [new(2, 100)]);
        battle.Combatants.Single(actor => actor.Id == "enemy").NativeStatus1 |= 0x800;
        BattleSmartAttackPreview preview = battle.PreviewSmartAttack("party", "enemy");
        Assert.Equal(BattleAbility.Attack, preview.Ability);
        Assert.Equal(0, preview.ExpectedDamage);
    }

    [Fact]
    public void PositiveEqualExpectedDamagePrefersDrawOverPersonalStock()
    {
        BattleNativeAction[] spells = [Spell(1, 48) with { Element = 1 }, Spell(2, 44)];
        int[] defenses = [802, 800, 800, 800, 800, 800, 800, 800];
        BattleSession personalOnly = Create(spells, [new(2, 100)], equippedDraw: false, enemySpirit: 255, defenses: defenses);
        BattleSmartAttackPreview personal = personalOnly.PreviewSmartAttack("party", "enemy");
        Assert.Equal(BattleAbility.Magic, personal.Ability);
        Assert.Equal(60.421875, personal.ExpectedDamage);
        BattleSession combined = Create(spells, [new(2, 100)], enemySpirit: 255, defenses: defenses);
        BattleSmartAttackPreview chosen = combined.PreviewSmartAttack("party", "enemy");
        Assert.Equal(BattleAbility.Draw, chosen.Ability);
        Assert.Equal(1, chosen.ActionId);
        Assert.Equal(personal.ExpectedDamage, chosen.ExpectedDamage);
        Assert.Equal(0, chosen.KillProbability);
    }

    [Fact]
    public void TargetWeaknessMakesSamePowerPersonalSpellReliableWhileDrawIsUnreliable()
    {
        BattleNativeAction[] spells = [Spell(1, 40) with { Element = 1 }, Spell(2, 40) with { Element = 2 }];
        int[] defenses = [800, 700, 800, 800, 800, 800, 800, 800];
        BattleSession drawOnly = Create(spells, [], enemyHp: 1800, defenses: defenses);
        BattleSmartAttackPreview draw = drawOnly.PreviewSmartAttack("party", "enemy");
        Assert.Equal(BattleAbility.Draw, draw.Ability);
        Assert.InRange(draw.KillProbability, 0, 0.9);
        BattleSession combined = Create(spells, [new(2, 100)], enemyHp: 1800, defenses: defenses);
        BattleSmartAttackPreview chosen = combined.PreviewSmartAttack("party", "enemy");
        Assert.Equal(BattleAbility.Magic, chosen.Ability);
        Assert.Equal(2, chosen.ActionId);
        Assert.True(chosen.KillProbability > 0.9);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void QueuedDrawFallsBackToPhysicalWhenSourceDiesLosesSpellOrChangesNativeIndex(int mutation)
    {
        BattleSession battle = Create([Spell(1, 40)], []);
        battle.QueueSmartAttack("party", "enemy");
        BattleCombatant source = battle.Combatants.Single(actor => actor.Id == "source");
        if (mutation == 0)
        {
            source.CurrentHp = 0;
            source.NativeStatus0 |= 1;
        }
        else if (mutation == 1)
        {
            source.RestoreDrawSlots([new(0, 0), new(0, 0), new(0, 0), new(0, 0)]);
        }
        else
        {
            source.NativeIndex = 7;
        }
        battle.Tick();
        Assert.Equal(BattleAbility.Attack, battle.ActiveAction!.Ability);
        Assert.Equal("enemy", battle.ActiveAction.TargetId);
        Assert.Null(battle.ActiveAction.NativeDraw);
        Assert.False(battle.ActiveAction.DrawCast);
        Assert.Empty(battle.MagicVitals!.MagicSystem.Reservations);
    }

    [Fact]
    public void ChangedSourceNativeIndexRejectsPreviouslyDisplayedPreview()
    {
        BattleSession battle = Create([Spell(1, 40)], []);
        BattleSmartAttackPreview original = battle.PreviewSmartAttack("party", "enemy");
        battle.Combatants.Single(actor => actor.Id == "source").NativeIndex = 7;
        BattleSmartAttackPreview changed = battle.PreviewSmartAttack("party", "enemy");
        Assert.Equal(BattleAbility.Draw, changed.Ability);
        Assert.Equal(7, changed.DrawSourceIndex);
        Assert.NotEqual(original.Revision, changed.Revision);
        Assert.Throws<InvalidOperationException>(() => battle.QueueSmartAttack(original));
        Assert.Empty(battle.QueuedActions);
    }

    [Fact]
    public void NewlyAvailableBetterSourceInvalidatesCacheAndWinsAcrossEnemies()
    {
        BattleSession battle = Create([Spell(1, 40)], [], sourceSlots: [new(1, 22), new(0, 0), new(0, 0), new(0, 0)]);
        BattleSmartAttackPreview original = battle.PreviewSmartAttack("party", "enemy");
        Assert.Equal("source", original.DrawSourceId);
        battle.Combatants.Single(actor => actor.Id == "enemy").RestoreDrawSlots([new(1, 0), new(0, 0), new(0, 0), new(0, 0)]);
        BattleSmartAttackPreview changed = battle.PreviewSmartAttack("party", "enemy");
        Assert.Equal("enemy", changed.DrawSourceId);
        Assert.True(changed.ExpectedDamage > original.ExpectedDamage);
        Assert.NotEqual(original.Revision, changed.Revision);
        Assert.Throws<InvalidOperationException>(() => battle.QueueSmartAttack(original));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void QueuedAndActiveJsonRestoreContinueWithoutConsumingStocksOrMagicTickets(bool active)
    {
        BattleSession battle = Create([Spell(1, 80), Spell(2, 10)], [new(2, 100)], infinite: true);
        string vitalsBefore = JsonSerializer.Serialize(battle.MagicVitals);
        battle.QueueSmartAttack("party", "enemy");
        if (active)
        {
            battle.Tick();
            Assert.NotNull(battle.ActiveAction!.NativeDraw);
        }
        else
        {
            Assert.Single(battle.QueuedActions);
        }
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
        for (int frame = 0; frame < 30; frame++)
        {
            battle.Tick();
            restored.Tick();
        }
        Assert.True(battle.Combatants.Single(actor => actor.Id == "enemy").CurrentHp < 60_000);
        Assert.Equal(60_000, battle.Combatants.Single(actor => actor.Id == "source").CurrentHp);
        Assert.Equal(vitalsBefore, JsonSerializer.Serialize(battle.MagicVitals));
        Assert.Equal(vitalsBefore, JsonSerializer.Serialize(restored.MagicVitals));
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    private static BattleSession Create(BattleNativeAction[] spells, BattleStock[] stocks, int enemyHp = 60_000, bool infinite = false, bool drawOnly = false, bool equippedDraw = true, int[]? known = null, bool knownBank = true, NativeBattleDrawSlot[]? sourceSlots = null, int enemySpirit = 5, int[]? defenses = null)
    {
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        foreach (BattleStock stock in stocks)
        {
            vitals = vitals.ChangeMagicStock(0, stock.Id, stock.Count, MagicOperationReason.ScriptReward, out _);
        }
        vitals = MagicSystemFixture.Modes(vitals, shared: true, infinite: infinite);
        vitals = vitals with { MagicSystem = vitals.MagicSystem with { SmartAttack = true } };
        vitals.MagicSystem.AttackModes[1] = MagicAttackMode.SmartMagic;
        NativeKnownMagic bank = new(NativeInputConfiguration.OriginalInitHash, ImmutableArray.Create<uint>(0, 0));
        foreach (int spell in known ?? spells.Select(spell => (int)spell.ActionId).ToArray())
        {
            bank = bank.Add(spell);
        }
        List<BattleAbility> abilities = [BattleAbility.Attack];
        if (!drawOnly)
        {
            abilities.Add(BattleAbility.Magic);
        }
        if (equippedDraw)
        {
            abilities.Add(BattleAbility.Draw);
        }
        BattleNativeAction attack = Spell(0, 1) with { Command = 1, AttackType = 1, Hit = 255, Critical = 0, Name = "Attack" };
        return new(new BattleDefinition
        {
            MagicVitals = vitals,
            NativeKnownMagic = knownBank ? bank : null,
            NativeRetainedMemory = NativeBattleRetainedMemory.FromColdProcess(),
            NativeCommandSequences = NativeBattleCommandSequenceFixture.Values,
            NativeActions = spells,
            ActionDefinitions = spells.Select(spell => new BattleActionDefinition(BattleAbility.Magic, spell.ActionId, spell.Name!, BattleActionEffect.Native, 0, false) { NativeAction = spell }).ToArray(),
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, ModelId = 1, MagicRecordId = 1, InitialAtb = BattleSession.AtbMaximum, Attack = 1, Magic = 100, Abilities = abilities.ToArray(), NativeAttack = attack, NativeCommandSlots = [1, 6, 2, 0] },
                new() { Id = "enemy", Name = "Enemy", ModelId = 2, MaximumHp = enemyHp, CurrentHp = enemyHp, Spirit = enemySpirit, ElementDefenses = defenses ?? [800, 800, 800, 800, 800, 800, 800, 800] },
                new() { Id = "source", Name = "Source", ModelId = 3, MaximumHp = 60_000, CurrentHp = 60_000, NativeDrawSlots = sourceSlots ?? [new(1, 0), new(0, 0), new(0, 0), new(0, 0)] }
            ]
        }, BattleAtbMode.TurnBased);
    }

    private static BattleNativeAction Spell(ushort id, byte power) => new(2, id, 1, id)
    {
        Name = $"Spell{id}", MagicId = id, AttackType = 2, Power = power, HitCount = 1, TargetFlags = 0x50, AttackFlags = 1, DamagePresentation = 0, Element = 0, ElementPercent = null, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, RawRecord = [], SupplementalRecord = []
    };
}
