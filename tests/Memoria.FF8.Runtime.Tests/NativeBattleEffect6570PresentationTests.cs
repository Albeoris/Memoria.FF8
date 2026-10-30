using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect6570PresentationTests
{
    [Theory]
    [InlineData(65, false)]
    [InlineData(65, true)]
    [InlineData(70, false)]
    [InlineData(70, true)]
    public void Family_depth_finalization_copies_actual_payload_or_explicit_native_empty_without_changing_geometry(int effect, bool empty)
    {
        NativeBattlePilotVisualGeometry geometry = new(new((ushort)effect, 1, 2, 3, 4), new(1, 2, 3), new(4, 5, 6), new(7, 8, 9), new(10, 11, 12), 4, false) { ScreenSpace = true, ScreenScale = 8, ScreenOffsets = [new(1, 2), new(3, 4), new(5, 6), new(7, 8)] };
        NativeBattleGeometryPrimitive primitive = new(8, [0x05000000, 0x28000000, 0, 320, 216u << 16, 320u | 216u << 16], empty ? [] : [13, 27, 41, 59]) { PilotGeometry = geometry };
        NativeBattleGfGeometryBatch batch = new([new(0, [], [], primitive)], 24);
        NativeBattleGfGeometryBatch result = Finalize(effect, batch);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(batch)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result)));
        NativeBattlePilotVisualGeometry retained = Assert.Single(result.Attempts).Primitive!.PilotGeometry!;
        Assert.Equal(geometry with { SourceDepths = empty ? Vector4.Zero : new(13, 27, 41, 59), UseSourceDepths = true }, retained);
        Assert.False(geometry.UseSourceDepths);
        Assert.Null(geometry.SourceDepths);
    }

    [Theory]
    [InlineData(65)]
    [InlineData(70)]
    public void Family_depth_finalization_keeps_default_foreign_state_and_already_explicit_metadata(int effect)
    {
        NativeBattlePilotVisualGeometry geometry = new(new((ushort)effect, 1, 2, 3, 4), default, default, default, default, 4) { UseSourceDepths = true, SourceDepths = new(9, 8, 7, 6) };
        NativeBattleGeometryPrimitive[] primitives = [new(8, [0, 0, 0], [1, 2, 3, 4]), new(8, [0, 0, 0], [1, 2, 3, 4]) { PilotGeometry = geometry with { Identity = geometry.Identity with { Effect = 249 }, UseSourceDepths = false } }, new(8, [0, 0], []) { PilotGeometry = geometry with { UseSourceDepths = false } }, new(8, [0, 0, 0], []) { PilotGeometry = geometry }];
        NativeBattleGfGeometryBatch batch = new(primitives.Select(primitive => new NativeBattleGfGeometryAttempt(0, [], [], primitive)).ToImmutableArray(), 0);
        NativeBattleGfGeometryBatch result = Finalize(effect, batch);
        for (int index = 0; index < primitives.Length; index++)
        {
            Assert.Same(primitives[index], result.Attempts[index].Primitive);
        }
    }

    [Theory]
    [InlineData(65)]
    [InlineData(70)]
    public void Family_depth_finalization_rejects_unsupported_native_payload_arity(int effect)
    {
        NativeBattleGeometryPrimitive primitive = new(8, [0, 0, 0], [1]) { PilotGeometry = new(new((ushort)effect, 1, 2, 3, 4), default, default, default, default, 4) };
        Assert.Throws<InvalidDataException>(() => Finalize(effect, new([new(0, [], [], primitive)], 0)));
    }

    private static NativeBattleGfGeometryBatch Finalize(int effect, NativeBattleGfGeometryBatch batch)
    {
        return effect == 65 ? NativeBattleEffect65Presentation.FinalizeCapturedBatch(batch) : NativeBattleEffect70Presentation.FinalizeCapturedBatch(batch);
    }
}
