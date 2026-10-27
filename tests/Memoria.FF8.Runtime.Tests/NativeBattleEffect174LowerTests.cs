using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect174Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect174LowerTests
{
    [Theory]
    [InlineData(29, 0u, 1, 30, 0)]
    [InlineData(30, 0u, 0, 31, 2)]
    [InlineData(29, 0x201u, 1, 29, 0)]
    public void Body_caller_uses_actual_blend_point_knownness_and_draw_before_pause(int age, uint flags, int draws, int nextAge, int result)
    {
        Fixture fixture = new(0x5D64E0, age) { Flags = flags };
        NativeBattleModulePoolTask task = Read(fixture.Frame, Fixture.TaskAddress);
        NativeBattleEffectArena point = NativeBattleEffectArena.Clear().Write(0, [10, 0, 20, 0, 30, 0, 0xA5]).MarkUnknown(7, 1);
        fixture.Frame = Write(fixture.Frame, Fixture.TaskAddress, CopyTask(task, 0x10, point, 8));
        NativeBattleEffect174Step step = fixture.Advance();
        NativeBattleEffect174DrawBody136[] calls = fixture.Operations.OfType<NativeBattleEffect174DrawBody136>().ToArray();
        Assert.Equal(draws, calls.Length);
        if (draws != 0)
        {
            Assert.Equal(0, calls[0].Actor);
            NativeBattleEffectArena temporary = fixture.BodyTemporary!;
            Assert.Equal((uint)((age << 12) / 30), U(temporary, 0x2C));
            Assert.Equal(0xA5, temporary.Read(0x66, 1)[0]);
            Assert.False(temporary.ReadSnapshot(0x67, 1).Known[0]);
            Assert.Equal((byte)6, fixture.Model.Read(0, 1)[0]);
        }
        Assert.Equal(nextAge, Read(step.Frame, Fixture.TaskAddress).ReadWord(12));
        Assert.Equal(result, step.Result);
        Assert.Equal(NativeBattleTemporaryMemory.Address, BinaryPrimitives.ReadUInt32LittleEndian(step.Frame.Staging!.Read(NativeBattleTemporaryMemory.Cursor, 4)));
    }

    [Fact]
    public void History_replays_previous_samples_then_restores_current_canonical_bones_mask_and_color()
    {
        Fixture fixture = new(0x5D4ED0, 2);
        for (int slot = 0; slot < 2; slot++)
        {
            uint address = 0x2395E58 + (uint)(slot * 0x404);
            fixture.Frame = fixture.Frame with { State = fixture.Frame.State.WriteDword(address, 1).WriteView(address + 4, PackedArena(Matrix(100 + slot)), 32) };
        }
        NativeBattleEffect174Step result = fixture.Advance();
        Assert.Equal(new[] { 101, 100 }, fixture.HistoryPositions);
        Assert.Equal(700, fixture.Bones[0].ReadMatrix().X);
        Assert.Equal(0x12345678u, U(fixture.Model, 0x28));
        Assert.Equal(0xA5A5A5A5u, U(fixture.Model, 0x7C));
        Assert.DoesNotContain(true, result.Frame.State.ReadView(0x2394BA0, 32).ReadSnapshot(18, 2).Known);
        Assert.DoesNotContain(true, result.Frame.State.ReadView(0x2395E58 + 2 * 0x404 + 4, 32).ReadSnapshot(18, 2).Known);
        Assert.Equal(700, BinaryPrimitives.ReadInt32LittleEndian(result.Frame.State.Read(0x2394BA0 + 20, 4)));
        Assert.Equal(3, Read(result.Frame, Fixture.TaskAddress).ReadWord(12));
    }

    [Fact]
    public void Paused_history_still_captures_draws_and_restores_without_advancing()
    {
        Fixture fixture = new(0x5D4ED0, 0) { Flags = 0x201 };
        NativeBattleEffect174Step result = fixture.Advance();
        Assert.Equal(new[] { 700 }, fixture.HistoryPositions);
        Assert.Equal(0, Read(result.Frame, Fixture.TaskAddress).ReadWord(12));
        Assert.Equal(700, fixture.Bones[0].ReadMatrix().X);
        Assert.Equal(1u, result.Frame.State.ReadDword(0x2395E58));
    }

    [Theory]
    [InlineData(61, 5)]
    [InlineData(65, 1)]
    public void History_tail_uses_native_remaining_sample_count(int age, int expected)
    {
        Fixture fixture = new(0x5D4ED0, age);
        for (int slot = 0; slot < 5; slot++)
        {
            uint address = 0x2395E58 + (uint)(slot * 0x404);
            fixture.Frame = fixture.Frame with { State = fixture.Frame.State.WriteDword(address, 1).WriteView(address + 4, PackedArena(Matrix(slot)), 32) };
        }
        NativeBattleEffect174Step result = fixture.Advance();
        Assert.Equal(expected, fixture.HistoryPositions.Count);
        Assert.Equal(age == 65 ? 2 : 0, result.Result);
    }

    [Theory]
    [InlineData(0x5D4CB0u, 3)]
    [InlineData(0x5D6AA0u, 20)]
    public void Particle_births_consume_five_CRT_draws_each_and_preserve_copied_point_W(uint callback, int births)
    {
        Fixture fixture = new(callback, 0);
        NativeCrtRandomState expected = fixture.Frame.Random;
        for (int index = 0; index < births * 5; index++)
        {
            expected = expected.Draw(out _);
        }
        NativeBattleEffect174Step result = fixture.Advance();
        Assert.Equal(expected, result.Frame.Random);
        Assert.Equal(callback == 0x5D4CB0 ? 1 : 0, fixture.PointCalls);
        Assert.Equal((uint)(callback == 0x5D4CB0 ? 1 : 2), result.Frame.State.ReadDword(0x2394240));
        Assert.Equal((ushort)0, result.Frame.State.ReadWord(0x2394244));
        Assert.Equal(0xA5, result.Frame.State.Read(0x239424E, 1)[0]);
        Assert.False(result.Frame.State.ReadView(0x239424F, 1).ReadSnapshot(0, 1).Known[0]);
        Assert.Equal(0u, result.Frame.State.ReadDword(0x2394240 + (uint)(births * 24)));
        Assert.Equal(1, Read(result.Frame, Fixture.TaskAddress).ReadWord(12));
        Assert.Empty(fixture.Operations.OfType<NativeBattleEffect174DrawBatch>());
    }

    [Theory]
    [InlineData(0x5D4CB0u)]
    [InlineData(0x5D6AA0u)]
    public void Full_shared_bank_stops_before_random_consumption(uint callback)
    {
        Fixture fixture = new(callback, 0);
        for (int index = 0; index < 100; index++)
        {
            fixture.Frame = fixture.Frame with { State = fixture.Frame.State.WriteDword(0x2394240 + (uint)(index * 24), 4) };
        }
        NativeCrtRandomState random = fixture.Frame.Random;
        NativeBattleEffect174Step result = fixture.Advance();
        Assert.Equal(random, result.Frame.Random);
        Assert.Equal(4u, result.Frame.State.ReadDword(0x2394240));
    }

    [Fact]
    public void Existing_particle_draw_and_motion_precede_new_births()
    {
        Fixture fixture = new(0x5D4CB0, 57);
        fixture.Frame = fixture.Frame with { State = fixture.Frame.State.WriteDword(0x2394240, 1).WriteWord(0x2394246, 1024).WriteWord(0x239424A, 20).WriteWord(0x2394252, unchecked((ushort)-5)) };
        NativeBattleEffect174Step result = fixture.Advance();
        Assert.Single(fixture.Operations.OfType<NativeBattleEffect174DrawBatch>());
        Assert.Equal((ushort)1, result.Frame.State.ReadWord(0x2394244));
        Assert.Equal((ushort)15, result.Frame.State.ReadWord(0x239424A));
        Assert.Equal(unchecked((ushort)-3), result.Frame.State.ReadWord(0x2394252));
        Assert.Equal(0, result.Result);
    }

    [Fact]
    public void Custom_caller_uses_current_cycle_and_native_individual_kernel()
    {
        Fixture fixture = new(0x5D5690, 4);
        NativeBattleEffect174Step result = fixture.Advance();
        NativeBattleEffect174DrawBatch delivery = Assert.Single(fixture.Operations.OfType<NativeBattleEffect174DrawBatch>());
        NativeBattleGeometryPrimitive primitive = Assert.Single(delivery.Batch.Attempts).Primitive!;
        Assert.Equal(0x26606064u, primitive.Words[1]);
        Assert.Equal(32, delivery.Batch.AllocationBytes);
        Assert.Equal(0x2397270u, delivery.CursorGlobal);
        Assert.Equal(5, Read(result.Frame, Fixture.TaskAddress).ReadWord(12));
    }

    [Fact]
    public void Camera_ring_uses_bucket_4122_shift14_and_draws_before_pause()
    {
        Fixture fixture = new(0x5D5560, 8) { Flags = 0x201 };
        NativeBattleEffect174Step result = fixture.Advance();
        NativeBattleEffect174DrawBatch delivery = Assert.Single(fixture.Operations.OfType<NativeBattleEffect174DrawBatch>());
        Assert.Equal(4122, Assert.Single(delivery.Batch.Attempts).Primitive!.Bucket);
        Assert.Equal(new int?[] { 0, 0, 777 }, result.Frame.Memory.GeometryRegisters!.Translation);
        Assert.Equal(8, Read(result.Frame, Fixture.TaskAddress).ReadWord(12));
    }

    [Fact]
    public void History_saved_state_JSON_retains_unknown_matrix_padding()
    {
        Fixture fixture = new(0x5D4ED0, 0);
        NativeBattleEffect174Step step = fixture.Advance();
        NativeBattleEffect174Frame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect174Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(step.Frame)))!);
        Assert.Equal(step.Frame.State.ReadView(0x2394BA0, 32).ReadSnapshot(0, 32).Known, restored.State.ReadView(0x2394BA0, 32).ReadSnapshot(0, 32).Known);
        Assert.DoesNotContain(true, restored.State.ReadView(0x2394BA0, 32).ReadSnapshot(18, 2).Known);
    }

    private sealed class Fixture
    {
        public const uint TaskAddress = 0x2394FB0;
        public NativeBattleEffect174Frame Frame;
        public uint Flags;
        public NativeBattleEffectArena Model = NativeBattleEffectArena.Clear().Write(0, [2]).Write(0x28, BitConverter.GetBytes(0x12345678u)).Write(0x7C, BitConverter.GetBytes(0xA5A5A5A5u));
        public ImmutableArray<NativeBattlePackedMatrix32> Bones = [Matrix(700)];
        public List<NativeBattleEffect174Operation> Operations = [];
        public List<int> HistoryPositions = [];
        public NativeBattleEffectArena? BodyTemporary;
        public int PointCalls;
        private readonly NativeBattleEffect174Resources resources;

        public Fixture(uint callback, int age)
        {
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x2394FA0, TaskAddress, 36, 100);
            (pool, _) = pool.Append(callback);
            NativeBattleModulePoolTask task = pool.Slots[0].WriteWord(12, unchecked((ushort)age)).WriteWord(0x1C, 4096).WriteWord(0x14, 2000);
            task = CopyTask(task, 0x10, NativeBattleEffectArena.Clear().Write(0, new byte[6]).Write(4, BitConverter.GetBytes((short)2000)).Write(6, [0xA5]).MarkUnknown(7, 1), 8);
            pool = pool.WriteSlot(0, task);
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess() with { Zsf3 = 0x155, Zsf4 = 0x100 }, ModuleSpriteBillboard21DFED0 = Matrix(0) };
            Frame = new(memory, new(174, 174, new(174, 0x5D45A0, 0x5D4580), null, null) { Pools = [pool] }, new(1234), NativeBattleEffect174State.FromColdProcess().WriteDword(0x2395E50, 3)) { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
            uint[] sources = [0xD5DF34, 0xD5E2CC, 0xD597DC, 0xD5ADCC, 0xD5D684];
            resources = new(new("fixture", []), new("fixture", []), sources.ToImmutableDictionary(source => source, Mesh), new uint[] { 0xD59460, 0xD59648 }.ToImmutableDictionary(source => source, Sprite), new uint[] { 1, 2, 3, 4, 5 }.ToImmutableArray(), Enumerable.Range(0, 12).Select(value => 0x00606060u + (uint)value).ToImmutableArray(), 0, ImmutableDictionary<uint, ImmutableArray<byte>>.Empty);
        }

        public NativeBattleEffect174Step Advance() => NativeBattleEffect174Lower.Advance(Frame, TaskAddress, resources, Inputs());

        private NativeBattleEffect174Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => Flags, _ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), (_, _) => Model, (_, _) => Matrix(0), _ => Matrix(0), _ => new(null, 100, 160, 108), _ => 0, _ => 777, (_, _) => 0, (frame, _, selector, offset, retained) =>
        {
            Assert.Equal((byte)25, selector);
            Assert.Equal((short)8192, offset);
            PointCalls++;
            return new(frame, retained);
        }, (_, _, _) => throw new InvalidOperationException(), (_, _) => Bones, (frame, _, matrices) =>
        {
            Bones = matrices;
            return frame;
        }, (frame, operation) =>
        {
            Operations.Add(operation);
            switch (operation)
            {
                case NativeBattleEffect174WriteModelBytes write:
                    Model = NativeBattleEffectNodeContextMemory.Copy(write.Data, 0, Model, write.Offset, write.Length);
                    break;
                case NativeBattleEffect174DrawBody136 draw:
                    BodyTemporary = Context(frame, draw.TemporaryAddress, 136);
                    break;
                case NativeBattleEffect174DrawModelHistory5120:
                    Assert.Equal(8u, U(Model, 0x7C));
                    HistoryPositions.Add(Bones[0].ReadMatrix().X);
                    Bones = [Matrix(-99)];
                    break;
            }
            return frame;
        });
    }

    private static NativeBattlePackedMatrix32 Matrix(int x) => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { X = x }, null);
    private static uint U(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleStaticMeshResource Mesh(uint source)
    {
        byte[] bytes = new byte[84];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 32);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 3);
        short[] xy = [-200, -200, 200, -200, -200, 200];
        for (int index = 0; index < 3; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 8), xy[index * 2]);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10 + index * 8), xy[index * 2 + 1]);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(40), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(44), 0x24606060);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(50), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(52), 4);
        return new(0, source, bytes.ToImmutableArray());
    }
    private static NativeBattleModuleResource Sprite(uint source)
    {
        byte[] bytes = new byte[18];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 14);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 14);
        return new(0, source, 0, 0, bytes.ToImmutableArray());
    }
}
