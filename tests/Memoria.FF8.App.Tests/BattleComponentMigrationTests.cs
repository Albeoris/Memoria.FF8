using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

/// <summary>Explicit migration of this task's declared frame0 component; this does not reconstruct an exploration save.</summary>
public sealed class BattleComponentMigrationTests
{
    [Fact]
    public void MigrateOnlyTheNamedInitialComponentAndValidateIndependentLoad()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_COMPONENT_MIGRATION_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_COMPONENT_MIGRATION_OUTPUT");
        Assert.SkipUnless(input is not null && output is not null, "Explicit original and separately named migration paths are required.");
        Assert.NotEqual(Path.GetFullPath(input!), Path.GetFullPath(output!));
        BattleSnapshot original = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllText(input!))!;
        Assert.Equal(0, original.Frame);
        Assert.Equal(512, original.Definition.EncounterId);
        Assert.Empty(original.QueuedActions);
        Assert.Null(original.ActiveAction);
        Assert.All(original.Combatants, actor => Assert.Empty(actor.StatusEffects));
        using GameAssets assets = new(LaunchOptions.DefaultGameDirectory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0).ToggleGuardianForceJunction(0, 0).RebuildParty().Rest();
        GameSession session = new(new GameState { Scene = SceneKind.Field, FieldId = 999, CharacterVitals = vitals, CharacterAppearance = assets.ReadNewGameCharacterAppearance(), Economy = assets.ReadNewGameEconomy(), BattleVariables = NativeBattleVariableState.CreateNewGame(), LimitProgress = assets.ReadNewGameLimitProgress(), SavedBattleFlags = assets.ReadNewGameSavedBattleFlags(), PocketStation = assets.ReadNewGamePocketStation(), ScannedModels = assets.ReadNewGameScannedModels(), ShotAvailability = assets.ReadNewGameShotAvailability(), GlobalRandom = assets.ReadNewGameGlobalRandom() });
        using GameWorld world = new(session, assets.GameDirectory);
        BattleDefinition installed = world.CreateEncounterBattle(512);
        //This declared component was constructed from the authentic New Game template, not a user's historical save.
        installed = installed with { NativeKnownMagic = assets.ReadNewGameKnownMagic() };
        Assert.Equal(original.Items, installed.NativeItemInventory!.Where(stock => stock.ItemId != 0 && stock.Count != 0).Select(stock => new BattleStock(stock.ItemId, stock.Count)));
        NativeBattleDrawSlot[] DrawSlots(BattleCombatantDefinition actor)
        {
            if (actor.NativeDrawSlots is { } slots)
            {
                return slots.ToArray();
            }
            NativeBattleDrawSlot[] entries = actor.DrawSpells.Select(spell => new NativeBattleDrawSlot((byte)spell.Id, (byte)spell.Resistance)).Concat(actor.DrawGuardianForces.Select(id => new NativeBattleDrawSlot((byte)(64 + id), 0))).ToArray();
            Assert.True(entries.Length <= 4);
            return entries.Concat(Enumerable.Repeat(new NativeBattleDrawSlot(0, 0), 4 - entries.Length)).ToArray();
        }
        BattleCombatantDefinition[] definitions = original.Definition.Combatants.Select(source =>
        {
            BattleCombatantDefinition current = installed.Combatants.Single(actor => actor.Id == source.Id);
            if (source.IsPlayer)
            {
                Assert.Equal(source.MaximumHp, current.MaximumHp);
            }
            else
            {
                //Corrected entry RNG changes a freshly rolled level; preserve this already allocated standalone actor.
                Assert.Equal(source.MaximumHp, current.NativeTemplate!.Levels[source.Level].MaximumHp);
                NativeBattleDrawSlot[] draw = world.BattleResources.ReadEnemyDrawSlots(source.ModelId, source.Level).Select(slot => slot.Id >= 64 && installed.GuardianForces.Single(gf => gf.Id == slot.Id - 64).Acquired ? slot with { Id = 0 } : slot).ToArray();
                current = current with { NativeAi = world.BattleResources.ReadEnemyAi(source.ModelId, source.Level), NativeDrawSlots = draw };
            }
            Assert.Equal(source.NativeAi?.Texts, current.NativeAi?.Texts);
            return source with { NativeRenzokukenMagicIds = current.NativeRenzokukenMagicIds, NativeDrawSlots = DrawSlots(current), NativeAi = current.NativeAi, NativeTemplate = current.NativeTemplate, NativeCombatantFlags = current.NativeCombatantFlags, NativeStatus0 = current.NativeStatus0, NativeStatus1 = current.NativeStatus1, NativeStatusResistances = current.NativeStatusResistances, NativeMagicInventory = current.NativeMagicInventory, NativeCommands = current.NativeCommands, NativeCommandSlots = current.NativeCommandSlots, NativeLimit = current.NativeLimit, NativeAttack = current.NativeAttack, NativeGrowth = current.NativeGrowth, GuardianForceCompatibility = current.GuardianForceCompatibility, NativeCharacterFlag100 = current.NativeCharacterFlag100, NativeKillCount = current.NativeKillCount, NativeDeathCount = current.NativeDeathCount };
        }).ToArray();
        BattleDefinition definition = installed with { NativeEntry = original.Definition.NativeEntry, NativeStageCameraBank = original.Definition.NativeStageCameraBank, NativeStageCameraSelectors = original.Definition.NativeStageCameraSelectors, Combatants = definitions, InitialRandom = original.Definition.InitialRandom, NativeSeed = original.Definition.NativeSeed, NativeExamTimerAtEntry = original.Definition.NativeExamTimerAtEntry };
        definition = definition with { NativeStageCameraControl = original.Definition.NativeStageCameraControl };
        BattleSnapshot migrated = original with
        {
            Definition = definition,
            NativeKnownMagic = definitions.Where(actor => actor.IsPlayer && actor.InitialActive).SelectMany(actor => actor.NativeMagicInventory ?? []).Where(stock => stock.SpellId != 0).Aggregate(definition.NativeKnownMagic!, (known, stock) => known.Add(stock.SpellId)),
            NativeScannedModels = definition.NativeScannedModels, NativeGlobalRandom = definition.NativeGlobalRandom, NativeGlobalRandomCalls = 0,
            GuardianForces = installed.GuardianForces.ToArray(),
            NativeItemInventory = installed.NativeItemInventory!.ToArray(),
            Items = [],
            Combatants = original.Combatants.Select(saved =>
            {
                BattleCombatantDefinition actor = definitions.Single(candidate => candidate.Id == saved.Id);
                Assert.Equal(actor.NativeMagicInventory?.Where(stock => stock.SpellId != 0 && stock.Count != 0).Select(stock => new BattleStock(stock.SpellId, stock.Count)) ?? actor.MagicStocks, saved.MagicStocks);
                NativeBattleParticipantSnapshot? native = saved.NativeParticipant;
                int physicalIndex = Array.FindIndex(definitions.Where(candidate => candidate.IsPlayer == actor.IsPlayer && candidate.InitialActive).ToArray(), candidate => candidate.Id == actor.Id);
                if (native is null)
                {
                    native = new(actor.InitialActive, actor.InitialHidden, actor.InitialUntargetable, actor.InitialSuspended, actor.InitialActive ? physicalIndex + (actor.IsPlayer ? 0 : 3) : -1, actor.NativeTemplate is not null && actor.InitialActive ? actor.Level : null, new uint[8], 0, null, null, false);
                }
                if (native is not null)
                {
                    uint lifecycle = (native.Active ? 1u : 0) | (native.Hidden ? 2u : 0) | (native.Untargetable ? 0x40u : 0) | (native.Suspended ? 0x80u : 0);
                    uint flags = actor.NativeCombatantFlags | (actor.NativeTemplate?.NativeCombatantFlags ?? 0) | (actor.IsPlayer ? 0x8800u : 0) | (actor.NativeCharacterFlag100 == true ? 0x100u : 0);
                    native = native with { NativeCombatantFlags = flags & ~0xC3u | lifecycle, ElementDefenses = actor.ElementDefenses.ToArray(), KillCount = actor.NativeKillCount, DeathCount = actor.NativeDeathCount, Growth = actor.NativeGrowth };
                }
                NativeMagicStock[] slots = actor.NativeMagicInventory ?? saved.MagicStocks.Select(stock => new NativeMagicStock(checked((byte)stock.Id), checked((byte)stock.Count))).Concat(Enumerable.Repeat(new NativeMagicStock(0, 0), 32 - saved.MagicStocks.Length)).ToArray();
                ushort hpFlags = saved.CurrentHp > 0 && saved.CurrentHp < actor.MaximumHp >> 1 ? (ushort)(saved.CurrentHp < actor.MaximumHp >> 2 ? 0x300 : 0x200) : (ushort)0;
                byte[] resistance = actor.NativeStatusResistances.Length == 40 ? actor.NativeStatusResistances.ToArray() : actor.NativeTemplate?.NativeStatusAllocation?.Resistances.ToArray() ?? [];
                return saved with { NativeDrawSlots = DrawSlots(actor), NativeParticipant = native, NativeMagicInventory = slots, MagicStocks = [], NativeStatus = new((ushort)((actor.NativeStatus0 & ~0x300) | hpFlags), actor.NativeStatus1, Enumerable.Repeat((short)-1111, 14).ToArray()) { Resistances = resistance } };
            }).ToArray()
        };
        BattleSession restored = BattleSession.Restore(migrated);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output!))!);
        File.WriteAllText(output!, JsonSerializer.Serialize(restored.Capture(), new JsonSerializerOptions { WriteIndented = true }));
        BattleSession independent = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllText(output!))!);
        Assert.Equal(JsonSerializer.Serialize(restored.Capture()), JsonSerializer.Serialize(independent.Capture()));
        restored.Tick();
        independent.Tick();
        Assert.Equal(JsonSerializer.Serialize(restored.Capture()), JsonSerializer.Serialize(independent.Capture()));
    }
}
