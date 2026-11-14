namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModelBoundaryTests
{
    [Fact]
    public void Bind_unbind_and_rebind_move_the_current_state_without_filling_unknown_fields()
    {
        NativeBattleGraphicsModel[] cells = OriginalCells();
        NativeBattleModelBoundary initial = new(cells);
        BattleActorModels empty = new([]);
        NativeBattleModelSlot slot = NativeBattleModelSlot.Decode(3);
        BattleEnemyActorModel first = Enemy("enemy-allocation-1");
        BattleEnemyActorModel next = Enemy("enemy-allocation-2");

        var (bound, models) = initial.BindOriginalSlot(slot, first, empty);
        Assert.Same(cells[3], models.Get(first));
        Assert.Same(cells[3], bound.ReadOriginalCell(slot, models));
        Assert.Null(models.Get(first).Flags8);
        Assert.Equal((ushort)3, models.Get(first).ResourceFlags44);
        Assert.Throws<InvalidDataException>(() => initial.DecodeLiteral(3, empty));

        NativeBattleGraphicsModel updated = models.Get(first) with { Flags8 = 0x800000, ResourceFlags44 = 0x40 };
        BattleActorModels changed = models.Set(first, updated);
        var (unbound, removed) = bound.UnbindOriginalSlot(slot, changed);
        Assert.False(removed.Contains(first));
        Assert.Same(updated, unbound.ReadOriginalCell(slot, removed));
        Assert.Throws<InvalidDataException>(() => unbound.DecodeLiteral(3, removed));

        var (rebound, replacement) = unbound.BindOriginalSlot(slot, next, removed);
        Assert.Same(updated, replacement.Get(next));
        Assert.Equal(next, rebound.DecodeLiteral(3, replacement));
        Assert.False(rebound.EncodeExactActor(first, replacement, out _));
        Assert.Same(cells[3], bound.ReadOriginalCell(slot, models));

        var (direct, directModels) = bound.BindOriginalSlot(slot, next, changed);
        Assert.Equal(next, direct.DecodeLiteral(3, directModels));
        Assert.Same(updated, directModels.Get(next));
        Assert.False(directModels.Contains(first));
    }

    [Fact]
    public void Original_bindings_are_slot_ordered_independent_of_load_order_and_append_no_extra_aliases()
    {
        NativeBattleGraphicsModel[] retained = OriginalCells();
        NativeBattleModelBoundary boundary = new(retained);
        BattleModelOwner[] extras = Enumerable.Range(0, 40).Select(index => Enemy($"extra-{index}")).ToArray();
        BattleActorModels models = new(extras.Select(owner => new BattleModelEntry<BattleModelOwner>(owner, retained[0])));
        BattleEnemyActorModel left = Enemy("left");
        BattleEnemyActorModel right = Enemy("right");
        BattlePartyActorModel party = new(new("party"));

        (boundary, models) = boundary.BindOriginalSlot(NativeBattleModelSlot.Decode(6), right, models);
        (boundary, models) = boundary.BindOriginalSlot(NativeBattleModelSlot.Decode(3), left, models);
        (boundary, models) = boundary.BindOriginalSlot(NativeBattleModelSlot.Decode(2), party, models);

        Assert.Equal<BattleModelOwner>([party, left, right, .. extras], models.Owners);
        Assert.Equal(retained, boundary.CaptureOriginalCells(models));
        Assert.Equal(BattleSide.Allies, NativeBattleModelSlot.Decode(2).Side);
        Assert.Equal(BattleSide.Enemies, NativeBattleModelSlot.Decode(3).Side);
        Assert.Equal(left, boundary.DecodeLiteral(3, models));
        BattleModelTargets selection = boundary.DecodeTargetMask(0x4C, models);
        Assert.Equal<BattleModelOwner>([party, left, right], selection.Enumerate());
        Assert.True(boundary.EncodeExactActor(right, models, out byte operand));
        Assert.Equal(6, operand);
        Assert.True(boundary.EncodeExactTargetMask(selection, models, out ushort mask));
        Assert.Equal(0x4C, mask);
        Assert.Same(BattleModelTargets.Empty, boundary.DecodeTargetMask(0, models));
        Assert.True(boundary.EncodeExactTargetMask(BattleModelTargets.Empty, models, out mask));
        Assert.Equal(0, mask);

        Assert.False(boundary.EncodeExactTargetMask(selection.Add(extras[^1]), models, out mask));
        Assert.Equal(0, mask);
        Assert.False(boundary.EncodeExactActor(extras[0], models, out _));
        Assert.Throws<InvalidDataException>(() => boundary.DecodeTargetMask(0x4D, models));
        Assert.Throws<ArgumentOutOfRangeException>(() => boundary.DecodeTargetMask(0x8C, models));
        Assert.Throws<ArgumentOutOfRangeException>(() => boundary.DecodeLiteral(7, models));
        Assert.Throws<ArgumentOutOfRangeException>(() => NativeBattleModelSlot.Decode(byte.MaxValue));
    }

    [Fact]
    public void Invalid_bindings_do_not_move_or_alias_retained_cells()
    {
        NativeBattleGraphicsModel[] retained = OriginalCells();
        NativeBattleModelBoundary initial = new(retained);
        BattleEnemyActorModel owner = Enemy("native");
        BattleEnemyActorModel extra = Enemy("extra");
        NativeBattleModelSlot current = NativeBattleModelSlot.Decode(3);
        NativeBattleModelSlot other = NativeBattleModelSlot.Decode(4);
        NativeBattleModelSlot party = NativeBattleModelSlot.Decode(0);
        var (boundary, models) = initial.BindOriginalSlot(current, owner, new([new(extra, retained[0])]));

        Assert.Throws<ArgumentException>(() => initial.BindOriginalSlot(party, owner, new([])));
        Assert.Throws<ArgumentException>(() => boundary.BindOriginalSlot(other, owner, models));
        Assert.Throws<ArgumentException>(() => boundary.BindOriginalSlot(other, extra, models));
        Assert.Throws<ArgumentException>(() => new NativeBattleModelBoundary(retained.Take(6)));
        Assert.Throws<ArgumentException>(() => new NativeBattleModelBoundary(retained.Append(retained[0])));
        Assert.Throws<ArgumentException>(() => new NativeBattleModelBoundary([.. retained.Take(6), null!]));
        BattleActorModels incomplete = models.Remove(owner);
        Assert.False(boundary.EncodeExactActor(owner, incomplete, out _));
        Assert.False(boundary.EncodeExactTargetMask(BattleModelTargets.From([owner]), incomplete, out _));
        Assert.Throws<InvalidDataException>(() => boundary.DecodeLiteral(3, incomplete));
        Assert.Throws<InvalidDataException>(() => boundary.BindOriginalSlot(other, Enemy("other"), incomplete));
        Assert.Throws<InvalidDataException>(() => boundary.UnbindOriginalSlot(other, incomplete));
        Assert.Same(retained[4], boundary.ReadOriginalCell(other, models));
        Assert.Equal(owner, boundary.DecodeLiteral(3, models));

        var (boundAgain, modelsAgain) = boundary.BindOriginalSlot(current, Enemy("native"), models);
        Assert.Same(boundary, boundAgain);
        Assert.Same(models, modelsAgain);
        var (emptyAgain, unchangedModels) = boundary.UnbindOriginalSlot(other, models);
        Assert.Same(boundary, emptyAgain);
        Assert.Same(models, unchangedModels);
    }

    private static NativeBattleGraphicsModel[] OriginalCells()
    {
        return Enumerable.Range(0, 7).Select(offset => new NativeBattleGraphicsModel(0, 0) { ResourceFlags44 = (ushort)offset }).ToArray();
    }

    private static BattleEnemyActorModel Enemy(string actorId) => new(new(actorId));
}
