using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleResolvedCommandListTests
{
    [Fact]
    public void Frozen_pages_keep_order_and_survive_each_builder_lifetime()
    {
        NativeBattleCommandPages<NativeBattleLogicalResolvedCommand>.Builder builder = new();
        NativeBattlePrimitiveDrawMode mode = new(0x65) { TextureWindowBits = 0x120 };
        for (int index = 0; index < 520; index++)
        {
            builder.Add(new(Command(index), mode, (byte)(index % 3)));
        }
        NativeBattleResolvedCommandList published = new(builder.Freeze());
        Assert.Throws<InvalidOperationException>(() => builder.Add(new(Command(700), mode, 0)));
        Assert.Throws<InvalidOperationException>(() => builder.Freeze());
        NativeBattleResolvedCommandList next = [new(Command(900), mode, 1)];

        Assert.Equal(520, published.Length);
        Assert.Equal(520, ((IReadOnlyList<NativeBattleLogicalResolvedCommand>)published).Count);
        int seen = 0;
        foreach (ref readonly NativeBattleLogicalResolvedCommand command in published)
        {
            Assert.Equal(seen * 16, command.Command.Packet.ByteOffset);
            Assert.Equal((byte)(seen % 3), command.Pass);
            Assert.Same(mode, command.DrawMode);
            seen++;
        }
        Assert.Equal(published.Length, seen);
        Assert.Equal(255 * 16, published[255].Command.Packet.ByteOffset);
        Assert.Equal(256 * 16, published[256].Command.Packet.ByteOffset);
        Assert.Equal(900 * 16, Assert.Single(next).Command.Packet.ByteOffset);
    }

    [Fact]
    public void Default_empty_and_json_preserve_the_existing_array_contract()
    {
        NativeBattleResolvedCommandList missing = default;
        NativeBattleResolvedCommandList empty = [];
        Assert.True(missing.IsDefault);
        Assert.False(missing.IsEmpty);
        Assert.True(missing.IsDefaultOrEmpty);
        Assert.False(empty.IsDefault);
        Assert.True(empty.IsEmpty);
        Assert.Empty(missing);
        Assert.Empty(empty);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = missing[0];
        });
        Assert.Equal("[]", JsonSerializer.Serialize(missing));
        Assert.Equal("[]", JsonSerializer.Serialize(empty));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattleResolvedCommandList>("{}"));

        NativeBattleResolvedCommandList values = [new(Command(12), new(0x66) { TexturePixelSnapshot = 4 }, 2), new(Command(13), new(null), 0)];
        string array = JsonSerializer.Serialize(values.ToArray());
        NativeBattlePrimitiveFrame frame = NativeBattlePrimitiveFrame.Clear() with { LastCommands = values };
        using JsonDocument primitiveJson = JsonDocument.Parse(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame)));
        using JsonDocument preparedJson = JsonDocument.Parse(JsonSerializer.Serialize(NativeBattlePreparedPrimitiveFrame.From(frame)));
        Assert.Equal(array, primitiveJson.RootElement.GetProperty("LastCommands").GetRawText());
        Assert.Equal(array, preparedJson.RootElement.GetProperty("LastCommands").GetRawText());
        NativeBattleResolvedCommandList restored = JsonSerializer.Deserialize<NativeBattleResolvedCommandList>(array);
        Assert.Equal(array, JsonSerializer.Serialize(restored));
        Assert.True(restored[0].Command.WordSpan.SequenceEqual(values[0].Command.WordSpan));
        Assert.Equal(values[0].DrawMode, restored[0].DrawMode);
    }

    [Fact]
    public void Private_frame_fork_shares_unmodified_pages_and_detaches_foreign_storage()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0, [67]);
        NativeBattleForeignSurface surface = new(new ArenaMaterial(arena), [Vector2.Zero, Vector2.UnitX, Vector2.UnitY], [Vector4.One, Vector4.One, Vector4.One]);
        NativeBattlePilotVisualGeometry geometry = new(new(1, 1, 0, 1, 0), default, default, default, default, 3) { ForeignSurface = surface };
        NativeBattleLogicalCommand rich = Command(256) with { PilotGeometry = geometry };
        NativeBattlePrimitiveDrawMode mode = new(0x65);
        NativeBattleCommandPages<NativeBattleLogicalResolvedCommand>.Builder builder = new();
        for (int index = 0; index < 256; index++)
        {
            builder.Add(new(Command(index), mode, 1));
        }
        builder.Add(new(rich, mode, 2));
        NativeBattleResolvedCommandList original = new(builder.Freeze());
        NativeBattlePreparedPrimitiveFrame frame = new(original, []);
        NativeBattlePreparedPrimitiveFrame privateFork = NativeBattleStorageSnapshots.Fork(frame);
        NativeBattleResolvedCommandList forked = privateFork.LastCommands;

        Assert.True(Unsafe.AreSame(ref Unsafe.AsRef(in original[255]), ref Unsafe.AsRef(in forked[255])));
        Assert.False(Unsafe.AreSame(ref Unsafe.AsRef(in original[256]), ref Unsafe.AsRef(in forked[256])));
        Assert.Same(mode, forked[256].DrawMode);
        Assert.Equal((byte)2, forked[256].Pass);
        ArenaMaterial forkedMaterial = Assert.IsType<ArenaMaterial>(forked[256].Command.PilotGeometry!.ForeignSurface!.Material);
        Assert.NotSame(arena, forkedMaterial.Arena);
        Assert.Equal((byte)67, forkedMaterial.Arena.Read(0, 1)[0]);
        _ = arena.Write(0, [88]);
        Assert.Equal((byte)67, forkedMaterial.Arena.Read(0, 1)[0]);
    }

    [Fact]
    public void Scene_capture_filters_only_the_copy_opcode_and_reuses_unchanged_pages()
    {
        NativeBattleResolvedCommandList original = Build(530, Command(1), new(0x65));
        Assert.Equal(original, BattlePresentationProcess.ExcludeSceneCopyCommands(original));

        NativeBattleResolvedCommandList varied = NativeBattleResolvedCommandList.From(original.Select((value, index) => index is 256 or 512 ? value with { Command = value.Command.ToCommand() with { Opcode = 0x80 } } : value));
        NativeBattleResolvedCommandList filtered = BattlePresentationProcess.ExcludeSceneCopyCommands(varied);
        Assert.Equal(528, filtered.Count);
        Assert.Equal(original.Take(256).Concat(original.Skip(257).Take(255)).Concat(original.Skip(513)), filtered);
        Assert.Equal((byte)0x80, varied[256].Command.Opcode);
    }

    [Fact]
    public void Publication_allocates_pages_and_small_index_storage_without_per_delivery_objects()
    {
        NativeBattleLogicalCommand command = Command(1);
        NativeBattlePrimitiveDrawMode mode = new(0x65);
        const int count = 4096;
        _ = Build(count, command, mode);
        long before = GC.GetAllocatedBytesForCurrentThread();
        NativeBattleResolvedCommandList result = Build(count, command, mode);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(count, result.Count);
        long payload = (long)count * Unsafe.SizeOf<NativeBattleLogicalResolvedCommand>();
        Assert.InRange(allocated, payload, payload + 4096);
    }

    private static NativeBattleLogicalCommand Command(int index)
    {
        return new(new("off_B6D070", 0, index * 16), 1, 0x20, [0x20332211u, (uint)index, (uint)index + 1, (uint)index + 2], 0, [1, 2, 3, 0], null);
    }

    private static NativeBattleResolvedCommandList Build(int count, NativeBattleLogicalCommand command, NativeBattlePrimitiveDrawMode mode)
    {
        NativeBattleCommandPages<NativeBattleLogicalResolvedCommand>.Builder builder = new();
        for (int index = 0; index < count; index++)
        {
            builder.Add(new(command, mode, (byte)(index % 3)));
        }
        return new(builder.Freeze());
    }

    private sealed record ArenaMaterial(NativeBattleEffectArena Arena) : INativeBattleForeignMaterial;
}
