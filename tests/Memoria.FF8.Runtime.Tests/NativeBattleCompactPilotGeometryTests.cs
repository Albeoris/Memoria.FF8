using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCompactPilotGeometryTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Compact_and_rich_references_share_corner_sampling_and_identity_boundaries(int boundary)
    {
        NativeBattlePilotVisualGeometry first = Geometry(0);
        NativeBattlePilotVisualGeometry next = Geometry(10);
        next = boundary switch
        {
            1 => next with { Identity = next.Identity with { ResourceGeneration = 9 } },
            2 => next with { Identity = next.Identity with { Generation = 10 } },
            3 => next with { Continuous = false },
            4 => next with { ScreenSpace = true },
            5 => next with { CornerCount = 4, D = new(20, 10, 100) },
            _ => next
        };
        NativeBattlePilotGeometryReference a = Compact(first);
        NativeBattlePilotGeometryReference b = Compact(next);
        Assert.True(a.IsModel);
        Assert.Equal(first, a.ToGeometry());
        Assert.Equal(next, b.ToGeometry());
        Assert.Same(first, new NativeBattlePilotGeometryReference(first).ToGeometry());
        foreach (float alpha in new[] { 0f, 0.5f, 1f })
        {
            for (int corner = 0; corner < 3; corner++)
            {
                Vector3 expected = NativeBattlePilotVisualGeometry.SampleCorner(first, next, corner, alpha);
                Assert.Equal(expected, NativeBattlePilotVisualGeometry.SampleCorner(a, b, corner, alpha));
                Assert.Equal(expected, NativeBattlePilotVisualGeometry.SampleCorner(a, new NativeBattlePilotGeometryReference(next), corner, alpha));
            }
        }
    }

    [Fact]
    public void Compact_reference_stays_small_and_sampling_does_not_materialize_legacy_objects()
    {
        Assert.Equal(16, Unsafe.SizeOf<NativeBattlePilotGeometryReference>());
        NativeBattlePilotGeometryReference first = Compact(Geometry(0));
        NativeBattlePilotGeometryReference next = Compact(Geometry(10));
        _ = NativeBattlePilotVisualGeometry.SampleCorner(first, next, 0, 0.5f);
        long before = GC.GetAllocatedBytesForCurrentThread();
        Vector3 total = default;
        for (int index = 0; index < 1000; index++)
        {
            total += NativeBattlePilotVisualGeometry.SampleCorner(first, next, index % 3, 0.5f);
            total.Z += first.SampleDepth(100) * first.ScreenScale;
            if (first.ForeignSurface is not null || first.Sprite is not null || !first.ScreenOffsets.IsEmpty || first.SourceDepths is not null || first.FocalLengthOverride is not null || first.ProjectionScreenBounds is not null || first.UseSourceDepths)
            {
                throw new InvalidOperationException("Basic model geometry has no richer effect overrides.");
            }
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.True(total.Z > 0);
        NativeBattlePilotVisualGeometry rich = Geometry(0);
        foreach (float value in new[] { 0f, -0f, 100.5f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            Assert.Equal(BitConverter.SingleToInt32Bits(rich.SampleDepth(value)), BitConverter.SingleToInt32Bits(first.SampleDepth(value)));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Physical_overlap_clears_current_pilot_reference_but_retains_snapshot_and_commands(bool reset)
    {
        NativeBattleModelPrimitiveBatch.Builder batchBuilder = new();
        NativeBattlePilotGeometryReference pilot = AddPilot(batchBuilder, Geometry(0));
        ImmutableArray<uint> words = [0x04000000, 0x200000FF, 0, 80, 80u << 16];
        ImmutableArray<int> depths = [100, 100, 100, 0];
        batchBuilder.AddRaw(20, words.AsSpan(), depths.AsSpan(), [], [], pilot, 0);
        NativeBattleModelPrimitiveBatch batch = batchBuilder.Freeze();
        NativeBattlePrimitiveReference reference = new("off_B6D070", 0, 0);
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        using (NativeBattleLogicalOrdering.Builder writer = new(ordering))
        {
            writer.PrependPlacedModel(new(batch, [reference], null, null), 0);
            writer.ToImmutable();
        }
        NativeBattleLogicalCommand command = Assert.Single(ordering.Traverse(Lengths()));
        if (reset)
        {
            ordering = ordering.ResetAfterDraw45D080();
        }
        NativeBattleLogicalOrdering saved = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(ordering));
        using NativeBattleLogicalOrdering.Builder overwrite = new(ordering);
        overwrite.InvalidatePacketRange(reference with { ByteOffset = 8 }, 4);
        Assert.True(Assert.Single(overwrite.ToImmutable().Packets).Pilot.IsEmpty);
        Assert.Equal(pilot, command.Pilot);
        Assert.Equal(pilot, Assert.Single(saved.Packets).Pilot);
        Assert.Equal(new Vector3(0, 0, 100), command.Pilot.Corner(0));
        string json = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(saved));
        Assert.DoesNotContain("\"Pilot\"", json);
        Assert.DoesNotContain("\"PilotGeometry\"", json);
        NativeBattleLogicalOrdering restored = JsonSerializer.Deserialize<NativeBattleLogicalOrdering>(json)!;
        Assert.True(Assert.Single(restored.Packets).Pilot.IsEmpty);
    }

    [Fact]
    public void Compatibility_initializers_replace_compact_references_without_changing_rich_geometry()
    {
        NativeBattleLogicalCommand original = new(new("pilot", 0, 0), 1, 0x20, [0x200000FF, 0, 80, 80u << 16], 0, [], null) { Pilot = Compact(Geometry(0)) };
        NativeBattlePilotVisualGeometry rich = Geometry(10) with { DepthQuantum = 4, DepthOffset = -300, SourceDepths = Vector4.One, UseSourceDepths = true };
        NativeBattleLogicalCommand replaced = original with { PilotGeometry = rich };
        Assert.Same(rich, replaced.PilotGeometry);
        Assert.False(replaced.Pilot.IsModel);
        Assert.Equal(rich.SampleDepth(1025), replaced.Pilot.SampleDepth(1025));
        Assert.Equal(rich.SourceDepths, replaced.Pilot.SourceDepths);
        Assert.True(replaced.Pilot.UseSourceDepths);
        Assert.True((replaced with { PilotGeometry = null }).Pilot.IsEmpty);
        Assert.True(original.Pilot.IsModel);
    }

    [Fact]
    public void Capturing_command_lists_does_not_walk_each_compact_pilot_page()
    {
        NativeBattleModelPrimitiveBatch.Builder builder = new();
        NativeBattleLogicalCommand template = new(new("pilot", 0, 0), 1, 0x20, [0x200000FF, 0, 80, 80u << 16], 0, [], null);
        ImmutableArray<NativeBattleLogicalCommand>.Builder commands = ImmutableArray.CreateBuilder<NativeBattleLogicalCommand>(256);
        for (int index = 0; index < 256; index++)
        {
            NativeBattlePilotVisualGeometry geometry = Geometry(index);
            NativeBattlePilotGeometryReference pilot = AddPilot(builder, geometry with { Identity = geometry.Identity with { Primitive = index } });
            commands.Add(template with { Pilot = pilot });
        }
        builder.Freeze();
        ImmutableArray<NativeBattleLogicalCommand> compact = commands.MoveToImmutable();
        ImmutableArray<NativeBattleLogicalCommand> empty = compact.Select(command => command with { Pilot = default }).ToImmutableArray();
        Func<object> compactCapture = () => NativeBattleStorageSnapshots.Capture(compact);
        Func<object> emptyCapture = () => NativeBattleStorageSnapshots.Capture(empty);
        GC.KeepAlive(compactCapture());
        GC.KeepAlive(emptyCapture());
        long emptyBytes = Allocated(emptyCapture);
        long compactBytes = Allocated(compactCapture);
        Assert.True(compactBytes <= emptyBytes + 8192, $"Capturing compact commands allocated {compactBytes} bytes; commands without pilot geometry allocated {emptyBytes} bytes. Pilot pages must not be scanned per command.");
        ImmutableArray<NativeBattleLogicalCommand> captured = NativeBattleStorageSnapshots.Capture(compact);
        for (int index = 0; index < compact.Length; index++)
        {
            Assert.Equal(compact[index].Pilot, captured[index].Pilot);
        }
    }

    [Fact]
    public void Rich_pilot_host_materials_preserve_recursive_storage_capture_and_restore()
    {
        NativeBattleLogicalOrdering working = NativeBattleLogicalOrdering.Clear().Prepend(20, new("off_B6D070", 0, 0), [0x04000000, 0x200000FF, 0, 80, 80u << 16], 0, [100, 100, 100, 0]);
        NativeBattlePilotVisualGeometry geometry = Geometry(0) with { ForeignSurface = new(new StorageMaterial(working), [], []) };
        NativeBattlePilotGeometryReference source = new(geometry);
        NativeBattlePilotGeometryReference captured = NativeBattleStorageSnapshots.Capture(source);
        StorageMaterial material = Assert.IsType<StorageMaterial>(captured.ForeignSurface!.Material);
        Assert.True(material.Ordering.IsSnapshot);
        Assert.False(working.IsSnapshot);
        Assert.NotSame(geometry, captured.ToGeometry());
        working.Prepend(21, new("off_B6D070", 0, 20), [0x04000000, 0x2000FF00, 0, 80, 80u << 16], 0, [100, 100, 100, 0]);
        Assert.Single(material.Ordering.Packets);
        NativeBattlePilotGeometryReference restored = NativeBattleStorageSnapshots.Restore(captured);
        StorageMaterial restoredMaterial = Assert.IsType<StorageMaterial>(restored.ForeignSurface!.Material);
        Assert.False(restoredMaterial.Ordering.IsSnapshot);
        Assert.Single(restoredMaterial.Ordering.Packets);
        Assert.Equal(geometry.Corner(0), restored.Corner(0));
    }

    private static long Allocated(Func<object> operation)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        object value = operation();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(value);
        return allocated;
    }

    private sealed record StorageMaterial(NativeBattleLogicalOrdering Ordering) : INativeBattleForeignMaterial;

    private static NativeBattlePilotGeometryReference Compact(NativeBattlePilotVisualGeometry geometry)
    {
        NativeBattleModelPrimitiveBatch.Builder builder = new();
        NativeBattlePilotGeometryReference reference = AddPilot(builder, geometry);
        builder.Freeze();
        Assert.Throws<InvalidOperationException>(() => builder.AddPilot(default));
        return reference;
    }

    private static NativeBattlePilotGeometryReference AddPilot(NativeBattleModelPrimitiveBatch.Builder builder, NativeBattlePilotVisualGeometry geometry)
    {
        int emission = builder.BeginEmission(new(null, null, null, geometry.Identity, geometry.Continuous, geometry.ScreenSpace));
        return builder.AddPilot(new(geometry.A, geometry.B, geometry.C, geometry.D, geometry.Identity.Primitive, emission, geometry.CornerCount));
    }

    private static NativeBattlePilotVisualGeometry Geometry(float x)
    {
        return new(new(0, 0x502D40, 11, 3, 4) { ResourceGeneration = 7 }, new(x, 0, 100), new(x + 10, 0, 100), new(x, 10, 100), default, 3);
    }

    private static ImmutableArray<byte> Lengths()
    {
        return new byte[256].ToImmutableArray().SetItem(0x20, 4);
    }
}
