using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class ExpandedBattleEffectMatrixTests
{
    [Theory]
    [InlineData(162)]
    [InlineData(163)]
    [InlineData(164)]
    [InlineData(165)]
    public void Installed_expanded_Renzokuken_selects_its_original_weapon_finisher(ushort effect)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets game = new(directory);
        using BattleAssets assets = new(directory);
        BattleNativeAction finisher = Assert.Single(assets.NativeActions, action => action is { Command: 249, KernelSection: 5 } && action.MagicId == effect);
        (string status, string detail) = RunExpandedRenzokukenFinisher(directory, PrepareField(directory), game, assets, finisher);
        Assert.True(status == "logical", detail);
        Assert.Contains($"{finisher.Name} effect {effect}", detail);
    }

    internal static (string Status, string Detail) RunExpandedRenzokukenFinisher(string directory, GameState field, GameAssets game, BattleAssets assets, BattleNativeAction finisher)
    {
        NativeCharacterVitals vitals = Assert.IsType<NativeCharacterVitals>(field.CharacterVitals);
        foreach (BattleWeaponDefinition weapon in assets.WeaponDefinitions.Where(weapon => weapon.CharacterId == 0))
        {
            NativeCharacterVital[] records = vitals.Records.ToArray();
            records[0] = records[0] with { Weapon = checked((byte)weapon.Id) };
            NativeCharacterVitals equipped = vitals with { Records = records };
            byte flags = assets.ReadPlayerNativeStatRules(equipped, 0).WeaponRecord[2];
            if ((flags & (1 << finisher.ActionId)) != 0)
            {
                return RunExpandedRenzokuken(directory, field with { CharacterVitals = equipped }, game, assets, effect: null, finisher: finisher);
            }
        }

        return ("blocked", $"No installed Squall weapon can select {finisher.Name}.");
    }

    private static void AssertExpandedRenzokukenFinisher(BattleSession battle, BattleNativeAction finisher, BattleCombatant caster, BattleCombatant target, string[] availableEnemies, int before)
    {
        BattleEvent[] contacts = battle.Events.Skip(before).Where(entry => entry.ActorId == caster.Id && entry.Kind == "EffectContact" && entry.Ability == BattleAbility.LimitBreak).ToArray();
        Assert.All(contacts, contact => Assert.Equal(finisher.Name, contact.Message));
        string[] recipients = (finisher.TargetFlags!.Value & 0x10) == 0 ? availableEnemies : [target.Id];
        string[] expected = Enumerable.Range(0, finisher.HitCount).SelectMany(_ => recipients).ToArray();
        Assert.Equal(expected, contacts.Select(contact => contact.TargetId));
        Assert.Equal(0, battle.Capture().NativeTimedAction!.Contacts);
    }
}
