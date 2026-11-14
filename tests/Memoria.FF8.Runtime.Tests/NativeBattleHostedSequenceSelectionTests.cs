using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedSequenceSelectionTests
{
    private static readonly NativeBattleGraphicsRoster Roster = new([
        new("original-party", true), null, null, new("original-enemy", false), new("other-enemy", false), null, new("last-native", false),
        new("first-extension", true), null, null, null, null, null, null, new("last-party", true), new("first-extra-enemy", false),
        null, null, null, null, null, null, null, new("unloaded-extra-enemy", false), new("last-enemy", false)]);

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(6)]
    public void Geometry92_native_literal_targets_keep_the_retained_source(byte selector)
    {
        NativeBattleActionGeometry retained = new(14, 0) { PresentationSourceTargets = Rows(15, 24) };

        var result = NativeBattleSequenceSelections.GeometryArguments92(7, selector, retained, Roster);

        Assert.Equal((byte)14, result.Actor);
        Assert.Equal(Rows(selector), result.Targets);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(255)]
    public void Geometry92_other_operands_use_current_row_and_full_source_targets(byte selector)
    {
        NativeBattleActionGeometry retained = new(14, 1 << 3) { PresentationSourceTargets = Rows(3, 15, 24) };

        var result = NativeBattleSequenceSelections.GeometryArguments92(7, selector, retained, Roster);

        Assert.Equal((byte)7, result.Actor);
        Assert.Equal(Rows(3, 15, 24), result.Targets);
    }

    [Fact]
    public void Geometry92_projects_legacy_native_selection_and_preserves_raw_native_operand()
    {
        NativeBattleActionGeometry retained = new(0, 0x4010);

        var hosted = NativeBattleSequenceSelections.GeometryArguments92(14, 7, retained, Roster);
        var native = NativeBattleSequenceSelections.GeometryArguments92(6, 7, retained);

        Assert.Equal((byte)14, hosted.Actor);
        Assert.Equal(Rows(4), hosted.Targets);
        Assert.Equal((byte)6, native.Actor);
        Assert.Equal((ushort)0x4010, native.TargetMask);
    }

    [Theory]
    [InlineData(7, 0x4010, false)]
    [InlineData(4, 0x0010, true)]
    public void Session_geometry92_preserves_mask_flags_only_for_the_retained_selection(byte selector, ushort expectedMask, bool expectedSelf)
    {
        BattleDefinition definition = new()
        {
            Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }]
        };
        BattleSession session = new(definition, BattleAtbMode.TurnBased);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeEffectMemory))!.SetValue(session, NativeBattleEffectMemory.FromColdProcess());
        NativeBattleGraphicsModel[] models = NativeBattleGraphicsModels.ClearPresentationBank(Roster);
        NativeBattleVector position = new(20, 0, 40);
        models[4] = models[4] with { Flags2 = 0, ModelId4 = 20, Transform = new(position, position, 0, 0, 4096) };
        NativeBattleGraphicsState graphics = new(models.ToImmutableArray(), Roster.Rows.Select(actor => actor?.ActorId).ToImmutableArray(), 0, 0)
        {
            PresentationRoster = Roster,
            ActionContext = new(4, 0x4010)
        };
        MethodInfo rebind = typeof(BattleSession).GetMethod("RebindNativeSequenceGeometry92", BindingFlags.Instance | BindingFlags.NonPublic)!;

        NativeBattleGraphicsState result = (NativeBattleGraphicsState)rebind.Invoke(session, [graphics, 4, selector])!;

        NativeBattleActionGeometry geometry = Assert.IsType<NativeBattleActionGeometry>(result.ActionContext);
        Assert.Equal(expectedMask, geometry.SourceTargetMask);
        Assert.Equal(Rows(4), geometry.PresentationSourceTargets);
        Assert.Equal(expectedSelf, geometry.IsSelfTarget);
        Assert.Equal(expectedSelf, geometry.UnadjustedDistance == 1000);
    }

    [Fact]
    public void Geometry92_rejects_only_reached_unbound_source_current_or_targets()
    {
        NativeBattleActionGeometry retained = new(14, 0) { PresentationSourceTargets = Rows(15, 24) };

        Assert.Throws<InvalidDataException>(() => NativeBattleSequenceSelections.GeometryArguments92(8, 0, retained, Roster));
        Assert.Throws<InvalidDataException>(() => NativeBattleSequenceSelections.GeometryArguments92(7, 1, retained, Roster));
        Assert.Throws<InvalidDataException>(() => NativeBattleSequenceSelections.GeometryArguments92(7, 0, retained with { ActorIndex = 8 }, Roster));
        Assert.Throws<InvalidDataException>(() => NativeBattleSequenceSelections.GeometryArguments92(7, 7, retained with { PresentationSourceTargets = Rows(8) }, Roster));
        Assert.Throws<NotSupportedException>(() => NativeBattleSequenceSelections.GeometryArguments92(7, 7, null, Roster));
        Assert.Equal(Rows(15, 24), NativeBattleSequenceSelections.GeometryArguments92(7, 7, retained with { ActorIndex = 8 }, Roster).Targets);
    }

    [Fact]
    public void Select93_searches_loaded_bound_model_ids_in_ascending_order_and_excludes_current()
    {
        NativeBattleGraphicsModel[] models = ModelBank();

        Assert.Equal(new[] { 0, 6, 14, 24 }, NativeBattleSequenceSelections.SelectModels93(models, 7, 77, Roster));
        Assert.Equal(new[] { 0, 6, 7, 14 }, NativeBattleSequenceSelections.SelectModels93(models, 24, 77, Roster));
        Assert.Equal(new[] { 15 }, NativeBattleSequenceSelections.SelectModels93(models, 7, 16, Roster));
        Assert.Empty(NativeBattleSequenceSelections.SelectModels93(models, 7, 255, Roster));
    }

    [Fact]
    public void Select93_literal_domain_stays_native_even_when_extension_rows_exist()
    {
        NativeBattleGraphicsModel[] models = ModelBank();

        Assert.Equal(new[] { 0 }, NativeBattleSequenceSelections.SelectModels93(models, 7, 0, Roster));
        Assert.Equal(new[] { 6 }, NativeBattleSequenceSelections.SelectModels93(models, 7, 6, Roster));
        Assert.Throws<NotSupportedException>(() => NativeBattleSequenceSelections.SelectModels93(models, 14, 7, Roster));
        Assert.Throws<NotSupportedException>(() => NativeBattleSequenceSelections.SelectModels93(models, 7, 15, Roster));
        Assert.Throws<InvalidDataException>(() => NativeBattleSequenceSelections.SelectModels93(models, 7, 1, Roster));
        Assert.Throws<InvalidDataException>(() => NativeBattleSequenceSelections.SelectModels93(models, 8, 77, Roster));
        Assert.Throws<InvalidDataException>(() => NativeBattleSequenceSelections.SelectModels93(models[..8], 7, 77, Roster));
    }

    [Fact]
    public void TargetBF_uses_bound_actor_side_with_native_status_and_readiness_order()
    {
        Assert.False(NativeBattleSequenceSelections.CanSelectTargetBF(new(7, 2) { Flags8 = 0x100000 }, Roster));
        Assert.False(NativeBattleSequenceSelections.CanSelectTargetBF(new(14, 2) { Flags8 = 0x200000, ResourceFlags44 = 0x140 }, Roster));
        Assert.True(NativeBattleSequenceSelections.CanSelectTargetBF(new(7, 2) { Flags8 = 0, ResourceFlags44 = 0x40 }, Roster));
        Assert.True(NativeBattleSequenceSelections.CanSelectTargetBF(new(24, 2) { ResourceFlags44 = 0x100 }, Roster));
        Assert.False(NativeBattleSequenceSelections.CanSelectTargetBF(new(24, 2) { Flags8 = 0x300000, ResourceFlags44 = 0x20 }, Roster));
        Assert.Throws<InvalidDataException>(() => NativeBattleSequenceSelections.CanSelectTargetBF(new(8, 2), Roster));
    }

    private static NativeBattleGraphicsModel[] ModelBank()
    {
        return Enumerable.Range(0, Roster.ActorCount).Select(index => index switch
        {
            0 or 6 or 7 or 14 or 24 => new NativeBattleGraphicsModel(index, 2) { ModelId4 = 77 },
            8 => new NativeBattleGraphicsModel(index, 2),
            15 => new NativeBattleGraphicsModel(index, 2) { ModelId4 = 16 },
            23 => new NativeBattleGraphicsModel(index, 0) { ModelId4 = 77 },
            _ => new NativeBattleGraphicsModel(index, 0)
        }).ToArray();
    }

    private static NativeBattleGraphicsActorSet Rows(params int[] rows)
    {
        NativeBattleGraphicsActorSet result = default;
        foreach (int row in rows)
        {
            result = result.Add(row);
        }
        return result;
    }
}
