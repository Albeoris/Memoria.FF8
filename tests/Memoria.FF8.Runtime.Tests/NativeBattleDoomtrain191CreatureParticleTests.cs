using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDoomtrain191CreatureParticleTests
{
    [Fact]
    public void Chimney_spawn_consumes_five_random_values_per_particle_and_preserves_unused_payload()
    {
        NativeBattleDoomtrain191Frame frame = Chimney(0);
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(0x2E00E, new byte[] { 0xA1, 0xB2, 0xC3, 0xD4 }).Write(0x2E014, new byte[] { 0xE5, 0xF6, 0x77, 0x88 }) } };
        NativeCrtRandomState expected = frame.Random;
        short[][] values = new short[7][];
        for (int index = 0; index < 7; index++)
        {
            ushort[] draws = new ushort[5];
            for (int draw = 0; draw < 5; draw++)
            {
                expected = expected.Draw(out draws[draw]);
            }
            values[index] = [unchecked((short)(draws[0] % 144 + 144)), unchecked((short)(draws[1] % 30 - 15)), unchecked((short)-(draws[2] % 40)), unchecked((short)(draws[3] % 30 - 15)), unchecked((short)(-115 - draws[4] % 240))];
        }
        var result = ChimneyStep(frame, 0);
        Assert.Equal(expected, result.Frame.Random);
        for (int index = 0; index < 7; index++)
        {
            int record = 0x2E000 + index * 24;
            Assert.Equal(4u, BinaryPrimitives.ReadUInt32LittleEndian(result.Frame.Memory.Arena!.Read(record, 4)));
            Assert.Equal(values[index], new short[] { Word(result.Frame, record + 6), Word(result.Frame, record + 8), Word(result.Frame, record + 10), Word(result.Frame, record + 12), Word(result.Frame, record + 18) });
        }
        Assert.Equal(new byte[] { 0xA1, 0xB2, 0xC3, 0xD4 }, result.Frame.Memory.Arena!.Read(0x2E00E, 4));
        Assert.Equal(new byte[] { 0xE5, 0xF6, 0x77, 0x88 }, result.Frame.Memory.Arena.Read(0x2E014, 4));
        Assert.Equal(0, result.Result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Chimney_draw_precedes_updates_and_terminal_frame_clears_only_unpaused(bool paused)
    {
        NativeBattleDoomtrain191Frame frame = Chimney(12);
        byte[] record = new byte[24];
        BinaryPrimitives.WriteUInt32LittleEndian(record, 4);
        BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(4), 1);
        BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(6), 100);
        BinaryPrimitives.WriteInt16LittleEndian(record.AsSpan(18), -121);
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(0x2E000, record) } };
        int calls = 0;
        var result = ChimneyStep(frame, paused ? 1u : 0u, (current, draw) =>
        {
            calls++;
            Assert.Equal(0xE342A4u, draw.ResourceAddress);
            Assert.Equal((ushort)1, draw.Phase);
            Assert.Equal((short)100, Word(current, 0x2E006));
            Assert.Equal((short)0, Word(current, 0x2E00A));
        });
        Assert.Equal(1, calls);
        Assert.Equal(paused ? 0 : 2, result.Result);
        Assert.Equal(paused ? 4u : 0u, BinaryPrimitives.ReadUInt32LittleEndian(result.Frame.Memory.Arena!.Read(0x2E000, 4)));
        Assert.Equal(paused ? 1 : 2, Word(result.Frame, 0x2E004));
        Assert.Equal(frame.Random, result.Frame.Random);
    }

    [Fact]
    public void Flare_constructor_uses_rotated_anchor_and_one_random_draw_for_the_shared_orbit()
    {
        NativeBattleDoomtrain191Frame frame = NativeBattleDoomtrain191ModelTests.Ready();
        var memory = NativeBattleDoomtrain191Model.Require(frame);
        frame = NativeBattleDoomtrain191Model.Put(frame, memory with { Anchor = NativeBattleDoomtrain191Controller.Pack(NativeBattleDoomtrain191ModelTests.Identity with { X = 100, Y = 200, Z = 300 }, memory.Anchor, true) });
        NativeCrtRandomState expected = frame.Random.Draw(out ushort angle);
        frame = NativeBattleDoomtrain191Flare.Spawn6437F0(frame);
        var task = Assert.Single(frame.Effect.Pools[1].Slots, row => (row.Flags & 1) != 0 && row.Callback == 0x643880);
        Assert.Equal((short)100, unchecked((short)task.ReadWord(0x10)));
        Assert.Equal((short)-950, unchecked((short)task.ReadWord(0x12)));
        Assert.Equal((short)-9450, unchecked((short)task.ReadWord(0x14)));
        Assert.Equal((ushort)0xB00, task.ReadWord(0x1C));
        Assert.Equal(expected, frame.Random);
        Assert.Equal(angle % 4096, NativeBattleDoomtrain191Model.Require(frame).OrbitAngle);
        Assert.Equal(0x110, NativeBattleDoomtrain191Model.Require(frame).OrbitStep);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(21, false)]
    [InlineData(21, true)]
    public void Flare_draws_before_pause_or_completion_and_caps_only_the_scale_angle(ushort age, bool paused)
    {
        NativeBattleDoomtrain191Frame frame = NativeBattleDoomtrain191Flare.Spawn6437F0(NativeBattleDoomtrain191ModelTests.Ready());
        var pool = frame.Effect.Pools[1];
        int slot = Enumerable.Range(0, pool.Slots.Length).Single(index => (pool.Slots[index].Flags & 1) != 0 && pool.Slots[index].Callback == 0x643880);
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(0xC, age)) }) } };
        int calls = 0;
        NativeBattleDoomtrain191MeshServices services = new(() => paused ? 1u : 0u, (current, draw) =>
        {
            calls++;
            int scale = unchecked(NativeBattleCameraMath.Sine(1760 * Math.Min((int)age, 19) / 19) * 0xB00) >> 12;
            Assert.Equal(unchecked((short)scale), draw.Matrix.Rotation[0]);
            Assert.Equal(unchecked((short)scale), draw.Matrix.Rotation[4]);
            Assert.Equal((short)0xB00, draw.Matrix.Rotation[8]);
            Assert.Equal(0xE3A744u, draw.ResourceAddress);
            Assert.Equal(0x33u, draw.Flags);
            Assert.Equal((byte)2, draw.DepthShift);
            return current;
        });
        var result = NativeBattleDoomtrain191Flare.Advance643880(frame, slot, services);
        Assert.Equal(1, calls);
        Assert.Equal(!paused && age == 21 ? 2 : 0, result.Result);
        Assert.Equal(paused ? age : unchecked((ushort)(age + 1)), result.Frame.Effect.Pools[1].Slots[slot].ReadWord(0xC));
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(NativeBattleDoomtrain191Flare.Advance643880(JsonSerializer.Deserialize<NativeBattleDoomtrain191Frame>(JsonSerializer.Serialize(frame))!, slot, services)));
    }

    private static NativeBattleDoomtrain191Frame Chimney(ushort age)
    {
        NativeBattleDoomtrain191Frame frame = NativeBattleDoomtrain191ModelTests.Ready();
        var pool = frame.Effect.Pools[1];
        var task = (pool.Slots[0] with { Callback = 0x643D10 }).WriteWord(0xC, age);
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool with { Slots = pool.Slots.SetItem(0, task) }) } };
    }
    private static NativeBattleDoomtrain191LeafStep ChimneyStep(NativeBattleDoomtrain191Frame frame, uint flags, Action<NativeBattleDoomtrain191Frame, NativeBattleDoomtrain191SteamDraw>? observe = null)
    {
        byte[] bytes = new byte[24];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 20);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), 0xFFFF);
        NativeBattleModuleResource resource = new(0, 0xE342A4, 0, 0, bytes.ToImmutableArray());
        return NativeBattleDoomtrain191Chimney.Advance643D10(frame, 0, resource, new(() => flags, (current, draw) =>
        {
            observe?.Invoke(current, draw);
            return current;
        }));
    }
    private static short Word(NativeBattleDoomtrain191Frame frame, int offset) => BinaryPrimitives.ReadInt16LittleEndian(frame.Memory.Arena!.Read(offset, 2));
}
