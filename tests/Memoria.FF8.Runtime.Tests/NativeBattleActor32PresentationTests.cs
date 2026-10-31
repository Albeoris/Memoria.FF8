namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleActor32PresentationTests
{
    [Theory]
    [InlineData(286)]
    [InlineData(289)]
    public void Actual_workspace_rebirth_and_selected_resources_break_only_metadata_identity(ushort effect)
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(17, [0xA5]);
        NativeBattlePilotVisualIdentity root = new(effect, 0x2507CC8, 2, 70, 0);
        byte[] before = arena.Read(17, 1);
        NativeBattlePilotVisualLifetimes.Allocated(arena, 0x2510000, 0);
        var first = NativeBattleActor32Presentation.Node(root, arena, 0x2510000, 0x1070000, 44);
        Assert.Equal(first, NativeBattleActor32Presentation.Node(root, arena, 0x2510000, 0x1070000, 44));
        Assert.NotEqual(first, NativeBattleActor32Presentation.Node(root, arena, 0x2510000, 0x1070000, 88));
        Assert.NotEqual(first, NativeBattleActor32Presentation.Node(root, arena, 0x2510000, 0x1080000, 44));
        NativeBattlePilotVisualLifetimes.Allocated(arena, 0x2510000, 0);
        Assert.NotEqual(first, NativeBattleActor32Presentation.Node(root, arena, 0x2510000, 0x1070000, 44));
        Assert.NotEqual(NativeBattleActor32Presentation.Resource(first, 100, 200), NativeBattleActor32Presentation.Resource(first, 100, 208));
        Assert.Equal(before, arena.Read(17, 1));
        Assert.Equal(root.Generation, first!.Value.Generation);
    }

    [Fact]
    public void Native_depth_sidecar_keeps_physical_packets_world_identity_and_uncaptured_gaps()
    {
        NativeBattlePilotVisualGeometry visual = new(new(289, 2, 3, 4, 5), new(1, 2, 3), new(4, 5, 6), new(7, 8, 9), new(10, 11, 12), 4);
        NativeBattleGeometryPrimitive primitive = new(99, [0x09000000, 0x2E112233], [101, 202, 303, 404]) { PilotGeometry = visual };
        NativeBattleGfGeometryBatch batch = new([new(40, [0xA5], [NativeBattleGfPacketByteState.Known], primitive), new(80, [0x5A], [NativeBattleGfPacketByteState.Unknown], primitive with { PilotGeometry = null })], 120);
        string expected = System.Text.Json.JsonSerializer.Serialize(batch);
        NativeBattleGfGeometryBatch captured = NativeBattleActor32Presentation.Depths(batch);
        Assert.Equal(expected, System.Text.Json.JsonSerializer.Serialize(captured));
        Assert.Equal(visual.Identity, captured.Attempts[0].Primitive!.PilotGeometry!.Identity);
        Assert.Equal(visual.A, captured.Attempts[0].Primitive!.PilotGeometry!.A);
        Assert.Equal(new System.Numerics.Vector4(101, 202, 303, 404), captured.Attempts[0].Primitive!.PilotGeometry!.SourceDepths!.Value);
        Assert.True(captured.Attempts[0].Primitive!.PilotGeometry!.UseSourceDepths);
        Assert.Null(captured.Attempts[1].Primitive!.PilotGeometry);
    }

    [Fact]
    public void Capture_is_explicit_and_other_effects_keep_default_policy()
    {
        var arena = NativeBattleEffectArena.Clear();
        Assert.Null(NativeBattleActor32Presentation.Resource(null, 1));
        Assert.Null(NativeBattleActor32Presentation.Node(null, arena, 2, 3, 4));
        Assert.Null(NativeBattleActor32Presentation.Resource(new(18, 2, 3, 4, 5), 1));
        Assert.Null(NativeBattleActor32Presentation.Node(new(18, 2, 3, 4, 5), arena, 2, 3, 4));
    }
}
