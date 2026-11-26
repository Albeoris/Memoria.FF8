using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;
using Memoria.FF8.Assets;
using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class InstalledBrothersFormTests(ITestOutputHelper output)
{
    private const int Ifrit = 2;
    private const int Brothers = 4;
    private const string Caster = "party-0";
    private const ushort PoisonMask = 1 << (int)NativeStatusId.Poison;
    private static readonly int[] entranceSampleOffsets = [0, 72, 84, 96, 108];
    private static readonly int[] madCowSampleAges = [1, 40, 64];

    [Fact]
    public void BothVariantsLandAndRestoreTheirInstalledForms()
    {
        VerifyInstalledForms(verifyActions: false);
    }

    [Fact]
    public void BothVariantsUseTheirInstalledAttacksAndSharedFinishers()
    {
        VerifyInstalledForms(verifyActions: true);
    }

    [Fact]
    public void EqualStatsFormDamageOverTheNativeLifetimeAgainstOneEnemy()
    {
        MeasureRegularAttackDamage(22, 1);
    }

    [Fact]
    public void EqualStatsFormDamageOverTheNativeLifetimeAgainstThreeEnemies()
    {
        MeasureRegularAttackDamage(24, 3);
    }

    private void MeasureRegularAttackDamage(ushort formation, int enemyCount)
    {
        string directory = TestResources.GameDirectory;
        var measured = new Dictionary<string, int[]>(StringComparer.Ordinal);
        (int Level, int Strength, int Speed)? commonCaster = null;
        foreach (int guardian in new[] { Ifrit, Brothers })
        {
            int requiredMeasurements = guardian == Ifrit ? 1 : 3;
            for (byte seed = 0; seed < 8 && measured.Count < requiredMeasurements; seed++)
            {
                using GameAssets game = new(directory);
                GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, game.ReadColdProcessNativeBattleMemory()));
                session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
                using GameWorld world = new(session, directory, sharedAssets: game, applyEncounterMods: (definition, _) => definition with
                {
                    Combatants = definition.Combatants.Select(combatant => combatant.IsPlayer ? combatant : combatant with
                    {
                        NativeCombatantFlags = combatant.NativeCombatantFlags | 8,
                        Level = 30,
                        MaximumHp = 60000,
                        CurrentHp = 60000,
                        Defense = combatant.NativeTemplate!.Levels[30].Vitality,
                        Evasion = combatant.NativeTemplate!.Levels[30].Evasion,
                        NativeTemplate = combatant.NativeTemplate! with { SceneLevel = 30, Levels = combatant.NativeTemplate!.Levels.Select(level => level with { MaximumHp = 60000 }).ToArray() }
                    }).ToArray()
                });
                ExpandedBattleInstalledEntry.Prepare(world, session, current => PrepareParty(current, guardian), formation, allies: 3, additionalEnemies: 0);
                world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { GuardianFormsEnabled = true, Difficulty = BattleDifficulty.Normal });
                world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: seed));
                BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
                ExpandedBattleInstalledEntry.WaitForEntry(world, battle);
                BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == Caster);
                commonCaster ??= (caster.Level, caster.Attack, caster.Speed);
                Assert.Equal(commonCaster.Value, (caster.Level, caster.Attack, caster.Speed));
                Ready(battle, caster);
                battle.RequestGuardianForm(Caster, guardian);
                WaitFor(battle, () => battle.GetGuardianForm(Caster)?.Phase == BattleGuardianFormPhase.Active && battle.ActiveAction is null, advance: true);
                BattleGuardianFormState form = battle.GetGuardianForm(Caster)!;
                string variant = form.VariantId!;
                if (measured.ContainsKey(variant))
                {
                    continue;
                }
                BattleCombatant[] enemies = battle.Combatants.Where(actor => !actor.IsPlayer && actor.IsAlive).ToArray();
                Assert.Equal(enemyCount, enemies.Length);
                Assert.All(enemies, enemy => Assert.Equal((4, 30, enemies[0].Defense, enemies[0].Evasion), (enemy.ModelId, enemy.Level, enemy.Defense, enemy.Evasion)));
                BattleGuardianFormAction attack = variant switch
                {
                    "ifrit" => BattleGuardianFormAction.HeavyStrike,
                    "sacred" => BattleGuardianFormAction.SacredMasterStrike,
                    "minotaur" when enemyCount == 1 => BattleGuardianFormAction.MinotaurAttack,
                    "minotaur" => BattleGuardianFormAction.MinotaurHeavyMower,
                    _ => throw new InvalidDataException($"Unexpected installed form variant '{variant}'.")
                };
                battle.SetMode(BattleAtbMode.Normal);
                long start = battle.Frame;
                int[] before = enemies.Select(enemy => enemy.CurrentHp).ToArray();
                int submissions = 0;
                for (int callback = 0; callback < 8000 && battle.GetGuardianForm(Caster) is { Phase: BattleGuardianFormPhase.Active, RemainingTicks: > 0 }; callback++)
                {
                    if (battle.ActiveAction is null && battle.CanChoosePlayerCommand(Caster))
                    {
                        battle.QueueGuardianFormAction(Caster, enemies[0].Id, attack);
                        submissions++;
                    }
                    battle.Tick();
                }
                Assert.InRange(form.RemainingTicks, 899, 900);
                Assert.False(battle.GetGuardianForm(Caster) is { Phase: BattleGuardianFormPhase.Active, RemainingTicks: > 0 });
                int[] damage = enemies.Select((enemy, index) => before[index] - enemy.CurrentHp).ToArray();
                measured.Add(variant, damage);
                output.WriteLine($"{variant}, seed {seed}, {form.RemainingTicks} form ticks over {battle.Frame - start} battle frames, {submissions} regular actions, damage [{string.Join(", ", damage)}].");
            }
        }
        Assert.Equal(new[] { "ifrit", "minotaur", "sacred" }, measured.Keys.Order().ToArray());
        Assert.All(measured.Values, damage => Assert.True(damage[0] > 0));
        Assert.All(measured["minotaur"], damage => Assert.True(damage > 0));
        Assert.All(measured["sacred"].Skip(1).Concat(measured["ifrit"].Skip(1)), damage => Assert.Equal(0, damage));
    }

    private static void VerifyInstalledForms(bool verifyActions)
    {
        string directory = TestResources.GameDirectory;
        using BattleAssets assets = new(directory);
        ImmutableArray<BattleGuardianFormResources> resources = ReadFormPackage(assets);
        string? captureDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_BROTHERS_CAPTURE");
        HashSet<string> tested = new(StringComparer.Ordinal);
        Dictionary<string, InstalledBrothersGestureProof.Sample> gestureReveal = new(StringComparer.Ordinal);
        for (byte seed = 0; seed < 8 && tested.Count < 2; seed++)
        {
            using GameAssets game = new(directory);
            GameSession session = new(ExpandedBattleEffectMatrixTests.PrepareField(directory, game.ReadColdProcessNativeBattleMemory()));
            session.SetExamTimer(session.ExamTimer!.SetRemaining(600));
            using GameWorld world = new(session, directory, sharedAssets: game, applyEncounterMods: (definition, _) => definition with
            {
                Combatants = definition.Combatants.Select(combatant => combatant.IsPlayer ? combatant : combatant with { NativeCombatantFlags = combatant.NativeCombatantFlags | 8, MaximumHp = 60000, CurrentHp = 60000 }).ToArray()
            });
            ExpandedBattleInstalledEntry.Prepare(world, session, current => PrepareParty(current, Brothers), formation: 24, allies: 3, additionalEnemies: 0);
            world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { GuardianFormsEnabled = true, Difficulty = BattleDifficulty.Normal });
            world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: seed));
            BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
            ExpandedBattleInstalledEntry.WaitForEntry(world, battle);
            BattleCombatant caster = Assert.Single(battle.Combatants, actor => actor.Id == Caster);
            int humanHp = caster.CurrentHp;
            string[] roster = battle.Combatants.Select(actor => actor.Id).ToArray();
            BattleModelOwner owner = battle.ReadPresentationModelOwner(Caster);
            byte? nativeModel = battle.Presentation!.Graphics.ActorModels.Get(owner).ModelId4;

            Ready(battle, caster);
            world.ApplyBattleInput(new(BattleInputKind.GuardianFormActivate, ActorId: Caster, TargetId: Caster, ActionId: Brothers));
            WaitFor(battle, () => battle.GetGuardianForm(Caster)?.Phase == BattleGuardianFormPhase.Entering, advance: true);
            BattleGuardianFormState entering = battle.GetGuardianForm(Caster)!;
            string variant = Assert.IsType<string>(entering.VariantId);
            if (!tested.Add(variant))
            {
                continue;
            }

            BattlePresentationProcess process = Assert.IsType<BattlePresentationProcess>(battle.Presentation);
            BattleScenePresentation? scene = string.IsNullOrWhiteSpace(captureDirectory) ? null : new(assets, battle.ReadDefinition());
            int entranceSamples = 0;
            long? firstEntranceSample = null;
            BattleSession? restored = null;
            if (!verifyActions)
            {
                SeedEnteringPoison(battle, caster);
                restored = battle.ForkForContinuation();
            }
            WaitFor(battle, () => battle.GetGuardianForm(Caster)?.Phase == BattleGuardianFormPhase.Active && battle.ActiveAction is null, observe: ObserveEntrance);
            if (scene is not null && !verifyActions)
            {
                SaveFrame(captureDirectory!, $"{variant}-active", scene.Render(battle, 320, 224));
            }
            if (restored is not null)
            {
                WaitFor(restored, () => restored.GetGuardianForm(Caster)?.Phase == BattleGuardianFormPhase.Active && restored.ActiveAction is null);
                Assert.Equal(battle.GetGuardianForm(Caster), restored.GetGuardianForm(Caster));
                AssertPoisonClearedAtLanding(battle);
                AssertPoisonClearedAtLanding(restored);
            }
            Assert.Equal(variant, battle.GetGuardianForm(Caster)!.VariantId);
            BattleGuardianFormResources expected = Assert.Single(resources, item => item.VariantId == variant);
            Assert.Equal((byte)(expected.Profile!.ModelId + 16), process.Graphics.ActorModels.Get(owner).ModelId4);
            Assert.Equal(expected.ElementDefenses.ToArray(), caster.ElementDefenses.ToArray());
            Assert.Equal(roster, battle.Combatants.Select(actor => actor.Id));
            BattleActionDefinition ordinarySummon = Assert.Single(battle.ActionDefinitions, action => action.Ability == BattleAbility.GuardianForce && action.Id == Brothers);
            BattleActionDefinition finisherDefinition = battle.GetGuardianFormActionDefinition(Caster, BattleGuardianFormAction.BrotherlyLove);
            Assert.Same(ordinarySummon.NativeAction, finisherDefinition.NativeAction);
            Assert.Equal(ordinarySummon.Statuses, finisherDefinition.Statuses);
            Assert.Equal(ordinarySummon with { Statuses = finisherDefinition.Statuses }, finisherDefinition);
            if (!verifyActions)
            {
                continue;
            }

            BattleCombatant[] enemies = battle.Combatants.Where(actor => !actor.IsPlayer && actor.IsActive && actor.IsAlive).ToArray();
            Assert.Equal(3, enemies.Length);
            bool sacred = variant == "sacred";
            Assert.Equal(sacred ? 900 : 1000, caster.ElementDefenses[3]);
            BattleGuardianFormAction attack = sacred ? BattleGuardianFormAction.SacredMasterStrike : BattleGuardianFormAction.MinotaurHeavyMower;
            BattleGuardianFormAction mend = sacred ? BattleGuardianFormAction.SacredMend : BattleGuardianFormAction.MinotaurMend;
            caster.CurrentHp -= 20;
            int injuredHp = caster.CurrentHp;
            caster.NativeStatus1 |= 1u << 13;
            int[] afterAttack = RunAction(battle, caster, enemies, attack);
            Assert.Equal(injuredHp, caster.CurrentHp);
            Assert.DoesNotContain(mend, battle.GetGuardianForm(Caster)!.UsedActions);
            if (sacred)
            {
                Assert.True(afterAttack[0] > 0);
                Assert.All(afterAttack.Skip(1), damage => Assert.Equal(0, damage));
            }
            else
            {
                Assert.All(afterAttack, damage => Assert.True(damage > 0));
            }

            caster.NativeStatus1 &= ~(1u << 13);
            RunAction(battle, caster, enemies, attack, expectedCompletions: 2);
            Assert.True(caster.CurrentHp > injuredHp);
            Assert.Contains(mend, battle.GetGuardianForm(Caster)!.UsedActions);

            enemies[0].SetNativeElementDefense(3, 800);
            enemies[1].SetNativeElementDefense(3, 900);
            enemies[2].SetNativeElementDefense(3, 1000);
            enemies[2].CurrentHp = Math.Max(1, enemies[2].MaximumHp / 2);
            BattleSession? companionFork = null;
            bool verifiedCompanionResume = false;
            bool drewCompanion = false;
            int madCowFrames = 0;
            int[] madCow = RunAction(battle, caster, enemies, BattleGuardianFormAction.MadCowSpecial, observe: () =>
            {
                NativeBattleGuardianFormPresentation? visual = process.Graphics.GuardianFormPresentation;
                if (companionFork is not null && !verifiedCompanionResume)
                {
                    companionFork.Tick();
                    NativeBattleGraphicsState restoredGraphics = companionFork.Presentation!.Graphics;
                    Assert.Equal(process.Graphics.Callback, restoredGraphics.Callback);
                    Assert.Equal(visual?.Companion, restoredGraphics.GuardianFormPresentation?.Companion);
                    Assert.Equal(visual?.CompanionAge, restoredGraphics.GuardianFormPresentation?.CompanionAge);
                    Assert.Equal(process.Graphics.ActorModels.Owners, restoredGraphics.ActorModels.Owners);
                    verifiedCompanionResume = true;
                }
                if (visual?.Companion is { } companion)
                {
                    Assert.False(process.Graphics.ActorModels.TryGet(companion, out _));
                    companionFork ??= battle.ForkForContinuation();
                    bool submittedCompanion = process.Graphics.PrimitiveFrame is { } frame && frame.LastCommands.Any(packet => packet.Command.PresentationActorId == companion.Presentation.Key);
                    drewCompanion |= submittedCompanion;
                    if (scene is not null && submittedCompanion && madCowFrames < madCowSampleAges.Length && visual.CompanionAge >= madCowSampleAges[madCowFrames])
                    {
                        SaveFrame(captureDirectory!, $"{variant}-mad-cow-{visual.CompanionAge:D3}", scene.Render(battle, 320, 224));
                        madCowFrames++;
                    }
                }
                Assert.Equal(roster, battle.Combatants.Select(actor => actor.Id));
            });
            Assert.True(verifiedCompanionResume);
            Assert.True(drewCompanion);
            Assert.Equal(BattleGuardianFormPhase.Active, battle.GetGuardianForm(Caster)?.Phase);
            Assert.Null(process.Graphics.GuardianFormPresentation!.Companion);
            Assert.Equal(roster, battle.Combatants.Select(actor => actor.Id));
            Assert.True(scene is null || madCowFrames > 0);
            Assert.True(madCow[0] > 0);
            Assert.Equal(0, madCow[1]);
            Assert.True(madCow[2] < 0);
            Assert.False(Assert.Single(battle.GetGuardianFormCommands(Caster), command => command.Id == (int)BattleGuardianFormAction.MadCowSpecial).Action.Available);

            BattleSession? skipped = null;
            int finisherStartedAt = battle.Events.Count;
            int[] finisher = RunAction(battle, caster, enemies, BattleGuardianFormAction.BrotherlyLove, observe: () =>
            {
                if (sacred && skipped is null && battle.CanSkipGuardianFormFinisher)
                {
                    skipped = battle.ForkForContinuation();
                    Assert.True(skipped.SkipGuardianFormFinisher());
                }
            });
            Assert.Contains(finisher, damage => damage > 0);
            WaitFor(battle, () => battle.GetGuardianForm(Caster) is null);
            Assert.Null(battle.GetGuardianForm(Caster));
            Assert.Equal(humanHp, caster.CurrentHp);
            Assert.Equal(nativeModel, process.Graphics.ActorModels.Get(owner).ModelId4);
            Assert.Equal(roster, battle.Combatants.Select(actor => actor.Id));
            if (sacred)
            {
                BattleSession skippedBattle = Assert.IsType<BattleSession>(skipped);
                WaitFor(skippedBattle, () => skippedBattle.GetGuardianForm(Caster) is null);
                Assert.Equal(battle.Combatants.Select(actor => (actor.Id, actor.CurrentHp)), skippedBattle.Combatants.Select(actor => (actor.Id, actor.CurrentHp)));
                Assert.Equal(nativeModel, skippedBattle.Presentation!.Graphics.ActorModels.Get(owner).ModelId4);
                Assert.Equal(DamageReceipts(battle), DamageReceipts(skippedBattle));
            }

            (string? Target, int Amount)[] DamageReceipts(BattleSession branch) => branch.Events.Skip(finisherStartedAt)
                .Where(entry => entry is { Kind: "Damage", ActorId: Caster }).Select(entry => (entry.TargetId, entry.Amount)).ToArray();

            void ObserveEntrance()
            {
                if (verifyActions ||
                    process.Graphics.OriginalEffect is not { RequestedEffect: 205, ReturnedDescriptor: > 0 } || process.Memory.ScriptedEffects is not { } core)
                {
                    return;
                }
                uint header = BinaryPrimitives.ReadUInt32LittleEndian(core.Read(0x279744C, 4));
                ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(core.Read(checked(header + 2), 2));
                if (firstEntranceSample is null && (flags & 0x80) == 0)
                {
                    return;
                }
                firstEntranceSample ??= process.Graphics.Callback;
                long offset = process.Graphics.Callback - firstEntranceSample.Value;
                if (offset == 84 && !gestureReveal.ContainsKey(variant))
                {
                    gestureReveal.Add(variant, InstalledBrothersGestureProof.Capture(process));
                }
                if (scene is null || entranceSamples == entranceSampleOffsets.Length || offset < entranceSampleOffsets[entranceSamples])
                {
                    return;
                }
                SaveFrame(captureDirectory!, $"{variant}-entry-{offset:D3}", scene.Render(battle, 320, 224));
                entranceSamples++;
            }
        }

        Assert.Equal(new[] { "minotaur", "sacred" }, tested.Order().ToArray());
        if (!verifyActions)
        {
            Assert.Equal(new[] { "minotaur", "sacred" }, gestureReveal.Keys.Order().ToArray());
            InstalledBrothersGestureProof.AssertSwitched(gestureReveal["minotaur"], gestureReveal["sacred"]);
        }
    }

    private static ImmutableArray<BattleGuardianFormResources> ReadFormPackage(BattleAssets assets)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "BundledMods", "Memoria", "Guardian Forms", "gameplay", "gf-forms.json");
        GuardianFormModDocument package = GuardianFormMods.Parse(File.ReadAllBytes(path));
        return package.Variants.Select(variant => assets.ReadGuardianFormResources(variant.Profile, variant.Actions.Add(variant.Profile.Finisher))).ToImmutableArray();
    }

    private static void SeedEnteringPoison(BattleSession battle, BattleCombatant caster)
    {
        const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
        ImmutableDictionary<string, NativeBattleStatusMirror> mirrors = battle.CaptureForContinuation().LogicalContinuation!.PartyReadyStatuses!;
        caster.NativeStatus0 |= PoisonMask;
        typeof(BattleSession).GetMethod("RefreshNativePresentationStatus", instance)!.Invoke(battle, [caster]);
        NativeBattleStatusMirror poisoned = caster.NativePresentationStatus;
        typeof(BattleSession).GetField("expandedPartyReadyStatusMirrors", instance)!.SetValue(battle, mirrors.SetItem(Caster, poisoned));
        Assert.Equal((int)PoisonMask, poisoned.Saved!.Value & PoisonMask);
    }

    private static void AssertPoisonClearedAtLanding(BattleSession battle)
    {
        BattleSnapshot continuation = battle.CaptureForContinuation();
        NativeBattleStatusSnapshot status = Assert.Single(continuation.Combatants, actor => actor.Id == Caster).NativeStatus!;
        Assert.Equal(0, status.Saved & PoisonMask);
        Assert.Equal(0, status.Presentation.Saved!.Value & PoisonMask);
        Assert.Equal(0, continuation.LogicalContinuation!.PartyReadyStatuses![Caster].Saved!.Value & PoisonMask);
    }

    private static void PrepareParty(GameSession session, int guardian)
    {
        ExpandedBattleGuardianTests.PrepareParty(session, learnedBoost: false, guardianId: guardian);
        NativeCharacterVitals vitals = session.State.CharacterVitals!;
        NativeCharacterVital source = vitals.Records[3];
        vitals = vitals.ReassignGuardianForceJunction(0, guardian, 3);
        NativeCharacterVital caster = vitals.Records[0];
        vitals.Records[0] = caster with { Junction = caster.Junction with { Commands = source.Junction.Commands }, GuardianForceCompatibility = source.GuardianForceCompatibility?.ToArray() };
        uint experience = NativeStatDefinitions.ExperienceForLevel(100, vitals.Definitions.GuardianForces[guardian]);
        vitals.GuardianForces[guardian] = vitals.GuardianForces[guardian] with { Experience = experience };
        session.SetCharacterVitals(vitals.WithBattleParty([0, 1, 2], 3).RefreshGuardianForces().Rest());
    }

    private static int[] RunAction(BattleSession battle, BattleCombatant caster, BattleCombatant[] enemies, BattleGuardianFormAction action, int expectedCompletions = 1, Action? observe = null)
    {
        Ready(battle, caster);
        Assert.Equal(enemies.Select(enemy => enemy.Id).Order(), battle.GetGuardianFormCommandTargets(Caster, (int)action).Select(target => target.Id).Order());
        int[] before = enemies.Select(enemy => enemy.CurrentHp).ToArray();
        int firstEvent = battle.Events.Count;
        battle.QueueGuardianFormAction(Caster, enemies[0].Id, action);
        WaitFor(battle, () => Completions() >= expectedCompletions, observe: observe);
        Assert.Equal(expectedCompletions, Completions());
        return enemies.Select((enemy, index) => before[index] - enemy.CurrentHp).ToArray();

        int Completions() => battle.Events.Skip(firstEvent).Count(entry => entry is { Kind: "ActionComplete", ActorId: Caster });
    }

    private static void Ready(BattleSession battle, BattleCombatant caster)
    {
        caster.Atb = BattleSession.AtbMaximum;
        WaitFor(battle, () => battle.CanChoosePlayerCommand(Caster));
    }

    private static void SaveFrame(string directory, string name, ReadOnlySpan<byte> pixels)
    {
        Directory.CreateDirectory(directory);
        using SkiaSharp.SKBitmap bitmap = new(320, 224);
        for (int y = 0; y < 224; y++)
        {
            for (int x = 0; x < 320; x++)
            {
                int offset = (y * 320 + x) * 4;
                bitmap.SetPixel(x, y, new SkiaSharp.SKColor(pixels[offset], pixels[offset + 1], pixels[offset + 2], pixels[offset + 3]));
            }
        }
        bitmap.SavePng(Path.Combine(directory, name + ".png"));
    }

    private static void WaitFor(BattleSession battle, Func<bool> reached, bool advance = false, Action? observe = null)
    {
        for (int callback = 0; callback < 1200 && !reached(); callback++)
        {
            if (advance && battle.ActiveAction is null)
            {
                battle.AdvanceTurn();
            }
            battle.Tick();
            observe?.Invoke();
        }
        Assert.True(reached(), $"Battle frame {battle.Frame}, callbacks {battle.NativeHostCallbacks}, phase {battle.GetGuardianForm(Caster)?.Phase}, action {battle.ActiveAction?.ActionId}.");
    }
}
