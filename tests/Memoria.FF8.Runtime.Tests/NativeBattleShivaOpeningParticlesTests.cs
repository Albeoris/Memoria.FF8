using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShivaOpeningParticlesTests
{
    [Fact]
    public void Paused_callback_draws_body_anchor_and_selected_records_with_distinct_actual_matrix_owners()
    {
        NativeBattleShivaEarlyTaskFrame frame = Fixture(0);
        NativeBattleModulePoolTask task = frame.Pool.Slots[0].WriteWord(0x20, 8192);
        NativeBattleEffectArena arena = Record(frame.Memory.Arena!, 0, 1, 0);
        arena = Record(arena, 1, 2, 0);
        frame = frame with { Pool = frame.Pool with { Slots = frame.Pool.Slots.SetItem(0, task) }, Memory = frame.Memory with { Arena = arena }, Geometry = new(new(1, -2, 3, 72), new(9, 8, 7, 6)) };
        List<uint> drawOrder = [];
        NativeBattleActionMatrix world = new([0, 0, 4096, 0, 4096, 0, -4096, 0, 0], 1000, 2000, 3000, 4096);
        NativeBattleShivaOpeningParticleInputs inputs = Inputs((current, draw) =>
        {
            drawOrder.Add(draw.Resource.Address);
            Assert.Equal(0x2030u, draw.Context.Flags);
            Assert.Null(draw.Context.Fade);
            Assert.Equal(0, draw.MaximumCount);
            Assert.Equal(36, draw.WindowCount);
            Assert.Equal(0x33000, draw.VertexArenaOffset);
            Assert.Equal(new short[] { 4096, 0, 0, 0, 8192, 0, 0, 0, 4096 }, draw.Matrix.Rotation.ToArray());
            Assert.Equal(107, draw.Matrix.X);
            Assert.Equal(209, draw.Matrix.Y);
            Assert.Equal(311, draw.Matrix.Z);
            return current;
        }, (current, draw) =>
        {
            drawOrder.Add(draw.Resource!.Address);
            if (draw.Resource.Address == 0xD209F4)
            {
                Assert.Equal(new NativeBattleModuleAnchor(1014, 2005, 2992, 0), draw.Anchor);
                Assert.Equal(2304, draw.Scale);
                Assert.Equal(-144, draw.DepthOffset);
            }
            else
            {
                Assert.Equal(new NativeBattleModuleAnchor(10, 20, 30, 0), draw.Anchor);
                Assert.Equal((ushort)0, draw.Phase);
            }
            Assert.Equal((ushort)0, draw.SpriteFlags);
            return current;
        }) with { EntryWorld = world };
        NativeBattleShivaEarlyTaskStep result = NativeBattleShivaOpeningParticles.Advance5C16B0(frame, 0, 0x201, inputs);
        Assert.Equal(new uint[] { 0xD215C4, 0xD209F4, 0xD20C7C }, drawOrder);
        Assert.Equal(JsonSerializer.Serialize(frame), JsonSerializer.Serialize(result.Frame));
        Assert.Equal(0, result.Result);
    }

    [Fact]
    public void Empty_body_window_is_still_active_until_current_counter_reaches_sixty()
    {
        NativeBattleShivaEarlyTaskFrame frame = Fixture(59);
        int bodyDraws = 0;
        NativeBattleShivaOpeningParticleInputs inputs = Inputs((current, draw) =>
        {
            bodyDraws++;
            Assert.Equal(0, draw.WindowCount);
            Assert.Equal(0x20F0u, draw.Context.Flags);
            Assert.Equal(4096, draw.Context.Fade);
            return current;
        }, (_, _) => throw new InvalidOperationException("No sprite remains at this boundary."));
        NativeBattleShivaEarlyTaskStep first = NativeBattleShivaOpeningParticles.Advance5C16B0(frame, 0, 0, inputs);
        Assert.Equal(1, bodyDraws);
        Assert.Equal((ushort)60, first.Frame.Pool.Slots[0].ReadWord(0x0C));
        Assert.Equal(0, first.Result);
        NativeBattleShivaEarlyTaskStep second = NativeBattleShivaOpeningParticles.Advance5C16B0(first.Frame, 0, 0, inputs with { DrawMesh = (_, _) => throw new InvalidOperationException("The body must retire at current counter60.") });
        Assert.Equal(2, second.Result);
        Assert.Equal(frame.Random, second.Frame.Random);
    }

    [Fact]
    public void Expired_record_increments_phase_then_dies_without_motion_and_signed_mask_selects_high_flags()
    {
        NativeBattleShivaEarlyTaskFrame frame = Fixture(60);
        NativeBattleModulePoolTask task = frame.Pool.Slots[0].WriteWord(0x0E, 0x8000);
        NativeBattleEffectArena arena = Record(frame.Memory.Arena!, 0, 0x80000000, 1);
        arena = Record(arena, 1, 2, 1);
        frame = frame with { Pool = frame.Pool with { Slots = frame.Pool.Slots.SetItem(0, task) }, Memory = frame.Memory with { Arena = arena } };
        int draws = 0;
        NativeBattleShivaEarlyTaskStep result = NativeBattleShivaOpeningParticles.Advance5C16B0(frame, 0, 0, Inputs(null!, (current, draw) =>
        {
            draws++;
            Assert.Equal((short)-1, draw.SpriteFrame!.NextOffset);
            return current;
        }));
        Assert.Equal(1, draws);
        Assert.Equal(0u, Dword(result.Frame.Memory.Arena!, 0x2C000));
        Assert.Equal((ushort)2, Uword(result.Frame.Memory.Arena!, 0x2C004));
        Assert.Equal((ushort)10, Uword(result.Frame.Memory.Arena!, 0x2C008));
        Assert.Equal((ushort)20, Uword(result.Frame.Memory.Arena!, 0x2C00A));
        Assert.Equal(2u, Dword(result.Frame.Memory.Arena!, 0x2C018));
        Assert.Equal((ushort)1, Uword(result.Frame.Memory.Arena!, 0x2C01C));
        Assert.Equal(2, result.Result);
    }

    [Fact]
    public void Spawn_consumes_six_samples_after_draw_uses_current_suffix_and_preserves_unknown_padding_through_JSON()
    {
        NativeBattleShivaEarlyTaskFrame frame = Fixture(30);
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        for (int index = 1; index < 150; index++)
        {
            arena = PutDword(arena, 0x2C000 + index * 24, 2);
        }
        arena = arena.MarkUnknown(0x2C00E, 2).MarkUnknown(0x2C016, 2);
        int source = 0x33000 + 73 * 8;
        arena = PutWord(PutWord(PutWord(arena, source, 4), source + 2, 5), source + 4, 6);
        frame = frame with { Memory = frame.Memory with { Arena = arena }, Geometry = frame.Geometry with { Current = frame.Geometry.Current with { W = 72 } } };
        int draws = 0;
        NativeBattleShivaOpeningParticleInputs inputs = Inputs((current, draw) =>
        {
            draws++;
            Assert.Equal(12, draw.WindowCount);
            Assert.Equal(2731, draw.Context.Fade);
            Assert.Equal(new NativeCrtRandomState(1), current.Random);
            return current;
        }, (_, _) => throw new InvalidOperationException("No preexisting record belongs to this mask, and new spawns cannot draw immediately."));
        inputs = inputs with { EntryWorld = NativeBattleCuragaMatrices.Identity8DD770() with { X = 100, Y = 200, Z = 300 } };
        NativeBattleShivaEarlyTaskStep result = NativeBattleShivaOpeningParticles.Advance5C16B0(frame, 0, 0, inputs);
        NativeBattleEffectArena after = result.Frame.Memory.Arena!;
        Assert.Equal(1, draws);
        Assert.Equal(1u, Dword(after, 0x2C000));
        Assert.Equal((ushort)0, Uword(after, 0x2C004));
        Assert.Equal((ushort)1065, Uword(after, 0x2C006));
        Assert.Equal((ushort)111, Uword(after, 0x2C008));
        Assert.Equal((ushort)214, Uword(after, 0x2C00A));
        Assert.Equal((ushort)317, Uword(after, 0x2C00C));
        Assert.Equal((ushort)8, Uword(after, 0x2C010));
        Assert.Equal(unchecked((ushort)-6), Uword(after, 0x2C012));
        Assert.Equal((ushort)32, Uword(after, 0x2C014));
        Assert.Throws<NotSupportedException>(() => after.Read(0x2C00E, 2));
        Assert.Throws<NotSupportedException>(() => after.Read(0x2C016, 2));
        Assert.Equal(new NativeCrtRandomState(0x3D6C1037), result.Frame.Random);
        NativeBattleShivaEarlyTaskFrame restored = JsonSerializer.Deserialize<NativeBattleShivaEarlyTaskFrame>(JsonSerializer.Serialize(frame))!;
        NativeBattleShivaEarlyTaskStep independent = NativeBattleShivaOpeningParticles.Advance5C16B0(restored, 0, 0, inputs);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(independent));
    }

    private static NativeBattleShivaOpeningParticleInputs Inputs(Func<NativeBattleShivaEarlyTaskFrame, NativeBattleShivaOpeningMeshDraw, NativeBattleShivaEarlyTaskFrame> mesh, Func<NativeBattleShivaEarlyTaskFrame, NativeBattleModuleDraw, NativeBattleShivaEarlyTaskFrame> sprite)
    {
        return new(NativeBattleCuragaMatrices.Identity8DD770(), NativeBattleCuragaMatrices.Identity8DD770() with { X = 100, Y = 200, Z = 300 }, new(0, 0xD215C4, [0]), Sprite(0xD209F4, 8), Sprite(0xD20C7C, 2), mesh, sprite);
    }

    private static NativeBattleShivaEarlyTaskFrame Fixture(ushort counter)
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x22BC198, 0x22BC1A8, 0x24, 100);
        (pool, int slot) = pool.Append(0x5C16B0);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(0x0C, counter).WriteWord(0x0E, 1).WriteWord(0x10, 7).WriteWord(0x12, 9).WriteWord(0x14, 11).WriteWord(0x18, 0).WriteWord(0x1A, 60).WriteWord(0x1C, 4096).WriteWord(0x20, 4096).WriteWord(0x22, 0);
        pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
        return new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, pool, new(1), new(new(0, 0, 0, 0), new(0, 0, 0, 0)));
    }

    private static NativeBattleModuleResource Sprite(uint address, int count)
    {
        int frame = 12 + 2 * count;
        byte[] bytes = new byte[frame + 4];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), (ushort)count);
        for (int index = 0; index < count; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + index * 2), (ushort)frame);
        }
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10 + count * 2), -1);
        return new(0, address, 0x570CC0, 0x570BC0, bytes.ToImmutableArray());
    }

    private static NativeBattleEffectArena Record(NativeBattleEffectArena arena, int index, uint flags, ushort phase)
    {
        int address = 0x2C000 + index * 24;
        arena = PutDword(arena, address, flags);
        arena = PutWord(arena, address + 4, phase);
        arena = PutWord(arena, address + 6, 1024);
        arena = PutWord(arena, address + 8, 10);
        arena = PutWord(arena, address + 10, 20);
        arena = PutWord(arena, address + 12, 30);
        arena = PutWord(arena, address + 16, 1);
        arena = PutWord(arena, address + 18, 2);
        return PutWord(arena, address + 20, 3);
    }

    private static ushort Uword(NativeBattleEffectArena arena, int address) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(address, 2));
    private static uint Dword(NativeBattleEffectArena arena, int address) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(address, 4));

    private static NativeBattleEffectArena PutWord(NativeBattleEffectArena arena, int address, int value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, unchecked((ushort)value));
        return arena.Write(address, bytes);
    }

    private static NativeBattleEffectArena PutDword(NativeBattleEffectArena arena, int address, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return arena.Write(address, bytes);
    }
}
