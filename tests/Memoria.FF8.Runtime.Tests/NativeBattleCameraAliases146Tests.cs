using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCameraAliases146Tests
{
    [Theory]
    [InlineData(0x90)]
    [InlineData(0xAF)]
    public void Cold_process_camera_aliases_are_available_before_any_command(byte reference)
    {
        NativeBattleCameraAliases aliases = Assert.IsType<NativeBattleCameraAliases>(NativeBattleEffectMemory.FromColdProcess().CameraAliases);
        Assert.Equal((byte)0, aliases.Resolve50D790(reference));
        Assert.Equal(new NativeBattleCameraPoint(11, 22, 33), Transform(aliases, reference, null));
    }

    [Fact]
    public void Retained_aliases_remain_independent_of_the_current_command_geometry()
    {
        NativeBattleCameraAliases aliases = new(3, 5);
        NativeBattleActionGeometry unrelated = new(1, 2) { TargetIndex = 1 };
        Assert.Equal(new NativeBattleCameraPoint(311, 322, 333), Transform(aliases, 0x9F, unrelated));
        Assert.Equal(new NativeBattleCameraPoint(511, 522, 533), Transform(aliases, 0xAF, unrelated));
    }

    [Fact]
    public void Native_geometry_publication_keeps_the_previous_target_for_an_empty_mask()
    {
        NativeBattleCameraAliases previous = new(3, 5);
        Assert.Equal(new NativeBattleCameraAliases(2, 5), previous.Observe50D300(2, 0));
        Assert.Equal(new NativeBattleCameraAliases(3, 6), previous.Observe50D300(3, 0x54));
    }

    [Theory]
    [InlineData(0x90)]
    [InlineData(0xA0)]
    public void An_unobserved_alias_has_no_restore_default(byte reference)
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleCameraAliases.Unknown().Resolve50D790(reference));
    }

    [Fact]
    public void An_actual_writer_establishes_only_the_fields_it_writes()
    {
        NativeBattleCameraAliases observed = NativeBattleCameraAliases.Unknown().Observe50D300(4, 0);
        Assert.Equal((byte)4, observed.Resolve50D790(0x90));
        Assert.Throws<NotSupportedException>(() => observed.Resolve50D790(0xA0));
    }

    [Fact]
    public void Retained_aliases_round_trip_and_legacy_missing_owner_remains_missing()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { CameraAliases = new(2, 6) };
        NativeBattleEffectMemory restored = JsonSerializer.Deserialize<NativeBattleEffectMemory>(JsonSerializer.Serialize(memory))!;
        Assert.Equal(memory.CameraAliases, restored.CameraAliases);
        string legacy = JsonSerializer.Serialize(memory with { CameraAliases = null });
        Assert.Null(JsonSerializer.Deserialize<NativeBattleEffectMemory>(legacy)!.CameraAliases);
    }

    private static NativeBattleCameraPoint Transform(NativeBattleCameraAliases aliases, byte reference, NativeBattleActionGeometry? context)
    {
        BattleSession session = new(new BattleDefinition { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.TurnBased);
        ImmutableArray<NativeBattleGraphicsModel> models = Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, 0)
        {
            Transform = new(default, new((short)(index * 100 + 10), (short)(index * 100 + 20), (short)(index * 100 + 30)), 0, 0, 2048),
            GeometryWorldMatrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], index * 100 + 10, index * 100 + 20, index * 100 + 30, 4096)
        }).ToImmutableArray();
        NativeBattleGraphicsState graphics = new(models, Enumerable.Repeat<string?>(null, 7).ToImmutableArray(), 0, 0) { ActionContext = context };
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeGraphics))!.SetValue(session, graphics);
        typeof(BattleSession).GetProperty(nameof(BattleSession.NativeEffectMemory))!.SetValue(session, NativeBattleEffectMemory.FromColdProcess() with { CameraAliases = aliases });
        MethodInfo method = typeof(BattleSession).GetMethod("TransformNativeCameraReference", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (NativeBattleCameraPoint)method.Invoke(session, [reference, new NativeBattleCameraPoint(1, 2, 3), 0u])!;
    }
}
