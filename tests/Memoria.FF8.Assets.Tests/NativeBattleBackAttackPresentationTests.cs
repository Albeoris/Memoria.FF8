using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleBackAttackPresentationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Installed_physical_contact_returns_the_target_from_rear_standing_and_restores_its_turn(bool rearFacing)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleEncounter formation = assets.LoadEncounter(512);
        BattleDefinition seed = NativeBattleGraphicsEntryTests.Create(game, assets, formation.Enemies.First().ModelId, formation: formation).Capture().Definition;
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
        BattleCombatantDefinition[] actors = seed.Combatants.Select(actor => actor.IsPlayer ? actor with
        {
            MaximumHp = vitals.GetMagicMaximumHitPoints(actor.ModelId),
            CurrentHp = vitals.GetMagicMaximumHitPoints(actor.ModelId),
            NativeStatusResistances = new byte[40],
            NativePlayerStatRules = assets.ReadPlayerNativeStatRules(vitals, actor.ModelId) with { NativeStatusPassiveFlags = 0x10000 },
            NativeGrowth = new(vitals.Records[actor.ModelId].HitPointBonus, vitals.Records[actor.ModelId].StatBonuses.ToImmutableArray()),
            NativeMagicInventory = vitals.Records[actor.ModelId].MagicInventory.ToArray(),
            NativeAttack = assets.ReadPlayerNativeAttackData(vitals, actor.ModelId)
        } : actor).ToArray();
        BattleSession battle = new(seed with
        {
            Combatants = actors,
            NativeFlags = rearFacing ? (ushort)0x40 : (ushort)0x80,
            NativeEntry = new(0, 0),
            NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), formation),
            NativeModuleResources = game.ReadNativeBattleModuleResources(),
            NativeStatusRules = assets.ReadNativeStatusRules(0),
            NativeMessageSpeed = game.ReadNewGameInputConfiguration().BattleMessageSpeed,
            NativeGlobalMessages = assets.ReadNativeGlobalBattleMessages(),
            NativeActionCameraRules = game.ReadNativeActionCameraRules()
        }, BattleAtbMode.TurnBased);
        Assert.Equal((byte?)(rearFacing ? 2 : 0), battle.NativeEntryAdvantage);
        // Keep the recipient turn isolated from unrelated enemy decisions after advantage initialization.
        BattleSnapshot initialized = battle.Capture();
        battle = BattleSession.Restore(initialized with
        {
            Combatants = initialized.Combatants.Select(actor => actor.Id == "enemy" ? actor with { Atb = 0, NativeAtb = actor.NativeAtb is { } gauge ? gauge with { Current = 0 } : null } : actor).ToArray()
        });
        for (int callback = 0; callback < 1200 && (!battle.NativeEntryReady || battle.NativeGraphics!.Models[0].Sequence != 1); callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        for (int callback = 0; callback < 8; callback++)
        {
            battle.Tick();
        }
        Assert.Equal((byte?)1, battle.NativeGraphics!.Models[0].Sequence);
        uint rearGraphicsFlag = game.ReadNativeBattleGraphicsRules().Project(0, 0x800000);
        Assert.Equal(4u, rearGraphicsFlag);
        Assert.Equal(rearFacing ? rearGraphicsFlag : 0u, battle.NativeGraphics.Models[0].Flags8!.Value & rearGraphicsFlag);
        //502AB0 rebuilds the retained matrix after drawing. Sample50A370's draw-time consumer, before that rebuild.
        short[] initialRotation = NativeBattleModelStatusMotion.Advance50A370(battle.NativeGraphics.Models[0]).GeometryWorldMatrix!.Rotation.ToArray();

        // Friendly targeting exercises the same recipient contract with the original player turn sequence.
        battle.QueueAttack("party-3", "party-0");
        for (int callback = 0; callback < 1200 && battle.NativeActionPackets!.ContactCount == 0; callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeActionPackets!.ContactCount > 0);
        Assert.Equal(0u, battle.Combatants.Single(actor => actor.Id == "party-0").NativeStatus1 & 0x800000u);
        Assert.Equal(0u, battle.NativeActionPackets.ReadContact(0).Primary.BattleStatuses8 & 0x800000u);
        BattleSession restored = RoundTrip(battle);
        bool completed = false;
        for (int callback = 0; callback < 1200; callback++)
        {
            battle.Tick();
            restored.Tick();
            NativeBattleGraphicsModel target = battle.NativeGraphics.Models[0];
            if (callback % 30 == 0)
            {
                Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
            }
            if (battle.ActiveAction is null && battle.QueuedActions.Count == 0 && battle.NativeActionPackets.ActionCount == 0 && target.Sequence == 1 && (target.Flags8!.Value & rearGraphicsFlag) == 0)
            {
                completed = true;
                break;
            }
        }
        Assert.True(completed);
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        NativeBattleActionMatrix finalMatrix = NativeBattleModelStatusMotion.Advance50A370(battle.NativeGraphics.Models[0]).GeometryWorldMatrix!;
        Assert.Equal(initialRotation, rearFacing ? NativeBattleGfMatrices.RotateY56D020(finalMatrix, 2048).Rotation.ToArray() : finalMatrix.Rotation.ToArray());
        Assert.Equal((byte)1, battle.NativeGraphics.Models[0].SequenceState!.Default2);
    }

    private static BattleSession RoundTrip(BattleSession battle)
    {
        return BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(battle.Capture()))!);
    }
}
