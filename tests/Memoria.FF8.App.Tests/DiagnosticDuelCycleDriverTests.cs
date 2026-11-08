using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticDuelCycleDriverTests
{
    [Fact]
    public void Ordinary_duel_rejects_a_preparation_prelude_before_prelude_execution()
    {
        DiagnosticBattleDriverRecipe driver = new(new(BattleInputKind.Action, ActorId: "party", TargetId: "enemy", Ability: BattleAbility.LimitBreak, ActionId: 11 << 16), 274)
        {
            LimitSelection = new(1, 1, 256),
            ExpectedNativeAction = 0,
            ExpectedNativeCommand = 241,
            DuelCycle = new(0)
        };
        driver.Validate();
        DiagnosticBattleRequest request = new(982, 0, 128, 0, 0)
        {
            Driver = driver,
            Prelude = new(new(BattleInputKind.Action, ActorId: "predecessor", TargetId: "enemy", Ability: BattleAbility.Attack), 4, 0, new string('a', 64), false)
        };
        Assert.Throws<NotSupportedException>(request.Validate);
    }

    [Fact]
    public void A_stripped_cycle_cannot_admit_command11_as_a_generic_single_dispatch()
    {
        DiagnosticBattleDriverRecipe recipe = new(new(BattleInputKind.Action, ActorId: "party", TargetId: "enemy", Ability: BattleAbility.LimitBreak, ActionId: 11 << 16), 274)
        {
            LimitSelection = new(1, 1, 256),
            ExpectedNativeAction = 0,
            ExpectedNativeCommand = 241
        };
        Assert.Throws<InvalidDataException>(recipe.Validate);
    }

    [Fact]
    public void The_nonterminating_raw239_basic_source_is_no_longer_preparable()
    {
        Assert.False(Memoria.FF8.App.Tests.NativeBattleLimitComponentSources.Supports(274));
        Assert.Throws<NotSupportedException>(() => Memoria.FF8.App.Tests.NativeBattleLimitComponentSources.Prepare(274, "unused", "unused"));
    }

    [Theory]
    [InlineData(239, 0)]
    [InlineData(241, 0)]
    [InlineData(11, 6)]
    public void Raw_commands_and_finisher_substitution_do_not_satisfy_the_ordinary_declaration(byte command, byte basic)
    {
        DiagnosticDuelCycleDeclaration declaration = new(basic);
        BattleInput input = new(BattleInputKind.Action, ActorId: "party", TargetId: "enemy", Ability: BattleAbility.LimitBreak, ActionId: command << 16);
        Assert.Throws<InvalidDataException>(() => declaration.Validate(input, 274, basic, 241));
    }

    [Fact]
    public void Native_button_edges_wait_for_the_real_ring_cursor_and_release_between_presses()
    {
        DiagnosticDuelCycleDriver cycle = Create();
        BattleNativeDuelState ready = State(BattleNativeDuelPhase.Ready);
        BattleNativeDuelChoice choice = new(1, 0, "Punch Rush", true, false, [16, 32]);
        cycle.BeginInput(choice, new(3, ready));
        Assert.Equal((ushort)16, cycle.Buttons(ready));
        Assert.Equal((ushort)16, cycle.Buttons(ready));
        BattleNativeDuelState first = ready with { InputCursor = 1, PreviousButtons = 16, InputRing = ready.InputRing.SetItem(1, 16) };
        Assert.Equal((ushort)0, cycle.Buttons(first));
        Assert.Equal((ushort)0, cycle.Buttons(first));
        Assert.Equal((ushort)32, cycle.Buttons(first with { PreviousButtons = 0 }));
        BattleNativeDuelState accepted = first with { Phase = BattleNativeDuelPhase.AwaitingEffect, MoveSetId = 1, InputCursor = 2, InputRing = first.InputRing.SetItem(2, 32), RemainingTimer = 239 };
        cycle.Observe(new(4, accepted), false);
        Assert.Equal((ushort)0, cycle.Buttons(accepted));
        DiagnosticDuelInputReceipt input = Assert.Single(cycle.Receipt.Inputs);
        Assert.True(NativeBattleDuelRules.Matches(input.Choice, input.After.Duel!.InputRing, input.After.Duel.InputCursor));
        Assert.Equal(240, input.Before.Duel!.RemainingTimer);
        Assert.Equal(239, input.After.Duel.RemainingTimer);
        Assert.Equal(3, input.Before.Callback);
    }

    [Fact]
    public void Timer_close_requires_the_real_255_transition_and_full_drain_after_input()
    {
        DiagnosticDuelCycleDriver cycle = Create();
        cycle.Observe(new(2, State(BattleNativeDuelPhase.Opening)), false);
        BattleNativeDuelState ready = State(BattleNativeDuelPhase.Ready);
        cycle.BeginInput(new(1, 0, "Punch Rush", true, false, [16]), new(3, ready));
        cycle.Observe(new(4, ready with { Phase = BattleNativeDuelPhase.AwaitingEffect, MoveSetId = 1, InputCursor = 1, InputRing = ready.InputRing.SetItem(1, 16) }), false);
        BattleDiagnosticEffectDispatch dispatch = new(3, 5, "party", BattleAbility.NativeCommand, 0, 274, 274, 241) { NativeActorIndex = 0, NativeHeaderIndex = 0, NativeHeaderGeneration = 1 };
        Assert.True(cycle.ObserveDispatch(dispatch, new(3, 5, 0, 1, NativeBattleActionPacketsState.Fresh())));
        cycle.Observe(new(6, ready with { MoveSetId = 1 }), false);
        Assert.NotNull(cycle.Receipt.ReadyAfterBasic);
        cycle.Observe(new(7, null), true);
        Assert.False(cycle.Completed);
        cycle.Observe(new(8, ready with { Phase = BattleNativeDuelPhase.Finisher, Selection1D28DEF = 255, RemainingTimer = 0 }), false);
        Assert.NotNull(cycle.Receipt.TimedOut);
        cycle.Observe(new(9, null), false);
        Assert.False(cycle.Completed);
        cycle.Observe(new(10, null), true);
        Assert.True(cycle.Completed);
        Assert.Equal(5, Assert.Single(cycle.Receipt.Dispatches).Callback);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Unlearned_or_finisher_choices_cannot_replace_a_basic(bool learned, bool finisher)
    {
        DiagnosticDuelCycleDriver cycle = Create();
        Assert.Throws<InvalidDataException>(() => cycle.BeginInput(new(1, 0, "Native", learned, finisher, [16]), new(3, State(BattleNativeDuelPhase.Ready))));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Actual_DTO_roundtrip_restores_optional_duel_default_and_preserves_explicit_recipe(bool duel)
    {
        JsonObject recipe = JsonNode.Parse("""{"encounter":982,"seed":0,"flags":128,"field_battle_mode":0,"elapsed_counter":0,"battle_music":5,"primary_music_channel":-1,"fixture":{"guardian_forces":[],"characters":[{"row":1,"character_id":1,"level":100,"hit_points":100,"stocks":[],"guardian_forces":[]}],"hold_enemy_commands":true},"driver":{"input":{"kind":"action","actor_id":"party-1","target_id":"enemy-6","ability":"LimitBreak","action_id":720896},"expected_effect":274,"expected_native_command":241,"expected_native_action":0,"limit_selection":{"minimum_crisis_level":1,"maximum_crisis_level":1,"maximum_selections":256},"duel_cycle":{"basic_action":0}}}""")!.AsObject();
        if (!duel)
        {
            recipe["driver"]!["input"]!["ability"] = "Magic";
            recipe["driver"]!["input"]!["action_id"] = 1;
            recipe["driver"]!["expected_effect"] = 2;
            recipe["driver"]!["expected_native_action"] = 1;
            recipe["driver"]!["expected_native_command"] = 2;
            recipe["driver"]!.AsObject().Remove("duel_cycle");
            recipe["driver"]!.AsObject().Remove("limit_selection");
        }
        JsonSerializerOptions json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, Converters = { new JsonStringEnumConverter() } };
        JsonObject canonical = FreshEntryRecipeCanonicalization.Canonicalize(recipe);
        DiagnosticBattleRequest typed = canonical.Deserialize<DiagnosticBattleRequest>(json)!;
        typed.Validate();
        Assert.Equal(duel, typed.Driver!.DuelCycle is not null);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(canonical), JsonSerializer.SerializeToElement(typed, json)));
        if (duel)
        {
            Assert.Equal((byte)0, typed.Driver.DuelCycle!.BasicAction);
        }
    }

    private static BattleNativeDuelState State(BattleNativeDuelPhase phase) => new(0, 8, 240, 240, phase) { Selection1D28DEF = 254 };
    private static DiagnosticDuelCycleDriver Create() => new(new(1, "party", "enemy", 0, 3, new(0), new("party", 1, 11, true, [1], NativeBattleRandomState.Initialize(0), NativeBattleRandomState.Initialize(1)), new(1, null)));
}
