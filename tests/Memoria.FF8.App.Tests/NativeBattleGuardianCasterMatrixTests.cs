using static Memoria.FF8.App.Tests.NativeBattleOriginalModelAssertions;
using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGuardianCasterMatrixTests(NativeBattleGuardianCasterMatrixAssets resources) : IClassFixture<NativeBattleGuardianCasterMatrixAssets>
{
    public static IEnumerable<object[]> SupportedCasters()
    {
        string? selection = Environment.GetEnvironmentVariable("MEMORIA_FF8_GUARDIAN_CASTER_CASES");
        HashSet<(int Character, int Slot)>? selected = null;
        if (!string.IsNullOrEmpty(selection))
        {
            selected = [];
            foreach (string cell in selection.Split(','))
            {
                string[] coordinates = cell.Split(':');
                if (coordinates.Length != 2 || !int.TryParse(coordinates[0], out int character) || !int.TryParse(coordinates[1], out int slot) || character is < 0 or > 10 || slot is < 0 or > 2)
                {
                    throw new InvalidDataException("Guardian caster selection requires character:slot pairs in the native ranges0..10 and0..2.");
                }
                selected.Add((character, slot));
            }
        }
        for (int character = 0; character <= 10; character++)
        {
            for (int slot = 0; slot < 3; slot++)
            {
                if (selected is null || selected.Contains((character, slot)))
                {
                    yield return [character, slot];
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(SupportedCasters))]
    public void Installed_Quezacotl_completes_for_each_native_character_and_physical_party_slot(int character, int slot)
    {
        Run(0, 116, character, slot);
    }

    [Theory]
    [MemberData(nameof(SupportedCasters))]
    public void Installed_Shiva_completes_for_each_native_character_and_physical_party_slot(int character, int slot)
    {
        Run(1, 185, character, slot);
    }

    [Theory]
    [MemberData(nameof(SupportedCasters))]
    public void Installed_Ifrit_completes_for_each_native_character_and_physical_party_slot(int character, int slot)
    {
        Run(2, 201, character, slot);
    }

    [Theory]
    [MemberData(nameof(SupportedCasters))]
    public void Installed_Siren_completes_for_each_native_character_and_physical_party_slot(int character, int slot)
    {
        Run(3, 95, character, slot);
    }

    [Theory]
    [MemberData(nameof(SupportedCasters))]
    public void Installed_Brothers_completes_for_each_native_character_and_physical_party_slot(int character, int slot)
    {
        Run(4, 205, character, slot);
    }

    [Theory]
    [MemberData(nameof(SupportedCasters))]
    public void Installed_Diablos_completes_for_each_native_character_and_physical_party_slot(int character, int slot)
    {
        Run(5, 325, character, slot);
    }

    [Theory]
    [MemberData(nameof(SupportedCasters))]
    public void Installed_Carbuncle_completes_for_each_native_character_and_physical_party_slot(int character, int slot)
    {
        Run(6, 278, character, slot);
    }

    [Theory]
    [MemberData(nameof(SupportedCasters))]
    public void Installed_Leviathan_completes_for_each_native_character_and_physical_party_slot(int character, int slot)
    {
        Run(7, 6, character, slot);
    }

    [Theory]
    [MemberData(nameof(SupportedCasters))]
    public void Installed_Pandemona_completes_for_each_native_character_and_physical_party_slot(int character, int slot)
    {
        Run(8, 291, character, slot);
    }

    [Theory]
    [MemberData(nameof(SupportedCasters))]
    public void Installed_Cerberus_completes_for_each_native_character_and_physical_party_slot(int character, int slot)
    {
        Run(9, 203, character, slot);
    }

    [Theory]
    [MemberData(nameof(SupportedCasters))]
    public void Installed_Alexander_completes_for_each_native_character_and_physical_party_slot(int character, int slot)
    {
        Run(10, 204, character, slot);
    }

    [Theory]
    [MemberData(nameof(SupportedCasters))]
    public void Installed_Doomtrain_completes_for_each_native_character_and_physical_party_slot(int character, int slot)
    {
        Run(11, 191, character, slot);
    }

    [Theory]
    [MemberData(nameof(SupportedCasters))]
    public void Installed_Bahamut_completes_for_each_native_character_and_physical_party_slot(int character, int slot)
    {
        Run(12, 202, character, slot);
    }

    [Theory]
    [MemberData(nameof(SupportedCasters))]
    public void Installed_Cactuar_completes_for_each_native_character_and_physical_party_slot(int character, int slot)
    {
        Run(13, 199, character, slot);
    }

    [Theory]
    [MemberData(nameof(SupportedCasters))]
    public void Installed_Tonberry_completes_for_each_native_character_and_physical_party_slot(int character, int slot)
    {
        Run(14, 90, character, slot);
    }

    [Theory]
    [MemberData(nameof(SupportedCasters))]
    public void Installed_Eden_completes_for_each_native_character_and_physical_party_slot(int character, int slot)
    {
        Run(15, 206, character, slot);
    }

    [Fact]
    public void Installed_Bahamut_completes_against_all_four_original_enemy_slots()
    {
        Run(12, 202, 0, 0, 4);
    }

    private void Run(int guardian, ushort effect, int character, int slot, int enemyCount = 1)
    {
        GameAssets game = resources.Game;
        BattleAssets assets = resources.Battle;
        BattleDefinition source = resources.Definition(guardian, effect);
        int[] spectators = new[] { 0, 3, 5 }.Where(model => model != character).Take(2).ToArray();
        List<BattleCombatantDefinition> actors = [];
        int next = 0;
        for (int index = 0; index < 3; index++)
        {
            int model = index == slot ? character : spectators[next++];
            int? weapon = model == 7 ? null : assets.WeaponDefinitions.First(item => item.CharacterId == model).Id;
            actors.Add(source.Combatants[0] with { Id = index == slot ? "caster" : "spectator-" + index, Name = "Character " + model, ModelId = model, WeaponId = weapon, Speed = 80, EquipmentIds = index == slot ? [guardian] : [] });
        }
        BattleEncounter formation = assets.LoadEncounter(94);
        BattleCombatantDefinition enemy = source.Combatants[1] with { NativeCombatantFlags = 8, MaximumHp = 60000, CurrentHp = 60000 };
        actors.Add(enemy);
        foreach (BattleEnemyPlacement placement in formation.Slots.Where(placement => placement.Slot != enemy.SceneSlot && placement.NativePosition is not null).Take(enemyCount - 1))
        {
            actors.Add(enemy with { Id = $"enemy-{placement.Slot}", SceneSlot = placement.Slot });
        }
        Assert.Equal(enemyCount, actors.Count(actor => !actor.IsPlayer));
        NativeBattleGraphicsDefinition graphicsResources = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), formation);
        BattleDefinition definition = source with { Combatants = actors.ToArray() };
        BattleSession battle = NativeBattleTypedFixture.CreateBattle(definition with { NativeClockRules = definition.NativeClockRules ?? assets.ReadNativeClockRules(), NativeBattleSpeed = definition.NativeBattleSpeed ?? game.ReadNewGameInputConfiguration().NativeBattleSpeedByte }, BattleAtbMode.TurnBased, graphicsResources);
        bool selected = false;
        bool restoredLive = false;
        bool completed = false;
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF8_GUARDIAN_CASTER_EVIDENCE");
        try
        {
            WaitFor(() => battle.CanChoosePlayerCommand("caster"), 600);
            Assert.Equal(slot, FindOriginalModelSlot(battle.Presentation!.Graphics, "caster"));
            ushort[] visibility = ReadOriginalModelCells(battle.Presentation!.Graphics).Select(model => (ushort)(model.Flags0 & 0xC)).ToArray();
            string?[] ids = Enumerable.Range(0, NativeBattleGraphicsModelAddress.NativeRowCount).Select(index => ReadOriginalModelActorId(battle.Presentation!.Graphics, index)).ToArray();
            BattleActionDefinition action = definition.ActionDefinitions.Single(item => item.Ability == BattleAbility.GuardianForce && item.Id == guardian);
            string target = action.Allies ? "caster" : "enemy";
            battle.QueueAction("caster", target, BattleAbility.GuardianForce, guardian);
            for (int callback = 0; callback < 4096; callback++)
            {
                if (battle.ActiveAction is null)
                {
                    battle.AdvanceTurn();
                }
                battle.Tick();
                NativeBattleGraphicsState graphics = battle.Presentation!.Graphics;
                if (graphics.OriginalEffect is { ReturnedDescriptor: > 0 } original && original.Binding.Effect == effect)
                {
                    NativeBattleEffectParameters parameters = Assert.IsType<NativeBattleEffectParameters>(original.Parameters);
                    if (!selected && enemyCount > 1)
                    {
                        NativeBattleContactPacket[] prepared = battle.Presentation!.NativeActionPackets.ReadHeaderContacts(parameters.Header).ToArray();
                        Assert.Equal(enemyCount, prepared.Length);
                        Assert.Equal(enemyCount, prepared.Select(contact => contact.Primary.Recipient).Distinct().Count());
                    }
                    selected = true;
                    Assert.Equal((byte)slot, ReadOriginalModelOperand(battle.Presentation!.NativeActionPackets.ModelAliases, parameters.Source));
                    if (!restoredLive && callback > 40)
                    {
                        RoundTripNext();
                        restoredLive = true;
                    }
                }
                if (selected && NativeBattleTargetGuardianCastTests.Released(battle.Capture()))
                {
                    completed = true;
                    break;
                }
            }
            Assert.True(selected);
            Assert.True(restoredLive);
            Assert.True(completed, "Guardian action did not release its native presentation owners.");
            Assert.Single(battle.Events, item => item.Kind == "GuardianForceCharging" && item.ActorId == "caster");
            Assert.Single(battle.Events, item => item.Kind == "GuardianForceReady" && item.ActorId == "caster");
            Assert.Contains(battle.Events, item => item.Kind == "Damage" && item.ActorId == "caster" && item.TargetId == target);
            Assert.Equal(ids, Enumerable.Range(0, NativeBattleGraphicsModelAddress.NativeRowCount).Select(index => ReadOriginalModelActorId(battle.Presentation!.Graphics, index)));
            Assert.Equal(visibility, ReadOriginalModelCells(battle.Presentation!.Graphics).Select(model => (ushort)(model.Flags0 & 0xC)));
            for (int index = 0; index < 3; index++)
            {
                Assert.Equal((byte)actors[index].ModelId, ReadOriginalModelCell(battle.Presentation!.Graphics, index).ModelId4);
            }
            RoundTripNext();
            Record("passed");
        }
        catch
        {
            if (root is not null)
            {
                Directory.CreateDirectory(root);
                string failure = Path.Combine(root, "classic-first-failed.battle.json");
                if (!File.Exists(failure))
                {
                    File.WriteAllText(failure, JsonSerializer.Serialize(battle.Capture()));
                    File.WriteAllText(Path.Combine(root, "classic-first-failed-case.json"), JsonSerializer.Serialize(new { guardian, effect, character, slot }));
                }
            }
            throw;
        }

        void RoundTripNext()
        {
            BattleSession restored = battle.ForkForContinuation();
            battle.Tick();
            restored.Tick();

            BattleSnapshot observed = battle.CaptureForContinuation();
            BattleSnapshot continued = restored.CaptureForContinuation();
            Assert.Equal(observed.Frame, continued.Frame);
            Assert.Equal(observed.ReadFixturePresentation().NativeGraphics!.Callback, continued.ReadFixturePresentation().NativeGraphics!.Callback);
            Assert.Equal(observed.ReadFixturePresentation().NativeGraphics!.OriginalEffect?.ReturnedDescriptor, continued.ReadFixturePresentation().NativeGraphics!.OriginalEffect?.ReturnedDescriptor);
            Assert.Equivalent(observed.Combatants, continued.Combatants, strict: true);
            Assert.Equal(observed.Random!.Lane, continued.Random!.Lane);
            Assert.Equal(observed.Random.Indexes.ToArray(), continued.Random.Indexes.ToArray());
            Assert.Equal(battle.Events, restored.Events);
            Assert.Equal(observed.ReadFixturePresentation().NativeActionPackets!.PublishedHeaders().ToArray(), continued.ReadFixturePresentation().NativeActionPackets!.PublishedHeaders().ToArray());
            Assert.Equivalent(observed.ReadFixturePresentation().NativeActionPackets!.ExportRetained(), continued.ReadFixturePresentation().NativeActionPackets!.ExportRetained(), strict: true);
            battle = restored;
        }

        void Record(string status)
        {
            if (root is not null)
            {
                Directory.CreateDirectory(root);
                File.AppendAllText(Path.Combine(root, "classic-results.jsonl"), JsonSerializer.Serialize(new { guardian, effect, character, slot, battle.Frame, NativeCallback = battle.Presentation!.Graphics.Callback, Status = status }) + Environment.NewLine);
            }
        }

        void WaitFor(Func<bool> ready, int maximum)
        {
            for (int callback = 0; callback < maximum && !ready(); callback++)
            {
                if (battle.ActiveAction is null)
                {
                    battle.AdvanceTurn();
                }
                battle.Tick();
            }
            Assert.True(ready());
        }
    }
}

public sealed class NativeBattleGuardianCasterMatrixAssets : IDisposable
{
    private GameAssets? game;
    private BattleAssets? battle;
    private readonly Dictionary<int, BattleDefinition> definitions = [];
    public GameAssets Game => game ??= new(TestResources.GameDirectory);
    public BattleAssets Battle => battle ??= new(Game.GameDirectory);

    public BattleDefinition Definition(int guardian, ushort effect)
    {
        if (!definitions.TryGetValue(guardian, out BattleDefinition? result))
        {
            result = NativeBattleTestDefinitions.CreateTargetGuardianDefinition(Game, Battle, guardian, effect, out _);
            definitions.Add(guardian, result);
        }
        return result;
    }

    public void Dispose()
    {
        battle?.Dispose();
        game?.Dispose();
    }
}
