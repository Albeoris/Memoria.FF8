using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshOpeningFlareTests
{
    [Fact]
    public void Mandatory_initializer_rotates_then_adds_low_translation_WORDs_and_preserves_task_suffix()
    {
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x21FFD58, 0x21FFD68, 36, 100);
        pool = pool with { Slots = pool.Slots.Select(task => task with { Payload = Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray() }).ToImmutableArray() };
        NativeBattleActionMatrix world = new([4096, 0, 0, 0, 0, -4096, 0, 4096, 0], 65541, -65540, 32760, 4096);
        NativeBattleEffectTaskPool result = NativeBattleGilgameshOpeningFlare.Initialize5931E0(pool, world);
        NativeBattleModulePoolTask child = result.Slots[0];
        Assert.Equal(0x593250u, child.Callback);
        Assert.Equal((ushort)0, child.ReadWord(12));
        Assert.Equal((ushort)5, child.ReadWord(16));
        Assert.Equal(unchecked((ushort)-4), child.ReadWord(18));
        Assert.Equal((ushort)18760, child.ReadWord(20));
        Assert.Equal((ushort)800, child.ReadWord(32));
        Assert.Equal((ushort)800, child.ReadWord(34));
        foreach (int offset in new[] { 14, 22, 24, 26, 28, 30 })
        {
            Assert.Equal((ushort)0xA5A5, child.ReadWord(offset));
        }
        for (int index = 1; index < 100; index++)
        {
            (result, _) = result.Append(1);
        }
        Assert.Throws<InvalidOperationException>(() => NativeBattleGilgameshOpeningFlare.Initialize5931E0(result, world));
    }

    [Fact]
    public void Screen_helper_preserves_retained_off_diagonals_and_padding_and_uses_actual_projection_with_fixed_subtractions()
    {
        NativeBattleGilgameshOpeningFlareState state = new(Enumerable.Repeat((byte)0xA5, 32).ToImmutableArray());
        NativeBattleCameraProjection projection = new(null, 400, 171, 119);
        NativeBattleVector point = new(100, -50, 300);
        NativeBattleModuleProjectedVertex expected = NativeBattleModuleProjection.Project45FAA0(View(), point, projection, -256, 0x1000000);
        NativeBattleGilgameshOpeningFlareProjection result = NativeBattleGilgameshOpeningFlare.Project593490(state, point, -352, 700, View(), projection, -256, 0x1000000);
        Assert.Equal(unchecked((short)expected.Screen) / 8 - 160, result.Matrix.X);
        Assert.Equal(unchecked((short)(expected.Screen >> 16)) / 8 - 108, result.Matrix.Y);
        Assert.Equal(1100, result.Matrix.Z);
        Assert.Equal(new short[] { -352, unchecked((short)0xA5A5), unchecked((short)0xA5A5), unchecked((short)0xA5A5), -352, unchecked((short)0xA5A5), unchecked((short)0xA5A5), unchecked((short)0xA5A5), -352 }, result.Matrix.Rotation.ToArray());
        foreach (int index in Enumerable.Range(0, 32).Where(index => index < 20 && index is not (0 or 1 or 8 or 9 or 16 or 17)))
        {
            Assert.Equal((byte)0xA5, result.State.ScreenMatrix[index]);
        }
        Assert.All(state.ScreenMatrix, value => Assert.Equal((byte)0xA5, value));
        Assert.Null(projection.View);
        Assert.Equal((ushort)400, projection.FocalLength);
    }

    [Fact]
    public void Screen_helper_ignores_projection_rejection_and_reads_H_as_signed_WORD()
    {
        NativeBattleCameraProjection projection = new(null, 0x8000, 160, 108);
        NativeBattleActionMatrix view = View() with { Z = -100 };
        NativeBattleModuleProjectedVertex projected = NativeBattleModuleProjection.Project45FAA0(view, new(100, 200, 0), projection, 0, 0);
        Assert.NotEqual(0u, projected.Flags & 0x60000);
        NativeBattleGilgameshOpeningFlareProjection result = NativeBattleGilgameshOpeningFlare.Project593490(NativeBattleGilgameshOpeningFlareState.FromColdProcess(), new(100, 200, 0), 352, int.MinValue, view, projection, 0, 0);
        Assert.Equal(unchecked(-32768 + int.MinValue), result.Matrix.Z);
        Assert.Equal(unchecked((short)projected.Screen) / 8 - 160, result.Matrix.X);
        Assert.Equal(unchecked((short)(projected.Screen >> 16)) / 8 - 108, result.Matrix.Y);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(21)]
    public void All_eight_rows_draw_before_pause_with_actual_frame_order_and_brightness(int counter)
    {
        NativeBattleGilgameshOpeningFlareState state = NativeBattleGilgameshOpeningFlareState.FromColdProcess();
        NativeBattleModulePoolTask task = Task(counter);
        NativeBattleGilgameshOpeningFlareStep paused = Step(task, state, 0x201);
        Assert.Same(task, paused.UpdatedTask);
        Assert.Equal(0, paused.Result);
        Assert.Equal(8, paused.Packets.Length);
        Assert.Equal(new uint[] { 0, 1, 2, 5, 4, 3, 1, 2 }, paused.Packets.Select(batch => Assert.Single(batch.Primitives).Primitive.Words[3] & 255));
        Assert.Equal((short)352, BinaryPrimitives.ReadInt16LittleEndian(paused.State.ScreenMatrix.AsSpan()));
        NativeBattleGilgameshOpeningFlareStep active = Step(task, state, 0);
        Assert.Equal(JsonSerializer.Serialize(paused.Packets), JsonSerializer.Serialize(active.Packets));
        Assert.Equal(JsonSerializer.Serialize(paused.State), JsonSerializer.Serialize(active.State));
        int brightness = counter < 2 ? counter << 6 : counter < 10 ? 128 : 228 - 10 * counter;
        if ((counter & 1) != 0)
        {
            brightness >>= 1;
        }
        uint expectedColor = unchecked((uint)(((brightness << 8) | brightness) << 8)) & 0xFFFFFF;
        foreach (NativeBattleModulePacketBatch batch in paused.Packets)
        {
            Assert.Equal(expectedColor, Assert.Single(batch.Primitives).Primitive.Words[1] & 0xFFFFFF);
        }
        Assert.Equal(counter == 21 ? 2 : 0, active.Result);
    }

    [Fact]
    public void Task_integration_uses_arithmetic_shift_and_WORD_wrap_and_JSON_keeps_last_helper_matrix()
    {
        NativeBattleModulePoolTask task = Task(32767).WriteWord(32, 32760).WriteWord(34, unchecked((ushort)-17));
        NativeBattleGilgameshOpeningFlareStep first = Step(task, NativeBattleGilgameshOpeningFlareState.FromColdProcess(), 0);
        Assert.Equal((ushort)32743, first.UpdatedTask.ReadWord(32));
        Assert.Equal(unchecked((ushort)-15), first.UpdatedTask.ReadWord(34));
        Assert.Equal((ushort)32768, first.UpdatedTask.ReadWord(12));
        Assert.Equal(0, first.Result);
        NativeBattleGilgameshOpeningFlareState restored = RoundTrip(first.State);
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(Step(first.UpdatedTask, first.State, 0)), JsonSerializer.Serialize(Step(RoundTrip(first.UpdatedTask), restored, 0)));
        JsonObject missing = JsonNode.Parse(JsonSerializer.Serialize(first.State))!.AsObject();
        missing.Remove(nameof(NativeBattleGilgameshOpeningFlareState.ScreenMatrix));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattleGilgameshOpeningFlareState>(missing.ToJsonString()));
        Assert.Throws<InvalidDataException>(() => new NativeBattleGilgameshOpeningFlareState(default).Validate());
        Assert.Throws<InvalidDataException>(() => new NativeBattleGilgameshOpeningFlareState(new byte[18].ToImmutableArray()).Validate());
    }

    private static NativeBattleGilgameshOpeningFlareStep Step(NativeBattleModulePoolTask task, NativeBattleGilgameshOpeningFlareState state, uint flags) => NativeBattleGilgameshOpeningFlare.Step593250(task, state, Resources(), new(1000, 2000, -3000), View(), new(null, 288, 160, 108), -256, 0x1000000, flags);
    private static NativeBattleModulePoolTask Task(int counter) => new NativeBattleModulePoolTask(1, 0, null, 0x593250, new byte[24].ToImmutableArray()).WriteWord(12, unchecked((ushort)counter)).WriteWord(32, 800).WriteWord(34, 800);
    private static NativeBattleActionMatrix View() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 3000, 4096);
    private static T RoundTrip<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
    private static NativeBattleGilgameshOpeningFlareResources Resources()
    {
        byte[] bytes = new byte[0xA8];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 6);
        for (int frame = 0; frame < 6; frame++)
        {
            int offset = 24 + frame * 24;
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + frame * 2), (ushort)offset);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), 1);
            bytes[offset + 4] = 16;
            bytes[offset + 5] = 128;
            bytes[offset + 6] = 16;
            bytes[offset + 7] = 0x2E;
            bytes[offset + 8] = (byte)frame;
        }
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(22), -1);
        short[] frames = [0, 1, 2, 5, 4, 3, 1, 2];
        short[] weights = [2560, 1792, 1280, 768, 512, -512, -1280, -1792];
        return new(new(0, 0, 0, 0, bytes.ToImmutableArray()), Enumerable.Range(0, 8).Select(index => new NativeBattleGilgameshOpeningFlareRow(frames[index], 352, weights[index], 0)).ToImmutableArray());
    }
}
