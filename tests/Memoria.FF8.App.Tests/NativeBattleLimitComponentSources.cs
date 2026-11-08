using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class NativeBattleLimitComponentSources
{
    internal static bool Supports(ushort effect)
    {
        return effect is 84 or 86 or 87 or 88 or 89 or 91 or 92 or 93 or 94 or 96 or 112 or 162 or 163 or 164 or 165 or 187 or 200 or 212 or 215 or 216 or 228 or 261 or 262 or 263 or 327 or 328 or 329 or 330;
    }

    internal static BattleSnapshot Prepare(ushort effect, string gameDirectory, string outputDirectory)
    {
        if (effect == 274)
        {
            throw new NotSupportedException("Duel274 requires the genuine ordinary command11 input and timer-closure recipe; raw239/basic sources cannot complete it.");
        }
        if (effect is 91 or 92 or 93 or 94 or 96 or 187 or 327 or 328 or 329 or 330)
        {
            return PrepareGuardianComponent(effect, gameDirectory, outputDirectory);
        }
        using GameAssets game = new(gameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        if (effect is 162 or 163 or 164 or 165)
        {
            return PrepareFinisher(game, assets, effect);
        }
        if (effect is 112 or 212 or 215 or 216)
        {
            return PrepareDuel(game, assets, effect);
        }
        int character = effect switch
        {
            84 or 86 or 87 or 88 or 89 => 4,
            200 => 6,
            228 => 7,
            261 => 8,
            262 => 9,
            263 => 10,
            _ => throw new NotSupportedException($"No installed limit component producer is selected for effect {effect}.")
        };
        BattleDefinition definition = NativeBattleGuestLimitCastTests.CreateDefinition(game, assets, character, effect);
        byte command = definition.Combatants[0].NativeLimit!.Command;
        BattleNativeAction action = definition.NativeActions.Single(row => row.Command == command && (row.MagicId & 0x1FF) == effect);
        if (character == 4)
        {
            definition = definition with
            {
                LimitProgress = effect == 84 ? definition.LimitProgress : definition.LimitProgress! with { AngeloFlags = checked((byte)(1 << (action.ActionId + 4))) },
                NativeSavedBattleFlags = effect == 84 ? (byte)(definition.NativeSavedBattleFlags!.Value | 0x20) : (byte)(definition.NativeSavedBattleFlags!.Value & ~0x30),
                NativeSavedBattleFlagsKnownMask = (byte)(definition.NativeSavedBattleFlagsKnownMask | 0x30)
            };
        }
        BattleSession battle = Ready(game, definition);
        battle = BattleSession.Restore(battle.Capture() with { NativeLimits = [new("party", 4)] });
        Attach(game, battle);
        battle.QueueNativePlayerCommand("party", "enemy", command, action.ActionId);
        return AwaitQueued(battle, effect);
    }

    private static BattleSnapshot PrepareDuel(GameAssets game, BattleAssets assets, ushort effect)
    {
        BattleNativeAction action = assets.NativeActions.First(row => row.Command == 239 && row.MagicId == effect);
        BattleDefinition source = NativeBattleGuestLimitCastTests.CreateDefinition(game, assets, 1, effect);
        BattleCombatantDefinition actor = source.Combatants[0];
        actor = actor with { NativeCommands = actor.NativeCommands.Add(new(239, action.Name ?? "Duel", action.TargetFlags!.Value, checked((byte)action.ActionId))) };
        BattleSession battle = Ready(game, source with { NativeVictoryFile = assets.ReadNativeBattleFile(766), Combatants = [actor, source.Combatants[1]] });
        battle.QueueNativePlayerCommand("party", "enemy", 239, action.ActionId);
        return AwaitQueued(battle, effect);
    }

    private static BattleSnapshot PrepareFinisher(GameAssets game, BattleAssets assets, ushort effect)
    {
        BattleNativeAction finisher = assets.NativeActions.Single(row => row.Command == 249 && row.MagicId == effect);
        byte finisherIndex = checked((byte)finisher.ActionId);
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
        byte weapon = Enumerable.Range(0, 7).Select(value => checked((byte)value)).First(value =>
        {
            NativeCharacterVitals equipped = vitals with { Records = vitals.Records.Select((record, index) => index == 0 ? record with { Weapon = value } : record).ToArray() };
            return (assets.ReadPlayerNativeStatRules(equipped, 0).WeaponRecord[2] & (1 << finisherIndex)) != 0;
        });
        vitals = vitals with { Records = vitals.Records.Select((record, index) => index == 0 ? record with { Weapon = weapon } : record).ToArray() };
        BattleNativePlayerStatRules rules = assets.ReadPlayerNativeStatRules(vitals, 0);
        NativeCharacterVital character = vitals.Records[0];
        BattleNativeCharacterGrowth growth = new(character.HitPointBonus, character.StatBonuses.ToImmutableArray());
        BattleNativePlayerStatistics statistics = rules.Project(character.MagicInventory, growth);
        BattleDefinition source = NativeBattleGuestLimitCastTests.CreateDefinition(game, assets, 0, effect);
        BattleCombatantDefinition actor = source.Combatants[0] with { WeaponId = weapon, NativePlayerStatRules = rules, NativeAttack = assets.ReadPlayerNativeAttackData(vitals, 0), NativeGrowth = growth, Attack = statistics.Strength, Defense = statistics.Vitality, Magic = statistics.Magic, Spirit = statistics.Spirit, Speed = statistics.Speed, Luck = statistics.Luck, Hit = statistics.Hit, Evasion = statistics.Evasion };
        BattleCombatantDefinition target = source.Combatants[1] with { NativeRenzokukenMagicIds = assets.ReadNativeRenzokukenMagicIds(61) };
        BattleDefinition definition = source with { Combatants = [actor, target], NativeRenzokukenResources = game.ReadNativeRenzokukenResources(), NativeGraphics = assets.ReadNativeGraphicsDefinition([actor, target], game.ReadNativeBattleGraphicsRules(), assets.LoadEncounter(94)), NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([160, effect]) };
        BattleSession battle = Ready(game, definition);
        const byte crisis = 1;
        NativeBattleRandomState random = Enumerable.Range(0, 256).Select(seed => NativeBattleRandomState.Initialize((byte)seed)).First(candidate =>
        {
            candidate = candidate.Draw(out byte resource).Draw(out byte finish).Draw(out byte selection);
            return target.NativeRenzokukenMagicIds[crisis + 1 + resource % 2] == 160 && NativeBattleRenzokukenRules.HasFinisher(crisis, finish) && NativeBattleRenzokukenRules.Finisher(rules.WeaponRecord[2], selection) == finisherIndex;
        });
        battle = BattleSession.Restore(battle.Capture() with { NativeLimits = [new("party", crisis)], Random = random });
        Attach(game, battle);
        battle.QueueNativePlayerCommand("party", "enemy", 5, 0);
        return AwaitQueued(battle, effect);
    }

    private static BattleSnapshot PrepareGuardianComponent(ushort effect, string gameDirectory, string outputDirectory)
    {
        ushort action = effect switch
        {
            91 => 11,
            92 => 14,
            93 => 12,
            94 => 13,
            96 => 6,
            187 => 0,
            327 => 10,
            328 => 9,
            329 => 7,
            330 => 8,
            _ => throw new NotSupportedException("The selected effect lacks an original guardian row.")
        };
        byte command = effect >= 327 ? (byte)245 : (byte)240;
        byte target = effect == 94 ? (byte)1 : effect is 92 or 93 or 96 ? (byte)0 : (byte)3;
        NativeEffectFixtureRequest request = new("native-limit-component-" + effect, effect, command, action, target, 400, 120)
        {
            PartyModelId = effect is 91 or 92 or 93 or 94 ? 4 : 0,
            HealingContact = effect is 93 or 94 or 96,
            RevivalContact = effect == 94
        };
        // These are explicit installed-row post-formula components, not support or ordinary command admission.
        string fixture = Path.GetFullPath(Path.Combine(outputDirectory, "limit-component-fixture-" + effect));
        NativeEffectFixtureExportResult exported = NativeBattleEffectFixtureExporter.Export(request, gameDirectory, fixture);
        try
        {
            if (exported.Status != "fixture-only" || exported.Boundary != "queued-action104")
            {
                throw new InvalidDataException($"The original guardian component was not prepared: {exported.Status}: {exported.FirstBlocker}");
            }
            BattleSnapshot snapshot = NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(fixture, "queued-action104.battle.json"));
            BattleSession restored = BattleSession.Restore(snapshot);
            return AwaitQueued(restored, effect);
        }
        finally
        {
            NativeBattleOrdinaryMagicFixtureFlagTests.DeleteFixtureSnapshots(fixture, exported);
        }
    }

    private static BattleSession Ready(GameAssets game, BattleDefinition definition)
    {
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        Attach(game, battle);
        for (int callback = 0; callback < 400 && !battle.CanChoosePlayerCommand("party"); callback++)
        {
            battle.Tick();
        }
        if (!battle.NativeEntryReady || !battle.CanChoosePlayerCommand("party"))
        {
            throw new InvalidDataException("The installed limit actor did not reach its original command boundary.");
        }
        return battle;
    }

    private static void Attach(GameAssets game, BattleSession battle)
    {
        battle.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
    }

    internal static BattleSnapshot AwaitQueued(BattleSession battle, ushort effect)
    {
        for (int callback = 0; callback < 4096; callback++)
        {
            BattleSnapshot snapshot = battle.Capture();
            NativeBattleGraphicsState graphics = snapshot.NativeGraphics ?? throw new InvalidDataException("The installed limit producer lost its native graphics owner.");
            int[] descriptors = graphics.Descriptors.Queue.Where(index => graphics.Descriptors.Packets[index].Command == 104).ToArray();
            if (graphics.OriginalEffect is { } original && original.Binding.Effect == effect)
            {
                if (original.ReturnedDescriptor is not null)
                {
                    throw new InvalidDataException("The selected limit effect initialized before its source was retained.");
                }
                if (descriptors.Length <= 1)
                {
                    _ = GameWorld.ValidateEffectComponentSource(snapshot, battle, effect);
                    return snapshot;
                }
            }
            foreach (int descriptor in descriptors)
            {
                uint argument = snapshot.NativeGraphics.Descriptors.Packets[descriptor].Argument;
                uint offset = checked(argument - 0x1D280C4u);
                if (offset % 20 != 0 || offset / 20 >= snapshot.NativeActionPackets!.ActionCount || offset / 20 >= snapshot.NativeActionPackets.Headers.Length)
                {
                    throw new InvalidDataException("The limit producer queued an invalid native action header pointer.");
                }
                NativeBattleActionHeaderSlot header = snapshot.NativeActionPackets.Headers[checked((int)(offset / 20))];
                if (NativeBattleActionGraphics.Word(header, 6) == effect && descriptors.Length == 1)
                {
                    return snapshot;
                }
            }
            battle.Tick();
        }
        throw new InvalidDataException($"The installed limit producer did not queue effect {effect} within its bounded preparation.");
    }
}
