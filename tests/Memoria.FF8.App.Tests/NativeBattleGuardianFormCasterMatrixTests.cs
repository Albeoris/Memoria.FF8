using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGuardianFormCasterMatrixTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    public void Installed_Ifrit_form_preserves_every_native_caster_in_every_party_slot(int character)
    {
        Run(character, 0, null);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(5, 2)]
    [InlineData(8, 0)]
    [InlineData(9, 1)]
    [InlineData(10, 2)]
    public void Installed_Ifrit_form_restores_each_distinct_alternate_native_costume(int character, int slot)
    {
        Run(character, 1, slot);
    }

    [Fact]
    public void Installed_caster_resources_keep_native_costume_and_every_weapon_selector()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        using BattleAssets assets = new(directory!);
        Assert.Equal(Enumerable.Range(0, 11), assets.PlayerCharacterIds);
        foreach (int character in assets.PlayerCharacterIds)
        {
            NativeBattleModelDatSource first = assets.ReadNativeModelDatSource((byte)character, false, 0);
            NativeBattleModelDatSource alternate = assets.ReadNativeModelDatSource((byte)character, false, 1);
            bool distinctCostume = character is 0 or 1 or 5 or 8 or 9 or 10;
            Assert.Equal(distinctCostume, first.FileId != alternate.FileId);
            foreach (int costume in distinctCostume ? new[] { 0, 1 } : new[] { 0 })
            {
                int?[] weapons = character == 7 ? [null] : assets.WeaponDefinitions.Where(weapon => weapon.CharacterId == character).Select(weapon => (int?)weapon.Id).ToArray();
                Assert.NotEmpty(weapons);
                foreach (int? weapon in weapons)
                {
                    BattleCombatantDefinition actor = new() { Id = "party", Name = "Caster", IsPlayer = true, ModelId = character, Costume = costume, WeaponId = weapon };
                    NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(actor);
                    Assert.Equal((byte)costume, resource.BodyFileSelector129);
                    Assert.NotNull(resource.PoseSource);
                    Assert.NotEmpty(resource.SequenceSection);
                    Assert.NotNull(resource.ModelLoadSources);
                    NativeBattleModelDatSource body = Assert.Single(resource.ModelLoadSources.Value, source => !source.Linked);
                    Assert.Equal(costume == 0 ? first.FileId : alternate.FileId, body.FileId);
                    if (weapon is int selected)
                    {
                        byte tier = checked((byte)assets.WeaponDefinitions.Single(item => item.Id == selected).Tier);
                        Assert.Equal(tier, resource.LinkedFileSelector128);
                        NativeBattleModelDatSource linked = Assert.Single(resource.ModelLoadSources.Value, source => source.Linked);
                        Assert.Equal(assets.ReadNativeModelDatSource((byte)character, true, tier).FileId, linked.FileId);
                    }
                    else
                    {
                        Assert.Null(resource.LinkedFileSelector128);
                        Assert.Single(resource.ModelLoadSources.Value);
                    }
                }
            }
        }
    }

    private static void Run(int character, int costume, int? physicalSlot)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(game.GameDirectory);
        Assert.Contains(character, assets.PlayerCharacterIds);
        BattleDefinition basis = NativeBattleTargetGuardianCastTests.CreateDefinition(game, assets, 2, 201) with
        {
            GuardianFormsEnabled = true,
            GuardianFormResources = [assets.ReadIfritGuardianFormResources()],
            ActionDefinitions = [assets.ActionDefinitions.Single(action => action.Ability == BattleAbility.Magic && action.Id == 1) with { Available = true }, assets.ActionDefinitions.Single(action => action.Ability == BattleAbility.GuardianForce && action.Id == 2) with { Level = 10, Available = true, NativeGuardianBoostAvailable = false }],
            NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([1, 201])
        };
        BattleCombatantDefinition human = basis.Combatants[0] with
        {
            ModelId = character,
            Costume = costume,
            WeaponId = character == 7 ? null : (costume == 0 ? assets.WeaponDefinitions.First(weapon => weapon.CharacterId == character).Id : assets.WeaponDefinitions.Last(weapon => weapon.CharacterId == character).Id),
            Speed = 80
        };
        NativeBattleGraphicsResource resource = assets.ReadNativeGraphicsResource(human);
        Assert.NotNull(resource.ModelLoadSources);
        Assert.NotEmpty(resource.ModelLoadSources);
        for (int slot = 0; slot < 3; slot++)
        {
            if (physicalSlot is null || physicalSlot == slot)
            {
                RunSlot(slot);
            }
        }

        void RunSlot(int slot)
        {
            int[] companions = Enumerable.Range(0, 11).Where(id => id != character).Take(2).ToArray();
            int companion = 0;
            BattleCombatantDefinition[] party = Enumerable.Range(0, 3).Select(index =>
            {
                int identity = index == slot ? character : companions[companion++];
                return human with
                {
                    Id = index == slot ? "party" : "party-" + index,
                    ModelId = identity,
                    Costume = index == slot ? costume : 0,
                    WeaponId = index == slot ? human.WeaponId : identity == 7 ? null : assets.WeaponDefinitions.First(weapon => weapon.CharacterId == identity).Id,
                    EquipmentIds = index == slot ? [2] : [],
                    GuardianForceCompatibility = Enumerable.Repeat((ushort)3000, 16).ToArray()
                };
            }).ToArray();
            BattleCombatantDefinition[] actors = [.. party, basis.Combatants[1] with { NativeCombatantFlags = 8 }];
            BattleDefinition definition = basis with
            {
                Combatants = actors,
                NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), assets.LoadEncounter(94))
            };
            BattleSession battle = new(definition, BattleAtbMode.TurnBased);
            WaitFor(() => battle.CanRequestGuardianForm("party", 2));
            NativeBattleGraphicsModel original = battle.NativeGraphics!.Models[slot];
            NativeBattleCameraPose humanCamera = battle.NativeCamera!.CurrentPose!;
            Assert.Equal((byte)character, original.ModelId4);
            Assert.Equal("party", battle.NativeGraphics.ActorIds[slot]);
            battle.RequestGuardianForm("party", 2);
            WaitFor(() => battle.GetGuardianForm("party")?.Phase == BattleGuardianFormPhase.Active);
            Assert.Equal(slot, battle.NativeGraphics!.GuardianFormPresentation!.ActorIndex);
            Assert.Equal((byte)77, battle.NativeGraphics.Models[slot].ModelId4);
            Assert.Equal(character, battle.Combatants.Single(actor => actor.Id == "party").ModelId);
            NativeBattleCameraPose formCamera = battle.NativeCamera!.CurrentPose!;
            foreach (BattleGuardianFormAction action in new[] { BattleGuardianFormAction.Attack, BattleGuardianFormAction.Fire })
            {
                WaitFor(() => battle.CanChoosePlayerCommand("party"));
                int damage = DamageCount();
                battle.QueueGuardianFormAction("party", "enemy", action);
                WaitFor(() => DamageCount() == damage + 1 && battle.ActiveAction is null && !battle.NativeGraphics!.GuardianFormPresentation!.IdleCameraRestorePending);
                AssertCamera(formCamera);
                Assert.Equal((byte)77, battle.NativeGraphics!.Models[slot].ModelId4);
            }
            WaitFor(() => battle.CanChoosePlayerCommand("party"));
            int beforeFinisher = DamageCount();
            battle.QueueGuardianFormAction("party", "enemy", BattleGuardianFormAction.HellFire);
            WaitFor(() => battle.NativeGraphics!.GuardianFormPresentation is { Phase: 5, Mode: GuardianFormInvocationMode.FinisherWithoutEntry });
            BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
            battle.Tick();
            restored.Tick();
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
            battle = restored;
            WaitFor(() => battle.GetGuardianForm("party") is null);
            Assert.Equal(beforeFinisher + 1, DamageCount());
            NativeBattleGraphicsModel returned = battle.NativeGraphics!.Models[slot];
            Assert.Equal(original.ModelId4, returned.ModelId4);
            Assert.Equal(original.Transform, returned.Transform);
            Assert.Equal(original.HasLinkedModelRing140, returned.HasLinkedModelRing140);
            Assert.Equal(original.LinkedModelIndex140, returned.LinkedModelIndex140);
            Assert.Equal(original.LinkedTransform, returned.LinkedTransform);
            Assert.Equal(JsonSerializer.Serialize(original.BodyTextureSlots), JsonSerializer.Serialize(returned.BodyTextureSlots));
            Assert.Equal("party", battle.NativeGraphics.ActorIds[slot]);
            AssertCamera(humanCamera);
            BattleCombatant returnedActor = battle.Combatants.Single(actor => actor.Id == "party");
            Assert.Equal(character, returnedActor.ModelId);
            Assert.Equal(costume, returnedActor.Costume);
            Assert.Equal(human.WeaponId, returnedActor.WeaponId);
            Assert.Equal(1000, returnedActor.CurrentHp);
            Console.WriteLine($"Native caster {character}, costume {costume}, physical slot {slot}: Ifrit entry, Attack, Fire, finisher, JSON next callback and human return passed.");

            int DamageCount() => battle.Events.Count(item => item.Kind == "Damage" && item.ActorId == "party");

            void AssertCamera(NativeBattleCameraPose expected)
            {
                NativeBattleCameraPose actual = battle.NativeCamera!.CurrentPose!;
                Assert.Equal(expected.Eye, actual.Eye);
                Assert.Equal(expected.Target, actual.Target);
                Assert.Equal(expected.Roll, actual.Roll);
                Assert.Equal(expected.FocalLength, actual.FocalLength);
            }

            void WaitFor(Func<bool> ready)
            {
                for (int callback = 0; callback < 1800 && !ready(); callback++)
                {
                    if (battle.NativeEntryReady && battle.ActiveAction is null)
                    {
                        battle.AdvanceTurn();
                    }
                    battle.Tick();
                }
                Assert.True(ready(), $"Caster {character}, physical slot {slot}, frame {battle.Frame} did not reach its form boundary.");
            }
        }
    }
}
