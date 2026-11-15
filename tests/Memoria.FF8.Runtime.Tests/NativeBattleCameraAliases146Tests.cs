using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCameraAliases146Tests
{
    [Theory]
    [InlineData(0x90)]
    [InlineData(0xAF)]
    public void Cold_process_camera_aliases_are_available_before_any_command(byte reference)
    {
        NativeBattleGraphicsState graphics = Graphics();
        NativeBattleCameraAliases aliases = Assert.IsType<NativeBattleCameraAliases>(NativeBattleEffectMemory.FromColdProcess().CameraAliases);
        Assert.Equal(Original(graphics, 0), aliases.Resolve50D790(reference, graphics.ActorModels, graphics.NativeBindings));
        Assert.Equal(new NativeBattleCameraPoint(11, 22, 33), Transform(graphics, aliases, reference));
    }

    [Fact]
    public void Retained_aliases_remain_independent_of_the_current_command_geometry()
    {
        NativeBattleGraphicsState graphics = Graphics();
        NativeBattleGraphicsState current = graphics with { ActionContext = new(new BattleRetainedModelReference.OriginalSlot(NativeBattleModelSlot.Decode(1)), BattleRetainedModelSelection.From([new BattleRetainedModelReference.OriginalSlot(NativeBattleModelSlot.Decode(2))]), false) };
        NativeBattleCameraAliases aliases = new(3, 5);
        Assert.Equal(new NativeBattleCameraPoint(311, 322, 333), Transform(current, aliases, 0x9F));
        Assert.Equal(new NativeBattleCameraPoint(511, 522, 533), Transform(current, aliases, 0xAF));
    }

    [Fact]
    public void Geometry_observation_keeps_the_previous_target_for_an_empty_set()
    {
        NativeBattleGraphicsState graphics = Graphics();
        NativeBattleCameraAliases previous = new(3, 5);
        NativeBattleModelAliases bindings = graphics.NativeBindings.CaptureAliases(graphics.ActorModels);
        NativeBattleCameraAliases empty = previous.Observe50D300(Original(graphics, 2), BattleModelTargets.Empty, graphics.ActorModels, bindings);
        Assert.Equal((byte)2, empty.Actor1D99B95);
        Assert.Equal((byte)5, empty.Target1D99AAA);
        Assert.Equal(Original(graphics, 5), empty.Resolve50D790(0xAF, graphics.ActorModels, graphics.NativeBindings));
        NativeBattleCameraAliases selected = empty.Observe50D300(Original(graphics, 3), BattleModelTargets.From([Original(graphics, 6), Original(graphics, 2), Original(graphics, 4)]), graphics.ActorModels, bindings);
        Assert.Equal((byte)6, selected.Target1D99AAA);
    }

    [Theory]
    [InlineData(0x90)]
    [InlineData(0xA0)]
    public void An_unobserved_alias_has_no_restore_default(byte reference)
    {
        NativeBattleGraphicsState graphics = Graphics();
        Assert.Throws<NotSupportedException>(() => NativeBattleCameraAliases.Unknown().Resolve50D790(reference, graphics.ActorModels, graphics.NativeBindings));
    }

    [Fact]
    public void Added_owner_is_preserved_in_memory_without_an_original_operand()
    {
        NativeBattleGraphicsState graphics = Graphics();
        BattleEnemyActorModel extra = new(new("addition"));
        BattleActorModels models = graphics.ActorModels.Add(extra, new(0));
        NativeBattleModelAliases bindings = graphics.NativeBindings.CaptureAliases(models);
        NativeBattleCameraAliases observed = NativeBattleCameraAliases.Unknown().Observe50D300(extra, BattleModelTargets.From([extra, Original(graphics, 6)]), models, bindings);
        Assert.Null(observed.Actor1D99B95);
        Assert.Null(observed.Target1D99AAA);
        Assert.Equal(extra, observed.Resolve50D790(0x90, models, graphics.NativeBindings));
        Assert.Equal(extra, observed.Resolve50D790(0xAF, models, graphics.NativeBindings));
        Assert.Throws<InvalidDataException>(() => observed.Resolve50D790(0x90, graphics.ActorModels, graphics.NativeBindings));
        Assert.Null(NativeBattleTestSnapshots.RestoreJson<NativeBattleCameraAliases>(NativeBattleTestSnapshots.Json(observed))!.Source);
    }

    [Fact]
    public void Retained_alias_bytes_round_trip_and_legacy_missing_owner_remains_missing()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { CameraAliases = new(2, 6) };
        NativeBattleEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(memory))!;
        Assert.Equal(memory.CameraAliases, restored.CameraAliases);
        string legacy = NativeBattleTestSnapshots.Json(memory with { CameraAliases = null });
        Assert.Null(NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(legacy)!.CameraAliases);
    }

    private static NativeBattleGraphicsState Graphics()
    {
        NativeBattleModelBoundary boundary = NativeBattleModelBoundary.FromRetainedMemory(null);
        BattleActorModels models = new([]);
        for (byte slot = 0; slot < NativeBattleModelBoundary.CellCount; slot++)
        {
            BattleModelOwner owner = slot < 3 ? new BattlePartyActorModel(new($"party-{slot}")) : new BattleEnemyActorModel(new($"enemy-{slot}"));
            (boundary, models) = boundary.BindOriginalSlot(NativeBattleModelSlot.Decode(slot), owner, models);
            models = models.Set(owner, new(0)
            {
                Transform = new(default, new((short)(slot * 100 + 10), (short)(slot * 100 + 20), (short)(slot * 100 + 30)), 0, 0, 2048),
                GeometryWorldMatrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], slot * 100 + 10, slot * 100 + 20, slot * 100 + 30, 4096)
            });
        }
        return new(models, boundary, 0, 0, NativeBattleEffectsState.Clear(BattleContactTaskStorageContract.OriginalArena));
    }

    private static BattleModelOwner Original(NativeBattleGraphicsState graphics, byte slot)
    {
        return graphics.NativeBindings.DecodeLiteral(slot, graphics.ActorModels);
    }

    private static NativeBattleCameraPoint Transform(NativeBattleGraphicsState graphics, NativeBattleCameraAliases aliases, byte reference)
    {
        BattleSession session = new(new BattleDefinition { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.TurnBased);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeGraphics))!.SetValue(session, graphics);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeEffectMemory))!.SetValue(session, NativeBattleEffectMemory.FromColdProcess() with { CameraAliases = aliases });
        MethodInfo method = typeof(BattleSession).GetMethod("TransformNativeCameraReference", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (NativeBattleCameraPoint)method.Invoke(session, [reference, new NativeBattleCameraPoint(1, 2, 3), 0u])!;
    }
}
