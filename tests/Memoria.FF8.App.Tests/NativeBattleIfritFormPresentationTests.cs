using System.Buffers.Binary;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleIfritFormPresentationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Installed_Ifrit_finisher_rebuilds_after_entry_and_restores_the_human(bool skip, bool skipAfterContact)
    {
        RunFinisher(skip, skipAfterContact);
    }

    [Fact]
    public void Loaded_save_Ifrit_form_restores_participants_and_shared_textures_through_actions_and_finisher()
    {
        string source = TestResources.FixturePath("App.Tests.IfritFormLoadedSave.1");
        GameState state = GameSession.Load(source);
        RunFinisher(false, false, state.NativeBattleMemory);
    }

    [Fact]
    public void Installed_Ifrit_form_Fire_completes_with_a_status_specific_human_standing_sequence()
    {
        RunFinisher(false, false, statusStanding: true);
    }

    private static void RunFinisher(bool skip, bool skipAfterContact, NativeBattleRetainedMemory? retainedMemory = null, bool statusStanding = false)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition definition = NativeBattleTargetGuardianCastTests.CreateDefinition(game, assets, 2, 201) with { GuardianFormsEnabled = true, GuardianFormResources = [assets.ReadIfritGuardianFormResources()] };
        if (retainedMemory is not null)
        {
            definition = definition with { NativeRetainedMemory = retainedMemory };
        }
        if (!skip)
        {
            BattleActionDefinition fire = assets.ActionDefinitions.Single(action => action.Ability == BattleAbility.Magic && action.Id == 1) with { Available = true };
            BattleCombatantDefinition other = new() { Id = "other", Name = "Quistis", IsPlayer = true, ModelId = 3, WeaponId = assets.WeaponDefinitions.First(weapon => weapon.CharacterId == 3).Id, MaximumHp = 1000, CurrentHp = 1000, InitialAtb = BattleSession.AtbMaximum, Speed = 1, Abilities = [BattleAbility.Attack, BattleAbility.Magic], MagicStocks = [new(1, 2)], NativeDeathCount = 0, GuardianForceCompatibility = Enumerable.Repeat((ushort)3000, 16).ToArray() };
            other = other with { Speed = 80, NativeAttack = assets.ReadPlayerNativeAttackData(game.ReadNewGameCharacterVitals(), 3) };
            BattleCombatantDefinition[] participants = [definition.Combatants[0] with { Speed = 80 }, other, definition.Combatants[1] with { NativeCombatantFlags = 8 }];
            definition = definition with { Combatants = participants, ActionDefinitions = [definition.ActionDefinitions[0], fire], NativeGraphics = assets.ReadNativeGraphicsDefinition(participants, game.ReadNativeBattleGraphicsRules(), assets.LoadEncounter(94)), NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([1, 201]), GuardianForces = [new(2, 600, 1200)] };
        }
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        BattleScenePresentation scene = new(assets, definition, true);
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF8_IFRIT_FORM_EVIDENCE");
        string? output = root is null ? null : Path.Combine(root, statusStanding ? "status-standing" : retainedMemory is not null ? "loaded-finisher" : skip ? skipAfterContact ? "skip-after-contact" : "skip" : "finisher");
        if (output is not null)
        {
            Directory.CreateDirectory(output);
        }
        bool advanceClassicCharge = false;
        try
        {
            WaitFor(() => battle.CanRequestGuardianForm("party", 2));
            ushort[] visibility = battle.NativeGraphics!.Models.Select(model => (ushort)(model.Flags0 & 0xC)).ToArray();
            battle.RequestGuardianForm("party", 2);
            WaitFor(() => battle.GetGuardianForm("party")?.Phase == BattleGuardianFormPhase.Active);
            if (statusStanding)
            {
                BattleSnapshot active = battle.Capture();
                NativeBattleGraphicsState graphics = active.NativeGraphics!;
                NativeBattleGraphicsModel model = graphics.Models[0];
                Assert.Equal((byte)1, model.SequenceState!.Default2);
                battle = BattleSession.Restore(active with { NativeGraphics = graphics with { Models = graphics.Models.SetItem(0, model with { Flags8 = model.Flags8!.Value | 0x1000000 }) } });
            }
            AssertVisibility();
            AssertCommonTexture();
            BattlePresentedVital activeVital = skip ? new(1000, 1000) : new(600, 1200);
            Assert.Equal(activeVital, battle.ReadPresentedVital("party"));
            Assert.Equal(activeVital, battle.ReadDisplayedVital("party"));
            ushort expectedAffinity = Math.Clamp(unchecked((ushort)(definition.Combatants[0].GuardianForceCompatibility![2] + definition.ActionDefinitions[0].NativeAction!.RawRecord[114] - 100)), (ushort)1000, (ushort)6000);
            Assert.Equal(expectedAffinity, battle.Capture().Combatants.Single(actor => actor.Id == "party").GuardianForceCompatibility![2]);
            NativeBattleRestorationPresentationTests.Publish(output, "active", battle.Capture(), output is null ? null : scene.Render(battle, 320, 224).ToArray());
            if (!skip)
            {
                WaitForCommand("other");
                battle.QueueAction("other", "enemy", BattleAbility.Attack, 0);
                WaitFor(() => battle.Events.Any(item => item.Kind == "Damage" && item.ActorId == "other") && battle.ActiveAction is null);
                AssertCommonTexture();
                RoundTripNext();
                int priorOtherDamage = battle.Events.Count(item => item.Kind == "Damage" && item.ActorId == "other");
                WaitForCommand("other");
                battle.QueueAction("other", "enemy", BattleAbility.Magic, 1);
                WaitFor(() => battle.Events.Count(item => item.Kind == "Damage" && item.ActorId == "other") > priorOtherDamage && battle.ActiveAction is null);
                RoundTripNext();
                foreach (BattleGuardianFormAction action in new[] { BattleGuardianFormAction.Attack, BattleGuardianFormAction.Fire })
                {
                    WaitForCommand("party");
                    int count = battle.Events.Count(item => item.Kind == "Damage" && item.ActorId == "party");
                    battle.QueueGuardianFormAction("party", "enemy", action);
                    WaitFor(() => battle.Events.Count(item => item.Kind == "Damage" && item.ActorId == "party") == count + 1 && battle.ActiveAction is null);
                    Assert.Empty(battle.Combatants.Single(actor => actor.Id == "party").MagicStocks);
                    Assert.Null(battle.NativeGraphics!.TriggerWidget);
                    RoundTripNext();
                }
            }
            WaitForCommand("party");
            ushort affinityBeforeFinisher = battle.Capture().Combatants.Single(actor => actor.Id == "party").GuardianForceCompatibility![2];
            int priorPartyDamage = battle.Events.Count(item => item.Kind == "Damage" && item.ActorId == "party");
            battle.QueueGuardianFormAction("party", "enemy", BattleGuardianFormAction.HellFire);
            bool visible = false;
            bool restoredAfterContact = false;
            using StreamWriter? trace = output is null ? null : new(Path.Combine(output, "continuation-trace.jsonl"));
            int continuationCallback = 0;
            int finaleCallbacks = 0;
            for (int callback = 0; callback < 1200 && battle.GetGuardianForm("party") is not null; callback++)
            {
                bool finalePlaying = battle.NativeGraphics?.GuardianFormPresentation is { SkipRequested: true } && NativeBattleIfritContinuation.HasReachedFinale(battle.NativeEffectMemory!.ScriptedEffects!);
                long previousGraphicsCallback = battle.NativeGraphics!.Callback;
                if (skip)
                {
                    battle.AdvanceNativeGraphicsPresentation(TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30));
                }
                else
                {
                    battle.Tick();
                }
                if (finalePlaying)
                {
                    Assert.Equal(previousGraphicsCallback + 1, battle.NativeGraphics!.Callback);
                }
                if (skip && battle.NativeGraphics?.GuardianFormPresentation is { SkipRequested: true } && NativeBattleIfritContinuation.HasReachedFinale(battle.NativeEffectMemory!.ScriptedEffects!))
                {
                    if (finaleCallbacks is 0 or 6 or 12 or 24)
                    {
                        NativeBattleRestorationPresentationTests.Publish(output, "meteor-" + finaleCallbacks.ToString("D3"), battle.Capture(), output is null ? null : scene.Render(battle, 320, 224).ToArray());
                    }
                    finaleCallbacks++;
                }
                if (battle.NativeGraphics?.GuardianFormPresentation is { Mode: GuardianFormInvocationMode.FinisherWithoutEntry, Phase: 5 } && output is not null)
                {
                    TraceContinuation(trace!, continuationCallback++);
                }
                if (!visible && battle.NativeGraphics?.GuardianFormPresentation is { Mode: GuardianFormInvocationMode.FinisherWithoutEntry, Phase: 5 })
                {
                    visible = true;
                    AssertFinisherTextures();
                    NativeBattleRestorationPresentationTests.Publish(output, "continuation", battle.Capture(), output is null ? null : scene.Render(battle, 320, 224).ToArray());
                    RoundTripNext();
                    if (skip && !skipAfterContact)
                    {
                        Assert.True(battle.SkipGuardianFormFinisher());
                        Assert.False(battle.SkipGuardianFormFinisher());
                        RoundTripNext();
                        Assert.True(NativeBattleIfritContinuation.HasReachedFinale(battle.NativeEffectMemory!.ScriptedEffects!));
                    }
                }
                if (!restoredAfterContact && battle.Events.Count(item => item.Kind == "Damage" && item.ActorId == "party") > priorPartyDamage)
                {
                    restoredAfterContact = true;
                    RoundTripNext();
                    if (skipAfterContact)
                    {
                        Assert.True(battle.SkipGuardianFormFinisher());
                        RoundTripNext();
                    }
                }
            }
            Assert.True(visible);
            Assert.True(restoredAfterContact);
            if (skip)
            {
                Assert.True(finaleCallbacks > 1);
            }
            Assert.Null(battle.GetGuardianForm("party"));
            AssertVisibility();
            AssertCommonTexture();
            Assert.Single(battle.Events, item => item.Kind == "GuardianForceCharging");
            Assert.Equal(priorPartyDamage + 1, battle.Events.Count(item => item.Kind == "Damage" && item.ActorId == "party"));
            Assert.Equal((byte)0, battle.NativeGraphics!.Models[0].ModelId4);
            Assert.Equal(1000, battle.Combatants.Single(actor => actor.Id == "party").CurrentHp);
            Assert.Equal(affinityBeforeFinisher, battle.Capture().Combatants.Single(actor => actor.Id == "party").GuardianForceCompatibility![2]);
            NativeBattleRestorationPresentationTests.Publish(output, "returned", battle.Capture(), output is null ? null : scene.Render(battle, 320, 224).ToArray());
            RoundTripNext();
            if (!skip)
            {
                battle.AdvanceTurn();
                WaitForCommand("party");
                battle.QueueAction("party", "enemy", BattleAbility.GuardianForce, 2);
                advanceClassicCharge = true;
                WaitFor(() => battle.NativeGraphics?.OriginalEffect is { Binding.Effect: 201, ReturnedDescriptor: > 0 } && battle.NativeEffectMemory?.Cinematic is { Effect: 201 });
                advanceClassicCharge = false;
                Assert.Null(battle.NativeGraphics!.GuardianFormPresentation);
                NativeBattleScriptedEffectMemory core = battle.NativeEffectMemory!.ScriptedEffects!;
                uint manager = BinaryPrimitives.ReadUInt32LittleEndian(core.Read(0x27973EC, 4));
                uint bank = BinaryPrimitives.ReadUInt32LittleEndian(core.Read(manager + 0x90, 4));
                uint resource = BinaryPrimitives.ReadUInt32LittleEndian(core.Read(0x2798A68, 4));
                uint pc = BinaryPrimitives.ReadUInt32LittleEndian(battle.NativeEffectMemory.Arena!.Read(checked((int)(bank - 0x20DFAB8u)), 4));
                Assert.Equal(resource + 0xFE, pc);
                WaitFor(() => battle.Events.Count(item => item.Kind == "Damage" && item.ActorId == "party") == priorPartyDamage + 2 && battle.ActiveAction is null);
                Assert.Equal(2, battle.Events.Count(item => item.Kind == "GuardianForceCharging"));
                RoundTripNext();
            }

            void AssertVisibility()
            {
                Assert.Equal(visibility, battle.NativeGraphics!.Models.Select(model => (ushort)(model.Flags0 & 0xC)));
            }
        }
        catch
        {
            if (output is not null)
            {
                File.WriteAllText(Path.Combine(output, "failed.battle.json"), JsonSerializer.Serialize(battle.Capture()));
            }
            throw;
        }

        void WaitFor(Func<bool> ready)
        {
            for (int index = 0; index < 1000 && !ready(); index++)
            {
                if (advanceClassicCharge && battle.ActiveAction is null)
                {
                    battle.AdvanceTurn();
                }
                battle.Tick();
            }
            Assert.True(ready());
        }

        void WaitForCommand(string actor)
        {
            for (int index = 0; index < 1000 && !battle.CanChoosePlayerCommand(actor); index++)
            {
                if (battle.ActiveAction is null)
                {
                    battle.AdvanceTurn();
                }
                battle.Tick();
            }
            Assert.True(battle.CanChoosePlayerCommand(actor));
        }

        void RoundTripNext()
        {
            BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
            if (skip)
            {
                TimeSpan elapsed = TimeSpan.FromTicks((2 * TimeSpan.TicksPerSecond + 30) / 31);
                long before = battle.NativeGraphics!.Callback;
                battle.AdvanceNativeGraphicsPresentation(elapsed, TimeSpan.Zero);
                restored.AdvanceNativeGraphicsPresentation(elapsed, TimeSpan.Zero);
                Assert.Equal(before + 1, battle.NativeGraphics!.Callback);
                Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
                for (int callback = 0; callback < 1200 && battle.GuardianAnimationSkipPending; callback++)
                {
                    Assert.True(restored.GuardianAnimationSkipPending);
                    before = battle.NativeGraphics.Callback;
                    battle.AdvanceNativeGraphicsPresentation(TimeSpan.Zero, TimeSpan.Zero);
                    restored.AdvanceNativeGraphicsPresentation(TimeSpan.Zero, TimeSpan.Zero);
                    Assert.Equal(before + 1, battle.NativeGraphics.Callback);
                    Assert.Equal(battle.NativeGraphics.Callback, restored.NativeGraphics!.Callback);
                }
                Assert.False(battle.GuardianAnimationSkipPending);
                Assert.False(restored.GuardianAnimationSkipPending);
            }
            else
            {
                battle.Tick();
                restored.Tick();
            }
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        }

        void AssertCommonTexture()
        {
            NativeBattleEffectInitializerAsset common = definition.NativeCommonEffectTexture!;
            NativeBattleEffectTextureState expected = NativeBattleEffectTextureState.FromColdProcess();
            (expected, _) = expected.EnqueueTim505E30(new(common.Name, common.Data));
            NativeBattleEffectArena baseline = new(NativeBattleStorageSnapshots.Capture(expected.Drain505D20().State).VramRegions);
            NativeBattleEffectArena actual = new(NativeBattleStorageSnapshots.Capture(battle.NativeEffectMemory!.Textures!).VramRegions);
            for (int y = 256; y < 512; y++)
            {
                Assert.Equal(baseline.Read(2 * (y * 1024 + 704), 192 * 2), actual.Read(2 * (y * 1024 + 704), 192 * 2));
            }
            for (int y = 225; y < 251; y++)
            {
                Assert.Equal(baseline.Read(2 * (y * 1024 + 624), 16 * 2), actual.Read(2 * (y * 1024 + 624), 16 * 2));
            }
        }

        void AssertFinisherTextures()
        {
            NativeBattleCinematicResources resources = battle.NativeGraphics!.OriginalEffect!.Binding.CinematicResources!;
            NativeBattleEffectArena actual = new(NativeBattleStorageSnapshots.Capture(battle.NativeEffectMemory!.Textures!).VramRegions);
            foreach ((int fileIndex, int textureIndex) in new[] { (2, 21), (3, 13), (4, 12) })
            {
                NativeBattleArchiveFile file = resources.Files.Single(file => file.FileId == NativeBattleCinematicProfile.ForEffect(201).SourceBase + fileIndex);
                NativeBattleEffectTextureRectangle rectangle = resources.Rectangle(textureIndex, false);
                int sourceOffset = 0;
                if (fileIndex == 3)
                {
                    int directory = BinaryPrimitives.ReadInt32LittleEndian(file.Bytes.AsSpan().Slice(0x14));
                    sourceOffset = checked(directory + (BinaryPrimitives.ReadInt32LittleEndian(file.Bytes.AsSpan().Slice(directory + textureIndex * 4)) & 0xFFFFFF));
                }
                for (int row = 0; row < rectangle.Height; row++)
                {
                    Assert.Equal(file.Bytes.AsSpan(sourceOffset + row * rectangle.Width * 2, rectangle.Width * 2).ToArray(), actual.Read(2 * ((rectangle.Y + row) * 1024 + rectangle.X), rectangle.Width * 2));
                }
            }
        }

        void TraceContinuation(StreamWriter trace, int callback)
        {
            NativeBattleEffectMemory memory = battle.NativeEffectMemory!;
            NativeBattleScriptedEffectMemory core = memory.ScriptedEffects!;
            uint Read(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(core.Read(address, 4));
            uint bank = Read(Read(0x27973EC) + 0x90);
            uint resource = Read(0x2798A68);
            var slots = new[] { 0, 14, 15, 17, 19 }.Select(index =>
            {
                byte[] bytes = memory.Arena!.Read(checked((int)(bank - 0x20DFAB8u) + index * 256), 256);
                return new { index, Pc = BinaryPrimitives.ReadUInt32LittleEndian(bytes), Pc1 = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)), Position = Convert.ToHexString(bytes.AsSpan(0x50, 12)), Translation = Convert.ToHexString(bytes.AsSpan(0x5C, 12)), Rotation = Convert.ToHexString(bytes.AsSpan(0x8C, 6)) };
            }).ToArray();
            trace.WriteLine(JsonSerializer.Serialize(new { callback, battle.Frame, Resource = resource, slots }));
            trace.Flush();
            if (callback % 30 == 0 && callback < 180)
            {
                NativeBattleRestorationPresentationTests.Publish(output, "finisher-" + callback.ToString("D3"), battle.Capture(), scene.Render(battle, 320, 224).ToArray());
            }
        }
    }

    [Fact]
    public void Installed_Ifrit_form_entry_lands_without_an_offensive_transaction()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition definition = NativeBattleTargetGuardianCastTests.CreateDefinition(game, assets, 2, 201) with { GuardianFormsEnabled = true, GuardianFormResources = [assets.ReadIfritGuardianFormResources()] };
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        BattleScenePresentation scene = new(assets, definition, true);
        for (int index = 0; index < 400 && (!battle.NativeEntryReady || !battle.CanChoosePlayerCommand("party")); index++)
        {
            battle.Tick();
        }
        Assert.True(battle.CanRequestGuardianForm("party", 2));
        BattleSnapshot uninvoked = battle.Capture();
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(uninvoked with { NativeGraphics = uninvoked.NativeGraphics! with { GuardianFormHumanFocalToRestore = 512 } }));
        battle.RequestGuardianForm("party", 2);
        bool restoredEmergence = false;
        int emergenceCallbacks = 0;
        for (int index = 0; index < 1000 && battle.GetGuardianForm("party")?.Phase != BattleGuardianFormPhase.Active; index++)
        {
            battle.Tick();
            if (!restoredEmergence && battle.NativeGraphics?.GuardianFormPresentation is { Phase: 1 } && ++emergenceCallbacks == 30)
            {
                BattleSnapshot emerging = battle.Capture();
                BattleSession emergingRestored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(emerging))!);
                battle.Tick();
                emergingRestored.Tick();
                BattleSnapshot originalNext = battle.Capture();
                BattleSnapshot restoredNext = emergingRestored.Capture();
                string? emergenceOutput = Environment.GetEnvironmentVariable("MEMORIA_FF8_IFRIT_FORM_EVIDENCE");
                NativeBattleRestorationPresentationTests.Publish(emergenceOutput, "emergence-original-next", originalNext, null);
                NativeBattleRestorationPresentationTests.Publish(emergenceOutput, "emergence-restored-next", restoredNext, null);
                Assert.Equal(JsonSerializer.Serialize(originalNext), JsonSerializer.Serialize(restoredNext));
                restoredEmergence = true;
            }
        }
        Assert.True(restoredEmergence);
        BattleSnapshot landed = battle.Capture();
        Assert.Throws<InvalidDataException>(() => BattleSession.Restore(landed with { NativeGraphics = landed.NativeGraphics! with { GuardianFormPresentation = landed.NativeGraphics.GuardianFormPresentation! with { HumanFocalLength = 0 } } }));
        Assert.Equal(BattleGuardianFormPhase.Active, battle.GetGuardianForm("party")?.Phase);
        Assert.DoesNotContain(battle.Events, item => item.Kind == "Damage" && item.ActorId == "party");
        Assert.Equal(9999, battle.Combatants.Single(actor => actor.Id == "enemy").CurrentHp);
        Assert.Equal((byte)77, battle.NativeGraphics!.Models[0].ModelId4);
        Assert.Equal("party", battle.NativeGraphics.ActorIds[0]);
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_IFRIT_FORM_EVIDENCE");
        NativeBattleRestorationPresentationTests.Publish(output, "landed", landed, output is null ? null : scene.Render(battle, 320, 224).ToArray());
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(landed))!);
        battle.Tick();
        restored.Tick();
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void Installed_Ifrit_presentation_exposes_original_script_and_model_boundaries()
    {
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_IFRIT_PRESENTATION_EVIDENCE");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(output is not null && directory is not null, "Select installed resources and an explicit presentation evidence directory.");
        Directory.CreateDirectory(output!);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition definition = NativeBattleTargetGuardianCastTests.CreateDefinition(game, assets, 2, 201);
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        BattleScenePresentation scene = new(assets, definition, true);
        for (int index = 0; index < 400 && (!battle.NativeEntryReady || !battle.CanChoosePlayerCommand("party")); index++)
        {
            battle.Tick();
        }
        battle.QueueAction("party", "enemy", BattleAbility.GuardianForce, 2);
        using StreamWriter trace = new(Path.Combine(output!, "script-model-trace.jsonl"));
        int cinematicCallback = 0;
        bool entered = false;
        for (int callback = 0; callback < 4096; callback++)
        {
            battle.Tick();
            BattleSnapshot snapshot = battle.Capture();
            if (snapshot.NativeGraphics?.OriginalEffect is not { Binding.Effect: 201, ReturnedDescriptor: > 0 })
            {
                if (entered)
                {
                    break;
                }
                continue;
            }
            entered = true;
            NativeBattleEffectMemory memory = snapshot.NativeEffectMemory!;
            NativeBattleScriptedEffectMemory core = memory.ScriptedEffects!;
            NativeBattleEffectArena arena = memory.Arena!;
            uint bank = Read(Read(0x27973EC) + 0x90);
            var slots = Enumerable.Range(0, 32).Select(index =>
            {
                int offset = checked((int)(bank - 0x20DFAB8u) + index * 256);
                byte[] bytes = arena.Read(offset, 256);
                return new { index, Pc = BinaryPrimitives.ReadUInt32LittleEndian(bytes), Pc1 = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)), Pc2 = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8)), Pose = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0xBC)), Position = Convert.ToHexString(bytes.AsSpan(0x50, 12)), Rotation = Convert.ToHexString(bytes.AsSpan(0x8C, 6)) };
            }).Where(slot => slot.Pc != 0 || slot.Pc1 != 0 || slot.Pc2 != 0).ToArray();
            trace.WriteLine(JsonSerializer.Serialize(new { cinematicCallback, snapshot.Frame, Resource = Read(0x2798A68), slots }));
            trace.Flush();
            if (cinematicCallback % 30 == 0)
            {
                NativeBattleRestorationPresentationTests.Publish(output, "presentation-" + cinematicCallback.ToString("D4"), snapshot, scene.Render(battle, 320, 224).ToArray());
            }
            cinematicCallback++;

            uint Read(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(core.Read(address, 4));
        }
        Assert.True(entered);
        Assert.Single(battle.Events, item => item.Kind == "Damage" && item.ActorId == "party");
    }
}
