using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Memoria.FF7.Runtime;
using Memoria.FF8.Runtime;
using Memoria.FF8.Assets;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverGuestAbilityCatalogTests
{
    [Fact]
    public void Cached_catalog_reuses_choices_and_refreshes_native_inputs()
    {
        NativeCharacterVitals vitals = (NativeCharacterVitals)RuntimeHelpers.GetUninitializedObject(typeof(NativeCharacterVitals));
        CrossoverFf8BattleKit kit = Kit(3, 15);
        GameplaySaveLimitProgress limits = new(1, 0, 0, 0, ImmutableArray.CreateRange(new byte[8]), 0, 0, 0);
        BattleNativeAction[] actions = [Action(15, 0, 1), Action(15, 0, 2), Action(15, 1, 2)];
        int builds = 0;
        CrossoverGuestAbilityCatalogCache cache = new((working, currentKit, progress, crisis) =>
        {
            builds++;
            return CrossoverGuestAbilityCatalog.Read(actions, [], Binding, working, currentKit, progress, crisis);
        });
        IReadOnlyList<CrossoverGuestAbility> first = cache.Read(vitals, kit, limits, 1);
        for (int query = 0; query < 1000; query++)
        {
            Assert.Same(first, cache.Read(vitals, kit, limits, 1));
        }
        Assert.Equal(1, builds);
        Assert.Equal((byte)1, Assert.Single(first).NativeAction!.CrisisLevel);
        Assert.Equal((byte)2, Assert.Single(cache.Read(vitals, kit, limits, 2)).NativeAction!.CrisisLevel);
        limits = limits with { BlueMagicFlags = 3 };
        Assert.Equal(2, cache.Read(vitals, kit, limits, 2).Count);
        vitals = vitals with { };
        Assert.Equal(2, cache.Read(vitals, kit, limits, 2).Count);
        kit = kit with { };
        Assert.Equal(2, cache.Read(vitals, kit, limits, 2).Count);
        Assert.Empty(cache.Read(vitals, kit, limits, 0));
        Assert.Equal(6, builds);
    }

    [Fact]
    public void Blue_magic_uses_only_learned_spells_and_the_retained_crisis_record()
    {
        BattleNativeAction[] actions = [Action(15, 0, 1), Action(15, 0, 2), Action(15, 1, 2), Action(15, 2, 2)];
        GameplaySaveLimitProgress limits = new(5, 0, 0, 0, ImmutableArray.CreateRange(new byte[8]), 0, 0, 0);
        CrossoverFf8BattleKit kit = Kit(3, 15);
        NativeCharacterVitals unusedVitals = (NativeCharacterVitals)RuntimeHelpers.GetUninitializedObject(typeof(NativeCharacterVitals));
        IReadOnlyList<CrossoverGuestAbility> choices = CrossoverGuestAbilityCatalog.Read(actions, [], Binding, unusedVitals, kit, limits, 2);
        Assert.Equal(new ushort[] { 0x0F00, 0x0F02 }, choices.Select(choice => choice.Key));
        Assert.All(choices, choice =>
        {
            Assert.Equal("Blue Magic", choice.Category);
            Assert.Equal((byte)2, choice.NativeAction!.CrisisLevel);
            Assert.Null(choice.RuleUnsupportedReason);
        });
        Assert.Empty(CrossoverGuestAbilityCatalog.Read(actions, [], Binding, unusedVitals, kit, limits, 0));
    }

    [Fact]
    public void Guardian_choices_require_assignment_acquisition_and_remaining_hp()
    {
        NativeCharacterVitals vitals = (NativeCharacterVitals)RuntimeHelpers.GetUninitializedObject(typeof(NativeCharacterVitals));
        vitals = vitals with
        {
            Records = [new(0, new(1, 1), 0, new(7, 0, 0, new byte[19]), 0, 0, 0, new byte[6], [], 0, 0)],
            GuardianForces = [new(100, 1, 20, new(0, 1ul << 19), 100, [], 0, 0), new(0, 0, 20, new(0, 0), 100, [], 0, 0), new(0, 1, 0, new(0, 0), 100, [], 0, 0)],
            Definitions = new([], [], [], [new(10, 0, 0, 1, 0), new(10, 0, 0, 1, 0), new(10, 0, 0, 1, 0)], [new(0, 0, 20), .. Enumerable.Range(0, 8).Select(_ => new NativeStatModifier(0, 0, 0))])
        };
        BattleActionDefinition[] definitions = Enumerable.Range(0, 3).Select(id => new BattleActionDefinition(BattleAbility.GuardianForce, id, $"GF {id}", BattleActionEffect.Native, 0, false) { NativeAction = Action(254, (ushort)(64 + id), 0) }).ToArray();
        CrossoverFf8BattleKit kit = Kit(0, 5) with { Commands = [3] };
        CrossoverGuestAbility choice = Assert.Single(CrossoverGuestAbilityCatalog.Read([], definitions, Binding, vitals, kit, null, 0));
        Assert.Equal((ushort)0xFE40, choice.Key);
        Assert.Equal("GF", choice.Category);
        Assert.Equal("GF 0", choice.Name);
        Assert.Equal(2, choice.AbilityLevel);
        Assert.Equal(20, choice.DamagePercent);
        Assert.Empty(CrossoverGuestAbilityCatalog.Read([], definitions, Binding, vitals, kit with { Commands = [] }, null, 0));
        vitals = vitals with { Records = [vitals.Records[0] with { Junction = vitals.Records[0].Junction with { GuardianForces = 0 } }] };
        Assert.Empty(CrossoverGuestAbilityCatalog.Read([], definitions, Binding, vitals, kit, null, 0));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(11)]
    [InlineData(14)]
    [InlineData(16)]
    [InlineData(19)]
    public void Process_driven_limits_expose_the_invocation_without_inventing_a_selected_action(byte command)
    {
        NativeCharacterVitals unusedVitals = (NativeCharacterVitals)RuntimeHelpers.GetUninitializedObject(typeof(NativeCharacterVitals));
        CrossoverGuestAbility choice = Assert.Single(CrossoverGuestAbilityCatalog.Read([], [], Binding, unusedVitals, Kit(0, command), null, 4));
        Assert.Equal(command, choice.Command);
        Assert.Equal("Limit", choice.Category);
        Assert.Null(choice.NativeAction);
        Assert.False(string.IsNullOrWhiteSpace(choice.RuleUnsupportedReason));
    }

    [Fact]
    public void Limit_readiness_consumes_one_native_byte_only_when_selection_is_allowed()
    {
        CrossoverFf8BattleKit kit = Kit(3, 15);
        Ff7BattleAiActor source = new(0, 0, 0, 10, 10, 1, 100);
        NativeBattleRandomState random = NativeBattleRandomState.Initialize(7);
        NativeBattleRandomState expected = random.Draw(out byte roll);
        byte crisis = CrossoverGuestAbilityCatalog.SelectLimitLevel(kit, source, 2, false, random, out NativeBattleRandomState next);
        Assert.Equal(NativeBattleLimitRules.CrisisLevel(kit.Limit, 1, 100, 0, 0, 2, roll), crisis);
        Assert.Equal(expected.Indexes.ToArray(), next.Indexes.ToArray());
        Assert.Equal(expected.Lane, next.Lane);
        Assert.Equal((byte)0, CrossoverGuestAbilityCatalog.SelectLimitLevel(kit, source, 2, true, random, out next));
        Assert.Same(random, next);
        kit = kit with { Statuses = new(0, 0x200, []) };
        Assert.Equal((byte)0, CrossoverGuestAbilityCatalog.SelectLimitLevel(kit, source, 2, false, random, out next));
        Assert.Same(random, next);
    }

    [Fact]
    public void All_owned_magic_is_listed_without_requiring_an_effect_implementation()
    {
        string? root = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(root), "Select the installed original FFVIII resources.");
        using GameAssets game = new(root!);
        using BattleAssets assets = new(root!);
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals().DeepCopy();
        int row = vitals.FindPhysicalRow(0);
        int magicAbility = vitals.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)2) + 20;
        vitals.Records[row] = vitals.Records[row] with { Junction = vitals.Records[row].Junction with { Commands = (uint)magicAbility } };
        vitals = vitals with { MagicSystem = vitals.MagicSystem with { SharedMagicStock = true } };
        ushort[] spells = assets.NativeActions.Where(action => action.Command == 2 && action.ActionId > 0 && action.ActionId < vitals.MagicSystem.SharedBattleMasks.Length).Select(action => action.ActionId).ToArray();
        foreach (ushort spell in spells)
        {
            vitals = vitals.ChangeMagicStock(row, spell, 4, MagicOperationReason.ScriptReward, out _);
        }
        CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.Read(vitals, assets, 0);
        IReadOnlyList<CrossoverGuestAbility> choices = CrossoverGuestAbilityCatalog.Read(assets, vitals, kit, null, 0);
        Assert.Equal(spells, choices.Where(choice => choice.Category == "Magic").Select(choice => choice.Key));
        foreach (ushort spell in spells)
        {
            CrossoverGuestAbility choice = Assert.Single(choices, choice => choice.Key == spell);
            Assert.Equal("Magic", choice.Category);
            Assert.Equal(MagicCastingService.Quote(vitals, row, spell, MagicCastContext.Battle).Available, choice.Stock);
            Assert.Null(choice.RuleUnsupportedReason);
        }
        CrossoverGuestAbilityCatalogCache cache = new((working, currentKit, limits, crisis) => CrossoverGuestAbilityCatalog.Read(assets, working, currentKit, limits, crisis));
        IReadOnlyList<CrossoverGuestAbility> before = cache.Read(vitals, kit, null, 0);
        Assert.Same(before, cache.Read(vitals, kit, null, 0));
        ushort consumed = spells[0];
        NativeCharacterVitals exhausted = vitals.ChangeMagicStock(row, consumed, -4, MagicOperationReason.ScriptReward, out _);
        IReadOnlyList<CrossoverGuestAbility> after = cache.Read(exhausted, kit, null, 0);
        Assert.DoesNotContain(after, choice => choice.Command == 2 && choice.Key == consumed);
        Assert.Contains(before, choice => choice.Command == 2 && choice.Key == consumed && choice.Stock == 4);
    }

    [Fact]
    public void Native_keys_keep_magic_ids_and_separate_other_command_namespaces()
    {
        Assert.Equal((ushort)1, CrossoverGuestAbilityCatalog.Key(2, 1));
        Assert.Equal((ushort)0xFE40, CrossoverGuestAbilityCatalog.Key(254, 64));
        Assert.Equal((ushort)0x0F01, CrossoverGuestAbilityCatalog.Key(15, 1));
        Assert.Throws<NotSupportedException>(() => CrossoverGuestAbilityCatalog.Key(15, 256));
    }

    private static CrossoverFf8BattleKit Kit(byte character, byte command)
    {
        return new(0, 1, 100, 1, 0, null!, new(0, 0, []), null!, new(character, command, 0, 10, ImmutableArray.CreateRange(new byte[32])), 0, [], []) { CharacterId = character };
    }

    private static BattleNativeCommandBinding Binding(byte command)
    {
        return new(command, $"Native command {command}", 0, 0);
    }

    private static BattleNativeAction Action(byte command, ushort id, byte crisis)
    {
        return new(command, id, 19, id)
        {
            Name = $"Native spell {id}", CrisisLevel = crisis, MagicId = id, AttackType = 0, Power = 0, HitCount = 1, TargetFlags = 0, AttackFlags = 0, DamagePresentation = 0, Element = 0, ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = [], SupplementalRecord = []
        };
    }
}
