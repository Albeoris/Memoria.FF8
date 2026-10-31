using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.Tests.NativeBattleEffect318RasterTests;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShared318CaptureTests
{
    [Theory]
    [InlineData(314)]
    [InlineData(316)]
    [InlineData(318)]
    public void Explicit_capture_preserves_detached_native_state_and_real_selected_world_depths(ushort effect)
    {
        byte[] bytes = Source().Bytes.ToArray();
        bytes[40] = 4;
        bytes[42] = 6;
        Array.Clear(bytes, 48, 4);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(56), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(58), 4096);
        var source = Source() with { Bytes = bytes.ToImmutableArray() };
        NativeBattleCameraProjection projection = Projection with { View = NativeBattleCuragaMatrices.Identity8DD770() };
        var plain = NativeBattleEffect318Raster.DrawLoaded(Memory(), Temporary(), source, projection);
        string expected = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(plain));
        NativeBattlePilotVisualIdentity identity = new(effect, 0x1234, 3, 55, 1) { ResourceGeneration = 100 };
        var captured = NativeBattleEffect318Raster.DrawLoaded(Memory(), Temporary(), source, projection, identity);
        Assert.Equal(expected, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(captured)));
        var primitive = Assert.Single(captured.Batch.Attempts).Primitive!;
        var visual = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
        Assert.Equal(effect, visual.Identity.Effect);
        Assert.Equal(identity.ResourceGeneration, visual.Identity.ResourceGeneration);
        Assert.Equal(new System.Numerics.Vector3(0, 0, 2000), visual.A);
        Assert.Equal(new System.Numerics.Vector3(64, 96, 2000), visual.D);
        Assert.Equal(new System.Numerics.Vector4(2000, 2000, 2000, 2000), visual.SourceDepths!.Value);
        Assert.False(visual.ScreenSpace);
        Assert.True(visual.Continuous);
        Assert.True(visual.UseSourceDepths);
    }

    [Theory]
    [InlineData(314)]
    [InlineData(316)]
    [InlineData(318)]
    public void Explicit_capture_preserves_rejection_and_does_not_read_unreached_fourth_Z_W(ushort effect)
    {
        var plain = NativeBattleEffect318Raster.DrawLoaded(Memory(), Temporary(0).MarkUnknown(0x60, 4), Source(), Projection);
        string expected = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(plain));
        var captured = NativeBattleEffect318Raster.DrawLoaded(Memory(), Temporary(0).MarkUnknown(0x60, 4), Source(), Projection, new(effect, 1, 2, 3, 4));
        Assert.Equal(expected, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(captured)));
        Assert.Equal(0, captured.Batch.AllocationBytes);
        Assert.All(captured.Batch.Attempts, attempt => Assert.Null(attempt.Primitive));
    }
}
