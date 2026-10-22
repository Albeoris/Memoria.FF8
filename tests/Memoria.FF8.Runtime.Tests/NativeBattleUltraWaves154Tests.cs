using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleUltraWaves154Tests
{
    [Fact]
    public void Cold_capture_source_is_seeded_once_and_warm_packets_offsets_and_unknown_import_are_preserved()
    {
        ImmutableArray<byte> original = Enumerable.Range(0, 48).Select(index => (byte)(index * 3)).ToImmutableArray();
        NativeBattleUltraWaves154Resources resources = new(null!, null!, 0, [], [], [], [], original);
        NativeBattleUltraWaves154State cold = NativeBattleUltraWaves154State.FromColdProcess();
        Assert.True(cold.ColdCaptureSourcesPending);
        Assert.Throws<NotSupportedException>(() => cold.CapturePackets.Read(0, 48));
        Assert.Equal(new byte[4], cold.CaptureOffsets!.Read(0, 4));
        NativeBattleUltraWaves154State seeded = cold.SeedColdCapture(resources);
        Assert.False(seeded.ColdCaptureSourcesPending);
        Assert.Equal(original.ToArray(), seeded.CapturePackets.Read(0, 48));
        NativeBattleUltraWaves154State warm = seeded with { CapturePackets = seeded.CapturePackets.Write(8, [0xAB, 0xCD]), CaptureOffsets = seeded.CaptureOffsets!.Write(0, [1, 2, 3, 4]) };
        NativeBattleUltraWaves154State repeated = warm.SeedColdCapture(resources with { CapturePackets = Enumerable.Repeat((byte)0xEE, 48).ToImmutableArray() });
        Assert.Same(warm, repeated);
        Assert.Equal(new byte[] { 0xAB, 0xCD }, repeated.CapturePackets.Read(8, 2));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, repeated.CaptureOffsets!.Read(0, 4));
        NativeBattleUltraWaves154State restored = JsonSerializer.Deserialize<NativeBattleUltraWaves154State>(JsonSerializer.Serialize(warm))!;
        Assert.Equal(JsonSerializer.Serialize(warm), JsonSerializer.Serialize(restored.SeedColdCapture(resources)));
        Assert.Throws<NotSupportedException>(() => (cold with { ColdCaptureSourcesPending = null }).SeedColdCapture(resources));
        Assert.Throws<InvalidDataException>(() => cold.SeedColdCapture(resources with { CapturePackets = original.RemoveAt(0) }));
    }

    [Fact]
    public void Common_texture_restore_publishes_the_actual_neighboring_executable_alias_and_preserves_its_tail()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        byte[] original = Enumerable.Repeat((byte)0xE7, 0x1994).ToArray();
        memory = memory with { StickyWeb153 = memory.StickyWeb153! with { Globals = original.ToImmutableArray() } };
        ImmutableArray<byte> data = Enumerable.Range(0, 0x18360).Select(index => (byte)(index * 13)).ToImmutableArray();
        NativeBattleEffectMemory copied = NativeBattleUltraWaves154TextureRestore.Copy(memory, data);
        Assert.Equal(data.AsSpan().Slice(0x18004).ToArray(), copied.StickyWeb153!.Globals.AsSpan().Slice(0, 0x35C).ToArray());
        Assert.All(copied.StickyWeb153.Globals.Skip(0x35C), value => Assert.Equal((byte)0xE7, value));
        Assert.Equal(data.ToArray(), copied.UltraWaves154!.Packets.Read(0, data.Length));
        Assert.All(memory.StickyWeb153!.Globals, value => Assert.Equal((byte)0xE7, value));
        Assert.Throws<NotSupportedException>(() => NativeBattleUltraWaves154TextureRestore.Copy(memory with { StickyWeb153 = null }, data));
    }

    [Fact]
    public void Screen_grid_keeps_native_edge_samples_and_unwritten_packet_padding_when_reinitialized()
    {
        NativeBattleUltraWaves154State state = NativeBattleUltraWaves154State.FromColdProcess();
        state = state with { Quads = state.Quads.Write(16 + 14, [0xAB, 0xCD]).Write(16 + 30, [0x12, 0x34]) };
        NativeBattleUltraWavesGridInitialization initialized = NativeBattleUltraWavesGrid.Initialize(NativeBattleEffectArena.Clear().Write(0, state.Grid.AsSpan()), state.Quads, 0x241EF00);
        state = state with { Grid = initialized.Grid.Read(0, NativeBattleUltraWavesGrid.GridLength).ToImmutableArray(), Quads = initialized.Quads };
        Assert.Equal(new byte[] { 0xAB, 0xCD }, state.Quads.Read(30, 2));
        Assert.Equal(new byte[] { 0x12, 0x34 }, state.Quads.Read(46, 2));
        int last = (26 * 40 + 39) * 96 + 16;
        Assert.Equal((byte)63, state.Quads.Read(last + 20, 1)[0]);
        Assert.Equal((byte)215, state.Quads.Read(last + 29, 1)[0]);
        ImmutableArray<byte> waved = NativeBattleUltraWavesGrid.Wave(initialized.Grid, 17).Read(0, NativeBattleUltraWavesGrid.GridLength).ToImmutableArray();
        Assert.Contains(Enumerable.Range(0, 28 * 41), index => BinaryPrimitives.ReadInt16LittleEndian(waved.AsSpan().Slice(index * 12 + 8)) != 0);
        for (int row = 0; row < 28; row++)
        {
            for (int column = 0; column < 41; column++)
            {
                if (row is 0 or 27 || column is 0 or 40)
                {
                    Assert.Equal(0, BinaryPrimitives.ReadInt16LittleEndian(waved.AsSpan().Slice((row * 41 + column) * 12 + 8)));
                }
            }
        }
    }
}
