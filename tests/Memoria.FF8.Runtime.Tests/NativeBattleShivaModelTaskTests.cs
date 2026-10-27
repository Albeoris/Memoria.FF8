using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShivaModelTaskTests
{
    [Fact]
    public void Initial_callback_preserves_matrix_padding_and_uses_bound_model_control_and_transient_shared_scratch()
    {
        var fixture = Fixture();
        NativeBattleCactuarRawModelState warm = new(Enumerable.Repeat((byte)0xA0, 0x90).ToImmutableArray(), Enumerable.Repeat((byte)0xB6, 0x34).ToImmutableArray(), null, null, null, null);
        List<string> operations = [];
        NativeBattleShivaModelInputs inputs = fixture.Inputs with
        {
            InitializeDistortion5C3130 = (frame, first, second) =>
            {
                operations.Add("initialize");
                Assert.Equal(new NativeBattleVector(0, 0, 0), first);
                Assert.Equal(new NativeBattleVector(0, -4096, 0), second);
                Assert.Equal(1250u, Dword(frame.RawModel.Model, 0x58));
                Assert.Equal((byte)0, frame.RawModel.Model[0x6C]);
                Assert.Equal((byte)1, frame.RawModel.Model[0x72]);
                return new(frame, new(Enumerable.Repeat((byte)0xA5, 0x1A8).ToImmutableArray(), new bool[0x1A8].ToImmutableArray()));
            },
            Texture5C4F80 = (frame, first, second) =>
            {
                operations.Add($"texture:{first}:{second}");
                return frame;
            },
            DrawDistortion5C5060 = (frame, context) =>
            {
                operations.Add("distortion");
                Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x33000, Dword(context.Bytes, 4));
                Assert.All(context.Known.Skip(4).Take(4), value => Assert.True(value));
                Assert.False(context.Known[0]);
                Assert.False(context.Known[8]);
                Assert.Equal((byte)0xA5, context.Bytes[0]);
                return frame;
            }
        };
        NativeBattleShivaModelStep result = NativeBattleShivaModelTask.Advance5C4AD0(fixture.Frame with { RawModel = warm }, 0, inputs);
        Assert.Equal(new[] { "initialize", "texture:1:0", "distortion" }, operations);
        Assert.Equal((ushort)3, Word(result.Frame.RawModel.Control, 2));
        Assert.Equal(0x22BCFD0u, Dword(result.Frame.RawModel.Model, 0x84));
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress, result.Frame.RawModel.WorkingBase);
        Assert.Equal((ushort)0xA0A0, Word(result.Frame.RawModel.Model, 0x52));
        Assert.Equal((ushort)4096, Word(result.Frame.RawModel.Model, 0x40));
        Assert.Equal((ushort)4096, Word(result.Frame.RawModel.Model, 0x48));
        Assert.Equal((ushort)4096, Word(result.Frame.RawModel.Model, 0x50));
        Assert.Equal(0u, Dword(result.Frame.RawModel.Model, 0x54));
        Assert.Equal(0u, Dword(result.Frame.RawModel.Model, 0x5C));
        Assert.Equal((ushort)1000, result.Frame.Pool.Slots[0].ReadWord(0x20));
        Assert.Equal((ushort)1, result.Frame.Pool.Slots[0].ReadWord(0x0C));
        Assert.Null(result.Frame.RawModel.ProjectedScratch);
    }

    [Fact]
    public void Paused_initial_callbacks_repeat_setup_and_draw_without_selecting_a_pose_or_changing_y_speed()
    {
        var fixture = Fixture();
        NativeBattleShivaModelInputs inputs = fixture.Inputs with { GraphicsFlags = 0x200 };
        NativeBattleShivaModelFrame frame = fixture.Frame;
        for (int pass = 0; pass < 2; pass++)
        {
            NativeBattleShivaModelStep step = NativeBattleShivaModelTask.Advance5C4AD0(frame, 0, inputs);
            frame = step.Frame;
            Assert.Equal(0, step.Result);
            Assert.Equal((ushort)0, frame.Pool.Slots[0].ReadWord(0x0C));
            Assert.Equal((ushort)1250, frame.Pool.Slots[0].ReadWord(0x20));
            Assert.Equal(1250u, Dword(frame.RawModel.Model, 0x58));
            Assert.Null(frame.RawModel.DecodedPose);
        }
    }

    [Theory]
    [InlineData(5, 1)]
    [InlineData(53, 1)]
    [InlineData(58, 1)]
    [InlineData(59, 2)]
    [InlineData(138, 2)]
    [InlineData(139, 1)]
    [InlineData(144, 1)]
    [InlineData(145, 2)]
    [InlineData(149, 2)]
    [InlineData(150, 2)]
    [InlineData(198, 2)]
    public void Only_the_proved_animation_windows_advance_after_the_actual_draw(int counter, byte nextPoseFrame)
    {
        var fixture = Fixture((ushort)counter);
        int draws = 0;
        NativeBattleShivaModelInputs inputs = fixture.Inputs with
        {
            DrawRaw5C4EE0 = frame =>
            {
                draws++;
                Assert.Equal((byte)1, frame.RawModel.Model[0x72]);
                return frame;
            }
        };
        NativeBattleShivaModelStep step = NativeBattleShivaModelTask.Advance5C4AD0(fixture.Frame, 0, inputs);
        Assert.Equal(1, draws);
        Assert.Equal(nextPoseFrame, step.Frame.RawModel.Model[0x72]);
        Assert.Equal(counter == 59 ? (byte)1 : counter == 150 ? (byte)2 : (byte)0, step.Frame.RawModel.Model[0x6C]);
        Assert.Equal(counter == 198 ? 2 : 0, step.Result);
    }

    [Theory]
    [InlineData(53)]
    [InlineData(58)]
    public void Initial_texture_table_operands_are_applied_even_while_paused(int counter)
    {
        var fixture = Fixture((ushort)counter);
        List<(int, int)> textures = [];
        NativeBattleShivaModelInputs inputs = fixture.Inputs with
        {
            GraphicsFlags = 1,
            Texture5C4F80 = (frame, first, second) =>
            {
                textures.Add((first, second));
                return frame;
            }
        };
        NativeBattleShivaModelStep step = NativeBattleShivaModelTask.Advance5C4AD0(fixture.Frame, 0, inputs);
        Assert.Equal(new[] { (1, inputs.TextureValuesD3250C[counter - 53]) }, textures);
        Assert.Equal((ushort)counter, step.Frame.Pool.Slots[0].ReadWord(0x0C));
        Assert.Equal((byte)1, step.Frame.RawModel.Model[0x72]);
    }

    [Fact]
    public void Complete_timeline_preserves_event_order_appended_children_and_the_one_entry_completion_counter()
    {
        var fixture = Fixture();
        List<(int Counter, int First, int Second)> textures = [];
        List<(int Counter, NativeBattleShivaModelOperation Operation)> operations = [];
        NativeBattleShivaModelInputs inputs = fixture.Inputs with
        {
            Texture5C4F80 = (frame, first, second) =>
            {
                textures.Add((Counter(frame), first, second));
                return frame;
            },
            Execute = (frame, operation) =>
            {
                operations.Add((Counter(frame), operation));
                return frame;
            }
        };
        NativeBattleShivaModelFrame frame = fixture.Frame;
        NativeBattleShivaModelStep last = new(frame, 0);
        for (int callback = 0; callback < 199; callback++)
        {
            last = NativeBattleShivaModelTask.Advance5C4AD0(frame, 0, inputs);
            frame = last.Frame;
            Assert.Equal(callback == 198 ? 2 : 0, last.Result);
        }
        Assert.Equal((ushort)199, frame.Pool.Slots[0].ReadWord(0x0C));
        Assert.Equal(0x121, frame.EntryCounter22BD010);
        Assert.Equal(new uint[] { 0x5C5950, 0x5C5D90, 0x5C62F0, 0x5C6040, 0x5C6690, 0x5C7CA0 }, frame.Pool.Slots.Skip(1).Where(task => (task.Flags & 1) != 0).Select(task => task.Callback));
        Assert.Equal((60, new NativeBattleShivaModelOverlay(0, 1, 8, 255)), (operations[0].Counter, Assert.IsType<NativeBattleShivaModelOverlay>(operations[0].Operation)));
        Assert.Equal(new[] { (102, 0x5C5BB0u), (163, 0x5C7180u), (164, 0x5C6FA0u), (168, 0x5C7570u) }, operations.Skip(1).Select(operation => (operation.Counter, Assert.IsType<NativeBattleShivaModelInitializer>(operation.Operation).Address)));
        Assert.Contains((103, 0, 1), textures);
        Assert.Contains((105, 0, 0), textures);
        Assert.Null(frame.RawModel.ProjectedScratch);
    }

    [Theory]
    [InlineData(59)]
    [InlineData(60)]
    [InlineData(102)]
    [InlineData(103)]
    [InlineData(108)]
    [InlineData(150)]
    [InlineData(163)]
    [InlineData(196)]
    public void Paused_event_frames_still_draw_but_do_not_spawn_select_advance_or_publish_services(int counter)
    {
        var fixture = Fixture((ushort)counter);
        int draws = 0;
        NativeBattleShivaModelInputs inputs = fixture.Inputs with
        {
            GraphicsFlags = 0x201,
            Texture5C4F80 = (_, _, _) => throw new InvalidOperationException("Paused event has no texture operation."),
            Execute = (_, _) => throw new InvalidOperationException("Paused event has no service operation."),
            DrawRaw5C4EE0 = frame =>
            {
                draws++;
                return frame;
            }
        };
        NativeBattleShivaModelStep step = NativeBattleShivaModelTask.Advance5C4AD0(fixture.Frame, 0, inputs);
        Assert.Equal(1, draws);
        Assert.Equal((ushort)counter, step.Frame.Pool.Slots[0].ReadWord(0x0C));
        Assert.Single(step.Frame.Pool.Slots.Where(task => (task.Flags & 1) != 0));
        Assert.Equal((byte)0, step.Frame.RawModel.Model[0x6C]);
        Assert.Equal((byte)1, step.Frame.RawModel.Model[0x72]);
        Assert.Equal(0x120, step.Frame.EntryCounter22BD010);
    }

    [Fact]
    public void Independent_snapshot_continuation_keeps_model_control_pose_and_shared_scratch_ownership()
    {
        var fixture = Fixture(145);
        NativeBattleShivaModelFrame first = fixture.Frame;
        NativeBattleShivaModelFrame second = NativeBattleTestSnapshots.RestoreJson<NativeBattleShivaModelFrame>(NativeBattleTestSnapshots.Json(first))!;
        for (int callback = 0; callback < 8; callback++)
        {
            NativeBattleShivaModelStep a = NativeBattleShivaModelTask.Advance5C4AD0(first, 0, fixture.Inputs);
            NativeBattleShivaModelStep b = NativeBattleShivaModelTask.Advance5C4AD0(second, 0, fixture.Inputs);
            Assert.Equal(NativeBattleTestSnapshots.Json(a), NativeBattleTestSnapshots.Json(b));
            first = a.Frame;
            second = b.Frame;
        }
        NativeBattleShivaModelInputs invalidDraw = fixture.Inputs with { DrawRaw5C4EE0 = frame => frame with { RawModel = frame.RawModel with { ProjectedScratch = new byte[8].ToImmutableArray() } } };
        Assert.Throws<InvalidDataException>(() => NativeBattleShivaModelTask.Advance5C4AD0(fixture.Frame, 0, invalidDraw));
    }

    private static (NativeBattleShivaModelFrame Frame, NativeBattleShivaModelInputs Inputs) Fixture(ushort counter = 0)
    {
        NativeBattleEffectOwnedModelResource resource = Resource();
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(0x22BC198, 0x22BC1A8, 0x24, 100);
        (pool, int slot) = pool.Append(0x5C4AD0);
        pool = NativeBattleTaskFixture.EditPool(pool, data => data with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(0x0C, counter)) });
        NativeBattleCactuarRawModelState raw = NativeBattleCactuarRawModelState.FromColdProcess();
        if (counter != 0)
        {
            raw = NativeBattleCactuarRawModel.Initialize5A8F40(raw, resource, NativeBattleEffectNodeMemory.ArenaAddress, 3, NativeBattleShivaModelTask.Binding);
            raw = NativeBattleCactuarRawModel.Select5A8FB0(raw, resource, 0, NativeBattleShivaModelTask.Binding);
        }
        NativeBattleShivaModelFrame frame = new(NativeBattleEffectMemory.FromColdProcess(), pool, raw, new(new(1, 2, 3, 4), new(5, 6, 7, 8)), 0x120);
        NativeBattleShivaModelInputs inputs = new(0, resource, [-7, 12, 34, 56, 78, 90], (current, _, _) => new(current, new(new byte[0x1A8].ToImmutableArray(), new bool[0x1A8].ToImmutableArray())), (current, _, _) => current, (current, _) => current, current => current, (current, _) => current);
        return (frame, inputs);
    }

    private static NativeBattleEffectOwnedModelResource Resource()
    {
        byte[] bytes = new byte[134];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 4);
        int[] sections = [24, 88, 96, 130, 134];
        for (int index = 0; index < sections.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4 + index * 4), sections[index]);
        }
        bytes[24] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(26), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(40), -1);
        byte[] animations = new byte[34];
        BinaryPrimitives.WriteInt32LittleEndian(animations, 3);
        for (int index = 0; index < 3; index++)
        {
            int offset = 16 + index * 6;
            BinaryPrimitives.WriteInt32LittleEndian(animations.AsSpan(4 + index * 4), offset);
            animations[offset] = 2;
        }
        animations.CopyTo(bytes, 96);
        NativeBattlePoseSource pose = new(bytes.AsSpan(24, 64).ToArray().ToImmutableArray(), animations.ToImmutableArray());
        return new(NativeBattleEffectNodeMemory.ArenaAddress, bytes.ToImmutableArray(), pose, []);
    }

    private static int Counter(NativeBattleShivaModelFrame frame)
    {
        return unchecked((short)frame.Pool.Slots[0].ReadWord(0x0C));
    }

    private static uint Dword(ImmutableArray<byte> bytes, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan().Slice(offset));
    }

    private static ushort Word(ImmutableArray<byte> bytes, int offset)
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan().Slice(offset));
    }
}
