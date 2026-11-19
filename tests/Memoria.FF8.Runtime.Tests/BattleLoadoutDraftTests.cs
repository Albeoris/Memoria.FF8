using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class BattleLoadoutDraftTests
{
    private static BattleLoadoutDefaults[] Defaults => [new(0, 0, [BattleLimitFamily.Renzokuken]), new(4, 12, [BattleLimitFamily.Combine, BattleLimitFamily.AngelWing]), new(8, 33, [BattleLimitFamily.Desperado])];

    [Fact]
    public void Explicit_empty_survives_serialization_and_reset_restores_native_defaults()
    {
        BattleLoadoutDraft draft = new(null, Defaults);
        draft.RemoveAllLimits(0);
        BattleLoadoutOverrides saved = JsonSerializer.Deserialize<BattleLoadoutOverrides>(JsonSerializer.Serialize(draft.Overrides))!;
        Assert.Empty(saved.Get(0)!.LimitFamilies!);
        Assert.Null(saved.Get(4));
        BattleLoadoutDraft loaded = new(saved, Defaults);
        Assert.Empty(loaded.ResolveFamilies(0));
        Assert.Equal([BattleLimitFamily.Combine, BattleLimitFamily.AngelWing], loaded.ResolveFamilies(4));
        loaded.ResetCharacter(0);
        Assert.Equal([BattleLimitFamily.Renzokuken], loaded.ResolveFamilies(0));
    }

    [Fact]
    public void Weapons_and_ordered_families_change_independently_and_copies_are_detached()
    {
        BattleLoadoutDraft draft = new(null, Defaults);
        draft.AssignWeapon(0, 33);
        draft.AddLimit(0, BattleLimitFamily.Combine);
        draft.AddLimit(0, BattleLimitFamily.Duel);
        draft.AddLimit(0, BattleLimitFamily.Combine);
        draft.ReorderLimit(0, BattleLimitFamily.Duel, -1);
        Assert.Equal([BattleLimitFamily.Renzokuken, BattleLimitFamily.Duel, BattleLimitFamily.Combine], draft.ResolveFamilies(0));
        Assert.Equal(33, draft.ResolveWeapon(0));
        BattleLoadoutOverrides copy = draft.Overrides;
        copy.Characters[0].LimitFamilies![0] = BattleLimitFamily.Shot;
        Assert.Equal(BattleLimitFamily.Renzokuken, draft.ResolveFamilies(0)[0]);
        draft.RemoveAllLimits(0);
        Assert.Equal(33, draft.ResolveWeapon(0));
    }

    [Fact]
    public void Exchanges_preserve_effective_values_empty_sets_order_and_duplicate_rules()
    {
        BattleLoadoutDraft draft = new(null, Defaults);
        draft.SwapWeapons(0, 8);
        Assert.Equal(33, draft.ResolveWeapon(0));
        Assert.Equal(0, draft.ResolveWeapon(8));
        draft.SwapWeapons(0, 8);
        Assert.Equal(0, draft.ResolveWeapon(0));
        Assert.Equal(33, draft.ResolveWeapon(8));
        draft.AddLimit(0, BattleLimitFamily.Combine);
        draft.MoveLimit(4, 0, BattleLimitFamily.Combine);
        Assert.Equal([BattleLimitFamily.Renzokuken, BattleLimitFamily.Combine], draft.ResolveFamilies(0));
        Assert.Equal([BattleLimitFamily.AngelWing], draft.ResolveFamilies(4));
        draft.RemoveAllLimits(8);
        draft.SwapLimitSets(4, 8);
        Assert.Empty(draft.ResolveFamilies(4));
        Assert.Equal([BattleLimitFamily.AngelWing], draft.ResolveFamilies(8));
        string before = JsonSerializer.Serialize(draft.Overrides);
        draft.MoveLimit(0, 0, BattleLimitFamily.Renzokuken);
        draft.SwapWeapons(0, 0);
        draft.SwapLimitSets(0, 0);
        Assert.Equal(before, JsonSerializer.Serialize(draft.Overrides));
        Assert.Throws<InvalidOperationException>(() => draft.SwapWeapons(0, 10));
        Assert.Equal(before, JsonSerializer.Serialize(draft.Overrides));
    }

    [Fact]
    public void Adding_existing_or_removing_absent_family_keeps_native_default_projection_unmodified()
    {
        BattleLoadoutDraft draft = new(null, Defaults);
        draft.AddLimit(0, BattleLimitFamily.Renzokuken);
        draft.RemoveLimit(0, BattleLimitFamily.Shot);
        Assert.Empty(draft.ChangedCharacters);
        Assert.Empty(draft.Overrides.Characters);
    }

    [Fact]
    public void Invalid_families_and_weapons_fail_before_changing_the_draft()
    {
        BattleLoadoutDraft draft = new(null, Defaults);
        string before = JsonSerializer.Serialize(draft.Overrides);
        Assert.Throws<InvalidDataException>(() => draft.AssignWeapon(0, -1));
        Assert.Throws<InvalidDataException>(() => draft.SetLimits(0, [BattleLimitFamily.Duel, BattleLimitFamily.Duel]));
        Assert.Equal(before, JsonSerializer.Serialize(draft.Overrides));
    }

    [Fact]
    public void Apply_is_atomic_rejects_foreign_sessions_stale_resources_and_reused_drafts()
    {
        GameSession session = Session();
        BattleLoadoutDraft draft = session.CreateBattleLoadoutDraft(Defaults, "resources-1");
        draft.AssignWeapon(0, 33);
        draft.RemoveAllLimits(4);
        GameState before = session.State;
        Assert.Throws<InvalidOperationException>(() => session.ApplyBattleLoadoutDraft(draft, "resources-2"));
        Assert.Same(before, session.State);
        Assert.Throws<InvalidOperationException>(() => Session().ApplyBattleLoadoutDraft(draft, "resources-1"));
        Assert.Throws<InvalidDataException>(() => session.ApplyBattleLoadoutDraft(draft, "resources-1", (character, _) =>
        {
            if (character == 4)
            {
                throw new InvalidDataException("Unavailable mapping for character 4.");
            }
        }));
        Assert.Same(before, session.State);
        session.ApplyBattleLoadoutDraft(draft, "resources-1");
        Assert.Equal(33, session.State.BattleLoadouts!.Get(0)!.WeaponId);
        Assert.Empty(session.State.BattleLoadouts.Get(4)!.LimitFamilies!);
        Assert.Same(before.CharacterVitals, session.State.CharacterVitals);
        Assert.Same(before.CrtRandom, session.State.CrtRandom);
        Assert.Throws<InvalidOperationException>(() => session.ApplyBattleLoadoutDraft(draft, "resources-1"));
    }

    [Fact]
    public void Applying_an_available_character_keeps_dormant_roster_assignments()
    {
        GameSession initial = Session();
        BattleLoadoutOverrides stored = new() { Characters = ImmutableSortedDictionary<int, BattleCharacterLoadoutOverride>.Empty.Add(10, new() { LimitFamilies = [] }) };
        GameSession session = new(initial.State with { BattleLoadouts = stored });
        BattleLoadoutDraft draft = session.CreateBattleLoadoutDraft(Defaults);
        Assert.DoesNotContain(10, draft.Characters);
        draft.AssignWeapon(0, 33);
        session.ApplyBattleLoadoutDraft(draft);
        Assert.Empty(session.State.BattleLoadouts!.Get(10)!.LimitFamilies!);
        Assert.Null(session.State.BattleLoadouts.Get(10)!.WeaponId);
    }

    [Fact]
    public void Reloading_identical_state_invalidates_the_previous_session_draft()
    {
        GameSession session = Session();
        BattleLoadoutDraft draft = session.CreateBattleLoadoutDraft(Defaults);
        draft.AssignWeapon(0, 33);
        session.Restore(session.State);
        Assert.Throws<InvalidOperationException>(() => session.ApplyBattleLoadoutDraft(draft));
        Assert.Null(session.State.BattleLoadouts);
    }

    [Fact]
    public void Dream_bindings_use_logical_identity_instead_of_the_reused_physical_row()
    {
        BattleLoadoutDraft draft = new(null, Defaults);
        draft.AssignWeapon(0, 12);
        draft.AssignWeapon(8, 0);
        NativeCharacterVitals vitals = MagicSystemFixture.Create();
        NativeCharacterVitals dream = vitals.PrepareJunction(1, [0, 1, 2], 7).Vitals;
        Assert.Equal(8, dream.Records[0].CharacterId);
        Assert.Equal(0, draft.ResolveWeapon(dream.Records[0].CharacterId));
        NativeCharacterVitals awake = dream.PrepareJunction(0, [8, 9, 10], 7).Vitals;
        Assert.Equal(0, awake.Records[0].CharacterId);
        Assert.Equal(12, draft.ResolveWeapon(awake.Records[0].CharacterId));
    }

    private static GameSession Session()
    {
        return new(GameSession.CreateColdProcess().State with { Scene = SceneKind.Field, FieldId = 74, CharacterVitals = MagicSystemFixture.Create() });
    }
}
