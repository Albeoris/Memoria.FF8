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
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition definition = NativeBattleTargetGuardianCastTests.CreateDefinition(game, assets, 2, 201) with { GuardianFormsEnabled = true, GuardianFormResources = [assets.ReadIfritGuardianFormResources()] };
        if (!skip)
        {
            BattleActionDefinition fire = assets.ActionDefinitions.Single(action => action.Ability == BattleAbility.Magic && action.Id == 1) with { Available = true };
            BattleCombatantDefinition other = new() { Id = "other", Name = "Zell", IsPlayer = true, ModelId = 1, WeaponId = assets.WeaponDefinitions.First(weapon => weapon.CharacterId == 1).Id, MaximumHp = 1000, CurrentHp = 1000, InitialAtb = BattleSession.AtbMaximum, Speed = 1, Abilities = [BattleAbility.Magic], MagicStocks = [new(1, 2)], NativeDeathCount = 0, GuardianForceCompatibility = Enumerable.Repeat((ushort)3000, 16).ToArray() };
            BattleCombatantDefinition[] participants = [definition.Combatants[0], other, definition.Combatants[1] with { NativeCombatantFlags = 8 }];
            definition = definition with { Combatants = participants, ActionDefinitions = [definition.ActionDefinitions[0], fire], NativeGraphics = assets.ReadNativeGraphicsDefinition(participants, game.ReadNativeBattleGraphicsRules(), assets.LoadEncounter(94)), NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([1, 201]), GuardianForces = [new(2, 600, 1200)] };
        }
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        BattleScenePresentation scene = new(assets, definition, true);
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF8_IFRIT_FORM_EVIDENCE");
        string? output = root is null ? null : Path.Combine(root, skip ? skipAfterContact ? "skip-after-contact" : "skip" : "finisher");
        if (output is not null)
        {
            Directory.CreateDirectory(output);
        }
        bool advanceClassicCharge = false;
        try
        {
            WaitFor(() => battle.CanRequestGuardianForm("party", 2));
            battle.RequestGuardianForm("party", 2);
            WaitFor(() => battle.GetGuardianForm("party")?.Phase == BattleGuardianFormPhase.Active);
            BattlePresentedVital activeVital = skip ? new(1000, 1000) : new(600, 1200);
            Assert.Equal(activeVital, battle.ReadPresentedVital("party"));
            Assert.Equal(activeVital, battle.ReadDisplayedVital("party"));
            ushort expectedAffinity = Math.Clamp(unchecked((ushort)(definition.Combatants[0].GuardianForceCompatibility![2] + definition.ActionDefinitions[0].NativeAction!.RawRecord[114] - 100)), (ushort)1000, (ushort)6000);
            Assert.Equal(expectedAffinity, battle.Capture().Combatants.Single(actor => actor.Id == "party").GuardianForceCompatibility![2]);
            NativeBattleRestorationPresentationTests.Publish(output, "active", battle.Capture(), output is null ? null : scene.Render(battle, 320, 224).ToArray());
            if (!skip)
            {
                WaitFor(() => battle.CanChoosePlayerCommand("other"));
                battle.QueueAction("other", "enemy", BattleAbility.Magic, 1);
                WaitFor(() => battle.Events.Any(item => item.Kind == "Damage" && item.ActorId == "other") && battle.ActiveAction is null);
                RoundTripNext();
                foreach (BattleGuardianFormAction action in new[] { BattleGuardianFormAction.Attack, BattleGuardianFormAction.Fire })
                {
                    WaitFor(() => battle.CanChoosePlayerCommand("party"));
                    int count = battle.Events.Count(item => item.Kind == "Damage" && item.ActorId == "party");
                    battle.QueueGuardianFormAction("party", "enemy", action);
                    WaitFor(() => battle.Events.Count(item => item.Kind == "Damage" && item.ActorId == "party") == count + 1 && battle.ActiveAction is null);
                    Assert.Empty(battle.Combatants.Single(actor => actor.Id == "party").MagicStocks);
                    Assert.Null(battle.NativeGraphics!.TriggerWidget);
                    RoundTripNext();
                }
            }
            WaitFor(() => battle.CanChoosePlayerCommand("party"));
            ushort affinityBeforeFinisher = battle.Capture().Combatants.Single(actor => actor.Id == "party").GuardianForceCompatibility![2];
            int priorPartyDamage = battle.Events.Count(item => item.Kind == "Damage" && item.ActorId == "party");
            battle.QueueGuardianFormAction("party", "enemy", BattleGuardianFormAction.HellFire);
            bool visible = false;
            bool restoredAfterContact = false;
            for (int callback = 0; callback < 1200 && battle.GetGuardianForm("party") is not null; callback++)
            {
                if (skip)
                {
                    battle.AdvanceNativeGraphicsPresentation(TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30));
                }
                else
                {
                    battle.Tick();
                }
                if (!visible && battle.NativeGraphics?.GuardianFormPresentation is { Mode: GuardianFormInvocationMode.FinisherWithoutEntry, Phase: 5 })
                {
                    visible = true;
                    NativeBattleRestorationPresentationTests.Publish(output, "continuation", battle.Capture(), output is null ? null : scene.Render(battle, 320, 224).ToArray());
                    RoundTripNext();
                    if (skip && !skipAfterContact)
                    {
                        Assert.True(battle.SkipGuardianFormFinisher());
                        Assert.False(battle.SkipGuardianFormFinisher());
                        RoundTripNext();
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
            Assert.Null(battle.GetGuardianForm("party"));
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
                WaitFor(() => battle.CanChoosePlayerCommand("party"));
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

        void RoundTripNext()
        {
            BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
            if (skip)
            {
                TimeSpan elapsed = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
                battle.AdvanceNativeGraphicsPresentation(elapsed);
                restored.AdvanceNativeGraphicsPresentation(elapsed);
            }
            else
            {
                battle.Tick();
                restored.Tick();
            }
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
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
                Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(emergingRestored.Capture()));
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


