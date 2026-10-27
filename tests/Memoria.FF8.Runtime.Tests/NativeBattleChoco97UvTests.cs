using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleChoco97UvTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(7)]
    public void U_aggregate_underflow_subtracts_64_from_every_corner_and_preserves_other_bytes(int group)
    {
        var fixture = Mesh(group, false, [128, 144, 160, 176]);
        ImmutableArray<byte> result = NativeBattleChoco97Uv.Scroll72DF60(fixture.Bytes, -8);
        byte[] expected = fixture.Bytes.ToArray();
        byte[] values = [56, 72, 88, 104];
        for (int index = 0; index < fixture.Corners.Length; index++)
        {
            expected[fixture.Corners[index]] = values[index];
        }
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(7)]
    public void V_aggregate_overflow_subtracts_128_from_every_corner_and_preserves_other_bytes(int group)
    {
        var fixture = Mesh(group, true, [250, 10, 20, 30]);
        ImmutableArray<byte> result = NativeBattleChoco97Uv.Scroll7311C0(fixture.Bytes, 12);
        byte[] expected = fixture.Bytes.ToArray();
        byte[] values = [134, 150, 160, 170];
        for (int index = 0; index < fixture.Corners.Length; index++)
        {
            expected[fixture.Corners[index]] = values[index];
        }
        Assert.Equal(expected, result);
    }

    [Fact]
    public void V_exact_255_does_not_wrap_and_DWORD_shift_discards_upper_delta_bits()
    {
        var fixture = Mesh(3, true, [243, 0, 1, 2]);
        ImmutableArray<byte> result = NativeBattleChoco97Uv.Scroll7311C0(fixture.Bytes, 12);
        Assert.Equal(new byte[] { 255, 12, 13, 14 }, fixture.Corners.Select(offset => result[offset]));
        Assert.Equal(result, NativeBattleChoco97Uv.Scroll7311C0(fixture.Bytes, unchecked(12 + (1 << 24))));
    }

    [Fact]
    public void Signed_delta_uses_unchecked_DWORD_candidates_for_both_axes()
    {
        var horizontal = Mesh(3, false, [0, 1, 2, 3]);
        ImmutableArray<byte> u = NativeBattleChoco97Uv.Scroll72DF60(horizontal.Bytes, int.MaxValue);
        Assert.Equal(new byte[] { 63, 64, 65, 66 }, horizontal.Corners.Select(offset => u[offset]));
        var vertical = Mesh(3, true, [0, 10, 20, 30]);
        ImmutableArray<byte> v = NativeBattleChoco97Uv.Scroll7311C0(vertical.Bytes, -1);
        Assert.Equal(new byte[] { 127, 137, 147, 157 }, vertical.Corners.Select(offset => v[offset]));
    }

    [Fact]
    public void Offset_owner_and_unaligned_directory_preserve_prefix_suffix_and_restore_next_mutation()
    {
        var fixture = Mesh(3, false, [0, 16, 32, 48]);
        byte[] mesh = fixture.Bytes.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(mesh, 11);
        ImmutableArray<byte> owner = new byte[] { 91, 92, 93 }.Concat(mesh).Concat(new byte[] { 94, 95 }).ToImmutableArray();
        ImmutableArray<byte> once = NativeBattleChoco97Uv.Scroll72DF60(owner, 3, -8);
        Assert.Equal(owner.Take(3), once.Take(3));
        Assert.Equal(owner.TakeLast(2), once.TakeLast(2));
        ImmutableArray<byte> restored = JsonSerializer.Deserialize<ImmutableArray<byte>>(JsonSerializer.Serialize(once));
        Assert.Equal(NativeBattleChoco97Uv.Scroll72DF60(once, 3, -8), NativeBattleChoco97Uv.Scroll72DF60(restored, 3, -8));
        Assert.Throws<InvalidDataException>(() => NativeBattleChoco97Uv.Scroll72DF60(mesh.Take(12).ToImmutableArray(), -8));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Two_aliasing_geometry_updates_precede_both_draws_and_null_hook_preserves_existing_sources(bool update)
    {
        var fixture = NativeBattleCuragaSpawnTests.Fixture();
        NativeBattleCuragaSpawnStep firstSpawn = NativeBattleCuragaNodeKinematics.Spawn8818A0(fixture.Frame, fixture.Task, NativeBattleCuragaSpawnTests.Emitter, 1, new(1));
        NativeBattleCuragaSpawnStep spawn = NativeBattleCuragaNodeKinematics.Spawn8818A0(firstSpawn.Frame, fixture.Task, NativeBattleCuragaSpawnTests.Emitter, 1, firstSpawn.Random);
        NativeBattleCuragaNodeMemory memory = new(spawn.Frame, fixture.Task);
        byte[] bytes = spawn.Frame.Memory.CuragaExecutable!.Data!.Value.ToArray();
        var mesh = Mesh(3, false, [0, 16, 32, 48]);
        mesh.Bytes.AsSpan().CopyTo(bytes.AsSpan(0x400));
        NativeBattleEffectNodeBank bank = new(NativeBattleCuragaExecutableState.Address, bytes.ToImmutableArray());
        NativeBattleEffectNodeInputs inputs = new(spawn.Frame.State.CopiedCamera, _ => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException())
        {
            ResourceUpdate = update ? NativeBattleChoco97Uv.UpdateDrawableResource : null
        };
        NativeBattleEffectNodeLifecycle lifecycle = new(memory, bank, inputs);
        byte[] source = bank.Read(bank.Address + 0x104, 0x158);
        int first = firstSpawn.Node;
        int second = spawn.Node;
        lifecycle.AdvanceDrawable(first, source);
        lifecycle.AdvanceDrawable(second, source);
        List<byte[]> observed = [];
        lifecycle.DrawNodes(new(null, 256, 160, 120), 0, (_, _) => (resource, _) =>
        {
            observed.Add(mesh.Corners.Select(offset => resource[offset]).ToArray());
            return new([], 0);
        });
        Assert.Equal(2, observed.Count);
        byte[] expected = update ? [48, 64, 80, 96] : [0, 16, 32, 48];
        Assert.All(observed, values => Assert.Equal(expected, values));
    }

    [Fact]
    public void Nonzero_source_indices_do_not_scroll_and_unproved_kind5_is_rejected()
    {
        var fixture = Mesh(3, false, [0, 16, 32, 48]);
        NativeBattleEffectNodeBank bank = new(0x1000, fixture.Bytes);
        Assert.Equal(bank.Data, NativeBattleChoco97Uv.UpdateDrawableResource(bank, bank.Address, 4, 2));
        Assert.Equal(bank.Data, NativeBattleChoco97Uv.UpdateDrawableResource(bank, bank.Address, 4, 3));
        Assert.Throws<NotSupportedException>(() => NativeBattleChoco97Uv.UpdateDrawableResource(bank, bank.Address, 5, 0));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(7)]
    public void Working_ACT_scroll_matches_scalar_bytes_and_detached_history_restores_independently(int group)
    {
        var fixture = Mesh(group, true, [250, 10, 20, 30]);
        NativeBattleEffectArena bank = NativeBattleEffectArena.CreateUnknown(fixture.Bytes.Length).Write(0, fixture.Bytes.AsSpan());
        NativeBattleGfAnimationSource source = NativeBattleOwnedGfAnimationSource.Own(NativeBattleGfAnimationSource.Borrow(0x1000, bank, 0, fixture.Bytes.Length));
        SourceHistory before = NativeBattleStorageSnapshots.Capture(new SourceHistory(source));
        NativeBattleGfAnimationSource result = NativeBattleChoco97Uv.Scroll7311C0(source, 0, 12);
        Assert.Same(source, result);
        Assert.Equal(NativeBattleChoco97Uv.Scroll7311C0(fixture.Bytes, 12), bank.Read(0, fixture.Bytes.Length));
        Assert.Equal(fixture.Bytes, before.Source.Data);
        string json = JsonSerializer.Serialize(before);
        SourceHistory frozen = JsonSerializer.Deserialize<SourceHistory>(json)!;
        SourceHistory first = NativeBattleStorageSnapshots.Restore(frozen);
        SourceHistory second = NativeBattleStorageSnapshots.Restore(frozen);
        NativeBattleChoco97Uv.Scroll7311C0(first.Source, 0, 12);
        using NativeBattleGfSourceReader unchanged = second.Source.OpenReader();
        Assert.Equal(fixture.Bytes, unchanged.ReadBytes(0, fixture.Bytes.Length));
        Assert.Throws<InvalidOperationException>(() => NativeBattleChoco97Uv.Scroll7311C0(frozen.Source, 0, 12));
        Assert.Throws<InvalidOperationException>(() => JsonSerializer.Serialize(new SourceHistory(source)));
    }

    [Fact]
    public void Working_ACT_preserves_unknown_padding_and_failed_scroll_does_not_publish_earlier_faces()
    {
        var fixture = Mesh(3, true, [250, 10, 20, 30]);
        NativeBattleEffectArena bank = NativeBattleEffectArena.CreateUnknown(fixture.Bytes.Length).Write(0, fixture.Bytes.AsSpan());
        bank.MarkUnknown(4, 1);
        NativeBattleGfAnimationSource source = NativeBattleOwnedGfAnimationSource.Own(NativeBattleGfAnimationSource.Borrow(0x1000, bank, 0, fixture.Bytes.Length));
        NativeBattleChoco97Uv.Scroll7311C0(source, 0, 12);
        var padding = bank.ReadSnapshot(4, 1);
        Assert.Equal((byte)0xA5, padding.Data[0]);
        Assert.False(padding.Known[0]);
        string json = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(new SourceHistory(source)));
        Assert.Contains("UnknownRegions", json);
        SourceHistory restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<SourceHistory>(json)!);
        using (NativeBattleGfSourceReader reader = restored.Source.OpenReader())
        {
            byte[] bytes = new byte[1];
            bool[] known = new bool[1];
            reader.ReadWithKnownness(4, bytes, known);
            Assert.Equal((byte)0xA5, bytes[0]);
            Assert.False(known[0]);
            Assert.Throws<NotSupportedException>(() => reader.Byte(4));
        }
        byte[] accepted = bank.ReadSnapshot(0, fixture.Bytes.Length).Data;
        bank.Write(accepted.Length - 4, [255, 255, 255, 127]);
        byte[] beforeFailure = bank.ReadSnapshot(0, fixture.Bytes.Length).Data;
        Assert.Throws<InvalidDataException>(() => NativeBattleChoco97Uv.Scroll7311C0(source, 0, 12));
        Assert.Equal(beforeFailure, bank.ReadSnapshot(0, fixture.Bytes.Length).Data);
    }

    private sealed record SourceHistory([property: System.Text.Json.Serialization.JsonConverter(typeof(NativeBattleOwnedGfAnimationSource.Converter))] NativeBattleGfAnimationSource Source);

    private static (ImmutableArray<byte> Bytes, int[] Corners) Mesh(int selected, bool vertical, byte[] values)
    {
        int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
        byte[] bytes = Enumerable.Repeat((byte)0xA5, 8 + 32 + strides[selected]).ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 8);
        int cursor = 8;
        int face = 0;
        for (int group = 0; group < 8; group++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(cursor), group == selected ? 1 : 0);
            cursor += 4;
            if (group == selected)
            {
                face = cursor;
                cursor += strides[group];
            }
        }
        int[] corners = (selected is 2 or 6 ? new[] { 10, 12, 16 } : new[] { 12, 16, 20, 22 }).Select(offset => face + offset + (vertical ? 1 : 0)).ToArray();
        for (int index = 0; index < corners.Length; index++)
        {
            bytes[corners[index]] = values[index];
        }
        return (bytes.ToImmutableArray(), corners);
    }
}
