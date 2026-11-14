using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModelRingTests
{
    [Fact]
    public void World_matrix_uses_the_closed_ring_owner_without_replacing_independent_animation_or_transform_state()
    {
        ImmutableArray<NativeBattleGraphicsModel> models = NativeBattleGraphicsModels.ClearBank().ToImmutableArray();
        NativeBattleActionMatrix ownerMatrix = new([0, 0, -4096, 0, 2048, 0, 4096, 0, 0], 123456, -654321, 4200, 4096);
        NativeBattleActionMatrix separateMatrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], -400, 200, -3600, 4096);
        models = models.SetItem(3, (models[3] with { GeometryWorldMatrix = ownerMatrix }).WriteLinkedModelIndex140(4));
        NativeBattleGraphicsModel sword = (models[4] with { GeometryWorldMatrix = separateMatrix, Sequence = 32, SequenceState = new() { Pose = 22, PoseFrame114 = 3, PoseLimit115 = 9 }, Transform = new(new(0, 0, -3600), new(0, 0, -3600), 0, 0, 4096) }).WriteLinkedModelIndex140(3);
        models = models.SetItem(4, sword);
        NativeBattleGraphicsModel result = NativeBattleModelRings.BindWorldMatrix502AB0(models, sword);
        Assert.Equal(sword with { GeometryWorldMatrix = ownerMatrix }, result);
        Assert.Equal(3, NativeBattleScanModelSelection.Minimum501FF0(models, 4));
        Assert.Equal(NativeBattleScanModelSelection.ModelBase + 3u * NativeBattleScanModelSelection.ModelStride, NativeBattleScanModelSelection.Minimum501FF0(NativeBattleScanPhysicalModels.Capture(models), NativeBattleScanModelSelection.ModelBase + 4u * NativeBattleScanModelSelection.ModelStride));
        ImmutableArray<NativeBattleGraphicsModel> restored = JsonSerializer.Deserialize<ImmutableArray<NativeBattleGraphicsModel>>(JsonSerializer.Serialize(models));
        NativeBattleGraphicsModel restoredSword = NativeBattleModelRings.BindWorldMatrix502AB0(restored, restored[4]);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(restoredSword));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void World_matrix_keeps_the_sources_own_matrix_for_null_links_and_open_chains(bool openChain)
    {
        ImmutableArray<NativeBattleGraphicsModel> models = NativeBattleGraphicsModels.ClearBank().ToImmutableArray();
        NativeBattleGraphicsModel source = models[4].WriteLinkedModelIndex140(openChain ? 3 : null);
        models = models.SetItem(3, models[3].WriteLinkedModelIndex140(null)).SetItem(4, source);
        Assert.Same(source, NativeBattleModelRings.BindWorldMatrix502AB0(models, source));
        Assert.Equal(4, NativeBattleScanModelSelection.Minimum501FF0(models, 4));
    }

    [Fact]
    public void World_matrix_requires_the_ring_owners_real_publication()
    {
        ImmutableArray<NativeBattleGraphicsModel> models = NativeBattleGraphicsModels.ClearBank().ToImmutableArray();
        models = models.SetItem(3, models[3].WriteLinkedModelIndex140(4)).SetItem(4, models[4].WriteLinkedModelIndex140(3));
        Assert.Throws<NotSupportedException>(() => NativeBattleModelRings.BindWorldMatrix502AB0(models, models[4]));
    }

    [Fact]
    public void Opcode90MatchesUnloadedPhysicalEnemyIdsAndUnlinksTheWholeClosedRing()
    {
        BattlePartyActorModel source = new(new("party"));
        BattleEnemyActorModel enemy = new(new("native-enemy"));
        NativeBattleModelBoundary boundary = EmptyBoundary();
        var models = new BattleActorModels([]);
        (boundary, models) = boundary.BindOriginalSlot(NativeBattleModelSlot.Decode(0), source, models);
        (boundary, models) = boundary.BindOriginalSlot(NativeBattleModelSlot.Decode(3), enemy, models);
        models = models.Set(enemy, models.Get(enemy) with { ModelId4 = 17 });

        models = NativeBattleModelRings.Bind90(models, source, 17, boundary);
        Assert.Equal(enemy, models.Get(source).ReadLinkedModel());
        Assert.Equal((ushort)0, models.Get(enemy).Flags0);
        models = NativeBattleModelRings.Bind90(models, enemy, 3, boundary);
        Assert.Equal(enemy, models.Get(enemy).ReadLinkedModel());
        models = models.Set(enemy, models.Get(enemy).WriteLinkedModel(source));
        Assert.Equal<BattleModelOwner>([source, enemy], NativeBattleModelRings.Walk(models, source));

        models = NativeBattleModelRings.Bind90(models, source, 0, boundary);
        Assert.Null(models.Get(source).ReadLinkedModel());
        Assert.Null(models.Get(enemy).ReadLinkedModel());
        Assert.Equal<BattleModelOwner>([source], NativeBattleModelRings.Walk(models, source));
        Assert.Throws<InvalidDataException>(() => NativeBattleModelRings.Bind90(models, source, 0, boundary));
    }

    [Fact]
    public void Opcode90SeparatesEnemyLookupFromAuthoredSlotsAndRejectsAnotherOwnersCycle()
    {
        BattlePartyActorModel source = new(new("party"));
        BattleEnemyActorModel literal = new(new("native-enemy"));
        BattleEnemyActorModel first = new(new("extra-first"));
        BattleEnemyActorModel second = new(new("extra-second"));
        NativeBattleModelBoundary boundary = EmptyBoundary();
        BattleActorModels models = new([new(first, new(0) { ModelId4 = 17 }), new(second, new(3) { ModelId4 = 17 })]);
        (boundary, models) = boundary.BindOriginalSlot(NativeBattleModelSlot.Decode(3), literal, models);
        (boundary, models) = boundary.BindOriginalSlot(NativeBattleModelSlot.Decode(0), source, models);
        models = models.Set(source, models.Get(source) with { ModelId4 = 17 });

        models = NativeBattleModelRings.Bind90(models, source, 3, boundary);
        Assert.Equal(literal, models.Get(source).ReadLinkedModel());
        Assert.Throws<InvalidDataException>(() => NativeBattleModelRings.Bind90(models, source, 4, boundary));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleModelRings.Bind90(models, source, 7, boundary));
        Assert.Same(models, NativeBattleModelRings.Bind90(models, source, 18, boundary));
        Assert.Throws<KeyNotFoundException>(() => NativeBattleModelRings.Bind90(models, new BattlePartyActorModel(new("unbound")), 18, boundary));

        models = NativeBattleModelRings.Bind90(models, source, 17, boundary);
        Assert.Equal(first, models.Get(source).ReadLinkedModel());
        models = models.Set(first, models.Get(first).WriteLinkedModel(second));
        models = models.Set(second, models.Get(second).WriteLinkedModel(first));
        Assert.Throws<InvalidDataException>(() => NativeBattleModelRings.Walk(models, source).ToArray());
        Assert.Throws<InvalidDataException>(() => NativeBattleModelRings.Bind90(models, source, 2, boundary));
    }

    [Fact]
    public void SharedRingCallbacksRetainPhysicalPointerAndDistinctOperandsAcrossJsonAndReleaseOnce()
    {
        NativeBattleAuxiliaryState state = NativeBattleAuxiliaryState.Clear();
        state = state.AppendModelRing(NativeBattleAuxiliaryKind.ModelSequence505C70, 3, sequence: 254).State;
        state = state.AppendModelRing(NativeBattleAuxiliaryKind.ModelStatus50A000, 6, 0xFFFFFFFF).State;
        state = state.AppendModelRing(NativeBattleAuxiliaryKind.ModelResourceFlags50B080, 0, 0x200).State;
        state = JsonSerializer.Deserialize<NativeBattleAuxiliaryState>(JsonSerializer.Serialize(state))!;
        List<(NativeBattleAuxiliaryKind Kind, int Model, uint Argument)> calls = [];
        NativeBattleAuxiliaryInputs inputs = new() { ApplyModelRing = (kind, model, argument) => calls.Add((kind, model, argument)) };
        state = state.Advance(inputs).State;
        state = state.Advance(inputs).State;
        Assert.Equal(new[] { (NativeBattleAuxiliaryKind.ModelSequence505C70, 3, 254u), (NativeBattleAuxiliaryKind.ModelStatus50A000, 6, uint.MaxValue), (NativeBattleAuxiliaryKind.ModelResourceFlags50B080, 0, 0x200u) }, calls);
        Assert.Null(state.Head);
        Assert.Null(state.Tail);
        Assert.Equal(0x831E0u, NativeBattleModelRings.PreservedStatusMask50A000(0x40));
        Assert.Equal(0x21E35F0u, NativeBattleModelRings.PreservedStatusMask50A000(0));
    }

    private static NativeBattleModelBoundary EmptyBoundary()
    {
        return new(Enumerable.Repeat(new NativeBattleGraphicsModel(0), 7));
    }
}
